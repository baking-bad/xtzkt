using Netezos;
using System.Numerics;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

partial class ProtoHelpers
{
    protected override BlueprintChunk ParseChunk(byte[] payload)
    {
        var stream = new RlpStream(payload);
        var rlp = stream.Read();
        if (stream.CanRead || rlp as RlpList is not [RlpItem r0, RlpItem r1, RlpItem r2, RlpItem r3, RlpItem])
            throw new FormatException("Invalid BlueprintChunk format");

        return new BlueprintChunk
        {
            Chunk = r0.Data,
            Level = HexNumber.GetInt32Reverse(r1.Data),
            ChunksCount = HexNumber.GetInt32Reverse(r2.Data),
            ChunkIndex = HexNumber.GetInt32Reverse(r3.Data),
        };
    }

    protected override DelayedOperation ParseDelayedOperation(DelayedTransaction cached)
    {
        return cached.Kind switch
        {
            "deposit" => ParseDelayedXtzDeposit(cached.Hash, cached.Payload),
            "fa_deposit" => ParseDelayedFaDeposit(cached.Hash, cached.Payload),
            "transaction" => ParseDelayedEvmTransaction(cached.Hash),
            "operation" => ParseDelayedMichelsonOperation(cached.Hash),
            _ => throw new FormatException("Invalid delayed transactions format"),
        };
    }

    protected override DelayedXtzDeposit ParseDelayedXtzDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1, RlpItem e2, RlpItem e3])
        {
            return new DelayedXtzDeposit
            {
                Hash = Hex.GetString(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.GetString(e1.Data),
                InboxLevel = HexNumber.GetInt32(e2.Data),
                InboxMessageId = HexNumber.GetInt32(e3.Data),
            };
        }
        else if (rlp is [RlpItem m0, RlpList ml, RlpItem m2, RlpItem m3] && ml is [RlpItem { Data: [1] }, RlpItem m1])
        {
            return new DelayedXtzDeposit
            {
                Hash = Netezos.Encoding.Base58.Convert(hash, Prefixes.o),
                Amount = new BigInteger(m0.Data, true, true),
                Receiver = (m1.Data[0], m1.Data[1]) switch
                {
                    (0, 0) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz1),
                    (0, 1) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz2),
                    (0, 2) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz3),
                    (0, 3) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz4),
                    (1, _) when m1.Data[^1] == 0 => Netezos.Encoding.Base58.Convert(m1.Data[1..^1], Prefixes.KT1),
                    _ => throw new FormatException("Invalid Tezos address"),
                },
                InboxLevel = HexNumber.GetInt32(m2.Data),
                InboxMessageId = HexNumber.GetInt32(m3.Data),
            };
        }
        throw new FormatException("Invalid delayed deposit rlp");
    }

    protected static DelayedMichelsonOperation ParseDelayedMichelsonOperation(byte[] hash)
    {
        return new DelayedMichelsonOperation
        {
            Hash = Netezos.Encoding.Base58.Convert(hash, Prefixes.o),
        };
    }

    protected override string GetTransactionHash(byte[] bytes)
    {
        return bytes[0] == 0
            ? Netezos.Encoding.Base58.Convert(Blake2Fast.Blake2b.ComputeHash(32, bytes.AsSpan(1)), Prefixes.o)
            : Keccak256.GetHash(bytes[1..]);
    }
}
