namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IParentOperation : IExplicitOperation
{
    int SenderId { get; }
    int Counter { get; }
    int GasUsed { get; set; }
    OperationStatus Status { get; }
    int? InternalOperations { get; set; }
}
