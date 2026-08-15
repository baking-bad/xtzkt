using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Utils;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(JsonBinder))]
public class JsonParameter : INormalizable
{
    public const string Null = "ъуъ";

    /// <summary>
    /// **Equal** filter mode (optional, i.e. `param.eq=123` is the same as `param=123`).
    /// Specify a JSON value to get items where the specified field is equal to the specified value.
    ///
    /// Example: `?parameter.from=tz1...` or `?parameter.signatures.[3].[0]=null` or `?parameter.sigs.[*]=null`.
    /// </summary>
    public List<(JsonPath[], string)>? Eq { get; set; }

    /// <summary>
    /// **Not equal** filter mode.
    /// Specify a JSON value to get items where the specified field is not equal to the specified value.
    ///
    /// Example: `?parameter.ne=true` or `?parameter.amount.ne=0`.
    /// </summary>
    public List<(JsonPath[], string)>? Ne { get; set; }

    /// <summary>
    /// **Greater than** filter mode.
    /// Specify a string to get items where the specified field is greater than the specified value.
    /// Note that all stored JSON values are strings, so this will be a comparison of two strings, so we recommend comparing values of the same type,
    /// e.g. numeric strings with numeric strings (`parameter.number.gt=123`), datetime strings with datetime strings (`parameter.date.gt=2021-01-01T00:00:00Z`), etc.
    /// Otherwise, result may surprise you.
    ///
    /// Example: `?parameter.balance.gt=1234` or `?parameter.time.gt=2021-02-01T00:00:00Z`.
    /// </summary>
    public List<(JsonPath[], string)>? Gt { get; set; }

    /// <summary>
    /// **Greater or equal** filter mode.
    /// Specify a string to get items where the specified field is greater than equal to the specified value.
    /// Note that all stored JSON values are strings, so this will be a comparison of two strings, so we recommend comparing values of the same type,
    /// e.g. numeric strings with numeric strings (`parameter.number.gt=123`), datetime strings with datetime strings (`parameter.date.gt=2021-01-01T00:00:00Z`), etc.
    /// Otherwise, result may surprise you.
    ///
    /// Example: `?parameter.balance.ge=1234` or `?parameter.time.ge=2021-02-01T00:00:00Z`.
    /// </summary>
    public List<(JsonPath[], string)>? Ge { get; set; }

    /// <summary>
    /// **Less than** filter mode.
    /// Specify a string to get items where the specified field is less than the specified value.
    /// Note that all stored JSON values are strings, so this will be a comparison of two strings, so we recommend comparing values of the same type,
    /// e.g. numeric strings with numeric strings (`parameter.number.gt=123`), datetime strings with datetime strings (`parameter.date.gt=2021-01-01T00:00:00Z`), etc.
    /// Otherwise, result may surprise you.
    ///
    /// Example: `?parameter.balance.lt=1234` or `?parameter.time.lt=2021-02-01T00:00:00Z`.
    /// </summary>
    public List<(JsonPath[], string)>? Lt { get; set; }

    /// <summary>
    /// **Less or equal** filter mode.
    /// Specify a string to get items where the specified field is less than or equal to the specified value.
    /// Note that all stored JSON values are strings, so this will be a comparison of two strings, so we recommend comparing values of the same type,
    /// e.g. numeric strings with numeric strings (`parameter.number.gt=123`), datetime strings with datetime strings (`parameter.date.gt=2021-01-01T00:00:00Z`), etc.
    /// Otherwise, result may surprise you.
    ///
    /// Example: `?parameter.balance.le=1234` or `?parameter.time.le=2021-02-01T00:00:00Z`.
    /// </summary>
    public List<(JsonPath[], string)>? Le { get; set; }

    /// <summary>
    /// **Same as** filter mode.
    /// Specify a string template to get items where the specified field matches the specified template.
    /// This mode supports wildcard `*`. Use `\*` as an escape symbol.
    ///
    /// Example: `?parameter.as=*mid*` or `?parameter.as=*end`.
    /// </summary>
    public List<(JsonPath[], string)>? As { get; set; }

    /// <summary>
    /// **Unlike** filter mode.
    /// Specify a string template to get items where the specified field doesn't match the specified template.
    /// This mode supports wildcard `*`. Use `\*` as an escape symbol.
    ///
    /// Example: `?parameter.un=*mid*` or `?parameter.un=*end`.
    /// </summary>
    public List<(JsonPath[], string)>? Un { get; set; }

