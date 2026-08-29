using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto07.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto06.Helpers.ProtoHelpers(protocol)
{
    #region meta reader
    protected override string ExpectedDepositSender(DelayedOperation deposit)
    {
        // xtz deposits are still synthesized on behalf of the null address
        return deposit is DelayedFaDeposit ? EvmRuntime.DepositOrigin : EvmRuntime.NullAddress;
    }

    protected override void DrainBridgeCalls(Queue<MetaContent> queue, EvmDeposit deposit)
    {
        while (queue.TryPeek(out var next) && next is EvmInternalOperation bridgeCall && bridgeCall.Operation == deposit.FeederCall)
        {
            queue.Dequeue();

            if (bridgeCall.Logs.Count != 0)
                throw new Exception("Bridge calls shouldn't emit logs");

            deposit.BridgeCalls.Add(bridgeCall);
        }
    }
    #endregion
}
