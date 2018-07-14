/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
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
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        public decimal Weigth { get; set; }

        /// <summary>
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        public decimal Expand { get; set; }

        /// <summary>
        /// тип паттерна
        /// </summary>
        public PatternType Type { get; set; }

        /// <summary>
        /// время начала торговли
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// время конца торговли
        /// </summary>
        public DateTime EndTime;

        /// <summary>
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="indicators">индикаторы</param>
        /// <param name="numberPattern">индекс по которому мы с мотрим паттерн</param>
        public bool ThisIsIt(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern)
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
        /// в этом паттерне не работает
        /// </summary>
        public void SetFromIndex(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern)
        {

        }

        /// <summary>
        /// загрузить паттерн из строки сохранения
        /// </summary>
        public void Load(string saveString)
        {
            string[] array = saveString.Split('^');

            Weigth = Convert.ToDecimal(array[1].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            StartTime = Convert.ToDateTime(array[2]);
            EndTime = Convert.ToDateTime(array[3]);
        }

        /// <summary>
        /// взять строку для сохранения паттерна
        /// </summary>
        public string GetSaveString()
        {
            // разделители на предыдущих уровнях: # * ? %

            string saveStr = PatternType.Time + "^";
            saveStr += Weigth + "^";
            saveStr += StartTime + "^";
            saveStr += EndTime + "^";

            return saveStr;
        }

        /// <summary>
        /// взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternTime pattern = new PatternTime();
            pattern.StartTime = StartTime;
            pattern.EndTime = EndTime;
            pattern.Expand = Expand;
            pattern.Weigth = Weigth;

            return pattern;
        }
    }
}
