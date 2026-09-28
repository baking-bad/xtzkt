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

        /// <summary>Primary domain name of the address, if it has one.</summary>
        public string? Domain { get; init; }

        /// <summary>Name from the address's public off-chain profile, if it has one.</summary>
        public string? Profile { get; init; }

        /// <summary>Owner of the address, if it's a runtime alias (`x_evm_alias` or `x_michelson_alias`).</summary>
        public AddressInfo? Owner { get; init; }
    }
}
