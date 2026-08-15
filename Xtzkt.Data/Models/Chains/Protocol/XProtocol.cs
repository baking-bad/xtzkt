using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models;

public class XProtocol() : Protocol(Layer.TezosX)
{
    public string? MichelsonHash { get; set; }

    public int MinBlockTimeMs { get; set; }
    public int MaxBlockTimeMs { get; set; }

    [Column(nameof(OriginationSize))]
    public int OriginationSize { get; set; }

    [Column(nameof(ByteCost))]
    public int ByteCost { get; set; }

    public long DaFeePerByte { get; set; }
    public BigInteger DaFeePerByte18 { get; set; }

    public long HardEvmBlockGasLimit { get; set; }
    public long HardEvmOperationGasLimit { get; set; }

    [Column(nameof(L1Protocol.HardBlockGasLimit))]
    public int HardMichelsonBlockGasLimit { get; set; }

    [Column(nameof(L1Protocol.HardOperationGasLimit))]
    public int HardMichelsonOperationGasLimit { get; set; }

    [Column(nameof(L1Protocol.HardOperationStorageLimit))]
    public int HardMichelsonOperationStorageLimit { get; set; }
}

public static class XProtocolModel
{
    public static void BuildXProtocolModel(this ModelBuilder modelBuilder)
    {
    }
}
