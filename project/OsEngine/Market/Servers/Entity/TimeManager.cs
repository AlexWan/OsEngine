using System;

namespace OsEngine.Market.Servers.Entity
{
    public static class TimeManager
    {
        /// <summary>
        /// Сдвиг московского времени относительно UTC. Москва без летнего времени (UTC+3).
        /// </summary>
        public const long MoscowUtcOffsetMs = 3 * 60 * 60 * 1000;

        public static DateTime GetExchangeTime(string needTimeZone)
        {
            TimeZoneInfo neededTimeZone = TimeZoneInfo.FindSystemTimeZoneById(needTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, neededTimeZone);
        }

        public static DateTime GetDateTimeFromTimeStamp(long timeStamp)
        {
            return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
        }

        public static DateTime GetDateTimeFromTimeStampSeconds(long timeStamp)
        {
            return new DateTime(1970, 1, 1).AddSeconds(timeStamp);
        }

        public static long GetUnixTimeStampSeconds()
        {
            return Convert.ToInt64(GetUnixTimeStamp().TotalSeconds);
        }

        public static long GetUnixTimeStampMilliseconds()
        {
            return Convert.ToInt64(GetUnixTimeStamp().TotalMilliseconds);
        }

        public static int GetTimeStampSecondsToDateTime(DateTime time)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            return (int)(time - yearBegin).TotalSeconds;
        }

        public static long GetTimeStampMilliSecondsToDateTime(DateTime time)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            return (long)(time - yearBegin).TotalMilliseconds;
        }

        public static long GetTimeStampMillisecondsFromStartTime(DateTime time)
        {
            return time.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static DateTime GetDateTimeFromStartTimeMilliseconds(long milliseconds)
        {
            return new DateTime(milliseconds * TimeSpan.TicksPerMillisecond);
        }

        private static TimeSpan GetUnixTimeStamp()
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);
            return DateTime.UtcNow - yearBegin;
        }
    }
}