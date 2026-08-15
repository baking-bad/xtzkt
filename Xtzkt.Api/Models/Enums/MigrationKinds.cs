using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class MigrationKinds
{
    public const string Bootstrap       = "bootstrap";
    public const string ActivateBaker   = "activate_baker";
    public const string AirDrop         = "air_drop";
    public const string ProposalInvoice = "proposal_invoice";
    public const string CodeChange      = "code_change";
    public const string Origination     = "origination";
    public const string RemoveBigMapKey = "remove_bigmap_key";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Bootstrap,       (int)MigrationKind.Bootstrap },
        { ActivateBaker,   (int)MigrationKind.ActivateBaker },
        { AirDrop,         (int)MigrationKind.AirDrop },
        { ProposalInvoice, (int)MigrationKind.ProposalInvoice },
        { CodeChange,      (int)MigrationKind.CodeChange },
        { Origination,     (int)MigrationKind.Origination },
        { RemoveBigMapKey, (int)MigrationKind.RemoveBigMapKey },
    };

    public static string ToString(int value) => value switch
    {
        (int)MigrationKind.Bootstrap       => Bootstrap,
        (int)MigrationKind.ActivateBaker   => ActivateBaker,
        (int)MigrationKind.AirDrop         => AirDrop,
        (int)MigrationKind.ProposalInvoice => ProposalInvoice,
        (int)MigrationKind.CodeChange      => CodeChange,
        (int)MigrationKind.Origination     => Origination,
        (int)MigrationKind.RemoveBigMapKey => RemoveBigMapKey,
        _ => throw new Exception("invalid value")
    };
}
