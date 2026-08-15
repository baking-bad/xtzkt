namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IContractOperation : IInternalOperation, IBigmapOperation
{
    public long? StorageId { get; set; }
}

public interface IBigmapOperation : IOperation, ISourceOperation
{
    int SenderId { get; set; }
    int? InitiatorId { get; set; }
    int? BigMapUpdates { get; set; }
    int? TokenTransfers { get; set; }
}
