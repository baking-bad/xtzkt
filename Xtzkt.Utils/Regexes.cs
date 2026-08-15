using System.Text.RegularExpressions;

namespace Xtzkt.Utils;

public static partial class Regexes
{
    public const char NullEscapeChar = '\uFFFD';
    public const string NullEscapeString = "\uFFFD";

    [GeneratedRegex("^(tz1|tz2|tz3|tz4|tz5|KT1|sr1)[1-9A-HJ-NP-Za-km-z]{33}$")]
    public static partial Regex MichelsonAddress();

    [GeneratedRegex("^0x[0-9A-Fa-f]{40}$")]
    public static partial Regex EvmAddress();

    [GeneratedRegex("^B[1-9A-HJ-NP-Za-km-z]{50}$")]
    public static partial Regex MichelsonBlockHash();

    [GeneratedRegex("^o[1-9A-HJ-NP-Za-km-z]{50}$")]
    public static partial Regex MichelsonOperationHash();

    [GeneratedRegex("^P[1-9A-HJ-NP-Za-km-z]{50}$")]
    public static partial Regex MichelsonProtocolHash();

    [GeneratedRegex("^expr[1-9A-HJ-NP-Za-km-z]{50}$")]
    public static partial Regex MichelsonExpressionHash();

    [GeneratedRegex("^0x[0-9A-Fa-f]{64}$")]
    public static partial Regex EvmHash();

    [GeneratedRegex("^[1-9A-HJ-NP-Za-km-z]+$")]
    public static partial Regex Base58();

    [GeneratedRegex("^0x[0-9A-Fa-f]+$")]
    public static partial Regex Hex();

    [GeneratedRegex("^[0-9]+$")]
    public static partial Regex Number();

    [GeneratedRegex(@"^[\w:]+$")]
    public static partial Regex Field();

    [GeneratedRegex(@"^[\w:]+(\.[\w:]+)+$")]
    public static partial Regex FieldPath();

    [GeneratedRegex(@"^"".*""$")]
    public static partial Regex Quoted();

    [GeneratedRegex(@"^\[[0-9]+\]$")]
    public static partial Regex ArrayIndex();

    [GeneratedRegex(@"(?:""(?:(?:\\"")|(?:[^""]))*"")|(?:[^"".]+)")]
    public static partial Regex JsonPathParser();

    [GeneratedRegex(@"(?<=(^|[^\\])(\\\\)*)\\u0000")]
    public static partial Regex RestrictedUnicode();
}
