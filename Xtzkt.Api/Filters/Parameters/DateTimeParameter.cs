using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils.Extensions;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(DateTimeBinder))]
public class DateTimeParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?timestamp=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?timestamp.ne=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Ne { get; set; }

    /// <summary>
    /// **Greater than** mode.
    /// Returns items where 'param' is greater than 'value'.
    ///
    /// Example: `?timestamp.gt=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** mode.
    /// Returns items where 'param' is greater than or equal to 'value'.
    ///
    /// Example: `?timestamp.ge=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Ge { get; set; }

    /// <summary>
    /// **Less than** mode.
    /// Returns items where 'param' is less than 'value'.
    ///
    /// Example: `?timestamp.lt=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Lt { get; set; }

    /// <summary>
    /// **Less or equal** mode.
    /// Returns items where 'param' is less than or equal to 'value'.
    ///
    /// Example: `?timestamp.le=2020-02-20T02:40:57Z`.
    /// </summary>
    public DateTime? Le { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?timestamp.in=2020-02-20T02:40:57Z,2020-02-21T02:40:57Z`.
    /// </summary>
    public List<DateTime>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?timestamp.ni=2020-02-20T02:40:57Z,2020-02-21T02:40:57Z`.
    /// </summary>
    public List<DateTime>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq.Value.Ticks}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne.Value.Ticks}&");

        if (Gt != null)
            sb.Append($"{name}.gt={Gt.Value.Ticks}&");

        if (Ge != null)
            sb.Append($"{name}.ge={Ge.Value.Ticks}&");

        if (Lt != null)
            sb.Append($"{name}.lt={Lt.Value.Ticks}&");

        if (Le != null)
            sb.Append($"{name}.le={Le.Value.Ticks}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x).Select(x => x.Ticks))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.OrderBy(x => x).Select(x => x.Ticks))}&");

        return sb.ToString();
    }

    public bool Reduce()
    {
        if (Eq != null)
        {
            if (Ne != null)
            {
                if (Ne == Eq) return false;
                Ne = null;
            }
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
            if (In != null)
            {
                if (!In.Contains(Eq.Value)) return false;
                In = null;
            }
            if (Ni != null)
            {
                if (Ni.Contains(Eq.Value)) return false;
                Ni = null;
            }
            return true;
        }

        if (In != null)
        {
            In = [.. In.Distinct().OrderBy(x => x)];
            if (Ne != null)
            {
                In.RemoveAll(x => x == Ne.Value);
                if (In.Count == 0) return false;
                Ne = null;
            }
            if (Gt != null)
            {
                In.RemoveAll(x => x <= Gt.Value);
                if (In.Count == 0) return false;
                Gt = null;
            }
            if (Ge != null)
            {
                In.RemoveAll(x => x < Ge.Value);
                if (In.Count == 0) return false;
                Ge = null;
            }
            if (Lt != null)
            {
                In.RemoveAll(x => x >= Lt.Value);
                if (In.Count == 0) return false;
                Lt = null;
            }
            if (Le != null)
            {
                In.RemoveAll(x => x > Le.Value);
                if (In.Count == 0) return false;
                Le = null;
            }
            if (Ni != null)
            {
                In.RemoveAll(x => Ni.Contains(x));
                if (In.Count == 0) return false;
                Ni = null;
            }
            if (In.Count == 0) return false;
            if (In.Count == 1)
            {
                Eq = In[0];
                In = null;
            }
            return true;
        }

        if (Ne != null)
        {
            if (Gt >= Ne) Ne = null;
            else if (Ge > Ne) Ne = null;
            else if (Lt <= Ne) Ne = null;
            else if (Le < Ne) Ne = null;
            else if (Ni != null)
            {
                if (!Ni.Contains(Ne.Value)) Ni.Add(Ne.Value);
                Ne = null;
            }
        }

        if (Ni != null)
        {
            Ni = [.. Ni.Distinct().OrderBy(x => x)];
            if (Gt != null)
            {
                Ni.RemoveAll(x => x <= Gt.Value);
                if (Ni.Count == 0) Ni = null;
            }
            if (Ni != null && Ge != null)
            {
                Ni.RemoveAll(x => x < Ge.Value);
                if (Ni.Count == 0) Ni = null;
            }
            if (Ni != null && Lt != null)
            {
                Ni.RemoveAll(x => x >= Lt.Value);
                if (Ni.Count == 0) Ni = null;
            }
            if (Ni != null && Le != null)
            {
                Ni.RemoveAll(x => x > Le.Value);
                if (Ni.Count == 0) Ni = null;
            }
            if (Ni != null)
            {
                if (Ni.Count == 0) Ni = null;
                else if (Ni.Count == 1)
                {
                    Ne = Ni[0];
                    Ni = null;
                }
            }
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

    public static bool TryMerge(DateTimeParameter? a, DateTimeParameter? b, out DateTimeParameter? res)
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

        if (a.In != null)
        {
            if (b.In != null)
                res.In = [.. a.In.Intersect(b.In)];
            else
                res.In = [.. a.In];
        }
        else
        {
            res.In = b.In?.ToList();
        }

        if (a.Ni != null || b.Ni != null)
        {
            res.Ni = [.. (res.Ni ?? []).Concat(a.Ni ?? []).Concat(b.Ni ?? []).Distinct()];
        }

        return res.Reduce();
    }

    public static implicit operator DateTimeParameter(DateTime eq) => new() { Eq = eq };
    public static implicit operator DateTimeParameter?(DateTimeRangeParameter? p) => p == null ? null : new() { Eq = p.Eq, Gt = p.Gt, Ge = p.Ge, Lt = p.Lt, Le = p.Le };
}
