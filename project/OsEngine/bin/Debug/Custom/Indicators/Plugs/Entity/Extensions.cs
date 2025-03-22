using System;
using System.Globalization;

namespace OsEngine.Entity
{
    public static class Extensions
    {
        public static decimal ToDecimal(this string value)
        {
            if (value.Contains("E"))
            {
                return Convert.ToDecimal(value.ToDouble());
            }
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

        public static string ToStringWithNoEndZero(this decimal value)
        {
            string result = value.ToString(CultureInfo.GetCultureInfo("ru-RU"));

            if (result.Contains(","))
            {
                result = result.TrimEnd('0');

                if (result.EndsWith(","))
                {
                    result = result.TrimEnd(',');
                }
            }

            return result;
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

        /// <summary>
        /// получить точность шкалы на основании количества знаков после запятой
        /// </summary>
        /// <param name="value">количество знаков после запятой</param>
        public static decimal GetValueByDecimals(this int value)
        {
            switch (value)
            {
                case 0:
                    return 1;
                case 1:
                    return 0.1m;
                case 2:
                    return 0.01m;
                case 3:
                    return 0.001m;
                case 4:
                    return 0.0001m;
                case 5:
                    return 0.00001m;
                case 6:
                    return 0.000001m;
                case 7:
                    return 0.0000001m;
                case 8:
                    return 0.00000001m;
                case 9:
                    return 0.000000001m;
                case 10:
                    return 0.0000000001m;
                default:
                    return 0;
            }
        }
    }

    public static class DateTimeParseHelper
    {
        /// <summary>
        /// Converts date-time from two strings, a date string and a time string.
        /// </summary>
        /// <param name="dateString">Date string in the format "YYYYMMDD".</param>
        /// <param name="timeString">Time string in the format  "HHmmSS".</param>
        public static DateTime ParseFromTwoStrings(string dateString, string timeString)
        {
            ParseDateOrTimeString(dateString, out int year, out int month, out int day);
            ParseDateOrTimeString(timeString, out int hour, out int minute, out int second);
            return new DateTime(year, month, day, hour, minute, second);
        }

        /// <summary>
        /// Converts a date or time string to the output variables year-month-day
        /// (if a date string) or hour-minute-second (if a time string).
        /// </summary>
        public static void ParseDateOrTimeString(string dateOrTimeString, out int yearHour, out int monthMinute, out int daySecond)
        {
            int dateOrTimeInt = Convert.ToInt32(dateOrTimeString);
            yearHour = dateOrTimeInt / 10000;
            monthMinute = dateOrTimeInt / 100 % 100;
            daySecond = dateOrTimeInt % 100;
        }
    }
}
