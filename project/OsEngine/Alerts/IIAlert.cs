/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.Alerts
{
    public interface IIAlert
    {

        void Save();

        void Load();

        void Delete();

        bool IsOn { get; set;}

        string Name { get; set; }

        AlertType TypeAlert { get; set; }

        AlertSignal CheckSignal(List<Candle> candles, Security sec);
    }

    /// <summary>
    /// alert type
    /// тип алерта
    /// </summary>
    public enum AlertType
    {

        /// <summary>
        /// alert for charts
        /// алерт для чарта
        /// </summary>
        ChartAlert,

        /// <summary>
        /// alert price
        /// алерт достижения цены
        /// </summary>
        PriceAlert
    }

    /// <summary>
    /// alert slippage type
    /// тип алерта
    /// </summary>
    public enum AlertSlippageType
    {
        /// <summary>
        /// процент
        /// </summary>
        Persent,
        
        /// <summary>
        /// абсолютные значения
        /// </summary>
        Absolute,

        /// <summary>
        /// кол-во шагов цены
        /// </summary>
        PriceStep
    }
}
