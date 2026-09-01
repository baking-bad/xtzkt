using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Xtzkt.Utils.Encoding;

public static class Base58
{
    public static string Encode(byte[] payload, byte[] prefix)
    {
        if (!TryClassify(prefix, out var shape))
            throw new ArgumentException("Invalid prefix", nameof(prefix));

        if (shape.PayloadLen != payload.Length)
            throw new ArgumentException("Invalid length", nameof(payload));

        return shape.Kind switch
        {
            Shapes.P2x32 => Base58Fixed<Shape2x32>.Encode(payload, prefix),
            Shapes.P4x32 => Base58Fixed<Shape4x32>.Encode(payload, prefix),
            _ => Base58Fixed<Shape3x20>.Encode(payload, prefix),
        };
    }

    public static bool TryEncode(byte[] payload, byte[] prefix, [NotNullWhen(true)] out string? result)
    {
        if (TryClassify(prefix, out var shape) && shape.PayloadLen == payload.Length)
        {
            result = shape.Kind switch
            {
                Shapes.P2x32 => Base58Fixed<Shape2x32>.Encode(payload, prefix),
                Shapes.P4x32 => Base58Fixed<Shape4x32>.Encode(payload, prefix),
                _ => Base58Fixed<Shape3x20>.Encode(payload, prefix),
            };
            return true;
        }

        result = null;
        return false;
    }

    public static byte[] Decode(ReadOnlySpan<char> base58, byte[] prefix)
    {
        if (!TryClassify(prefix, out var shape))
            throw new ArgumentException("Invalid prefix", nameof(prefix));

        if (shape.Base58Len != base58.Length)
            throw new ArgumentException("Invalid length", nameof(base58));

        var result = shape.Kind switch
        {
            Shapes.P2x32 => Base58Fixed<Shape2x32>.Decode(base58, prefix),
            Shapes.P4x32 => Base58Fixed<Shape4x32>.Decode(base58, prefix),
            _ => Base58Fixed<Shape3x20>.Decode(base58, prefix),
        };
        
        return result ?? throw new ArgumentException("Invalid Base58", nameof(base58));
    }

    public static bool TryDecode(ReadOnlySpan<char> value, byte[] prefix, [NotNullWhen(true)] out byte[]? payload)
    {
        if (TryClassify(prefix, out var shape) && shape.Base58Len == value.Length)
        {
            payload = shape.Kind switch
            {
                Shapes.P2x32 => Base58Fixed<Shape2x32>.Decode(value, prefix),
                Shapes.P4x32 => Base58Fixed<Shape4x32>.Decode(value, prefix),
                _ => Base58Fixed<Shape3x20>.Decode(value, prefix),
            };
            return payload != null;
        }

        payload = null;
        return false;
    }

    static bool TryClassify(byte[] prefix, out Shape shape)
    {
        switch (prefix)
        {
            case [1, 52]:               // B
            case [5, 116]:              // o
            case [2, 170]:              // P
                shape = new(Shapes.P2x32, 32, 51);
                return true;
            case [13, 44, 64, 27]:      // expr
            case [17, 165, 134, 138]:   // src1
                shape = new(Shapes.P4x32, 32, 54);
                return true;
            case [6, 161, 159]:         // tz1
            case [6, 161, 161]:         // tz2
            case [6, 161, 164]:         // tz3
            case [6, 161, 166]:         // tz4
            case [2, 90, 121]:          // KT1
            case [6, 124, 117]:         // sr1
                shape = new(Shapes.P3x20, 20, 36);
                return true;
            default:
                shape = default;
                return false;
        }
    }

    enum Shapes { P2x32, P4x32, P3x20 }

    readonly record struct Shape(Shapes Kind, int PayloadLen, int Base58Len);
}

interface IBase58Shape
{
    static abstract int PrefixLen { get; }
    static abstract int PayloadLen { get; }
    static abstract int Base58Len { get; }
}

/// <summary>2 + 32 -> 51 characters: `B`, `o`, `P`.</summary>
readonly struct Shape2x32 : IBase58Shape
{
    public static int PrefixLen => 2;
    public static int PayloadLen => 32;
    public static int Base58Len => 51;
}

/// <summary>4 + 32 -> 54 characters: `expr`, `src1`.</summary>
readonly struct Shape4x32 : IBase58Shape
{
    public static int PrefixLen => 4;
    public static int PayloadLen => 32;
    public static int Base58Len => 54;
}

/// <summary>3 + 20 -> 36 characters: `tz1`, `tz2`, `tz3`, `tz4`, `KT1`, `sr1`.</summary>
readonly struct Shape3x20 : IBase58Shape
{
    public static int PrefixLen => 3;
    public static int PayloadLen => 20;
    public static int Base58Len => 36;
}

static class Base58Fixed<TShape> where TShape : struct, IBase58Shape
{
    static ReadOnlySpan<byte> Alphabet => "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"u8;

    static ReadOnlySpan<byte> Base58Ascii =>
    [
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255,   0,
          1,   2,   3,   4,   5,   6,   7,   8, 255, 255,
        255, 255, 255, 255, 255,   9,  10,  11,  12,  13,
         14,  15,  16, 255,  17,  18,  19,  20,  21, 255,
         22,  23,  24,  25,  26,  27,  28,  29,  30,  31,
         32, 255, 255, 255, 255, 255, 255,  33,  34,  35,
         36,  37,  38,  39,  40,  41,  42,  43, 255,  44,
         45,  46,  47,  48,  49,  50,  51,  52,  53,  54,
         55,  56,  57
    ];

