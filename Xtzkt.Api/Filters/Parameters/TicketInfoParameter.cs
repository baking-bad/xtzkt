using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(TicketInfoBinder))]
public class TicketInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal ticket id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by ticketer (contract that issued the ticket).
    /// Click on the parameter to expand more details.
    /// </summary>
    public AddressInfoParameter? Ticketer { get; set; }

    /// <summary>
    /// Filters by ticket content type in Micheline format (specified as a JSON value).
    /// Click on the parameter to expand more details.
    /// </summary>
    public MichelineParameter? RawType { get; set; }

    /// <summary>
    /// Filters by ticket content in Micheline format (specified as a JSON value).
    /// Click on the parameter to expand more details.
    /// </summary>
    public MichelineParameter? RawContent { get; set; }

    /// <summary>
    /// Filters by ticket content in JSON format.
    /// Click on the parameter to expand more details.
    /// </summary>
    public JsonParameter? Content { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the ticket content type.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? TypeHash { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the ticket content.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? ContentHash { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        Ticketer == null &&
        RawType == null &&
        RawContent == null &&
        Content == null &&
        TypeHash == null &&
        ContentHash == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.ticketer", Ticketer),
        ($"{name}.rawType", RawType),
        ($"{name}.rawContent", RawContent),
        ($"{name}.content", Content),
        ($"{name}.typeHash", TypeHash),
        ($"{name}.contentHash", ContentHash));
}
