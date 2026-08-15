using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XEvmOriginationOperation() : OriginationOperation(Env.XEvm), IParentOperation, ILogsOperation
{
    public EvmOpType OpType { get; set; }
    public EvmOpCode OpCode { get; set; }
    public BigInteger? GasPrice { get; set; }
    public BigInteger? MaxFeePerGas { get; set; }
    public BigInteger? MaxPriorityFeePerGas { get; set; }
    public BigInteger? EffectiveGasPrice { get; set; }

    [Column($"{nameof(DaFee)}18")]
    public BigInteger DaFee { get; set; }

    [Column($"{nameof(GasFee)}18")]
    public BigInteger GasFee { get; set; }

    [Column($"{nameof(Balance)}18")]
    public BigInteger Balance { get; set; }

    public int? InternalOperations { get; set; }
    public int? LogsCount { get; set; }
    public bool? ReOriginated { get; set; }
    public bool? NonceConsumed { get; set; }
}

public static class XEvmOriginationOperationModel
{
    public static void BuildXEvmOriginationOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