    // 58^5, the largest power of 58 that fits in a uint32, so that dividing a 64-bit accumulator
    // by it stays a single reciprocal multiply and each pass produces 5 digits.
    const uint Pow5 = 656356768;

    // stackalloc sizes have to be compile-time constants: a size derived from TShape becomes a
    // real localloc - allocated and zeroed at runtime - which costs more than the conversion
    // itself. So take a constant upper bound and slice it down to the shape.
    const int MaxRaw = 40;
    const int MaxLimbs = 10;
    const int MaxChars = 54;

    static int DataLen => TShape.PrefixLen + TShape.PayloadLen;
    static int RawLen => DataLen + 4;
    static int Limbs => (RawLen + 3) / 4;
    static int Lead => 4 - (Limbs * 4 - RawLen); // bytes held by the most significant limb
    static int Head => TShape.Base58Len % 5; // digits in the first, partial chunk

    public static string Encode(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> prefix)
    {
        Span<byte> raw = stackalloc byte[MaxRaw];
        raw = raw[..RawLen];
        prefix.CopyTo(raw);
        payload.CopyTo(raw[TShape.PrefixLen..]);

        Span<byte> checksum = stackalloc byte[32];
        SHA256.HashData(raw[..DataLen], checksum);
        SHA256.HashData(checksum, checksum);
        checksum[..4].CopyTo(raw[DataLen..]);

        Span<uint> n = stackalloc uint[MaxLimbs];
        n = n[..Limbs];
        Load(raw, n);

        Span<char> chars = stackalloc char[MaxChars];
        chars = chars[..TShape.Base58Len];

        var pos = TShape.Base58Len;
        var first = 0;
        while (pos > 0)
        {
            // divide the whole number by 58^5, leaving 5 digits worth of remainder behind
            ulong rem = 0;
            for (int i = first; i < Limbs; i++)
            {
                var acc = (rem << 32) | n[i];
                var q = acc / Pow5;
                rem = acc - q * Pow5;
                n[i] = (uint)q;
            }

            var digits = (uint)rem;
            for (int i = 0; i < 5 && pos > 0; i++)
            {
                chars[--pos] = (char)Alphabet[(int)(digits % 58)];
                digits /= 58;
            }

            while (first < Limbs && n[first] == 0)
                first++;
        }

        // the value must be fully consumed - otherwise Chars in TryClassify is wrong for this
        // prefix and the most significant digits were silently dropped
        Debug.Assert(first == Limbs);

        return new string(chars);
    }

    public static byte[]? Decode(ReadOnlySpan<char> value, ReadOnlySpan<byte> prefix)
    {
        Span<uint> n = stackalloc uint[MaxLimbs];
        n = n[..Limbs];

        var pos = 0;
        if (Head > 0)
        {
            uint digits = 0, mul = 1;
            for (int i = 0; i < Head; i++)
            {
                if (!TryDigit(value[pos++], out var d)) return null;
                digits = digits * 58 + d;
                mul *= 58;
            }
            if (!MulAdd(n, mul, digits)) return null;
        }

        for (int chunk = 0; chunk < TShape.Base58Len / 5; chunk++)
        {
            uint digits = 0;
            for (int i = 0; i < 5; i++)
            {
                if (!TryDigit(value[pos++], out var d)) return null;
                digits = digits * 58 + d;
            }
            if (!MulAdd(n, Pow5, digits)) return null;
        }

        Span<byte> raw = stackalloc byte[MaxRaw];
        raw = raw[..RawLen];
        if (!Store(n, raw))
            return null;

        if (!raw[..TShape.PrefixLen].SequenceEqual(prefix))
            return null;

        Span<byte> checksum = stackalloc byte[32];
        SHA256.HashData(raw[..DataLen], checksum);
        SHA256.HashData(checksum, checksum);
        if (!checksum[..4].SequenceEqual(raw[DataLen..]))
            return null;

        return raw[TShape.PrefixLen..DataLen].ToArray();
    }

    static bool MulAdd(Span<uint> n, uint mul, uint add)
    {
        ulong carry = add;
        for (int i = Limbs - 1; i >= 0; i--)
        {
            var acc = (ulong)n[i] * mul + carry;
            n[i] = (uint)acc;
            carry = acc >> 32;
        }
        return carry == 0;
    }

    static bool TryDigit(char c, out uint digit)
    {
        digit = c < 123 ? Base58Ascii[c] : 255u;
        return digit != 255u;
    }

    static void Load(ReadOnlySpan<byte> raw, Span<uint> n)
    {
        uint lead = 0;
        for (int i = 0; i < Lead; i++)
            lead = lead << 8 | raw[i];
        n[0] = lead;

        for (int i = 0; i < Limbs - 1; i++)
            n[i + 1] = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(Lead + i * 4, 4));
    }

    static bool Store(ReadOnlySpan<uint> n, Span<byte> raw)
    {
        var lead = n[0];
        if (Lead < 4 && lead >> (Lead * 8) != 0)
            return false; // more significant bytes than the shape has room for

        for (int i = 0; i < Lead; i++)
            raw[i] = (byte)(lead >> ((Lead - 1 - i) * 8));

        for (int i = 0; i < Limbs - 1; i++)
            BinaryPrimitives.WriteUInt32BigEndian(raw.Slice(Lead + i * 4, 4), n[i + 1]);

        return true;
    }
}
