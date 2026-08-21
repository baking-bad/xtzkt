using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TransactionOperationFilter : ManagerOperationFilter
{
    /// <summary>
    /// Matches an address against any of the listed fields (`sender`, `target`, `initiator`),
    /// instead of just one. This is how you get everything related to an address in a single request,
    /// rather than querying each field separately and merging the results yourself.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?anyof.sender.target=tz1...`, `?anyof.sender.target.initiator=tz1...`.
    /// </summary>
    public AnyOfParameter? Anyof { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the sender's contract code.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?senderCodeHash=123456`.
    /// </summary>
    public Int32NullParameter? SenderCodeHash { get; set; }

    /// <summary>
    /// Filters by target address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?target=KT1...`.
    /// </summary>
    public AddressInfoParameter? Target { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the target's contract code.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?targetCodeHash=123456`.
    /// </summary>
    public Int32NullParameter? TargetCodeHash { get; set; }

    /// <summary>
    /// Filters by initiator address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?initiator=tz1...`.
    /// </summary>
    public AddressInfoNullParameter? Initiator { get; set; }

    /// <summary>
    /// Filters by entrypoint called on the target contract.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?entrypoint=transfer`, `?entrypoint=null`.
    /// </summary>
    public StringNullParameter? Entrypoint { get; set; }

    /// <summary>
    /// Filters by parameters passed to the target contract.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?parameters.to=tz1...`.
    /// </summary>
    public JsonParameter? Parameters { get; set; }

    /// <summary>
    /// Filters by which source the entrypoint, the parameters and the result were decoded with:
    /// `false` for operations, decoded with a trusted one (contract ABI for `evm`, contract schema
    /// for `michelson`), `true` for operations, where the only available source was a guess, made
    /// by matching the function selector against popular standards, because the contract ABI is
    /// unknown.
    ///
    /// Example: `?guessed=false`.
    /// </summary>
    public bool? Guessed { get; set; }

    /// <summary>
    /// Filters by alias address (Tezos X cross-runtime only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?alias=KT1...`.
    /// </summary>
    public AddressInfoNullParameter? Alias { get; set; }

    /// <summary>
    /// Filters by gateway address (Tezos X cross-runtime only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?gateway=0x...`.
    /// </summary>
    public AddressInfoNullParameter? Gateway { get; set; }

    /// <summary>
    /// Filters by the deposit operation the transaction claimed off the queue (Tezos X bridge claims
    /// only). This is the deposit's `id`, not its `depositId` — use `?claimDepositId.ne=null`
    /// to get all the claims.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?claimDepositId=123`, `?claimDepositId.ne=null`.
    /// </summary>
    public Int64NullParameter? ClaimDepositId { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Anyof == null &&
        SenderCodeHash == null &&
        Target == null &&
        TargetCodeHash == null &&
        Initiator == null &&
        Entrypoint == null &&
        Parameters == null &&
        Guessed == null &&
        Alias == null &&
        Gateway == null &&
        ClaimDepositId == null &&
        Or == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.anyof", Anyof),
        ($"{name}.senderCodeHash", SenderCodeHash),
        ($"{name}.target", Target),
        ($"{name}.targetCodeHash", TargetCodeHash),
        ($"{name}.initiator", Initiator),
        ($"{name}.entrypoint", Entrypoint),
        ($"{name}.parameters", Parameters),
        ($"{name}.guessed", Guessed),
        ($"{name}.alias", Alias),
        ($"{name}.gateway", Gateway),
        ($"{name}.claimDepositId", ClaimDepositId),
        ($"{name}.or", Or));
}
