using System.Globalization;

namespace Xtzkt.Utils;

/// <summary>
/// Postgres' jsonb helpers.
/// </summary>
public static class Jsonb
{
    const long MaxIntegerDigits = 131_072;
    const long MaxScale = 16_383;
    const long MaxExponent = int.MaxValue / 2;

    /// <summary>
    /// Checks that a JSON number fits into numeric, which jsonb stores numbers as.
    /// </summary>
    /// <param name="number">Raw UTF-8 text of a JSON number.</param>
    /// <param name="textLength">Length of the number as postgres prints it back.</param>
    /// <returns></returns>
    public static bool IsValidNumber(ReadOnlySpan<byte> number, out int textLength)
    {
        textLength = 0;

        var pos = 0;
        var negative = pos < number.Length && number[pos] == '-';
        if (negative)
            pos++;

        var start = pos;
        pos = SkipDigits(number, pos);
        var intPart = number[start..pos];
        if (intPart.Length == 0)
            return false;

        var fracPart = ReadOnlySpan<byte>.Empty;
        if (pos < number.Length && number[pos] == '.')
        {
            start = ++pos;
            pos = SkipDigits(number, pos);
            fracPart = number[start..pos];
            if (fracPart.Length == 0)
                return false;
        }

        long exponent = 0;
        if (pos < number.Length && (number[pos] == 'e' || number[pos] == 'E'))
        {
            pos++;
            var negativeExponent = pos < number.Length && number[pos] == '-';
            if (pos < number.Length && (number[pos] == '-' || number[pos] == '+'))
                pos++;

            start = pos;
            pos = SkipDigits(number, pos);
            if (pos == start)
                return false;

            foreach (var digit in number[start..pos])
            {
                exponent = exponent * 10 + (digit - '0');
                if (exponent > MaxExponent)
                    return false;
            }

            if (negativeExponent)
                exponent = -exponent;
        }

        if (pos != number.Length)
            return false;

        // the scale counts every written fractional digit, trailing zeros included
        var scale = Math.Max(fracPart.Length - exponent, 0);
        if (scale > MaxScale)
            return false;

        // the weight is taken from the first significant digit, and zero has none (nor a sign: -0 comes back as 0)
        long integerDigits = 0;
        var first = intPart.IndexOfAnyExcept((byte)'0');
        if (first >= 0)
            integerDigits = intPart.Length - first + exponent;
        else if ((first = fracPart.IndexOfAnyExcept((byte)'0')) >= 0)
            integerDigits = exponent - first;
        else
            negative = false;

        if (integerDigits > MaxIntegerDigits)
            return false;

        // numeric_out writes plain digits: the sign, the integer part (a lone 0 if there's none), the scale after a point
        textLength = (negative ? 1 : 0) + (int)Math.Max(integerDigits, 1) + (scale > 0 ? (int)scale + 1 : 0);
        return true;
    }

    /// <summary>
    /// Checks if a JSON string escapes a surrogate without its pair, which jsonb rejects.
    /// </summary>
    /// <param name="escaped">Raw UTF-8 text of a JSON string with escapes.</param>
    /// <returns></returns>
    public static bool HasLoneSurrogate(ReadOnlySpan<byte> escaped)
    {
        var high = false;
        for (var i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] != '\\' || escaped[++i] != 'u')
            {
                if (high)
                    return true;
                continue;
            }

            var unit = (char)ushort.Parse(escaped.Slice(i + 1, 4), NumberStyles.AllowHexSpecifier);
            i += 4;

            if (char.IsLowSurrogate(unit))
            {
                if (!high)
                    return true;
                high = false;
            }
            else if (high)
            {
                return true;
            }
            else
            {
                high = char.IsHighSurrogate(unit);
            }
        }

        return high;
    }

    static int SkipDigits(ReadOnlySpan<byte> span, int pos)
    {
        while (pos < span.Length && span[pos] >= '0' && span[pos] <= '9')
            pos++;

        return pos;
    }
}
