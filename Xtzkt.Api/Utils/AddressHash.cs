using System.Diagnostics.CodeAnalysis;
using Xtzkt.Utils;

namespace Xtzkt.Api.Utils;

public static class AddressHash
{
    public static bool TryNormalize(string rawValue, [NotNullWhen(true)] out string? result)
    {
        if (Regexes.EvmAddress().IsMatch(rawValue))
        {
            result = rawValue.ToLowerInvariant();
            return true;
        }
        if (Regexes.MichelsonAddress().IsMatch(rawValue))
        {
            result = rawValue;
            return true;
        }
        result = null;
        return false;
    }
}