    /// <summary>
    /// **In list** (any of) filter mode.
    /// Specify a comma-separated list of strings or JSON array to get items where the specified field is equal to one of the specified values.
    ///
    /// Example: `?parameter.amount.in=1,2,3` or `?parameter.in=[{"from":"tz1","to":"tz2"},{"from":"tz2","to":"tz1"}]`.
    /// </summary>
    public List<(JsonPath[], string[])>? In { get; set; }

    /// <summary>
    /// **Not in list** (none of) filter mode.
    /// Specify a comma-separated list of strings to get items where the specified field is not equal to all the specified values.
    ///
    /// Example: `?parameter.amount.ni=1,2,3` or `?parameter.ni=[{"from":"tz1","to":"tz2"},{"from":"tz2","to":"tz1"}]`.
    /// </summary>
    public List<(JsonPath[], string[])>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
        {
            foreach (var (path, value) in Eq)
            {
                sb.Append($"{name}.{Normalize(path)}.eq={value}&");
            }
        }

        if (Ne != null)
        {
            foreach (var (path, value) in Ne)
            {
                sb.Append($"{name}.{Normalize(path)}.ne={value}&");
            }
        }

        if (Gt != null)
        {
            foreach (var (path, value) in Gt)
            {
                sb.Append($"{name}.{Normalize(path)}.gt={value}&");
            }
        }

        if (Ge != null)
        {
            foreach (var (path, value) in Ge)
            {
                sb.Append($"{name}.{Normalize(path)}.ge={value}&");
            }
        }

        if (Lt != null)
        {
            foreach (var (path, value) in Lt)
            {
                sb.Append($"{name}.{Normalize(path)}.lt={value}&");

            }
        }

        if (Le != null)
        {
            foreach (var (path, value) in Le)
            {
                sb.Append($"{name}.{Normalize(path)}.le={value}&");

            }
        }

        if (As != null)
        {
            foreach (var (path, value) in As)
            {
                sb.Append($"{name}.{Normalize(path)}.as={value}&");
            }
        }

        if (Un != null)
        {
            foreach (var (path, value) in Un)
            {
                sb.Append($"{name}.{Normalize(path)}.un={value}&");

            }
        }

        if (In != null)
        {
            foreach (var (path, values) in In)
            {
                sb.Append($"{name}.{Normalize(path)}.in={string.Join(",", values.OrderBy(x => x))}&");
            }
        }

        if (Ni != null)
        {
            foreach (var (path, values) in Ni)
            {
                sb.Append($"{name}.{Normalize(path)}.ni={string.Join(",", values.OrderBy(x => x))}&");
            }
        }

        return sb.ToString();
    }

    static string Normalize(JsonPath[] jsonPaths)
    {
        return string.Join(".", jsonPaths.Select(x => x.Type > JsonPathType.Key ? $"[{x.Value ?? "*"}]" : x.Value));
    }
}

public class JsonPath
{
    public JsonPathType Type { get; }
    public string? Value { get; }

    public JsonPath(string value)
    {
        if (Regexes.Field().IsMatch(value))
        {
            Type = JsonPathType.Field;
            Value = value;
        }
        else if (Regexes.Quoted().IsMatch(value))
        {
            Type = JsonPathType.Key;
            Value = value[1..^1];
        }
        else if (Regexes.ArrayIndex().IsMatch(value))
        {
            Type = JsonPathType.Index;
            Value = value[1..^1];
        }
        else if (value == "[*]")
        {
            Type = JsonPathType.Any;
            Value = null;
        }
        else
        {
            Type = JsonPathType.None;
            Value = value;
        }
    }

    public static bool TryParse(string path, out JsonPath[] res)
    {
        res = [..(path.Contains('"')
            ? Regexes.JsonPathParser().Matches(path).Select(x => x.Value)
            : path.Split("."))
            .Select(x => new JsonPath(x))];

        return res.All(x => x.Type != JsonPathType.None);
    }

    public static string Merge(JsonPath[] path, string value, int ind = 0)
    {
        if (ind == path.Length)
            return value;

        if (path[ind].Type > JsonPathType.Key)
            return $"[{Merge(path, value, ++ind)}]";

        return $"{{\"{path[ind].Value}\":{Merge(path, value, ++ind)}}}";
    }

    public static string?[] Select(JsonPath[] path)
    {
        return [.. path.Select(x => x.Value)];
    }
}

public enum JsonPathType
{
    None,
    Field,
    Key,
    Index,
    Any
}
