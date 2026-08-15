using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class Eip7702Delegation
{
    /// <summary>Internal unique delegation id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the delegation belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block where the delegation happened.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block where the delegation happened.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Id of the transaction operation, carried the authorization.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long TransactionId { get; set; }

    /// <summary>Sender of the transaction, carried the authorization.</summary>
    public required AddressInfo Sender { get; set; }

    /// <summary>Account that signed the authorization and delegated its code.</summary>
    public required AddressInfo Authority { get; set; }

    /// <summary>Authority nonce the authorization was signed with.</summary>
    public int Nonce { get; set; }

    /// <summary>Contract the authority had been delegated to before (`null` if there was no delegation).</summary>
    public AddressInfo? PrevDelegate { get; set; }

    /// <summary>Contract the authority is delegated to (`null` if the delegation was revoked).</summary>
    public AddressInfo? Delegate { get; set; }
}
