using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

partial class TransactionCommit
{
    public Task<XEvmTransactionOperation> ApplyEvm(byte[] hash, JsonElement tx, JsonElement receipt, JsonElement trace, bool isDelayedOp)
    {
        return ApplyEvm(hash, tx, receipt, trace, isDelayedOp, 0);
    }

    public virtual async Task<XEvmTransactionOperation> ApplyInternalEvm(IParentOperation parent, IParentOperation? cracParent, JsonElement trace, OperationStatus traceStatus)
    {
        var op = await ApplyInternalEvm(parent, trace, traceStatus, 0);
        cracParent?.GasUsed -= MichelsonRuntime.ConvertGas(op.GasUsed);
        return op;
    }

    protected override bool IsSelfDestructWithBurn(XEvmTransactionOperation op)
    {
        // eip6780: selfdestruct deletes the account, burning its balance along with it, but only if
        // the contract was created in the same transaction and is its own beneficiary
        return op.OpCode is EvmOpCode.SelfDestruct or EvmOpCode.Suicide
            && op.SenderId == op.TargetId
            && op.Amount != 0
            && Context.OriginationOps.Any(x => x.Hash.SequenceEqual(op.Hash) && x.ContractId == op.SenderId);
    }
}
