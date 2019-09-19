using System;

namespace OsEngine.Market.Servers.Entity
{
    public static class TimeManager
    {
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

        private static TimeSpan GetUnixTimeStamp()
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);
            return DateTime.UtcNow - yearBegin;
        }
    }
}