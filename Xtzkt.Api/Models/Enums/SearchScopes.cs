namespace Xtzkt.Api.Models.Enums;

public class SearchScopes
{
    public const string Address = "address";
    public const string Block = "block";
    public const string Operation = "operation";
    public const string Token = "token";

    public static bool IsValid(string value) => value switch
    {
        Address => true,
        Block => true,
        Operation => true,
        Token => true,
        _ => false
    };

    public static readonly HashSet<string> Default =
    [
        Address,
        Block,
        Operation,
        Token,
    ];
}
