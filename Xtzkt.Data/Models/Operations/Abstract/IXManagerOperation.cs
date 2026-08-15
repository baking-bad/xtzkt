namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IXManagerOperation : IManagerOperation
{
    long DaFee { get; set; }
    long GasFee { get; set; }
    long GasRefund { get; set; }
}
