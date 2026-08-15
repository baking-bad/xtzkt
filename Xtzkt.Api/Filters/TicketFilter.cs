using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TicketFilter : INormalizable
{
    /// <summary>
    /// Filters by internal unique id. Within a chain ids grow over time, so sorting by id sorts chronologically.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?id=123`, `?id.in=123,456`.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Filters by ticketer (contract that issued the ticket).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?ticketer=KT1...`.
    /// </summary>
    public AddressInfoParameter? Ticketer { get; set; }

    /// <summary>
    /// Filters by address that first minted the ticket.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstMinter=KT1...`.
    /// </summary>
    public AddressInfoParameter? FirstMinter { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the ticket content type.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?typeHash=123456`.
    /// </summary>
    public Int32Parameter? TypeHash { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the ticket content.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contentHash=123456`.
    /// </summary>
    public Int32Parameter? ContentHash { get; set; }

    /// <summary>
    /// Filters by ticket content type in Micheline format (specified as a JSON value).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?rawType={"prim":"nat"}`.
    /// </summary>
    public MichelineParameter? RawType { get; set; }

    /// <summary>
    /// Filters by ticket content in Micheline format (specified as a JSON value).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?rawContent={"int":"0"}`.
    /// </summary>
    public MichelineParameter? RawContent { get; set; }

    /// <summary>
    /// Filters by ticket content in JSON format.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?content.amount=1`.
    /// </summary>
    public JsonParameter? Content { get; set; }

    /// <summary>
    /// Filters by level of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by level of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Ticketer == null &&
        FirstMinter == null &&
        TypeHash == null &&
        ContentHash == null &&
        RawType == null &&
        RawContent == null &&
        Content == null &&
        FirstLevel == null &&
        LastLevel == null &&
        FirstTimestamp == null &&
        LastTimestamp == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.ticketer", Ticketer),
        ($"{name}.firstMinter", FirstMinter),
        ($"{name}.typeHash", TypeHash),
        ($"{name}.contentHash", ContentHash),
        ($"{name}.rawType", RawType),
        ($"{name}.rawContent", RawContent),
        ($"{name}.content", Content),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastTimestamp", LastTimestamp));
}
