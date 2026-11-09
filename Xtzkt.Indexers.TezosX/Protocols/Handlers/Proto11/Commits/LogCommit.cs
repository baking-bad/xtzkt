using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class LogCommit(ProtocolHandler protocol) : Proto10.LogCommit(protocol)
{
    protected override bool IsIgnoredMichelsonLog(XMichelsonContract contract)
    {
        // the gateway only announces alias materializations, dropped like the logs of its evm counterpart
        return contract.Hash == MichelsonRuntime.EvmGateway;
    }
}
