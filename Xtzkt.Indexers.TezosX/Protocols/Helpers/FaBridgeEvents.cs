using System.Numerics;
using System.Text;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols;

public static class FaBridgeEvents
{
    public static readonly byte[] DepositTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes(
            "Deposit(uint256,address,address,uint256,uint256,uint256)"));

    public static readonly byte[] WithdrawalTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes(
            "Withdrawal(uint256,address,address,bytes22,bytes22,uint256,uint256)"));

    public static readonly byte[] FastWithdrawalTopic =
        Keccak256.GetHashBytes(Encoding.UTF8.GetBytes(
            "FastFaWithdrawal(uint256,address,address,bytes22,bytes22,uint256,uint256,uint256,bytes)"));

    public static bool TryParseUpdate(byte[][] topics, byte[] data, ISourceOperation op, out BridgeTicketUpdateData update)
    {
        update = null!;

        if (topics.Length == 0)
            return false;

        if (topics[0].IsEqual(DepositTopic))
        {
            // (address ticketOwner, address receiver, uint256 amount, uint256 inboxLevel, uint256 inboxMsgId)
            if (topics.Length != 2 || topics[1].Length != 32 || data.Length != 5 * 32)
                throw new FormatException($"Invalid FA bridge Deposit event: {topics.Length} topics, {data.Length} bytes of data");

            update = new(
                TicketHash: topics[1],
                From: null,
                To: Address(data, 0),
                Amount: Uint256(data, 2 * 32),
                Op: op);

            return true;
        }

        if (topics[0].IsEqual(WithdrawalTopic))
        {
            // (address sender, address ticketOwner, bytes22 receiver, bytes22 proxy, uint256 amount, uint256 withdrawalId)
            if (topics.Length != 2 || topics[1].Length != 32 || data.Length != 6 * 32)
                throw new FormatException($"Invalid FA bridge Withdrawal event: {topics.Length} topics, {data.Length} bytes of data");

            update = new(
                TicketHash: topics[1],
                From: Address(data, 1 * 32),
                To: null,
                Amount: Uint256(data, 4 * 32),
                Op: op);

            return true;
        }

        if (topics[0].IsEqual(FastWithdrawalTopic))
        {
            // (address sender, address ticketOwner, bytes22 receiver, bytes22 proxy,
            //  uint256 amount, uint256 withdrawalId, uint256 timestamp, bytes payload)
            if (topics.Length != 2 || topics[1].Length != 32 || data.Length < 8 * 32)
                throw new FormatException($"Invalid FA bridge FastFaWithdrawal event: {topics.Length} topics, {data.Length} bytes of data");

            update = new(
                TicketHash: topics[1],
                From: Address(data, 1 * 32),
                To: null,
                Amount: Uint256(data, 4 * 32),
                Op: op);

            return true;
        }

        return false;
    }

    static string Address(byte[] data, int offset) =>
        Hex.GetString(data.AsSpan(offset + 12, 20));

    static BigInteger Uint256(byte[] data, int offset) =>
        new(data.AsSpan(offset, 32), isUnsigned: true, isBigEndian: true);
}
