namespace Xtzkt.Api.Exceptions
{
    public class BadRequestException(string field, string message) : Exception(message)
    {
        public string Field { get; } = field;
    }
}
