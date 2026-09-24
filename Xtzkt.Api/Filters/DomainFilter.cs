using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class DomainFilter : INormalizable
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
    /// Filters by name registry contract.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?registry.hash=KT1...`.
    /// </summary>
    public AddressInfoParameter? Registry { get; set; }

    /// <summary>
    /// Filters by domain level: `1` for top-level domains, `2` for `alice.tez`, and so on.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?level=2`.
    /// </summary>
    public Int32Parameter? Level { get; set; }

    /// <summary>
    /// Filters by domain name.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?name=alice.tez`, `?name.as=*.alice.tez`.
    /// </summary>
    public StringParameter? Name { get; set; }

    /// <summary>
    /// Filters by owner address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?owner=tz1...`.
    /// </summary>
    public AddressHashParameter? Owner { get; set; }

    /// <summary>
    /// Filters by address the domain resolves to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?address=tz1...`, `?address=null`.
    /// </summary>
    public AddressHashNullParameter? Address { get; set; }

    /// <summary>
    /// Filters by reverse record: `true` for domains that are the primary name of their address.
    ///
    /// Example: `?reverse=true`.
    /// </summary>
    public bool? Reverse { get; set; }

    /// <summary>
    /// Filters by expiration time.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?expiration.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? Expiration { get; set; }

    /// <summary>
    /// Filters by domain data.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?data.twitter:handle=alice`.
    /// </summary>
    public JsonParameter? Data { get; set; }

    /// <summary>
    /// Filters by level of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by level of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.registry", Registry),
        ($"{name}.level", Level),
        ($"{name}.name", Name),
        ($"{name}.owner", Owner),
        ($"{name}.address", Address),
        ($"{name}.reverse", Reverse),
        ($"{name}.expiration", Expiration),
        ($"{name}.data", Data),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.lastTimestamp", LastTimestamp));
}
