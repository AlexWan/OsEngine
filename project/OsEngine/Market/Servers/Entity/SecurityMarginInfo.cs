using System;

namespace OsEngine.Market.Servers.Entity
{
    /// <summary>
    /// margin settings of one security on the exchange: margin mode and leverage
    /// настройки маржи одного инструмента на бирже: режим маржи и плечо
    /// </summary>
    public class SecurityMarginInfo
    {
        /// <summary>
        /// security name as in Security.Name
        /// имя инструмента, как в Security.Name
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// leverage set on the exchange for this security
        /// плечо, выставленное на бирже для этого инструмента
        /// </summary>
        public decimal Leverage;

        /// <summary>
        /// true - isolated margin, false - cross margin
        /// true - изолированная маржа, false - кросс-маржа
        /// </summary>
        public bool IsIsolated;

        /// <summary>
        /// time when the exchange data was received (UTC)
        /// время получения данных от биржи (UTC)
        /// </summary>
        public DateTime TimeUpdate;
    }
}
