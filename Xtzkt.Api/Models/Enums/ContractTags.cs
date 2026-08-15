using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class ContractTags
{
    public const string FA        = "fa";
    public const string FA1       = "fa1";
    public const string FA12      = "fa12";
    public const string FA2       = "fa2";
    public const string Constants = "constants";
    public const string Ledger    = "ledger";
    public const string Nft       = "nft";
    public const string ERC       = "erc";
    public const string ERC20     = "erc20";
    public const string ERC721    = "erc721";
    public const string ERC1155   = "erc1155";

    public static List<string> ToList(int value)
    {
        var tags = new List<string>(4);
        if ((value & (int)AllContractTags.FA)        == (int)AllContractTags.FA)        tags.Add(FA);
        if ((value & (int)AllContractTags.FA12)      == (int)AllContractTags.FA12)      tags.Add(FA12);
        if ((value & (int)AllContractTags.FA2)       == (int)AllContractTags.FA2)       tags.Add(FA2);
        if ((value & (int)AllContractTags.FA1)       == (int)AllContractTags.FA1)       tags.Add(FA1);
        if ((value & (int)AllContractTags.Constants) == (int)AllContractTags.Constants) tags.Add(Constants);
        if ((value & (int)AllContractTags.Ledger)    == (int)AllContractTags.Ledger)    tags.Add(Ledger);
        if ((value & (int)AllContractTags.Nft)       == (int)AllContractTags.Nft)       tags.Add(Nft);
        if ((value & (int)AllContractTags.ERC)       == (int)AllContractTags.ERC)       tags.Add(ERC);
        if ((value & (int)AllContractTags.ERC20)     == (int)AllContractTags.ERC20)     tags.Add(ERC20);
        if ((value & (int)AllContractTags.ERC721)    == (int)AllContractTags.ERC721)    tags.Add(ERC721);
        if ((value & (int)AllContractTags.ERC1155)   == (int)AllContractTags.ERC1155)   tags.Add(ERC1155);
        return tags;
    }
}