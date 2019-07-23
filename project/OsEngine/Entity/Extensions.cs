using System;
using System.Globalization;

namespace OsEngine.Entity
{
    public static class Extensions
    {
        public static decimal ToDecimal(this string value)
        {
            try
            {
                return Convert.ToDecimal(value.Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return Convert.ToDecimal(value.ToDouble());
            }
        }

        public static double ToDouble(this string value)
        {
            return Convert.ToDouble(value.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
        }

        public static int DecimalsCount(this string value)
        {
            value = value.Replace(",", ".");

            while (value.Length > 0 &&
                   value.EndsWith("0"))
            {
                value = value.Remove(value.Length - 1);
            }

            if (value.Split('.').Length == 1)
            {
                return 0;
            }

            return value.Split('.')[1].Length;
        }
    }
}
