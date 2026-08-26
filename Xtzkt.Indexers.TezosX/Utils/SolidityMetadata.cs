using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.Numerics;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils;

public class SolidityMetadata
{
    public string? IpfsCid { get; init; }
    public string? Bzzr0 { get; init; }
    public string? Bzzr1 { get; init; }
    public string? SolcVersion { get; init; }
    public bool? Experimental { get; init; }

    public static bool TryRead(byte[] bytecode, [NotNullWhen(true)] out SolidityMetadata? result)
    {
        result = null;
        
        if (bytecode.Length < 2)
            return false;

        var len = (bytecode[^2] << 8) | bytecode[^1];
        if (len == 0 || len > bytecode.Length - 2)
            return false;

        var cbor = bytecode.AsMemory(bytecode.Length - len - 2, len);
        if (cbor.Span[0] != 0xa1 &&
            cbor.Span[0] != 0xa2 &&
            cbor.Span[0] != 0xa3)
            return false;

        try
        {
            string? ipfsCid = null;
            string? bzzr0 = null;
            string? bzzr1 = null;
            string? solcVersion = null;
            bool? experimental = null;
            bool match = false;

            var reader = new CborReader(cbor);
            if (reader.PeekState() != CborReaderState.StartMap)
                return false;

            if (reader.ReadStartMap() is not int cnt)
                return false;

            var dict = new Dictionary<string, string>(cnt);
            for (int i = 0; i < cnt; i++)
            {
                if (reader.PeekState() != CborReaderState.TextString)
                {
                    reader.SkipValue();
                    reader.SkipValue();
                    continue;
                }

                switch (reader.ReadTextString())
                {
                    case "ipfs":
                        if (TryReadByteString(reader, out var _ipfsCid))
                        {
                            ipfsCid = EncodeCid(_ipfsCid);
                            match = true;
                        }
                        break;
                    case "bzzr0":
                        if (TryReadByteString(reader, out var _bzzr0))
                        {
                            bzzr0 = Hex.GetString(_bzzr0);
                            match = true;
                        }
                        break;
                    case "bzzr1":
                        if (TryReadByteString(reader, out var _bzzr1))
                        {
                            bzzr1 = Hex.GetString(_bzzr1);
                            match = true;
                        }
                        break;
                    case "solc":
                        if (TryReadSolcVersion(reader, out var _solcVersion))
                        {
                            solcVersion = _solcVersion;
                            match = true;
                        }
                        break;
                    case "experimental":
                        if (TryReadBoolean(reader, out var _experimental))
                        {
                            experimental = _experimental;
                            match = true;
                        }
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (!match)
                return false;

            result = new SolidityMetadata
            {
                IpfsCid = ipfsCid,
                Bzzr0 = bzzr0,
                Bzzr1 = bzzr1,
                SolcVersion = solcVersion,
                Experimental = experimental,
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool TryReadSolcVersion(CborReader reader, [NotNullWhen(true)] out string? res)
    {
        if (reader.PeekState() == CborReaderState.TextString)
        {
            res = reader.ReadTextString();
            return res.Length != 0;
        }

        if (TryReadByteString(reader, out var bytes) && bytes.Length == 3)
        {
            res = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
            return true;
        }

        res = null;
        return false;
    }

    static bool TryReadByteString(CborReader reader, [NotNullWhen(true)] out byte[]? res)
    {
        if (reader.PeekState() != CborReaderState.ByteString)
        {
            reader.SkipValue();
            res = null;
            return false;
        }

        res = reader.ReadByteString();
        return true;
    }

    static bool TryReadBoolean(CborReader reader, out bool res)
    {
        if (reader.PeekState() != CborReaderState.Boolean)
        {
            reader.SkipValue();
            res = false;
            return false;
        }

        res = reader.ReadBoolean();
        return true;
    }

    static string EncodeCid(Span<byte> bytes)
    {
        var chars = new List<char>(46);

        var num = 0;
        while (num < bytes.Length && bytes[num] == 0)
        {
            chars.Add('1');
            num++;
        }

        var bigInteger = new BigInteger(bytes[num..], true, true);
        
        while (bigInteger > 0)
        {
            chars.Add("123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"[(int)(bigInteger % 58)]);
            bigInteger /= 58;
        }

        return new string([..chars.Reverse<char>()]);
    }
}