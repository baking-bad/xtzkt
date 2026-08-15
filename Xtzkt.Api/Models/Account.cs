namespace Xtzkt.Api.Models;

public class Account
{
    /// <summary>
    /// Canonical hash of the account — the hash of its first owner address. Note that it's not
    /// necessarily the hash the account was looked up by: looking an account up by an alias hash
    /// returns the hash of the address owning that alias.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// All the addresses the account owns, owners first, aliases last.
    /// </summary>
    public required List<Address> Addresses { get; set; }
}
