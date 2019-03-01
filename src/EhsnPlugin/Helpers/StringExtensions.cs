using System.Globalization;

namespace EhsnPlugin.Helpers
{
    public static class StringExtensions
    {
        public static double? ToNullableDouble(this string value)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                return number;

            return null;
        }
    }
}
