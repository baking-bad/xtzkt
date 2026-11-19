using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Utf8BytesNullBinder))]
public class Utf8BytesNullParameter : INormalizable
{
    /// <summary>
    /// Sentinel value used to represent a `null` filter.
    /// </summary>
    public static readonly byte[] Null = [];

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to the UTF-8 bytes of 'value'. Use `null` to get items where 'param' is not set.
    ///
    /// Example: `?entrypoint=deposit` or `?entrypoint=null`.
    /// </summary>
    public byte[]? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to the UTF-8 bytes of 'value'. Use `null` to get items where 'param' is set.
    ///
    /// Example: `?entrypoint.ne=deposit` or `?entrypoint.ne=null`.
    /// </summary>
    public byte[]? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to the UTF-8 bytes of any of comma-separated 'values'. Use `null` to include items where 'param' is not set.
    ///
    /// Example: `?entrypoint.in=deposit,mint` or `?entrypoint.in=deposit,null`.
    /// </summary>
    public List<byte[]>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to the UTF-8 bytes of any of comma-separated 'values'. Use `null` to exclude items where 'param' is not set.
    ///
    /// Example: `?entrypoint.ni=deposit,mint` or `?entrypoint.ni=deposit,null`.
    /// </summary>
    public List<byte[]>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Format(Eq)}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Format(Ne)}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.Select(Format).OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.Select(Format).OrderBy(x => x))}&");

        return sb.ToString();
    }

    static string Format(byte[] value) => value.Length == 0 ? "null" : Hex.GetStringRaw(value);
}
