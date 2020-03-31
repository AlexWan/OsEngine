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
    public class PatternVolume:IPattern
    {
        /// <summary>
        /// volume search pattern
        /// паттерн для поиска по объёмам
        /// </summary>
        public PatternVolume()
        {
            Type = PatternType.Volume;
            Weigth = 1;
            Expand = 99.8m;
            Length = 2;
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
        /// pattern length
        /// длинна паттерна
        /// </summary>
        public int Length;

        /// <summary>
        /// pattern points
        /// точки паттерна
        /// </summary>
        public decimal[][] Sequence;

        /// <summary>
        /// take a pattern in the form of candles
        /// взять паттерн в виде свечек
        /// </summary>
        public List<Candle> GetInCandle()
        {
            if (Sequence == null || Sequence.Length == 0 || Sequence[0].Length == 0)
            {
                return null;
            }

            List<Candle> result = new List<Candle>();

            for (int i = 0; i < Length; i++)
            {
                Candle newCandle = new Candle();

                newCandle.Volume = Math.Round((Sequence[i][0] + Sequence[i][1]) / 2, 3);

                result.Add(newCandle);
            }

            return result;
        }

        /// <summary>
        /// is the current formation our pattern
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">the index on which we watch the pattern/индекс по которому мы смотрим паттерн</param>
        public bool ThisIsIt(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {
            if (numberPattern - Length <= 0)
            {
                return false;
            }

            if (Sequence == null ||
                Sequence.Length == 0)
            {
                return false;
            }

            PatternVolume researched = new PatternVolume();
            researched.Length = Length;
            researched.Expand = Expand;

            researched.SetFromIndex(candles, null, numberPattern);

            List<Candle> incomPattern = researched.GetInCandle();

            for (int i = 0; i < Sequence.Length; i++)
            {
                if (Sequence[i][0] < incomPattern[i].Volume || Sequence[i][1] > incomPattern[i].Volume)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// set pattern with current data
        /// установить паттерн с текущих данных
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">the index on which we watch the pattern/индекс по которому мы с мотрим паттерн</param>
        public void SetFromIndex(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {
            Sequence = new decimal[Length][];

            for (int i = 0; i < Sequence.Length; i++)
            {
                Sequence[i] = new decimal[2];
            }

            //Variable activation/Активация переменных
            decimal thisExpand = (100 - Expand) / 100;
            decimal divider = candles[numberPattern].Volume / 100;
            decimal lockal;
            for (int i = 0; i < Length; i++)
            {
                lockal = candles[numberPattern - Length + 1 + i].Volume / divider;
                Sequence[i][0] = lockal + lockal * thisExpand;
                Sequence[i][1] = lockal - lockal * thisExpand;
            }
        }

        /// <summary>
        /// load pattern from save line
        /// загрузить паттерн из строки сохранения
        /// </summary>
        public void Load(string saveString)
        {
            string[] array = saveString.Split('^');

            Length = Convert.ToInt32(array[1]);
            Weigth = array[2].ToDecimal();

            if (array.Length < 3)
            {
                return;
            }

            Sequence = new decimal[Length][];

            for (int i = 0; i < Length; i++)
            {
                string[] lockal = array[3 + i].Split(';');

                Sequence[i] = new decimal[2];
                //Open:
                Sequence[i][0] = lockal[0].ToDecimal();
                Sequence[i][1] = lockal[1].ToDecimal();
            }
        }

        /// <summary>
        /// take a string to save the pattern
        /// взять строку для сохранения паттерна
        /// </summary>
        public string GetSaveString()
        {
            // delimiters on previous levels: # *? %
            // разделители на предыдущих уровнях: # * ? %

            string saveStr = PatternType.Volume+ "^";

            saveStr += Length + "^";

            saveStr += Weigth + "^";

            if (Sequence != null)
            {
                for (int i = 0; i < Sequence.Length; i++) //we run on the first imitation/бежим по первому измирению
                {
                    if (i != 0)
                    {
                        saveStr += "^";
                    }

                    for (int ii = 0; ii < Sequence[i].Length; ii++)// run on the second/бежим по второму
                    {
                        saveStr += Convert.ToString(Sequence[i][ii]);
                        saveStr += ";";
                    }

                }
            }

            return saveStr;
        }

        /// <summary>
        /// take a copy/взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternVolume pattern = new PatternVolume();

            string save = GetSaveString();
            pattern.Load(save);

            return pattern;
        }
    }
}
