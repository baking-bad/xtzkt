using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(DepositTypeBinder))]
public class DepositTypeParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?type=xtz`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?type.ne=fa`.
    /// </summary>
    public int? Ne { get; set; }

    public static implicit operator DepositTypeParameter(DepositType value) => new() { Eq = (int)value };

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne}&");

        return sb.ToString();
    }
}
