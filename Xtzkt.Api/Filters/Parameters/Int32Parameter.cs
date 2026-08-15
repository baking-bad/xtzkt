using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Int32Binder))]
public class Int32Parameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?balance=123`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?balance.ne=123`.
    /// </summary>
    public int? Ne { get; set; }

    /// <summary>
    /// **Greater than** mode.
    /// Returns items where 'param' is greater than 'value'.
    ///
    /// Example: `?balance.gt=123`.
    /// </summary>
    public int? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** mode.
    /// Returns items where 'param' is greater than or equal to 'value'.
    ///
    /// Example: `?balance.ge=123`.
    /// </summary>
    public int? Ge { get; set; }

    /// <summary>
    /// **Less than** mode.
    /// Returns items where 'param' is less than 'value'.
    ///
    /// Example: `?balance.lt=123`.
    /// </summary>
    public int? Lt { get; set; }

    /// <summary>
    /// **Less or equal** mode.
    /// Returns items where 'param' is less than or equal to 'value'.
    ///
    /// Example: `?balance.le=123`.
    /// </summary>
    public int? Le { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?balance.in=12,34,56`.
    /// </summary>
    public List<int>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?balance.ni=12,34,56`.
    /// </summary>
    public List<int>? Ni { get; set; }

    public bool Matches(int value) =>
        (Eq == null || value == Eq) &&
        (Ne == null || value != Ne) &&
        (Gt == null || value > Gt) &&
        (Ge == null || value >= Ge) &&
        (Lt == null || value < Lt) &&
        (Le == null || value <= Le) &&
        (In == null || In.Contains(value)) &&
        (Ni == null || !Ni.Contains(value));

    public static implicit operator Int32Parameter(int value) => new() { Eq = value };

    public static Int32Parameter? operator +(Int32Parameter? a, Int32Parameter? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        var res = new Int32Parameter();

        if (a.Eq != null)
        {
            if (b.Eq != null && b.Eq != a.Eq)
                res.Eq = -1;
            else
                res.Eq = a.Eq;
        }
        else
        {
            res.Eq = b.Eq;
        }

        if (a.Ne != null)
        {
            if (b.Ne != null && b.Ne != a.Ne)
                res.Ni = [a.Ne.Value, b.Ne.Value];
            else
                res.Ne = a.Ne;
        }
        else
        {
            res.Ne = b.Ne;
        }

        if (a.Gt != null)
        {
            if (b.Gt != null && b.Gt != a.Gt)
                res.Gt = Math.Max(a.Gt.Value, b.Gt.Value);
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
                res.Ge = Math.Max(a.Ge.Value, b.Ge.Value);
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
                res.Lt = Math.Min(a.Lt.Value, b.Lt.Value);
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
                res.Le = Math.Min(a.Le.Value, b.Le.Value);
            else
                res.Le = a.Le;
        }
        else
        {
            res.Le = b.Le;
        }

        if (a.In != null)
        {
            if (b.In != null)
                res.In = [.. a.In.Intersect(b.In)];
            else
                res.In = a.In;
        }
        else
        {
            res.In = b.In;
        }

        if (a.Ni != null)
        {
            if (b.Ni != null)
                res.Ni = [.. a.Ni.Concat(b.Ni).Distinct()];
            else
                res.Ni = a.Ni;
        }
        else
        {
            res.Ni = b.Ni;
        }

        return res;
    }

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
