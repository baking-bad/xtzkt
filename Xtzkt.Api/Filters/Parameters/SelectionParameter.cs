using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(SelectionBinder))]
public class SelectionParameter : INormalizable
{
    /// <summary>
    /// **Fields** selection mode (optional, i.e. `select.fields=balance` is the same as `select=balance`).
    /// Specify a comma-separated list of fields to include into response.
    ///
    /// Example:
    /// `?select=address,balance as b,metadata.name as meta_name` will result in
    /// `[ { "address": "asd", "b": 10, "meta_name": "qwe" } ]`.
    /// </summary>
    public List<SelectionField>? Fields { get; set; }

    /// <summary>
    /// **Values** selection mode.
    /// Specify a comma-separated list of fields to include their values into response.
    ///
    /// Example:
    /// `?select.values=address,balance,metadata.name`  will result in
    /// `[ [ "asd", 10, "qwe" ] ]`.
    /// </summary>
    public List<SelectionField>? Values { get; set; }

    public string Normalize(string name)
    {
        return $"{name}.{(Fields != null ? "fields" : "values")}={string.Join(",", (Fields ?? Values)!.Select(x => $"{x.Full} as {x.Alias}"))}";
    }
}

public class SelectionField
{
    public required string Alias { get; init; }
    public required string Field { get; init; }
    public required string Full { get; init; }
    public string[]? Path { get; init; }

    public string? PathString => Path == null ? null : string.Join(",", Path);

    public string? Column { get; set; }

    public SelectionField? SubField()
    {
        if (Path == null) return null;

        var subField = Path[0];
        var (subPath, subFull) = Path.Length > 1
            ? (Path[1..], string.Join(".", Path))
            : (null, subField);

        return new()
        {
            Field = subField,
            Path = subPath,
            Alias = subFull,
            Full = subFull
        };
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out SelectionField? field)
    {
        var ss = value.Split(" as ");
        if (ss.Length == 1 || ss.Length == 2 && Regexes.Field().IsMatch(ss[1]))
        {
            if (Regexes.Field().IsMatch(ss[0]))
            {
                field = new()
                {
                    Field = ss[0],
                    Alias = ss[^1],
                    Full = ss[0]
                };
                return true;
            }
            else if (Regexes.FieldPath().IsMatch(ss[0]))
            {
                var sss = ss[0].Split('.');
                field = new()
                {
                    Field = sss[0],
                    Path = sss[1..],
                    Alias = ss[^1],
                    Full = ss[0]
                };
                return true;
            }
        }
        field = null;
        return false;
    }
}