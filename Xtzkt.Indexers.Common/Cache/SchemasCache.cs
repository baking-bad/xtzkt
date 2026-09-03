using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Indexers.Common.Cache;

public class SchemasCache(XtzktContext db)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, ContractScript> Cached = [];

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

    public void Add(L1Contract contract, ContractScript schema)
    {
        Cached[contract.Id] = schema;
    }

    public void Add(XMichelsonContract contract, ContractScript schema)
    {
        Cached[contract.Id] = schema;
    }

    public void Add(L1SmartRollup smartRollup, ContractScript schema)
    {
        Cached[smartRollup.Id] = schema;
    }

    public void Remove(L1Contract contract)
    {
        Cached.Remove(contract.Id);
    }

    public void Remove(XMichelsonContract contract)
    {
        Cached.Remove(contract.Id);
    }

    public void Remove(L1SmartRollup smartRollup)
    {
        Cached.Remove(smartRollup.Id);
    }

    public async Task PreloadAsync(IEnumerable<int> contracts)
    {
        var missed = contracts.Where(x => !Cached.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var scripts = await Db.Scripts
                .OfType<MichelsonScript>()
                .Where(x => missed.Contains(x.ContractId) && x.Current)
                .ToListAsync();

            foreach (var script in scripts)
                Cached.Add(script.ContractId, script.Schema);
        }
    }

    public async Task<ContractScript> GetAsync(L1Contract contract)
    {
        if (!Cached.TryGetValue(contract.Id, out var item))
        {
            item = (await Db.Scripts.OfType<MichelsonScript>().FirstOrDefaultAsync(x => x.ContractId == contract.Id && x.Current))?.Schema
                ?? throw new Exception($"Script for contract #{contract.Id} doesn't exist");

            Add(contract, item);
        }

        return item;
    }

    public async Task<ContractScript> GetAsync(XMichelsonContract contract)
    {
        if (!Cached.TryGetValue(contract.Id, out var item))
        {
            item = (await Db.Scripts.OfType<MichelsonScript>().FirstOrDefaultAsync(x => x.ContractId == contract.Id && x.Current))?.Schema
                ?? throw new Exception($"Script for contract #{contract.Id} doesn't exist");

            Add(contract, item);
        }

        return item;
    }

    public async Task<ContractScript> GetAsync(L1SmartRollup smartRollup)
    {
        if (!Cached.TryGetValue(smartRollup.Id, out var item))
        {
            var bytes = (await Db.SmartRollupOriginateOps.SingleOrDefaultAsync(x => x.SmartRollupId == smartRollup.Id && x.Status == OperationStatus.Applied))?.ParameterType
                ?? throw new Exception($"Origination of smart rollup #{smartRollup.Id} doesn't exist");
            
            var parameter = new MichelinePrim
            {
                Prim = PrimType.parameter,
                Args = [Micheline.FromBytes(bytes)]
            };
            var storage = new MichelinePrim
            {
                Prim = PrimType.storage,
                Args = [new MichelinePrim { Prim = PrimType.never }]
            };
            item = new ContractScript(parameter, storage);
            
            Add(smartRollup, item);
        }

        return item;
    }
}
