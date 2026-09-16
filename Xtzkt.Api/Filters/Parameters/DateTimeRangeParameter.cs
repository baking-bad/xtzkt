using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils.Extensions;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(DateTimeRangeBinder))]
public class DateTimeRangeParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?timestamp=2024-01-01`.
    /// </summary>
    public DateTime? Eq { get; set; }

    /// <summary>
    /// **Greater than** mode.
    /// Returns items where 'param' is greater than 'value'.
    ///
    /// Example: `?timestamp.gt=2024-01-01`.
    /// </summary>
    public DateTime? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** mode.
    /// Returns items where 'param' is greater than or equal to 'value'.
    ///
    /// Example: `?timestamp.ge=2024-01-01`.
    /// </summary>
    public DateTime? Ge { get; set; }

    /// <summary>
    /// **Less than** mode.
    /// Returns items where 'param' is less than 'value'.
    ///
    /// Example: `?timestamp.lt=2024-01-01`.
    /// </summary>
    public DateTime? Lt { get; set; }

    /// <summary>
    /// **Less or equal** mode.
    /// Returns items where 'param' is less than or equal to 'value'.
    ///
    /// Example: `?timestamp.le=2024-01-01`.
    /// </summary>
    public DateTime? Le { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null) sb.Append($"{name}.eq={Eq.Value.Ticks}&");
        if (Gt != null) sb.Append($"{name}.gt={Gt.Value.Ticks}&");
        if (Ge != null) sb.Append($"{name}.ge={Ge.Value.Ticks}&");
        if (Lt != null) sb.Append($"{name}.lt={Lt.Value.Ticks}&");
        if (Le != null) sb.Append($"{name}.le={Le.Value.Ticks}&");

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

    public static bool TryMerge(DateTimeRangeParameter? a, DateTimeRangeParameter? b, out DateTimeRangeParameter? res)
    {
        if (a == null)
        {
            res = b;
            return true;
        }

        if (b == null)
        {
            res = a;
            return true;
        }

        res = new();

        if (a.Eq != null)
        {
            if (b.Eq != null && b.Eq != a.Eq)
                return false;
            else
                res.Eq = a.Eq;
        }
        else
        {
            res.Eq = b.Eq;
        }

        if (a.Gt != null)
        {
            if (b.Gt != null && b.Gt != a.Gt)
                res.Gt = DateTimeExtension.Max(a.Gt.Value, b.Gt.Value);
            else
                res.Gt = a.Gt;
        }
        else
        {
            res.Gt = b.Gt;
        }

        if (a.Ge != null)
        {
            if (b.Ge != null && b.Ge != a.Ge)
                res.Ge = DateTimeExtension.Max(a.Ge.Value, b.Ge.Value);
            else
                res.Ge = a.Ge;
        }
        else
        {
            res.Ge = b.Ge;
        }

        if (a.Lt != null)
        {
            if (b.Lt != null && b.Lt != a.Lt)
                res.Lt = DateTimeExtension.Min(a.Lt.Value, b.Lt.Value);
            else
                res.Lt = a.Lt;
        }
        else
        {
            res.Lt = b.Lt;
        }

        if (a.Le != null)
        {
            if (b.Le != null && b.Le != a.Le)
                res.Le = DateTimeExtension.Min(a.Le.Value, b.Le.Value);
            else
                res.Le = a.Le;
        }
        else
        {
            res.Le = b.Le;
        }

        return res.Reduce();
    }
}
