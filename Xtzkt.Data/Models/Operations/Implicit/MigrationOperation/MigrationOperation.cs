using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class MigrationOperation(Runtime runtime) : IOperation
    {
        public Runtime Runtime { get; private set; } = runtime;

        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }

        public required MigrationKind Kind { get; set; }
        public required int AddressId { get; set; }
        public int? ScriptId { get; set; }
    }

    public enum MigrationKind
    {
        Bootstrap,
        ActivateBaker,
        AirDrop,
        ProposalInvoice,
        CodeChange,
        Origination,
        RemoveBigMapKey,
    }

    public static class MigrationOperationModel
    {
        public static void BuildMigrationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<MigrationOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<MigrationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<MigrationOperation>()
                .HasIndex(x => x.AddressId);
            #endregion

            #region inheritance
            modelBuilder.Entity<MigrationOperation>()
                .HasDiscriminator<Runtime>(nameof(MigrationOperation.Runtime))
                .HasValue<MichelsonMigrationOperation>(Runtime.Michelson)
                .HasValue<EvmMigrationOperation>(Runtime.Evm);

            modelBuilder.Entity<MigrationOperation>()
                .Property(x => x.Runtime)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            modelBuilder.BuildMichelsonMigrationOperationModel();
            modelBuilder.BuildEvmMigrationOperationModel();
            #endregion
        }
    }
}
