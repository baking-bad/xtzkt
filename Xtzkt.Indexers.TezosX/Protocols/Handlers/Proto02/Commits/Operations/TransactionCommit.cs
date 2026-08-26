using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

partial class TransactionCommit(ProtocolHandler protocol) : Proto01.TransactionCommit(protocol)
{
    protected override (int GasUsed, int OwnGasUsed) GetGasUsed(JsonElement receipt, JsonElement trace)
    {
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        var ownGasUsed = gasUsed - SubcallsGasUsed(trace);
        return (gasUsed, ownGasUsed);
    }

    protected override EvmOpCode GetOpCode(JsonElement trace)
    {
        return trace.RequiredEvmOpCode("type");
    }

    protected override byte[]? GetInput(JsonElement tx, JsonElement trace)
    {
        return trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
    }

    protected override byte[]? GetOutput(JsonElement trace)
    {
        return trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;
    }

    protected override string? GetError(JsonElement trace)
    {
        return trace.OptionalEscapedString("revertReason") ?? trace.OptionalEscapedString("error");
    }

    protected override void TransferAmount(XEvmUser sender, XEvmAddress target, BigInteger amount)
    {
        Spend(sender, amount);
        if (target.Hash != EvmRuntime.XtzBridge)
            Receive(target, amount);

        if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
            Context.Statistics.TotalBanished += amount;
        else if (target.Hash == EvmRuntime.XtzBridge)
            Context.Statistics.TotalBurned += amount;
    }

    protected override void RevertTransferAmount(XEvmUser sender, XEvmAddress target, BigInteger amount)
    {
        RevertSpend(sender, amount);
        if (target.Hash != EvmRuntime.XtzBridge)
            RevertReceive(target, amount);
    }
}
