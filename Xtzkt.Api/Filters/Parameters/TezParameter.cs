using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(TezBinder))]
public class TezParameter : INormalizable
{
    public const int Decimals = 18;

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value' tez.
    ///
    /// Example: `?amountTez=1.5`.
    /// </summary>
    public BigInteger? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value' tez.
    ///
    /// Example: `?amountTez.ne=0`.
    /// </summary>
    public BigInteger? Ne { get; set; }

    /// <summary>
    /// **Greater than** mode.
    /// Returns items where 'param' is greater than 'value' tez.
    ///
    /// Example: `?amountTez.gt=123.4`.
    /// </summary>
    public BigInteger? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** mode.
    /// Returns items where 'param' is greater than or equal to 'value' tez.
    ///
    /// Example: `?amountTez.ge=123.4`.
    /// </summary>
    public BigInteger? Ge { get; set; }

    /// <summary>
    /// **Less than** mode.
    /// Returns items where 'param' is less than 'value' tez.
    ///
    /// Example: `?amountTez.lt=0.000001`.
    /// </summary>
    public BigInteger? Lt { get; set; }

    /// <summary>
    /// **Less or equal** mode.
    /// Returns items where 'param' is less than or equal to 'value' tez.
    ///
    /// Example: `?amountTez.le=0.000001`.
    /// </summary>
    public BigInteger? Le { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values' tez.
    ///
    /// Example: `?amountTez.in=1,2.5`.
    /// </summary>
    public List<BigInteger>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values' tez.
    ///
    /// Example: `?amountTez.ni=1,2.5`.
    /// </summary>
    public List<BigInteger>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne}&");

        if (Gt != null)
            sb.Append($"{name}.gt={Gt}&");

        if (Ge != null)
            sb.Append($"{name}.ge={Ge}&");

        if (Lt != null)
            sb.Append($"{name}.lt={Lt}&");

        if (Le != null)
            sb.Append($"{name}.le={Le}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.OrderBy(x => x))}&");

        return sb.ToString();
    }
}
