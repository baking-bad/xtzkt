using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class TokenStandards
{
    public const string Fa12 = "fa1.2";
    public const string Fa2 = "fa2";
    public const string Erc20 = "erc20";
    public const string Erc721 = "erc721";
    public const string Erc1155 = "erc1155";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Fa12, (int)TokenTags.Fa12 },
        { Fa2, (int)TokenTags.Fa2 },
        { Erc20, (int)TokenTags.Erc20 },
        { Erc721, (int)TokenTags.Erc721 },
        { Erc1155, (int)TokenTags.Erc1155 },
    };

    public static string ToString(int tags)
    {
        if ((tags & (int)TokenTags.Fa12) == (int)TokenTags.Fa12) return Fa12;
        if ((tags & (int)TokenTags.Fa2) == (int)TokenTags.Fa2) return Fa2;
        if ((tags & (int)TokenTags.Erc20) == (int)TokenTags.Erc20) return Erc20;
        if ((tags & (int)TokenTags.Erc721) == (int)TokenTags.Erc721) return Erc721;
        if ((tags & (int)TokenTags.Erc1155) == (int)TokenTags.Erc1155) return Erc1155;

        throw new InvalidOperationException("Invalid token tags");
    }
}
