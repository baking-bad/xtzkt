using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class MigrationOperationFilter : BaseOperationFilter
{
    /// <summary>
    /// Filters by runtime (`evm` or `michelson`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?runtime=evm`.
    /// </summary>
    public RuntimeParameter? Runtime { get; set; }

    /// <summary>
    /// Filters by migration kind (`bootstrap`, `activate_baker`, `air_drop`, `proposal_invoice`,
    /// `code_change`, `origination`, `remove_bigmap_key`, `burn_balance`, `amend_address` or `bind_alias`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?kind=origination`.
    /// </summary>
    public MigrationKindParameter? Kind { get; set; }

    /// <summary>
    /// Filters by account the migration is applied to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?account.hash=tz1...`.
    /// </summary>
    public AddressInfoParameter? Account { get; set; }

    /// <summary>
    /// Filters by raw balance change. The value is compared as is, without converting between decimals,
    /// so it's mutez for `michelson` migrations, and 18 decimals for `evm` ones.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?balanceChange.ne=0`.
    /// </summary>
    public BigIntegerParameter? BalanceChange { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Runtime == null &&
        Kind == null &&
        Account == null &&
        BalanceChange == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.runtime", Runtime),
        ($"{name}.kind", Kind),
        ($"{name}.account", Account),
        ($"{name}.balanceChange", BalanceChange));
}
