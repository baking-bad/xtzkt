using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class Selection : INormalizable
{
    /// <summary>
    /// Comma-separated list of fields to return instead of the default set, which makes responses
    /// noticeably smaller and faster. Each item is `{field}{.path?}{ as alias?}`, so you can reach into
    /// nested objects and rename the result. Leave it out to get the default set of fields.
    ///
    /// Note that selecting a single field flattens the response into a plain array of values.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?select=id,balance`, `?select=balance,token.metadata.symbol as symbol`.
    /// </summary>
    public SelectionParameter? Select { get; set; }

    public string[]? Cols() => Select?.Fields?.Select(x => x.Alias).ToArray();

    public List<SelectionField> Fields() => (Select?.Fields ?? Select?.Values)!;

    public string Normalize(string name) => ResponseCacheService.BuildKey("", ($"{name}.select", Select));
}
