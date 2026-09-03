namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IOperation
{
    public long Id { get; }
    public int ChainId { get; }
    public int Level { get; }
    public DateTime Timestamp { get; }
}
