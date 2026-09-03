using Blake2Fast;
using Netezos.Encoding;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols
{
    static class Blind
    {
        static readonly byte[] Prefix = [1, 2, 49, 223];

        public static string GetBlindedAddress(string address, string secret)
        {
            var pkh = Base58.Parse(address).GetBytes(3, 20);
            var key = Hex.Parse(secret);
            var blind = Blake2b.ComputeHash(20, key, pkh);

            return Base58.Convert(blind, Prefix);
        }
    }
}
