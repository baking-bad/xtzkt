using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(SearchScopesBinder))]
public class SearchScopesParameter : INormalizable
{
    [JsonIgnore]
    public HashSet<string>? Scopes { get; set; }

    public string Normalize(string name)
    {
        return Scopes != null
            ? $"{name}={string.Join(',', Scopes.OrderBy(x => x))}&"
            : string.Empty;
    }
}
