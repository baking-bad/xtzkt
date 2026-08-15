using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models;

public abstract class Log(Runtime runtime)
{
    public Runtime Runtime { get; private set; } = runtime;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required int AddressId { get; set; }
    public required int ContractTypeHash { get; set; }
    public required int ContractCodeHash { get; set; }

    public string? Name { get; set; }
    public string? Payload { get; set; }
    public bool? Guessed { get; set; }
}

public static class LogModel
{
    public static void BuildLogModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Log>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        modelBuilder.Entity<Log>()
            .Property(x => x.Payload)
            .HasColumnType("jsonb");
        #endregion

        #region indexes
        modelBuilder.Entity<Log>()
            .HasIndex(x => new { x.ChainId, x.Level });
        #endregion

        #region inheritance
        modelBuilder.Entity<Log>()
            .HasDiscriminator<Runtime>(nameof(Log.Runtime))
            .HasValue<MichelsonLog>(Runtime.Michelson)
            .HasValue<EvmLog>(Runtime.Evm);

        modelBuilder.Entity<Log>()
            .Property(x => x.Runtime)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildMichelsonLogModel();
        modelBuilder.BuildEvmLogModel();
        #endregion
    }
}
