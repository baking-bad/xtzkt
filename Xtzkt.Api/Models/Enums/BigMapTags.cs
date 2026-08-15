using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class BigMapTags
{
    public const string Persistent    = "persistent";
    public const string Metadata      = "metadata";
    public const string TokenMetadata = "token_metadata";
    public const string Ledger        = "ledger";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Persistent,    (int)BigMapTag.Persistent },
        { Metadata,      (int)BigMapTag.Metadata },
        { TokenMetadata, (int)BigMapTag.TokenMetadata },
        { Ledger,        (int)BigMapTag.Ledger },
    };

    public static List<string> ToList(int value)
    {
        var tags = new List<string>(4);
        if ((value & (int)BigMapTag.Persistent)    == (int)BigMapTag.Persistent)    tags.Add(Persistent);
        if ((value & (int)BigMapTag.Metadata)      == (int)BigMapTag.Metadata)      tags.Add(Metadata);
        if ((value & (int)BigMapTag.TokenMetadata) == (int)BigMapTag.TokenMetadata) tags.Add(TokenMetadata);
        if ((value & (int)BigMapTag.Ledger)        == (int)BigMapTag.Ledger)        tags.Add(Ledger);
        return tags;
    }
}
