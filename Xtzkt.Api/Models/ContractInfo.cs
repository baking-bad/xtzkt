namespace Xtzkt.Api.Models
{
    public class ContractInfo : AddressInfo
    {
        /// <summary>32-bit hash of the contract parameter and storage types (helps to find similar contracts).</summary>
        public int TypeHash { get; set; }

        /// <summary>32-bit hash of the contract code (helps to find identical contracts).</summary>
        public int CodeHash { get; set; }

        /// <summary>Address that originated the contract.</summary>
        public required AddressInfo Creator { get; init; }
    }
}
