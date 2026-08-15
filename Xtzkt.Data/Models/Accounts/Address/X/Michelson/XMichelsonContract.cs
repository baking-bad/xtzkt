using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XMichelsonContract() : XMichelsonAddress(AddressType.XMichelsonContract)
{
    [Column(nameof(Kind))]
    public required XContractKind Kind { get; set; }

    [Column(nameof(TypeHash))]
    public int TypeHash { get; set; }

    [Column(nameof(CodeHash))]
    public int CodeHash { get; set; }

    [Column(nameof(Tags))]
    public XMichelsonContractTags Tags { get; set; }

    [Column(nameof(TokensCount))]
    public int TokensCount { get; set; }

    [Column(nameof(LogsCount))]
    public int LogsCount { get; set; }

    [Column(nameof(TicketsCount))]
    public int TicketsCount { get; set; }

    [Column(nameof(CreatorId))]
    public required int CreatorId { get; set; }
}

public enum XContractKind
{
    SmartContract = AllContractKind.SmartContract,
    Asset = AllContractKind.Asset,
}

[Flags]
public enum XMichelsonContractTags
{
    None        = AllContractTags.None,

    FA          = AllContractTags.FA,
    FA1         = AllContractTags.FA1,
    FA12        = AllContractTags.FA12,
    FA2         = AllContractTags.FA2,

    Constants   = AllContractTags.Constants,
    Ledger      = AllContractTags.Ledger,
    Nft         = AllContractTags.Nft,
}

public static class XMichelsonContractModel
{
    public static void BuildXMichelsonContractModel(this ModelBuilder modelBuilder)
    {
    }
}
