using System;

namespace OsEngine.Entity
{
    public class SecurityVolumes
    {
        public string SecurityNameCode;

        /// <summary>
        /// volume in currency
        /// </summary>
        public decimal Volume24h;

        /// <summary>
        /// volume in USDT
        /// </summary>
        public decimal Volume24hUSDT;

        public DateTime TimeUpdate;
    }
}
