using System.Numerics;
using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

class DepositCommit(ProtocolHandler protocol) : Proto05.DepositCommit(protocol)
{
    protected override BigInteger? GetDepositId(JsonElement feederReceipt)
    {
        // QueuedDeposit(uint256,address,uint256,address,uint256,uint256,uint256)
        return GetDepositIdFromLogs(feederReceipt, "0xb02d79c5657e344e23d91529b954c3087c60a974d598939583904a4f0b959614");
    }
}
