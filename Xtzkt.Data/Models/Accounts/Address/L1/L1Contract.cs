using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class L1Contract() : L1Address(AddressType.L1Contract)
    {
        [Column(nameof(Kind))]
        public required L1ContractKind Kind { get; set; }

        [Column(nameof(TypeHash))]
        public int TypeHash { get; set; }

        [Column(nameof(CodeHash))]
        public int CodeHash { get; set; }

        [Column(nameof(Tags))]
        public L1ContractTags Tags { get; set; }

        [Column(nameof(TokensCount))]
        public int TokensCount { get; set; }

        [Column(nameof(LogsCount))]
        public long LogsCount { get; set; }

        [Column(nameof(TicketsCount))]
        public int TicketsCount { get; set; }

        [Column(nameof(CreatorId))]
        public required int CreatorId { get; set; }
    }

    public enum AllContractKind
    {
        DelegatorContract,
        SmartContract,
        Asset,
    }

    public enum L1ContractKind
    {
        DelegatorContract = AllContractKind.DelegatorContract,
        SmartContract = AllContractKind.SmartContract,
        Asset = AllContractKind.Asset,
    }

    [Flags]
    public enum AllContractTags
    {
        None        = 0b_0000_0000_0000,

        FA          = 0b_0000_0000_0001, // FA token
        FA1         = 0b_0000_0000_0011, // tzip-5
        FA12        = 0b_0000_0000_0111, // tzip-7
        FA2         = 0b_0000_0000_1001, // tzip-12

        Constants   = 0b_0000_0001_0000, // refers at least one global constant
        Ledger      = 0b_0000_0010_0000, // has valid ledger bigmap
        Nft         = 0b_0000_0100_0000, // has ledger of type (bigmap nat address)
        
        ERC         = 0b_0001_0000_0000, // ERC token
        ERC20       = 0b_0011_0000_0000, // erc-20
        ERC721      = 0b_0101_0000_0000, // erc-721
        ERC1155     = 0b_1001_0000_0000, // erc-1155
    }

    [Flags]
    public enum L1ContractTags
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

    public static class L1ContractModel
    { 
        public static void BuildL1ContractModel(this ModelBuilder modelBuilder)
        {
            #region props
            // shadow property
            modelBuilder.Entity<L1Address>()
                .Property<string>("Metadata")
                .HasColumnType("jsonb");
            #endregion
        }
    }
}
