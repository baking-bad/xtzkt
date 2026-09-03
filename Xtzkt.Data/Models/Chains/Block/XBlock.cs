using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models;

public class XBlock() : Block(Layer.TezosX)
{
    [Column(nameof(Events), Order = 27)]
    public XBlockEvents Events { get; set; }

    [Column(nameof(Operations), Order = 2)]
    public XOperations Operations { get; set; }

    [Column($"{nameof(L1Block.BakerFees)}18")]
    public BigInteger DaFees { get; set; }

    [Column($"{nameof(BurnedFees)}18")]
    public BigInteger BurnedFees { get; set; }

    [Column(Order = 16)]
    public long EvmGasUsed { get; set; }

    [Column(nameof(L1Block.GasUsed), Order = 17)]
    public int MichelsonGasUsed { get; set; }

    [Column(nameof(L1Block.ProposerId), Order = 28)]
    public int? SequencerPoolId { get; set; }

    [Column(nameof(MichelsonHash))]
    public byte[]? MichelsonHash { get; set; }

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<XBlock> blocks)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.Blocks)}" (
                {BinaryColumns},
                "{nameof(Events)}",
                "{nameof(Operations)}",
                "{nameof(L1Block.BakerFees)}18",
                "{nameof(BurnedFees)}18",
                "{nameof(EvmGasUsed)}",
                "{nameof(L1Block.GasUsed)}",
                "{nameof(L1Block.ProposerId)}",
                "{nameof(MichelsonHash)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var block in blocks)
        {
            block.WriteBinaryBase(writer);

            writer.Write((int)block.Events, NpgsqlDbType.Integer);
            writer.Write((long)block.Operations, NpgsqlDbType.Bigint);
            writer.Write(block.DaFees, NpgsqlDbType.Numeric);
            writer.Write(block.BurnedFees, NpgsqlDbType.Numeric);
            writer.Write(block.EvmGasUsed, NpgsqlDbType.Bigint);
            writer.Write(block.MichelsonGasUsed, NpgsqlDbType.Integer);
            writer.WriteNullable(block.SequencerPoolId, NpgsqlDbType.Integer);
            writer.WriteNullable(block.MichelsonHash, NpgsqlDbType.Bytea);
        }

        writer.Complete();
    }
    #endregion
}

[Flags]
public enum XBlockEvents
{
    None                    = AllBlockEvents.None,

    NewAddresses            = AllBlockEvents.NewAddresses,
    Bigmaps                 = AllBlockEvents.Bigmaps,
    Tokens                  = AllBlockEvents.Tokens,
    Events                  = AllBlockEvents.Events,
    Tickets                 = AllBlockEvents.Tickets,
    BridgeTickets           = AllBlockEvents.BridgeTickets,
}

[Flags]
public enum XOperations : long
{
    None                    = AllOperations.None,

    Migration               = AllOperations.Migration,

    Deposit                 = AllOperations.Deposit,
    Origination             = AllOperations.Origination,
    Transaction             = AllOperations.Transaction,
    IncreasePaidStorage     = AllOperations.IncreasePaidStorage,
    RegisterConstant        = AllOperations.RegisterConstant,
    Reveal                  = AllOperations.Reveal,
    TransferTicket          = AllOperations.TransferTicket,
}

public static class XBlockModel
{
    public static void BuildXBlockModel(this ModelBuilder modelBuilder)
    {
    }
}
