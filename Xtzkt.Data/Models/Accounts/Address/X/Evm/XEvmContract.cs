using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XEvmContract() : XEvmAddress(AddressType.XEvmContract)
{
    [Column(nameof(Kind))]
    public required XContractKind Kind { get; set; }

    [Column(nameof(TypeHash))]
    public int TypeHash { get; set; }

    [Column(nameof(CodeHash))]
    public int CodeHash { get; set; }

    [Column(nameof(Tags))]
    public XEvmContractTags Tags { get; set; }

    [Column(nameof(TokensCount))]
    public int TokensCount { get; set; }

    [Column(nameof(CreatorId))]
    public required int CreatorId { get; set; }
}

[Flags]
public enum XEvmContractTags
{
    None = AllContractTags.None,

    ERC = AllContractTags.ERC,
    ERC20 = AllContractTags.ERC20,
    ERC721 = AllContractTags.ERC721,
    ERC1155 = AllContractTags.ERC1155,
}

public static class XEvmContractModel
{
    public static void BuildXEvmContractModel(this ModelBuilder modelBuilder)
    {
    }
}
