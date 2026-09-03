using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Services.Cache
{
    public class AbiCache(XtzktContext db)
    {
        #region static
        static int SoftCap = 0;
        static int TargetCap = 0;
        static Dictionary<int, Abi?> Cached = [];

        public static void Configure(CacheSize? size)
        {
            SoftCap = size?.SoftCap ?? 30_000;
            TargetCap = size?.TargetCap ?? 25_000;
            Cached = new(SoftCap + 1024);
        }
        #endregion

        readonly XtzktContext Db = db;

        public void Reset()
        {
            Cached.Clear();
        }

        public void Trim()
        {
            if (Cached.Count > SoftCap)
            {
                var toRemove = Cached.Keys
                    .OrderBy(x => x)
                    .Take(Cached.Count - TargetCap)
                    .ToList();

                foreach (var key in toRemove)
                    Cached.Remove(key);
            }
        }

        public void Add(XEvmContract contract, Abi? abi)
        {
            Cached[contract.Id] = abi;
        }

        public void Remove(XEvmContract contract)
        {
            Cached.Remove(contract.Id);
        }

        public async Task PreloadAsync(IEnumerable<int> contracts)
        {
            var missed = contracts.Where(x => !Cached.ContainsKey(x)).ToHashSet();
            if (missed.Count != 0)
            {
                var scripts = await Db.Scripts
                    .OfType<EvmScript>()
                    .Where(x => missed.Contains(x.ContractId) && x.Current)
                    .ToListAsync();

                foreach (var script in scripts)
                    Cached.Add(script.ContractId, script.AbiJson == null ? null : Abi.FromJson(script.AbiJson));
            }
        }

        public async Task<Abi> GetAsync(XEvmContract contract)
        {
            if (!Cached.TryGetValue(contract.Id, out var item))
            {
                var script = await Db.Scripts.OfType<EvmScript>().FirstOrDefaultAsync(x => x.ContractId == contract.Id && x.Current)
                    ?? throw new Exception($"Script for contract #{contract.Id} doesn't exist");

                item = script.AbiJson == null ? null : Abi.FromJson(script.AbiJson);
                Add(contract, item);
            }

            if (item == null)
                throw new Exception($"ABI for contract #{contract.Id} doesn't exist");

            return item;
        }

        public async Task<Abi?> GetOrDefaultAsync(XEvmContract contract)
        {
            if (!Cached.TryGetValue(contract.Id, out var item))
            {
                var script = await Db.Scripts.OfType<EvmScript>().FirstOrDefaultAsync(x => x.ContractId == contract.Id && x.Current)
                    ?? throw new Exception($"Script for contract #{contract.Id} doesn't exist");

                item = script.AbiJson == null ? null : Abi.FromJson(script.AbiJson);
                Add(contract, item);
            }

            return item;
        }
    }
}
