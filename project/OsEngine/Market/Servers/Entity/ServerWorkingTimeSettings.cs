using System;

namespace OsEngine.Market.Servers.Entity
{
    public class ServerWorkingTimeSettings
    {
        /// <summary>
        /// начало торговой сессии
        /// </summary>
        public TimeSpan StartSessionTime;

        /// <summary>
        /// конец торговой сессии
        /// </summary>
        public TimeSpan EndSessionTime;

        /// <summary>
        /// временная зона сервера
        /// </summary>
        public string ServerTimeZone;

        /// <summary>
        /// если биржа работает по выходным возвращается true
        /// </summary>
        public bool WorkingAtWeekend;
    }
}
