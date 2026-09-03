using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models;

public abstract class Address(Layer layer, Runtime runtime, AddressType type)
{
    public Layer Layer { get; private set; } = layer;
    public Runtime Runtime { get; private set; } = runtime;
    public AddressType Type { get; private set; } = type;

    public required int Id { get; set; }
    public required int ChainId { get; set; }
    public required string Hash { get; set; }

    public required int FirstLevel { get; set; }
    public required DateTime FirstTimestamp { get; set; }
    public required int LastLevel { get; set; }
    public required DateTime LastTimestamp { get; set; }

    #region counters
    public int ContractsCount { get; set; }

    public int ActiveTokensCount { get; set; }
    public int TokenBalancesCount { get; set; }
    public long TokenTransfersCount { get; set; }

    public int ActiveTicketsCount { get; set; }
    public int TicketBalancesCount { get; set; }
    public int TicketTransfersCount { get; set; }

    public long TransactionsCount { get; set; }
    public int OriginationsCount { get; set; }
    public int MigrationsCount { get; set; }
    #endregion

    #region helpers
    public virtual bool IsEmpty() =>
        TokenTransfersCount == 0 &&
        TicketTransfersCount == 0 &&
        TransactionsCount == 0 &&
        OriginationsCount == 0 &&
        MigrationsCount == 0;
    #endregion
}

public enum AddressType
{
    L1User              = 0,
    L1Baker             = 1,
    L1Contract          = 2,
    L1SmartRollup       = 3,
    L1Ghost             = 4,

    XEvmUser            = 10,
    XEvmAlias           = 11,
    XEvmContract        = 12,

    XMichelsonUser      = 20,
    XMichelsonAlias     = 21,
    XMichelsonContract  = 22,
    XMichelsonGhost     = 23,
}

public static class AddressModel
{
    public static void BuildAddressModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Address>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        // shadow property
        modelBuilder.Entity<Address>()
            .Property<string>("Extras")
            .HasColumnType("jsonb");
        #endregion

        #region inheritance
        modelBuilder.Entity<Address>()
            .HasDiscriminator<AddressType>(nameof(Address.Type))

            .HasValue<L1User>(AddressType.L1User)
            .HasValue<L1Baker>(AddressType.L1Baker)
            .HasValue<L1Contract>(AddressType.L1Contract)
            .HasValue<L1SmartRollup>(AddressType.L1SmartRollup)
            .HasValue<L1Ghost>(AddressType.L1Ghost)

            .HasValue<XEvmUser>(AddressType.XEvmUser)
            .HasValue<XEvmAlias>(AddressType.XEvmAlias)
            .HasValue<XEvmContract>(AddressType.XEvmContract)

            .HasValue<XMichelsonUser>(AddressType.XMichelsonUser)
            .HasValue<XMichelsonAlias>(AddressType.XMichelsonAlias)
            .HasValue<XMichelsonContract>(AddressType.XMichelsonContract)
            .HasValue<XMichelsonGhost>(AddressType.XMichelsonGhost);

        modelBuilder.Entity<Address>()
            .Property(x => x.Type)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);

        modelBuilder.BuildL1AddressModel();
        modelBuilder.BuildL1UserModel();
        modelBuilder.BuildL1BakerModel();
        modelBuilder.BuildL1ContractModel();
        modelBuilder.BuildL1SmartRollupModel();
        modelBuilder.BuildL1GhostModel();

        modelBuilder.BuildXAddressModel();
        modelBuilder.BuildXEvmAddressModel();
        modelBuilder.BuildXEvmUserModel();
        modelBuilder.BuildXEvmAliasModel();
        modelBuilder.BuildXEvmContractModel();
        modelBuilder.BuildXMichelsonAddressModel();
        modelBuilder.BuildXMichelsonAliasModel();
        modelBuilder.BuildXMichelsonUserModel();
        modelBuilder.BuildXMichelsonContractModel();
        modelBuilder.BuildXMichelsonGhostModel();
        #endregion

        #region indexes
        modelBuilder.Entity<L1Address>()
            //.HasIndex(x => new { x.ChainId, x.Type });
            .HasIndex(x => x.Type);

        modelBuilder.Entity<Address>()
            //.HasIndex(x => new { x.ChainId, x.Hash });
            .HasIndex(x => x.Hash);

        modelBuilder.Entity<Address>()
            //.HasIndex(x => new { x.ChainId, x.FirstLevel });
            .HasIndex(x => x.FirstLevel);
        #endregion
    }
}