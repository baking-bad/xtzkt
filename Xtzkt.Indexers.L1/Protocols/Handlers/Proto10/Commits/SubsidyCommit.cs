using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto10
{
    class SubsidyCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement content)
        {
            var balanceUpdate = content.RequiredArray("balance_updates").EnumerateArray()
                .First(x => x.RequiredString("kind") == "contract");
            var contract = (await Cache.Addresses.GetExistingAsync(balanceUpdate.RequiredString("contract")) as L1Contract)!;
            Db.TryAttach(contract);
            var op = new SubsidyOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                AddressId = contract.Id,
                Amount = balanceUpdate.RequiredInt64("change"),
                Level = block.Level,
                Timestamp = block.Timestamp
            };
            Db.SubsidyOps.Add(op);
            Context.SubsidyOps.Add(op);
            Cache.Chain.Get().SubsidyOpsCount++;

            Cache.Statistics.Current.TotalCreated += op.Amount;

            contract.SubsidyCount++;
            Receive(contract, null, op.Amount);

            block.Operations |= L1Operations.Subsidy;
            
            var schema = await Cache.Schemas.GetAsync(contract);
            var currStorage = await Cache.Storages.GetAsync(contract);

            Db.TryAttach(currStorage);
            currStorage.Current = false;

            var newStorageMicheline = schema.OptimizeStorage(content.RequiredMicheline("storage"), false);
            var newStorageBytes = newStorageMicheline.ToBytes();
            var newStorage = new Storage
            {
                Id = Cache.Chain.NextStorageId(),
                ChainId = contract.ChainId,
                Level = op.Level,
                ContractId = contract.Id,
                MigrationId = op.Id,
                RawValue = newStorageBytes,
                JsonValue = Regexes.RestrictedUnicode().Replace(schema.HumanizeStorage(newStorageMicheline), Regexes.NullEscapeString),
                Current = true,
            };

            Db.Storages.Add(newStorage);
            Cache.Storages.Add(contract, newStorage);

            op.StorageId = newStorage.Id;
        }

        public virtual async Task Revert(L1Block block)
        {
            foreach (var op in Context.SubsidyOps)
            {
                var contract = (await Cache.Addresses.GetAsync(op.AddressId) as L1Contract)!;
                Db.TryAttach(contract);
                contract.SubsidyCount--;
                RevertReceive(contract, null, op.Amount);

                Cache.Chain.Get().SubsidyOpsCount--;
                Db.SubsidyOps.Remove(op);
                Cache.Chain.ReleaseOperationId();

                var storage = await Cache.Storages.GetAsync(contract);
                if (storage.MigrationId == op.Id)
                {
                    var prevStorage = await Db.Storages
                        .Where(x => x.ContractId == contract.Id && x.Id < storage.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstAsync();

                    prevStorage.Current = true;
                    Cache.Storages.Add(contract, prevStorage);

                    Db.Storages.Remove(storage);
                    Cache.Chain.ReleaseStorageId();
                }
            }
        }
    }
}
