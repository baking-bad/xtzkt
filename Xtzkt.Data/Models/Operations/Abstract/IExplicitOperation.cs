namespace Xtzkt.Data.Models.Operations.Abstract;

public interface IExplicitOperation : IOperation
{
    public byte[] Hash { get; }
}
