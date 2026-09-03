namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IXManagerOperation : IManagerOperation
{
    int? GasLimit { get; set; }
    long? DaFee { get; set; }
    long? GasFee { get; set; }
    long? GasFeeRefunded { get; set; }
}
