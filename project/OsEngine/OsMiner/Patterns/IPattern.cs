/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// interface for patterns in the system
    /// интерфейс для паттернов в системе
    /// </summary>
    public interface IPattern
    {
        /// <summary>
        /// pattern weight while searching for entry and exit
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        decimal Weigth { get; set; }

        /// <summary>
        /// pattern recognition. 100% - maximum
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        decimal Expand { get; set; }

        /// <summary>
        /// pattern type
        /// тип паттерна
        /// </summary>
        PatternType Type { get; set; }

        /// <summary>
        /// is the current formation our pattern
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">the index on which we watch the pattern/индекс по которому мы смотрим паттерн</param>
        /// <returns></returns>
        bool ThisIsIt(List<Candle> candles, List<IIndicator> indicators, int numberPattern);

        /// <summary>
        /// set pattern with current data
        /// установить паттерн с текущих данных
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">the index on which we watch the pattern/индекс по которому мы с мотрим паттерн</param>
        void SetFromIndex(List<Candle> candles, List<IIndicator> indicators, int numberPattern);

        /// <summary>
        /// load pattern from save line
        /// загрузить паттерн из строки сохранения
        /// </summary>
        void Load(string saveString);

        /// <summary>
        /// take a string to save the pattern
        /// взять строку для сохранения паттерна
        /// </summary>
        /// <returns></returns>
        string GetSaveString();

        /// <summary>
        /// Take a copy
        /// Взять копию
        /// </summary>
        IPattern GetCopy();

    }

    /// <summary>
    /// types of patterns in the system
    /// типы паттернов в системе
    /// </summary>
    public enum PatternType
    {
        /// <summary>
        /// candle pattern
        /// свечной паттерн
        /// </summary>
        Candle,

        /// <summary>
        /// volume pattern
        /// паттерн на объёмах
        /// </summary>
        Volume,

        /// <summary>
        /// trading time
        /// время торговли
        /// </summary>
        Time,

        /// <summary>
        /// pattern on indicators
        /// паттерн на индикаторах
        /// </summary>
        Indicators,
    }

    /// <summary>
    /// pattern usage type
    /// тип использования паттерна
    /// </summary>
    public enum UsePatternType
    {
        /// <summary>
        /// to open a position
        /// для открытия позиции
        /// </summary>
        OpenPosition,

        /// <summary>
        /// to close a position
        /// для закрытия позиции
        /// </summary>
        ClosePosition
    }
}
