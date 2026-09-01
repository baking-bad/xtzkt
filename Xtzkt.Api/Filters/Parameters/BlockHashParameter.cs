using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BlockHashBinder))]
public class BlockHashParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?hash=B...` or `?hash=0x...`.
    /// </summary>
    public byte[]? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?hash.ne=B...`.
    /// </summary>
    public byte[]? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?hash.in=B...,0x...`.
    /// </summary>
    public List<byte[]>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?hash.ni=B...,0x...`.
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
