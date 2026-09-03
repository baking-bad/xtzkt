using System.Numerics;
using Netezos.Encoding;
using Netezos.Forging;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Indexers.Common.Helpers;

public class TicketUpdates
{
    public required TicketIdentity Ticket { get; set; }
    public required IEnumerable<TicketUpdate> Updates { get; set; }
}

public class TicketIdentity
{
    public required string Ticketer { get; set; }
    public required byte[] RawType { get; set; }
    public required byte[] RawContent { get; set; }
    public string? JsonContent { get; set; }

    byte[]? _WeakHash = null;
    public byte[] WeakHash => _WeakHash ??= Keccak256.GetHashBytes([
        .. LocalForge.ForgeAddress(Ticketer),
        .. LocalForge.ForgeMicheline(Micheline.FromBytes(RawContent))]);

    public override bool Equals(object? obj)
    {
        return obj is TicketIdentity ticket &&
            ticket.Ticketer == Ticketer &&
            ticket.RawType.IsEqual(RawType) &&
            ticket.RawContent.IsEqual(RawContent);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Ticketer.GetHashCode(), RawType.GetHashCodeExt(), RawContent.GetHashCodeExt());
    }
}

public class TicketUpdate
{
    public required string Address { get; set; }
    public BigInteger Amount { get; set; }
}