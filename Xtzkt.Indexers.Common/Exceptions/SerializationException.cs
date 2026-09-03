namespace Xtzkt.Indexers.Common.Exceptions;

public class SerializationException(string message)
    : BaseException($"Serialization exception - {message}", false)
{
}
