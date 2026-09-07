using System.ComponentModel.DataAnnotations;

namespace Xtzkt.Api.Utils.Validation;

[AttributeUsage(AttributeTargets.Property)]
public abstract class RangeAttribute(int minimum) : ValidationAttribute("Must be between {1} and {2}.")
{
    public int Minimum { get; } = minimum;

    public abstract int Maximum { get; }

    public override bool IsValid(object? value)
    {
        return value is int v && v >= Minimum && v <= Maximum;
    }

    public override string FormatErrorMessage(string name)
    {
        return string.Format(ErrorMessageString, name, Minimum, Maximum);
    }
}
