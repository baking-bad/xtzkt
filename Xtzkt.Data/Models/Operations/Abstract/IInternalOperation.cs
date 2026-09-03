namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IInternalOperation : IManagerOperation
{
    public int? InitiatorId { get; set; }
    public int? Nonce { get; set; }
}
