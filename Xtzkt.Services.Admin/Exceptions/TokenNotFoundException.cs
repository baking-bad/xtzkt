namespace Xtzkt.Services.Admin.Exceptions;

public class TokenNotFoundException(long id) : Exception($"Token {id} doesn't exist")
{
    public long Id { get; } = id;
}
