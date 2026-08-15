using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XEvmTransactionOperation() : TransactionOperation(Direction.XEvm), IParentOperation
{
    [Column(nameof(OpType))]
    public EvmOpType OpType { get; set; }

    [Column(nameof(OpCode))]
    public EvmOpCode OpCode { get; set; }

    [Column(nameof(GasPrice))]
    public BigInteger? GasPrice { get; set; }

    [Column(nameof(MaxFeePerGas))]
    public BigInteger? MaxFeePerGas { get; set; }

    [Column(nameof(MaxPriorityFeePerGas))]
    public BigInteger? MaxPriorityFeePerGas { get; set; }

    [Column(nameof(EffectiveGasPrice))]
    public BigInteger? EffectiveGasPrice { get; set; }

    [Column($"{nameof(DaFee)}18")]
    public BigInteger DaFee { get; set; }

    [Column($"{nameof(GasFee)}18")]
    public BigInteger GasFee { get; set; }


    [Column($"{nameof(Amount)}18")]
    public BigInteger Amount { get; set; }


    [Column(nameof(Input))]
    public byte[]? Input { get; set; }

    [Column(nameof(Output))]
    public byte[]? Output { get; set; }

    [Column(nameof(Result))]
    public string? Result { get; set; }


    [Column(nameof(Eip7702DelegationCount))]
    public int? Eip7702DelegationCount { get; set; }
}

public enum EvmOpType
{
    Legacy,
    AccessList,
    DynamicFee,
    Blob,
    SetCode,
    Trace = 255
}

public enum EvmOpCode
{
    Create,
    Create2,
    Call,
    CallCode,
    DelegateCall,
    StaticCall,
    SelfDestruct,
    Suicide,
}

public static class XEvmTransactionOperationModel
{
    public static void BuildXEvmTransactionOperationModel(this ModelBuilder modelBuilder)
    {
        #region props
        modelBuilder.Entity<XEvmTransactionOperation>()
            .Property(x => x.Result)
            .HasColumnType("jsonb");
        #endregion
    }
}
