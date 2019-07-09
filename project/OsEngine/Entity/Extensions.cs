using System;
using System.Globalization;

namespace OsEngine.Entity
{
    public static class Extensions
    {
        public static decimal ToDecimal(this string value)
        {
            return Convert.ToDecimal(value.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
        }
    }
}
