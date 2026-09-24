using System.Diagnostics.CodeAnalysis;

namespace Xtzkt.Api.Utils;

public static class LikePattern
{
    /// <summary>
    /// Converts a template, where `*` is a wildcard and `\*` is a literal `*`, into a LIKE pattern
    /// that takes everything else literally.
    /// </summary>
    [return: NotNullIfNotNull(nameof(template))]
    public static string? FromTemplate(string? template) => template?
        .Replace("\\*", "ъуъ")
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_")
        .Replace("*", "%")
        .Replace("ъуъ", "*");
}
