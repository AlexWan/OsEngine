using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bybit.Utilities
{
    public static class Utils
    {
        private static DateTime epoch_time = new DateTime(1970, 1, 1, 0, 0, 0);

        public static long GetMillisecondsFromEpochStart()
        {
            return GetMillisecondsFromEpochStart(DateTime.UtcNow);
        }

        public static long GetMillisecondsFromEpochStart(DateTime time)
        {
            return (long)(time - epoch_time).TotalMilliseconds;
        }

        public static long GetSecondsFromEpochStart(DateTime time)
        {
            return (long)(time - epoch_time).TotalSeconds;
        }

        public static DateTime LongToDateTime(long seconds)
        {
            var start_time = new DateTime(1970, 1, 1, 0, 0, 0);
            TimeSpan time_span = TimeSpan.FromSeconds(seconds);

            return start_time + time_span;
        }

        public static decimal StringToDecimal(string value)
        {
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return Convert.ToDecimal(value.Replace(",", sep).Replace(".", sep));
        }
    }
}
