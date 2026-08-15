using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Utf8BytesBinder))]
public class Utf8BytesParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to the UTF-8 bytes of 'value'.
    ///
    /// Example: `?entrypoint=deposit`.
    /// </summary>
    public byte[]? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to the UTF-8 bytes of 'value'.
    ///
    /// Example: `?entrypoint.ne=deposit`.
    /// </summary>
    public byte[]? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to the UTF-8 bytes of any of comma-separated 'values'.
    ///
    /// Example: `?entrypoint.in=deposit,mint`.
    /// </summary>
    public List<byte[]>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to the UTF-8 bytes of any of comma-separated 'values'.
    ///
    /// Example: `?entrypoint.ni=deposit,mint`.
    /// </summary>
    public List<byte[]>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Convert.ToHexString(Eq)}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Convert.ToHexString(Ne)}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.Select(Convert.ToHexString).OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.Select(Convert.ToHexString).OrderBy(x => x))}&");

        return sb.ToString();
    }
}
