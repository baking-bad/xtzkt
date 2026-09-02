using System.Numerics;
using System.Text.Json;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public abstract class ProtocolCommit(ProtocolHandler protocol)
    {
        protected static readonly BigInteger M12 = new(1_000_000_000_000);

        protected readonly XtzktContext Db = protocol.Db;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ProtocolHandler Proto = protocol;
        protected readonly BatchContext Batch = protocol.Batch;
        protected readonly BlockContext Context = protocol.Context;
        protected readonly IEvmRuntime EvmRuntime = protocol.EvmRuntime;
        protected readonly IMichelsonRuntime MichelsonRuntime = protocol.MichelsonRuntime;
        protected readonly ILogger Logger = protocol.Logger;
        protected readonly IHelpers Helpers = protocol.Helpers;

        #region fees
        protected void PayFee(XMichelsonAddress address, long daFee)
        {
            Spend(address, daFee);
            var daFee18 = new BigInteger(daFee) * M12;
            Context.Block.DaFees += daFee18;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                Receive(sequencerPool, daFee18);
            else
                Context.Statistics.TotalBurned += daFee18;
        }

        protected void RevertPayFee(XMichelsonAddress address, long daFee)
        {
            RevertSpend(address, daFee);
            var daFee18 = new BigInteger(daFee) * M12;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                RevertReceive(sequencerPool, daFee18);
        }

        protected void PayFee(XEvmAddress address, BigInteger daFee)
        {
            Spend(address, daFee);
            Context.Block.DaFees += daFee;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                Receive(sequencerPool, daFee);
            else
                Context.Statistics.TotalBurned += daFee;
        }

        protected void RevertPayFee(XEvmAddress address, BigInteger daFee)
        {
            RevertSpend(address, daFee);
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                RevertReceive(sequencerPool, daFee);
        }

        protected void BurnFee(XEvmAddress address, BigInteger fee)
        {
            Spend(address, fee);
            Context.Block.BurnedFees += fee;
            Context.Statistics.TotalBurned += fee;
        }

        protected void RevertBurnFee(XEvmAddress address, BigInteger fee)
        {
            RevertSpend(address, fee);
        }

        protected void BurnFee(XMichelsonAddress address, long fee)
        {
            Spend(address, fee);
            var fee18 = new BigInteger(fee) * M12;
            Context.Block.BurnedFees += fee18;
            Context.Statistics.TotalBurned += fee18;
        }

        protected void RevertBurnFee(XMichelsonAddress address, long fee)
        {
            RevertSpend(address, fee);
        }
        #endregion

        #region money flow
        protected void Spend(XEvmAddress address, BigInteger amount)
        {
            address.Balance -= amount;
        }

        protected void RevertSpend(XEvmAddress address, BigInteger amount)
        {
            address.Balance += amount;
        }

        protected void Spend(XMichelsonAddress address, long amount)
        {
            address.Balance -= amount;
        }

        protected void RevertSpend(XMichelsonAddress address, long amount)
        {
            address.Balance += amount;
        }

        protected void Receive(XEvmAddress address, BigInteger amount)
        {
            address.Balance += amount;
        }

        protected void RevertReceive(XEvmAddress address, BigInteger amount)
        {
            address.Balance -= amount;
        }

        protected void Receive(XMichelsonAddress address, long amount)
        {
            address.Balance += amount;
        }

        protected void RevertReceive(XMichelsonAddress address, long amount)
        {
            address.Balance -= amount;
        }
        #endregion

        #region helpers
        protected (long? StorageFee, long? AllocationFee) GetStorageFees(JsonElement result, bool allocated, int? paidStorageSize = null)
        {
            var totalBurned = result
                .OptionalArray("balance_updates")?
                .EnumerateArray()
                .Where(x => x.RequiredString("kind") == "burned" && x.RequiredString("category") == "storage fees")
                .Sum(x => x.RequiredInt64("change"))
                ?? 0;

            if (totalBurned == 0)
                return (null, null);

            var allocationFee = allocated ? Context.Protocol.OriginationSize * Context.Protocol.ByteCost : 0;
            if (allocationFee > totalBurned)
                throw new ValidationException("Unexpected allocation burn");

            var storageFee = totalBurned - allocationFee;
            if (paidStorageSize is int size && storageFee != size * Context.Protocol.ByteCost)
                throw new ValidationException("Unexpected storage burn");

            return (storageFee != 0 ? storageFee : null, allocationFee != 0 ? allocationFee : null);
        }

        protected static OperationStatus GetEvmTraceStatus(OperationStatus rootStatus, OperationStatus traceStatus)
        {
            return rootStatus != OperationStatus.Applied && traceStatus == OperationStatus.Applied
                ? OperationStatus.Backtracked
                : traceStatus;
        }

        protected async Task<XEvmAddress?> GetEip7702Delegate(XEvmAddress address)
        {
            if (address is XEvmUser user && user.Eip7702DelegateId is int userDelegateId)
                return await Cache.Addresses.GetAsync(userDelegateId) as XEvmAddress;

            if (address is XEvmAlias alias && alias.Eip7702DelegateId is int aliasDelegateId)
                return await Cache.Addresses.GetAsync(aliasDelegateId) as XEvmAddress;

            return null;
        }

        protected int SubcallsGasUsed(JsonElement trace, OperationStatus traceStatus, int frameGasOffset = 0)
        {
            if (trace.OptionalArray("calls") is not JsonElement calls)
                return 0;

            var skippingStatic = Proto.CanSkip(traceStatus);

            var res = 0;
            foreach (var call in calls.EnumerateArray())
            {
                if (skippingStatic && call.IsStaticCall())
                    continue;

                res += call.RequiredHexInt32("gasUsed") - frameGasOffset;
            }
            return res;
        }

        protected int GetGasLimit(JsonElement tx)
        {
            var gasLimit = tx.RequiredHexUInt64("gas");
            return gasLimit <= int.MaxValue ? (int)gasLimit : int.MaxValue;
        }
        #endregion
    }
}
