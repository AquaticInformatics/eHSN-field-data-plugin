using System.Globalization;

namespace EhsnPlugin.Helpers
{
    public static class StringExtensions
    {
        public static double? ToNullableDouble(this string text)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                return number;

            return null;
        }

        public static bool? ToNullableBoolean(this string text)
        {
            if (bool.TryParse(text, out var value))
                return value;

            return null;
        }

        public static bool ToBoolean(this string text, bool defaultValue = false)
        {
            return text.ToNullableBoolean() ?? defaultValue;
        }

        public static string WithDefaultValue(this string text, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(text)
                ? defaultValue
                : text;
        }
    }
}
