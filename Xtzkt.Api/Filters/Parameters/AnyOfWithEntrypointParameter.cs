using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AnyOfWithEntrypointBinder))]
public class AnyOfWithEntrypointParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (optional, i.e. `param.eq=123` is the same as `param=123`).
    /// Specify a value to get items where any of the specified fields is equal to the specified value.
    /// The value is an internal address id (default, so `.id` may be omitted), or `null` to match items
    /// where any of the fields is not set. To specify an address hash instead, use the `.hash` form,
    /// which matches that hash on every chain it exists on and accepts `null` the same way.
    /// Append `%entrypoint` to an address to match only that entrypoint of it, or a bare `%` to match
    /// the address without an entrypoint.
    ///
    /// Examples: `?anyof.from.to=123%deposit`, `?anyof.from.to.hash=KT1...%deposit`.
    /// </summary>
    public (int Id, string? Entrypoint)? Eq { get; set; }

    /// <summary>
    /// **In list** (any of) filter mode.
    /// Specify a comma-separated list of values to get items where any of the specified fields is equal to one of the specified values.
    ///
    /// Examples: `?anyof.from.to.in=123,456%deposit,null`, `?anyof.from.to.hash.in=tz1...,KT1...%,null`.
    /// </summary>
    public List<(int Id, string? Entrypoint)>? In { get; set; }

    [JsonIgnore]
    public IEnumerable<string> Fields { get; set; } = [];

    internal List<(int Id, string? Entrypoint)> Values => Eq is { } eq ? [eq] : In ?? [];

    internal AnyOfParameter WithoutEntrypoints()
    {
        var ids = Values.Select(x => x.Id).Distinct().ToList();
        return new()
        {
            Fields = Fields,
            Eq = ids.Count == 1 ? ids[0] : null,
            In = ids.Count > 1 ? ids : null,
        };
    }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq is { } eq)
            sb.Append($"{name}.{string.Join(".", Fields.OrderBy(x => x))}.eq={Format(eq)}&");

        if (In?.Count > 0)
            sb.Append($"{name}.{string.Join(".", Fields.OrderBy(x => x))}.in={string.Join(",", In.Select(Format).OrderBy(x => x))}&");

        return sb.ToString();
    }

    static string Format((int Id, string? Entrypoint) value) => value.Entrypoint == null
        ? $"{value.Id}"
        : $"{value.Id}%{Uri.EscapeDataString(value.Entrypoint)}";
}
