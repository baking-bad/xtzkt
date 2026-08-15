using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models
{
    public abstract class Script(Runtime runtime)
    {
        public Runtime Runtime { get; private set; } = runtime;

        public required int Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required int ContractId { get; set; }
        public long? OriginationId { get; set; }
        public long? MigrationId { get; set; }
        public bool Current { get; set; }

        public int TypeHash { get; set; }
        public int CodeHash { get; set; }
    }

    public static class ScriptModel
    {
        public static void BuildScriptModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Script>()
                .HasKey(x => x.Id);
            #endregion

            #region inheritance
            modelBuilder.Entity<Script>()
                .HasDiscriminator<Runtime>(nameof(Script.Runtime))
                .HasValue<MichelsonScript>(Runtime.Michelson)
                .HasValue<EvmScript>(Runtime.Evm);

            modelBuilder.Entity<Script>()
                .Property(x => x.Runtime)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            modelBuilder.BuildMichelsonScriptModel();
            modelBuilder.BuildEvmScriptModel();
            #endregion

            #region indexes
            modelBuilder.Entity<Script>()
                .HasIndex(x => new { x.ContractId, x.Id });

            modelBuilder.Entity<Script>()
                .HasIndex(x => x.ContractId, $"IX_{nameof(XtzktContext.Scripts)}_{nameof(Script.ContractId)}_Partial")
                .HasFilter($@"""{nameof(Script.Current)}"" = true");
            #endregion
        }
    }
}
