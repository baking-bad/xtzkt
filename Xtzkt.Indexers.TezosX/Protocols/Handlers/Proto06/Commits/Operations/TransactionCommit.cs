using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto06;

class TransactionCommit(ProtocolHandler protocol) : Proto02.TransactionCommit(protocol)
{
    protected override bool IsBurnTarget(XEvmAddress target)
    {
        return false;
    }
}
