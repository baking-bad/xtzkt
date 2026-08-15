using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

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

    public static implicit operator DateTimeParameter(DateTime value) => new() { Eq = value };

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
}
