using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models;

public abstract class Protocol(Layer layer)
{
    public Layer Layer { get; private set; } = layer;

    public required int Id { get; set; }
    public required int ChainId { get; set; }
    public required string Hash { get; set; }
    public required int Version { get; set; }

    public required int FirstLevel { get; set; }
    public required int LastLevel { get; set; }
}

public static class ProtocolModel
{
    public static void BuildProtocolModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Protocol>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        // shadow property
        modelBuilder.Entity<Protocol>()
            .Property<string>("Extras")
            .HasColumnType("jsonb");
        #endregion

        #region inheritance
        modelBuilder.Entity<Protocol>()
            .HasDiscriminator<Layer>(nameof(Protocol.Layer))
            .HasValue<L1Protocol>(Layer.L1)
            .HasValue<XProtocol>(Layer.TezosX);

        modelBuilder.Entity<Protocol>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1ProtocolModel();
        modelBuilder.BuildXProtocolModel();
        #endregion

        #region indexes
        modelBuilder.Entity<Protocol>()
            .HasIndex(x => new { x.ChainId, x.Hash });
        #endregion
    }
}
