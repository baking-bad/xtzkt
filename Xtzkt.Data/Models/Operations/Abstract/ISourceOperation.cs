namespace Xtzkt.Data.Models.Operations.Abstract;

public interface ISourceOperation : IOperation
{
    int? SubsCounter { get; set; }
}
