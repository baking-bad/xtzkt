using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto05;

class DepositCommit(ProtocolHandler protocol) : Proto01.DepositCommit(protocol)
{
    protected override BigInteger? GetDepositId(JsonElement feederReceipt)
    {
        // QueuedDeposit(uint256,address,uint256,uint256,uint256)
        return GetDepositIdFromLogs(feederReceipt, "0x27a88c034649434b9c0dd0bf36ed46822cad6427dab69d15870da95c0e069acd");
    }

    protected static BigInteger? GetDepositIdFromLogs(JsonElement feederReceipt, string topic)
    {
        foreach (var log in feederReceipt.RequiredArray("logs").EnumerateArray())
        {
            var topics = log.RequiredArray("topics");
            if (topics.GetArrayLength() != 0 && topics[0].RequiredString() == topic)
                return new BigInteger(log.Required("data").RequiredHexBytes().AsSpan(0, 32), true, true);
        }

        return null;
    }
}
