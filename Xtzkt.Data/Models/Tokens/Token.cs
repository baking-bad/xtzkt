using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class Token
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int ContractId { get; set; }
        public required BigInteger TokenId { get; set; }
        public TokenTags Tags { get; set; }

        public required int FirstMinterId { get; set; }
        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }

        public int TransfersCount { get; set; }
        public int BalancesCount { get; set; }
        public int HoldersCount { get; set; }

        public BigInteger TotalMinted { get; set; }
        public BigInteger TotalBurned { get; set; }
        public BigInteger TotalSupply { get; set; }

        public int? OwnerId { get; set; }
        public byte[]? OwnerEntrypoint { get; set; }
        public int? IndexedAt { get; set; }
    }

    [Flags]
    public enum TokenTags
    {
        None    = 0b_0000_0000,

        Fa      = 0b_0000_0001,
        Fa12    = 0b_0000_0011,
        Fa2     = 0b_0000_0101,
        FaNft   = 0b_0000_1101,

        Erc     = 0b_0001_0000,
        Erc20   = 0b_0011_0000,
        Erc721  = 0b_0101_0000,
        Erc1155 = 0b_1001_0000,
    }

    public enum TokenMetadataStatus
    {
        // Pending occupies the range [0..99]: the value equals the number of
        // resolve attempts already made, giving up to 100 retries before the
        // token is marked as permanently failed to fetch.
        Pending = 0,
        MaxRetry = 99,

        FailedToFetch = 100,
        FailedToDecode = 101,
        SizeLimitExceeded = 102,
        DepthLimitExceeded = 103,
        InvalidJson = 104,
        InvalidUri = 105,

        Ok = 200,
    }

    public static class TokenModel
    {
        public static void BuildTokenModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Token>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            // shadow property
            modelBuilder.Entity<Token>()
                .Property<string>("Metadata")
                .HasColumnType("jsonb");

            // shadow property
            modelBuilder.Entity<Token>()
                .Property<decimal>("Value")
                .HasColumnType("numeric");

            // shadow property
            modelBuilder.Entity<Token>()
                .Property<int>("MetadataStatus")
                .HasDefaultValue((int)TokenMetadataStatus.Pending);

            // shadow property
            modelBuilder.Entity<Token>()
                .Property<DateTime?>("MetadataSyncedAt");

            // shadow property
            modelBuilder.Entity<Token>()
                .Property<string>("MetadataLink");

            // shadow properties: normalized, strictly-typed projection of the token's own metadata,
            // written by the metadata service (the raw payload stays in the Metadata jsonb).
            modelBuilder.Entity<Token>()
                .Property<string>("Name");

            modelBuilder.Entity<Token>()
                .Property<string>("Symbol");

            modelBuilder.Entity<Token>()
                .Property<int?>("Decimals");
            #endregion

            #region indexes
            modelBuilder.Entity<Token>()
                .HasIndex(x => new { x.ContractId, x.TokenId });
            #endregion
        }
    }
}
