using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Int64Binder))]
public class Int64Parameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?balance=123`.
    /// </summary>
    public long? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?balance.ne=123`.
    /// </summary>
    public long? Ne { get; set; }

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

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?balance.in=12,34,56`.
    /// </summary>
    public List<long>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?balance.ni=12,34,56`.
    /// </summary>
    public List<long>? Ni { get; set; }

    public static implicit operator Int64Parameter(long value) => new() { Eq = value };

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
