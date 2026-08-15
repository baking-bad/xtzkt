using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BigMapTagsBinder))]
public class BigMapTagsParameter : INormalizable
{
    /// <summary>
    /// **Has** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' contains the specified tag.
    ///
    /// Example: `?tags=ledger`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **Doesn't have** mode.
    /// Returns items where 'param' doesn't contain the specified tag.
    ///
    /// Example: `?tags.ne=ledger`.
    /// </summary>
    public int? Ne { get; set; }

    /// <summary>
    /// **Has any** mode.
    /// Returns items where 'param' contains any of the specified tags.
    ///
    /// Example: `?tags.any=metadata,token_metadata`.
    /// </summary>
    public int? Any { get; set; }

    /// <summary>
    /// **Has all** mode.
    /// Returns items where 'param' contains all of the specified tags.
    ///
    /// Example: `?tags.all=persistent,ledger`.
    /// </summary>
    public int? All { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne}&");

        if (Any != null)
            sb.Append($"{name}.any={Any}&");

        if (All != null)
            sb.Append($"{name}.all={All}&");

        return sb.ToString();
    }
}
