using System;

namespace OsEngine.Market.Servers.Entity
{
    public class ServerWorkingTimeSettings
    {
        /// <summary>
        /// beginning of the trading session
        /// начало торговой сессии
        /// </summary>
        public TimeSpan StartSessionTime;

        /// <summary>
        /// ending of the trading session
        /// конец торговой сессии
        /// </summary>
        public TimeSpan EndSessionTime;

        /// <summary>
        /// server time zone
        /// временная зона сервера
        /// </summary>
        public string ServerTimeZone;

        /// <summary>
        /// if the exchange is working on weekends, it returns true 
        /// если биржа работает по выходным возвращается true
        /// </summary>
        public bool WorkingAtWeekend;
    }
}
