using Netezos;
using Xtzkt.Data.Models;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Data.Utils;

public static class Hashes
{
    #region blocks
    public static string FormatEvmBlockHash(byte[] hash) => Hex.GetString(hash);
    public static string FormatMichelsonBlockHash(byte[] hash) => Base58.Encode(hash, Prefixes.B);
    public static string FormatBlockHash(byte[] hash, Layer layer) => layer switch
    {
        Layer.TezosX => Hex.GetString(hash),
        _ => Base58.Encode(hash, Prefixes.B),
    };

    public static byte[] ParseEvmBlockHash(string hash) => Hex.GetBytes(hash);
    public static byte[] ParseMichelsonBlockHash(string hash) => Base58.Decode(hash, Prefixes.B);
    public static byte[] ParseBlockHash(string hash) => hash[0] == 'B' ? Base58.Decode(hash, Prefixes.B) : Hex.GetBytes(hash);
    #endregion

    #region operations
    public static string FormatEvmOperationHash(byte[] hash) => Hex.GetString(hash);
    public static string FormatMichelsonOperationHash(byte[] hash) => Base58.Encode(hash, Prefixes.o);
    public static string FormatOperationHash(byte[] hash, Direction direction) => direction switch
    {
        Direction.XEvm or Direction.XEvmMichelson => Hex.GetString(hash),
        _ => Base58.Encode(hash, Prefixes.o),
    };
    public static string FormatOperationHash(byte[] hash, Env env) => env switch
    {
        Env.XEvm => Hex.GetString(hash),
        _ => Base58.Encode(hash, Prefixes.o),
    };
    public static string FormatOperationHash(byte[] hash, Runtime runtime) => runtime switch
    {
        Runtime.Evm => Hex.GetString(hash),
        _ => Base58.Encode(hash, Prefixes.o),
    };

    public static byte[] ParseEvmOperationHash(string hash) => Hex.GetBytes(hash);
    public static byte[] ParseMichelsonOperationHash(string hash) => Base58.Decode(hash, Prefixes.o);
    public static byte[] ParseOperationHash(string hash) => hash[0] == 'o' ? Base58.Decode(hash, Prefixes.o) : Hex.GetBytes(hash);
    #endregion

    #region bigmap keys
    public static string FormatExprHash(byte[] hash) => Base58.Encode(hash, Prefixes.expr);
    public static byte[] ParseExprHash(string hash) => Base58.Decode(hash, Prefixes.expr);
    #endregion

    #region smart rollup commitments
    public static string FormatSrc1Hash(byte[] hash) => Base58.Encode(hash, Prefixes.src1);
    public static byte[] ParseSrc1Hash(string hash) => Base58.Decode(hash, Prefixes.src1);
    #endregion
}
