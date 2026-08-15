namespace Xtzkt.Data.Models.Operations.Abstract;

public interface ILogsOperation : IOperation
{
    OperationStatus Status { get; }
    int? LogsCount { get; set; }
}
