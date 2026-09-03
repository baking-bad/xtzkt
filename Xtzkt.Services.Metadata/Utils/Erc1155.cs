using System.Numerics;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Services.Metadata.Utils
{
    static class Erc1155
    {
        public static string TokenIdToHex64(BigInteger tokenId)
        {
            var bytes = tokenId.ToByteArray(true, true);
            return Hex.GetStringRaw(bytes).PadLeft(64, '0');
        }
    }
}
