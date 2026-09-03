using Microsoft.EntityFrameworkCore;
using Netezos.Utils;

namespace Xtzkt.Data.Models
{
    public class EvmScript() : Script(Runtime.Evm)
    {
        public required byte[] Code { get; set; }
        public string? AbiJson { get; set; }

        public string? SolidityMetadataBzzr0 { get; set; }
        public string? SolidityMetadataBzzr1 { get; set; }
        public string? SolidityMetadataIpfs { get; set; }
        public string? SolidityMetadataSolc { get; set; }
        public bool? SolidityMetadataExperimental { get; set; }

        #region hash
        public static int GetHash(byte[] bytes)
        {
            var hash = Blake2b.GetDigest(bytes, 32);
            return (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | (hash[3]);
        }
        #endregion
    }

    public static class EvmScriptModel
    {
        public static void BuildEvmScriptModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
