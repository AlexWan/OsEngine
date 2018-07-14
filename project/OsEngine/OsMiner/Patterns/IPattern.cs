/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// интерфейс для паттернов в системе
    /// </summary>
    public interface IPattern
    {
        /// <summary>
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        decimal Weigth { get; set; }

        /// <summary>
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        decimal Expand { get; set; }

        /// <summary>
        /// тип паттерна
        /// </summary>
        PatternType Type { get; set; }

        /// <summary>
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="indicators">индикаторы</param>
        /// <param name="numberPattern">индекс по которому мы с мотрим паттерн</param>
        /// <returns></returns>
        bool ThisIsIt(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern);

        /// <summary>
        /// установить паттерн с текущих данных
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="indicators">индикаторы</param>
        /// <param name="numberPattern">индекс по которому мы с мотрим паттерн</param>
        void SetFromIndex(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern);

        /// <summary>
        /// загрузить паттерн из строки сохранения
        /// </summary>
        void Load(string saveString);

        /// <summary>
        /// взять строку для сохранения паттерна
        /// </summary>
        /// <returns></returns>
        string GetSaveString();

        /// <summary>
        /// Взять копию
        /// </summary>
        IPattern GetCopy();

    }

    /// <summary>
    /// типы паттернов в системе
    /// </summary>
    public enum PatternType
    {
        /// <summary>
        /// свечной паттерн
        /// </summary>
        Candle,

        /// <summary>
        /// паттерн на объёмах
        /// </summary>
        Volume,

        /// <summary>
        /// время торговли
        /// </summary>
        Time,

        /// <summary>
        /// паттерн на индикаторах
        /// </summary>
        Indicators,
    }

    /// <summary>
    /// тип использования паттерна
    /// </summary>
    public enum UsePatternType
    {
        /// <summary>
        /// для открытия позиции
        /// </summary>
        OpenPosition,

        /// <summary>
        /// для закрытия позиции
        /// </summary>
        ClosePosition
    }
}
