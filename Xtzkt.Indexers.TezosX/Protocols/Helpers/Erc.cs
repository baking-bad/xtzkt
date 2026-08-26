using System.Numerics;
using System.Text;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils.Abi;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols;

public static class Erc
{
    public static readonly byte[] TransferTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes("Transfer(address,address,uint256)"));

    public static readonly byte[] TransferSingleTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes("TransferSingle(address,address,address,uint256,uint256)"));

    public static readonly byte[] TransferBatchTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes("TransferBatch(address,address,address,uint256[],uint256[])"));

    static readonly AbiParameter[] BatchParams =
    [
        new() { Type = "uint256[]", Name = "ids" },
        new() { Type = "uint256[]", Name = "values" },
    ];

    public static bool TryParseTransfers(
        byte[][] topics,
        byte[] data,
        out TokenTags type,
        out List<(BigInteger TokenId, string From, string To, BigInteger Amount)> transfers)
    {
        type = TokenTags.None;
        transfers = [];

        if (topics.Length == 0)
            return false;

        var topic0 = topics[0];

        if (topic0.IsEqual(TransferTopic))
        {
            if (topics.Length == 3)
            {
                if (data.Length != 32)
                    return false;

                type = TokenTags.Erc20;
                transfers.Add((
                    BigInteger.Zero,
                    Address(topics[1]),
                    Address(topics[2]),
                    Uint256(data)));

                return true;
            }

            if (topics.Length == 4)
            {
                if (data.Length != 0)
                    return false;

                type = TokenTags.Erc721;
                transfers.Add((
                    Uint256(topics[3]),
                    Address(topics[1]),
                    Address(topics[2]),
                    BigInteger.One));

                return true;
            }

            return false;
        }

        if (topic0.IsEqual(TransferSingleTopic))
        {
            if (topics.Length != 4 || data.Length != 64)
                return false;

            type = TokenTags.Erc1155;
            transfers.Add((
                Uint256(data, 0),
                Address(topics[2]),
                Address(topics[3]),
                Uint256(data, 32)));

            return true;
        }

        if (topic0.IsEqual(TransferBatchTopic))
        {
            if (topics.Length != 4)
                return false;

            Dictionary<string, object> decoded;
            try { decoded = AbiDecoder.Decode(data, BatchParams); }
            catch { return false; }

            if (!decoded.TryGetValue("ids", out var p1) || p1 is not object[] ids ||
                !decoded.TryGetValue("values", out var p2) || p2 is not object[] values ||
                ids.Length != values.Length)
                return false;

            type = TokenTags.Erc1155;
            var from = Address(topics[2]);
            var to = Address(topics[3]);
            for (var i = 0; i < ids.Length; i++)
                transfers.Add(((BigInteger)ids[i], from, to, (BigInteger)values[i]));

            return true;
        }

        return false;
    }

    static string Address(byte[] topic) =>
        Hex.GetString(topic.AsSpan(topic.Length - 20, 20));

    static BigInteger Uint256(byte[] word) =>
        new(word.AsSpan(0, 32), isUnsigned: true, isBigEndian: true);

    static BigInteger Uint256(byte[] data, int offset) =>
        new(data.AsSpan(offset, 32), isUnsigned: true, isBigEndian: true);
}
