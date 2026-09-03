namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IL1ManagerOperation : IManagerOperation
{
    int? GasLimit { get; set; }
    long? BakerFee { get; set; }
}
