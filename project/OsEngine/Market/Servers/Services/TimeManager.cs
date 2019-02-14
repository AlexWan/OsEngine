using System;

namespace OsEngine.Market.Servers.Services
{
    public static class TimeManager
    {
        public static DateTime GetExchangeTime(string needTimeZone)
        {
            TimeZoneInfo neededTimeZone = TimeZoneInfo.FindSystemTimeZoneById(needTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, neededTimeZone);
        }
    }
}
