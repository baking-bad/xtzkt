using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class BigMapKey
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int BigMapId { get; set; }
        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }
        public int Updates { get; set; }
        public bool Active { get; set; }

        public required string KeyHash { get; set; }
        public required byte[] RawKey { get; set; }
        public required string JsonKey { get; set; }

        public required byte[] RawValue { get; set; }
        public required string JsonValue { get; set; }
    }

    public static class BigMapKeyModel
    {
        public static void BuildBigMapKeyModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BigMapKey>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<BigMapKey>()
                .Property(x => x.KeyHash)
                .IsFixedLength(true)
                .HasMaxLength(54);

            modelBuilder.Entity<BigMapKey>()
                .Property(x => x.JsonKey)
                .HasColumnType("jsonb");

            modelBuilder.Entity<BigMapKey>()
                .Property(x => x.JsonValue)
                .HasColumnType("jsonb");
            #endregion

            #region indexes
            modelBuilder.Entity<BigMapKey>()
                .HasIndex(x => new { x.BigMapId, x.KeyHash });

            modelBuilder.Entity<BigMapKey>()
                .HasIndex(x => new { x.ChainId, x.LastLevel });
            #endregion
        }
    }
}
