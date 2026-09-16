using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Int64RangeBinder))]
public class Int64RangeParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?balance=123`.
    /// </summary>
    public long? Eq { get; set; }

    /// <summary>
    /// **Greater than** mode.
    /// Returns items where 'param' is greater than 'value'.
    ///
    /// Example: `?balance.gt=123`.
    /// </summary>
    public long? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** mode.
    /// Returns items where 'param' is greater than or equal to 'value'.
    ///
    /// Example: `?balance.ge=123`.
    /// </summary>
    public long? Ge { get; set; }

    /// <summary>
    /// **Less than** mode.
    /// Returns items where 'param' is less than 'value'.
    ///
    /// Example: `?balance.lt=123`.
    /// </summary>
    public long? Lt { get; set; }

    /// <summary>
    /// **Less or equal** mode.
    /// Returns items where 'param' is less than or equal to 'value'.
    ///
    /// Example: `?balance.le=123`.
    /// </summary>
    public long? Le { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null) sb.Append($"{name}.eq={Eq}&");
        if (Gt != null) sb.Append($"{name}.gt={Gt}&");
        if (Ge != null) sb.Append($"{name}.ge={Ge}&");
        if (Lt != null) sb.Append($"{name}.lt={Lt}&");
        if (Le != null) sb.Append($"{name}.le={Le}&");

        return sb.ToString();
    }

    public bool Reduce()
    {
        if (Eq != null)
        {
            if (Gt != null)
            {
                if (Gt >= Eq) return false;
                Gt = null;
            }
            if (Ge != null)
            {
                if (Ge > Eq) return false;
                Ge = null;
            }
            if (Lt != null)
            {
                if (Lt <= Eq) return false;
                Lt = null;
            }
            if (Le != null)
            {
                if (Le < Eq) return false;
                Le = null;
            }
            return true;
        }

        if (Gt != null)
        {
            if (Gt < Ge) Gt = null;
            else if (Gt >= Lt) return false;
            else if (Gt >= Le) return false;
        }

        if (Ge != null)
        {
            if (Ge <= Gt) Ge = null;
            else if (Ge >= Lt) return false;
            else if (Ge > Le) return false;
        }

        if (Lt != null)
        {
            if (Lt > Le) Lt = null;
            else if (Lt <= Gt) return false;
            else if (Lt <= Ge) return false;
        }

        if (Le != null)
        {
            if (Le >= Lt) Le = null;
            else if (Le <= Gt) return false;
            else if (Le < Ge) return false;
        }

        return true;
    }

    public static implicit operator Int64RangeParameter(long eq) => new() { Eq = eq };
}
