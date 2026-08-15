using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models;

public class XBlock() : Block(Layer.TezosX)
{
    [Column(nameof(Events))]
    public XBlockEvents Events { get; set; }

    [Column(nameof(Operations))]
    public XOperations Operations { get; set; }

    [Column($"{nameof(L1Block.BakerFees)}18")]
    public BigInteger DaFees { get; set; }

    [Column($"{nameof(BurnedFees)}18")]
    public BigInteger BurnedFees { get; set; }

    [Column(nameof(L1Block.ProposerId))]
    public int? SequencerPoolId { get; set; }

    [Column(nameof(MichelsonHash))]
    public string? MichelsonHash { get; set; }
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
