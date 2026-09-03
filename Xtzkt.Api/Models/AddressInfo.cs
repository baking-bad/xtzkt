namespace Xtzkt.Api.Models
{
    public class AddressInfo
    {
        /// <summary>Internal unique address id.</summary>
        public int Id { get; init; }

        /// <summary>Address hash (`tz`, `KT`, `sr` or `0x`).</summary>
        public required string Hash { get; init; }

        /// <summary>Address type (`l1_user`, `l1_baker`, `l1_contract`, `x_evm_contract`, ...).</summary>
        public string? Type { get; init; }

        /// <summary>Human-readable name of the address, if it's a known one.</summary>
        public string? Alias { get; init; }
    }
}
