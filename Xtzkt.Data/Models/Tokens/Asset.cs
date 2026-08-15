using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    /// <summary>
    /// A curated group of tokens representing the same real-world asset across chains and runtimes.
    /// </summary>
    public class Asset
    {
        public required int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public required long[] Tokens { get; set; }
    }

    public static class AssetModel
    {
        public static void BuildAssetModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Asset>()
                .HasKey(x => x.Id);
            #endregion
        }
    }
}
