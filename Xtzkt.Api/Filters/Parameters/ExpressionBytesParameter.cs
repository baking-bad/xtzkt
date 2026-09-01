using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ExpressionBytesBinder))]
public class ExpressionBytesParameter : INormalizable
{
    /// <summary>
    /// **Equal** filter mode (optional, i.e. `param.eq=123` is the same as `param=123`).
    /// Specify an expression hash to get items where the specified field is equal to the specified value.
    ///
    /// Example: `?address=expr...`.
    /// </summary>
    public byte[]? Eq { get; set; }

    /// <summary>
    /// **Not equal** filter mode.
    /// Specify an expression hash to get items where the specified field is not equal to the specified value.
    ///
    /// Example: `?address.ne=expr...`.
    /// </summary>
    public byte[]? Ne { get; set; }

    /// <summary>
    /// **In list** (any of) filter mode.
    /// Specify a comma-separated list of expression hashes to get items where the specified field is equal to one of the specified values.
    ///
    /// Example: `?address.in=expr...,expr...`.
    /// </summary>
    public List<byte[]>? In { get; set; }

    /// <summary>
    /// **Not in list** (none of) filter mode.
    /// Specify a comma-separated list of expression hashes to get items where the specified field is not equal to all the specified values.
    ///
    /// Example: `?address.ni=expr...,expr...`.
    /// </summary>
    public List<byte[]>? Ni { get; set; }


    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Hex.GetStringRaw(Eq)}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Hex.GetStringRaw(Ne)}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.Select(x => Hex.GetStringRaw(x)).OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.Select(x => Hex.GetStringRaw(x)).OrderBy(x => x))}&");

        return sb.ToString();
    }
}
