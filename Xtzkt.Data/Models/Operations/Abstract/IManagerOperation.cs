namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IManagerOperation : IExplicitOperation
{
    public int SenderId { get; set; }
    public int Counter { get; set; }
    public long? StorageFee { get; set; }
    public int GasUsed { get; set; }
    public int StorageUsed { get; set; }
    public OperationStatus Status { get; set; }
    public string? Errors { get; set; }
}

public enum OperationStatus : byte
{
    None,
    Applied,
    Backtracked,
    Skipped,
    Failed
}
