/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// trading time
    /// время торговли
    /// </summary>
    public class PatternTime : IPattern
    {
        public PatternTime()
        {
            Type = PatternType.Time;

            StartTime = new DateTime(1,1,1,9,0,0);
            EndTime= new DateTime(1,1,1,20,0,0);
            Weigth = 1;
        }

        /// <summary>
        /// pattern weight while searching for entry and exit
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        public decimal Weigth { get; set; }

        /// <summary>
        /// pattern recognition. 100% - maximum
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        public decimal Expand { get; set; }

        /// <summary>
        /// pattern type
        /// тип паттерна
        /// </summary>
        public PatternType Type { get; set; }

        /// <summary>
        /// time to start trading
        /// время начала торговли
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// end of trading time
        /// время конца торговли
        /// </summary>
        public DateTime EndTime;

        /// <summary>
        /// is the current formation our pattern
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">index on which we pattern pattern/индекс по которому мы с мотрим паттерн</param>
        public bool ThisIsIt(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {
            if (candles[numberPattern].TimeStart.Hour > StartTime.Hour &&
                candles[numberPattern].TimeStart.Hour < EndTime.Hour) 
            {
                return true;
            }

            if (candles[numberPattern].TimeStart.Hour == StartTime.Hour &&
              candles[numberPattern].TimeStart.Minute >= StartTime.Minute)
            {
                return true;
            }

            if (candles[numberPattern].TimeStart.Hour == EndTime.Hour &&
               candles[numberPattern].TimeStart.Minute <= EndTime.Minute)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// this pattern does not work
        /// в этом паттерне не работает
        /// </summary>
        public void SetFromIndex(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {

        }

        /// <summary>
        /// load pattern from save line
        /// загрузить паттерн из строки сохранения
        /// </summary>
        public void Load(string saveString)
        {
            string[] array = saveString.Split('^');

            Weigth = array[1].ToDecimal();
            StartTime = Convert.ToDateTime(array[2]);
            EndTime = Convert.ToDateTime(array[3]);
        }

        /// <summary>
        /// take a string to save the pattern
        /// взять строку для сохранения паттерна
        /// </summary>
        public string GetSaveString()
        {
            // delimiters on previous levels: # *? %
            // разделители на предыдущих уровнях: # * ? %

            string saveStr = PatternType.Time + "^";
            saveStr += Weigth + "^";
            saveStr += StartTime + "^";
            saveStr += EndTime + "^";

            return saveStr;
        }

        /// <summary>
        /// take a copy
        /// взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternTime pattern = new PatternTime();

            string save = GetSaveString();
            pattern.Load(save);

            return pattern;
        }
    }
}
