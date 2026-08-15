using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class AutostakingCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement rawBlock)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var balanceUpdates = rawBlock
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .Where(x => x.RequiredString("origin") == "block")
                .ToList();

            var updates = ParseStakingUpdates(block, balanceUpdates);

            if (updates.Count > 0)
            {
                Db.TryAttach(block);
                block.Operations |= L1Operations.Autostaking;

                var state = Cache.Chain.Get();
                Db.TryAttach(state);

                foreach (var group in updates.GroupBy(x => x.BakerId))
                {
                    var staked = group.Where(x => x.Type == StakingUpdateType.Stake || x.Type == StakingUpdateType.Restake).Sum(x => x.Amount);
                    var unstaked = group.Where(x => x.Type == StakingUpdateType.Unstake).Sum(x => x.Amount);
                    var finalized = group.Where(x => x.Type == StakingUpdateType.Finalize).Sum(x => x.Amount);

                    var operation = new AutostakingOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        Level = group.First().Level,
                        Timestamp = block.Timestamp,
                        Action = staked != 0 ? StakingAction.Stake : unstaked != 0 ? StakingAction.Unstake : StakingAction.Finalize,
                        Amount = staked != 0 ? staked : unstaked != 0 ? unstaked : finalized,
                        BakerId = group.Key,
                        StakingUpdatesCount = group.Count()
                    };

                    foreach (var update in group)
                        update.AutostakingOpId = operation.Id;

                    var baker = Cache.Addresses.GetBaker(group.Key);
                    Db.TryAttach(baker);
                    baker.AutostakingOpsCount++;
                    baker.LastLevel = block.Level;
                    baker.LastTimestamp = block.Timestamp;

                    state.AutostakingOpsCount++;

                    Db.AutostakingOps.Add(operation);
                    Context.AutostakingOps.Add(operation);
                }

                await new StakingUpdateCommit(Proto).Apply(updates);
            }
        }

        public virtual async Task Revert(L1Block block)
        {
            if (!block.Operations.HasFlag(L1Operations.Autostaking))
                return;

            var state = Cache.Chain.Get();
            Db.TryAttach(state);

            foreach (var op in await Db.AutostakingOps.Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync())
            {
                var baker = Cache.Addresses.GetBaker(op.BakerId);
                Db.TryAttach(baker);
                baker.AutostakingOpsCount--;

                state.AutostakingOpsCount--;

                var updates = await Db.StakingUpdates
                    .Where(x => x.AutostakingOpId == op.Id)
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                await new StakingUpdateCommit(Proto).Revert(updates);

                Db.AutostakingOps.Remove(op);
                Cache.Chain.ReleaseOperationId();
            }
        }

        protected virtual List<StakingUpdate> ParseStakingUpdates(L1Block block, List<JsonElement> balanceUpdates)
        {
            var res = new List<StakingUpdate>();

            if (balanceUpdates.Count % 2 != 0)
                throw new Exception("Unexpected autostaking balance updates behavior");

            for (int i = 0; i < balanceUpdates.Count; i += 2)
            {
                var update = balanceUpdates[i];
                var kind = update.RequiredString("kind");
                var category = update.OptionalString("category");

                var nextUpdate = balanceUpdates[i + 1];
                var nextKind = nextUpdate.RequiredString("kind");
                var nextCategory = nextUpdate.OptionalString("category");

                if (kind == "contract")
                {
                    if (nextKind != "freezer" || nextCategory != "deposits")
                        throw new Exception("Unexpected autostaking balance updates behavior");

                    #region stake
                    var baker = GetFreezerBaker(nextUpdate);
                    var change = nextUpdate.RequiredInt64("change");

                    if (baker != update.RequiredString("contract") ||
                        change != -update.RequiredInt64("change"))
                        throw new Exception("Unexpected autostaking balance updates behavior");

                    res.Add(new StakingUpdate
                    {
                        Id = Cache.Chain.NextStakingUpdateId(),
                        ChainId = block.ChainId,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        Cycle = block.Cycle,
                        BakerId = Cache.Addresses.GetExistingBaker(baker).Id,
                        StakerId = Cache.Addresses.GetExistingBaker(baker).Id,
                        Type = StakingUpdateType.Stake,
                        Amount = change
                    });
                    #endregion
                }
                else if (kind == "freezer" && category == "deposits")
                {
                    if (nextKind == "freezer" && nextCategory == "unstaked_deposits")
                    {
                        #region unstake
                        var baker = nextUpdate.Required("staker").RequiredString("delegate");
                        var staker = nextUpdate.Required("staker").RequiredString("contract");
                        var change = nextUpdate.RequiredInt64("change");
                        var cycle = nextUpdate.RequiredInt32("cycle");

                        if (baker != staker || 
                            baker != GetFreezerBaker(update) ||
                            change != -update.RequiredInt64("change"))
                            throw new Exception("Unexpected autostaking balance updates behavior");

                        res.Add(new StakingUpdate
                        {
                            Id = Cache.Chain.NextStakingUpdateId(),
                            ChainId = block.ChainId,
                            Level = block.Level,
                            Timestamp = block.Timestamp,
                            Cycle = cycle,
                            BakerId = Cache.Addresses.GetExistingBaker(baker).Id,
                            StakerId = Cache.Addresses.GetExistingBaker(staker).Id,
                            Type = StakingUpdateType.Unstake,
                            Amount = change
                        });
                        #endregion
                    }
                    else
                    {
                        throw new Exception("Unexpected autostaking balance updates behavior");
                    }
                }
                else if (kind == "freezer" && category == "unstaked_deposits")
                {
                    if (nextKind == "contract")
                    {
                        var baker = update.Required("staker").RequiredString("delegate");
                        var staker = update.Required("staker").RequiredString("contract");
                        var change = nextUpdate.RequiredInt64("change");
                        var cycle = update.RequiredInt32("cycle");

                        #region finalize
                        if (baker != staker ||
                            baker != nextUpdate.RequiredString("contract") ||
                            change != -update.RequiredInt64("change"))
                            throw new Exception("Unexpected autostaking balance updates behavior");

                        res.Add(new StakingUpdate
                        {
                            Id = Cache.Chain.NextStakingUpdateId(),
                            ChainId = block.ChainId,
                            Level = block.Level,
                            Timestamp = block.Timestamp,
                            Cycle = cycle,
                            BakerId = Cache.Addresses.GetExistingBaker(baker).Id,
                            StakerId = Cache.Addresses.GetExistingBaker(staker).Id,
                            Type = StakingUpdateType.Finalize,
                            Amount = change
                        });
                        #endregion
                    }
                    else if (nextKind == "freezer" && nextCategory == "deposits")
                    {
                        var baker = update.Required("staker").RequiredString("delegate");
                        var staker = update.Required("staker").RequiredString("contract");
                        var change = nextUpdate.RequiredInt64("change");
                        var cycle = update.RequiredInt32("cycle");

                        #region restake
                        if (baker != staker || 
                            baker != GetFreezerBaker(nextUpdate) ||
                            change != -update.RequiredInt64("change"))
                            throw new Exception("Unexpected autostaking balance updates behavior");

                        res.Add(new StakingUpdate
                        {
                            Id = Cache.Chain.NextStakingUpdateId(),
                            ChainId = block.ChainId,
                            Level = block.Level,
                            Timestamp = block.Timestamp,
                            Cycle = cycle,
                            BakerId = Cache.Addresses.GetExistingBaker(baker).Id,
                            StakerId = Cache.Addresses.GetExistingBaker(staker).Id,
                            Type = StakingUpdateType.Restake,
                            Amount = change
                        });
                        #endregion
                    }
                    else
                    {
                        throw new Exception("Unexpected autostaking balance updates behavior");
                    }
                }
                else if (kind != "accumulator" && kind != "minted")
                {
                    throw new Exception("Unexpected autostaking balance updates behavior");
                }
            }

            return res;
        }

        protected virtual string GetFreezerBaker(JsonElement update)
        {
            return update.Required("staker").RequiredString("baker");
        }
    }
}
