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
    /// `code_change`, `origination`, `remove_bigmap_key`, `burn_balance` or `amend_address`).
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
    /// Example: `?account=tz1...`.
    /// </summary>
    public AddressInfoParameter? Account { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Runtime == null &&
        Kind == null &&
        Account == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.runtime", Runtime),
        ($"{name}.kind", Kind),
        ($"{name}.account", Account));
}
