using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(CursorBinder))]
public class CursorParameter : INormalizable
{
    public List<string>? Cols { get; set; }

    public string Normalize(string name)
    {
        return Cols?.Count > 0
            ? $"{name}={string.Join(',', Cols)}&"
            : string.Empty;
    }
}
