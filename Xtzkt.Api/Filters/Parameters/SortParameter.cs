using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(SortBinder))]
public class SortParameter : INormalizable
{
    public required List<(string field, bool asc)> Cols { get; set; }

    public string Normalize(string name)
    {
        return Cols.Count > 0
            ? $"{name}={string.Join(',', Cols.Select(x => $"{x.field}.{(x.asc ? "a" : "d")}"))}&"
            : string.Empty;
    }
}
