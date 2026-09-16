using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Int32NullBinder))]
public class Int32NullParameter : INormalizable
{
    /// <summary>
    /// Sentinel value used to represent a `null` filter.
    /// </summary>
    public const int Null = int.MinValue;

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'. Use `null` to get items where 'param' is not set.
    ///
    /// Example: `?balance=123` or `?balance=null`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'. Use `null` to get items where 'param' is set.
    ///
    /// Example: `?balance.ne=123` or `?balance.ne=null`.
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
    /// Returns items where 'param' is equal to any of comma-separated 'values'. Use `null` to include items where 'param' is not set.
    ///
    /// Example: `?balance.in=12,34,56` or `?balance.in=12,null`.
    /// </summary>
    public List<int>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'. Use `null` to exclude items where 'param' is not set.
    ///
    /// Example: `?balance.ni=12,34,56` or `?balance.ni=12,null`.
    /// </summary>
    public List<int>? Ni { get; set; }

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

    public bool Reduce()
    {
        Gt = NullIfNull(Gt);
        Ge = NullIfNull(Ge);
        Lt = NullIfNull(Lt);
        Le = NullIfNull(Le);

        if (Eq != null)
        {
            if (Eq == Null)
            {
                if (Ne == Eq) return false;
                if (Gt != null || Ge != null || Lt != null || Le != null) return false;
                if (In != null && !In.Contains(Null)) return false;
                if (Ni != null && Ni.Contains(Null)) return false;
                Ne = null;
                In = null;
                Ni = null;
                return true;
            }

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
                In.RemoveAll(x => x == Null || x <= Gt.Value);
                if (In.Count == 0) return false;
                Gt = null;
            }
            if (Ge != null)
            {
                In.RemoveAll(x => x == Null || x < Ge.Value);
                if (In.Count == 0) return false;
                Ge = null;
            }
            if (Lt != null)
            {
                In.RemoveAll(x => x == Null || x >= Lt.Value);
                if (In.Count == 0) return false;
                Lt = null;
            }
            if (Le != null)
            {
                In.RemoveAll(x => x == Null || x > Le.Value);
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
            if (Ne == Null)
            {
                if (Gt != null || Ge != null || Lt != null || Le != null)
                {
                    Ne = null;
                }
                else if (Ni != null)
                {
                    if (!Ni.Contains(Null)) Ni.Add(Null);
                    Ne = null;
                }
            }
            else
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
        }

        if (Ni != null)
        {
            Ni = [.. Ni.Distinct().OrderBy(x => x)];
            if (Gt != null || Ge != null || Lt != null || Le != null) Ni.RemoveAll(x => x == Null);
            if (Gt != null) Ni.RemoveAll(x => x <= Gt.Value);
            if (Ge != null) Ni.RemoveAll(x => x < Ge.Value);
            if (Lt != null) Ni.RemoveAll(x => x >= Lt.Value);
            if (Le != null) Ni.RemoveAll(x => x > Le.Value);
            if (Ni.Count == 0) Ni = null;
            else if (Ni.Count == 1)
            {
                Ne = Ni[0];
                Ni = null;
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

    public static bool TryMerge(Int32NullParameter? a, Int32NullParameter? b, out Int32NullParameter? res)
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

        res.Gt = Max(NullIfNull(a.Gt), NullIfNull(b.Gt));
        res.Ge = Max(NullIfNull(a.Ge), NullIfNull(b.Ge));
        res.Lt = Min(NullIfNull(a.Lt), NullIfNull(b.Lt));
        res.Le = Min(NullIfNull(a.Le), NullIfNull(b.Le));

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

    static int? NullIfNull(int? value) => value == Null ? null : value;

    static int? Max(int? a, int? b) => a == null ? b : b == null ? a : Math.Max(a.Value, b.Value);

    static int? Min(int? a, int? b) => a == null ? b : b == null ? a : Math.Min(a.Value, b.Value);
}
