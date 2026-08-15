using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(TokenInfoBinder))]
public class TokenInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal token id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by token contract address.
    /// Click on the parameter to expand more details.
    /// </summary>
    public AddressInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by token id within the contract.
    /// Click on the parameter to expand more details.
    /// </summary>
    public BigIntegerParameter? TokenId { get; set; }

    /// <summary>
    /// Filters by token standard (`fa1.2`, `fa2`, `erc20`, `erc721` or `erc1155`).
    /// Click on the parameter to expand more details.
    /// </summary>
    public TokenStandardParameter? Standard { get; set; }

    /// <summary>
    /// Filters by token metadata.
    /// Click on the parameter to expand more details.
    /// </summary>
    public JsonParameter? Metadata { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        Contract == null &&
        TokenId == null &&
        Standard == null &&
        Metadata == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.contract", Contract),
        ($"{name}.tokenId", TokenId),
        ($"{name}.standard", Standard),
        ($"{name}.metadata", Metadata));
}
