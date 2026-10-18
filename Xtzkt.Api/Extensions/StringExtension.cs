using Xtzkt.Utils;

namespace Xtzkt.Api.Extensions
{
    public static class StringExtension
    {
        /// <summary>
        /// Replaces all chars restricted in postgres' jsonb or text with specific escape char.
        /// </summary>
        public static string SanitizeText(this string value)
        {
            return Regexes.RestrictedUnicode()
                .Replace(value, Regexes.NullEscapeString)
                .Replace((char)0, Regexes.NullEscapeChar);
        }
    }
}
