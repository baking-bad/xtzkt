using System.Text;
using Microsoft.AspNetCore.Mvc;
using Netezos.Encoding;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(MichelineBinder))]
public class MichelineParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to the specified Micheline JSON value.
    ///
    /// Example: `?rawType={"prim":"nat"}`.
    /// </summary>
    public IMicheline? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to the specified Micheline JSON value.
    ///
    /// Example: `?rawType.ne={"prim":"nat"}`.
    /// </summary>
    public IMicheline? Ne { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Convert.ToHexString(Eq.ToBytes())}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Convert.ToHexString(Ne.ToBytes())}&");

        return sb.ToString();
    }
}
