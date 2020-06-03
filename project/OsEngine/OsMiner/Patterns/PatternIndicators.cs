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
    /// pattern on indicators
    /// паттерн на индикаторах
    /// </summary>
    public class PatternIndicators : IPattern
    {
        public PatternIndicators()
        {
            Type = PatternType.Indicators;
            Length = 2;
            Weigth = 1;
            Expand = 99;
            SearchType = PatternIndicatorSearchType.IndicatorsAngle;
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
        /// identification pattern identification type
        /// тип идентификации индикаторного паттерна
        /// </summary>
        public PatternIndicatorSearchType SearchType;

        /// <summary>
        /// pattern length
        /// длина паттерна
        /// </summary>
        public int Length;

        /// <summary>
        /// pattern points
        /// точки паттерна
        /// </summary>
        public decimal[][] Sequence;

        /// <summary>
        /// candle arrangement
        /// расположение свечи
        /// </summary>
        public decimal[][] SequenceCandlePosition;

        /// <summary>
        /// indicators moving averages, representing pattern lines
        /// индикаторы скользящие средние, представляющие линии паттерна
        /// </summary>
        public List<IIndicator> Indicators;

        /// <summary>
        /// is the current formation our pattern
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="indicators">indicators/индикаторы</param>
        /// <param name="numberPattern">the index on which we watch the pattern/индекс по которому мы смотрим паттерн</param>
        public bool ThisIsIt(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {
            if (indicators == null ||
               indicators.Count == 0)
            {
                return false;
            }

            if (numberPattern - Length - 2 <=0)
            {
                return false;
            }

            if (Sequence == null ||
                Sequence.Length == 0)
            {
                return false;
            }

            PatternIndicators tempIndicators = new PatternIndicators();
            tempIndicators.Length = Length;
            tempIndicators.Weigth = Weigth;
            tempIndicators.SearchType = SearchType;
            tempIndicators.Expand = Expand;
            tempIndicators.SetFromIndex(candles, indicators, numberPattern);

            if (tempIndicators.SequenceCandlePosition == null)
            {
                return false;
            }

            if (SearchType == PatternIndicatorSearchType.CandlePosition)
            {
                for (int i = 0; i < SequenceCandlePosition.Length; i++)
                {
                    for (int i2 = 0; i2 < SequenceCandlePosition[0].Length; i2 ++)
                    {
                        if (SequenceCandlePosition[i][i2] != tempIndicators.SequenceCandlePosition[i][i2])
                        {
                            return false;
                        }
                    }
                }
            }

            if (SearchType == PatternIndicatorSearchType.IndicatorsAngle)
            {
                decimal[][] sequence = tempIndicators.Sequence;

                if (sequence == null || 
                    sequence.Length == 0 ||
                    sequence.Length != Sequence.Length ||
                    sequence[0].Length != Sequence[0].Length)
                {
                    return false;
                }

                for (int i = 0; i < sequence.Length; i++)
                {
                    for (int i2 = 0; i2 < Sequence[0].Length; i2 += 2)
                    {
                        decimal price = (sequence[i][i2] + sequence[i][i2 + 1]) / 2;

                        if (price > Sequence[i][i2] ||
                            price < Sequence[i][i2 + 1])
                        {
                            return false;
                        }
                    }
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
            if (indicators == null ||
                indicators.Count == 0)
            {
                return;
            }

            if (numberPattern - Length <= 0)
            {
                return;
            }

            List<List<decimal>> valuesToChart = new List<List<decimal>>();

            for (int i = 0; i < indicators.Count; i++)
            {
                if (indicators[i].GetType().Name == "Volume")
                {
                    continue;
                }
                valuesToChart.AddRange(indicators[i].ValuesToChart);
            }

            if (valuesToChart.Count == 0)
            {
                return;
            }

            SequenceCandlePosition = new decimal[valuesToChart.Count][];

            for (int i = 0; i < SequenceCandlePosition.Length; i++)
            {
                SequenceCandlePosition[i] = new decimal[Length];
            }

            for (int indexIndicator = 0; indexIndicator < valuesToChart.Count; indexIndicator++)
            {
                for (int i2 = numberPattern - Length, i3 = 0; i2 < numberPattern; i2++,i3++)
                {
                    decimal candleClose = candles[i2].Close;

                    if (candleClose > valuesToChart[indexIndicator][i2])
                    {
                        SequenceCandlePosition[indexIndicator][i3] = 1;
                    }
                    else
                    {
                        SequenceCandlePosition[indexIndicator][i3] = 0;
                    }
                }
            }

            Sequence = new decimal[valuesToChart.Count][];

            for (int i = 0; i < Sequence.Length; i++)
            {
                Sequence[i] = new decimal[Length*2];
            }

            Indicators = new List<IIndicator>();

            //Variable activation/Активация переменных
            decimal thisExpand = (100 - Expand)/100;
            decimal divider = valuesToChart[0][numberPattern]/100;
            // divider, applied in the whole calculation, measured once. Shows how much one percent of the first entry weighs.
            // делитель, применяется во всём вычислении, измеряется один раз. Показывает сколько весит один процент первого входа

            if (divider == 0)
            {
                return;
            }
            decimal lockal;

            for (int indexIndicator = 0; indexIndicator < valuesToChart.Count; indexIndicator++)
            {
                MovingAverage newMoving = new MovingAverage("moving" + indexIndicator, true);
                newMoving.Values = new List<decimal>();
                newMoving.NameSeries = "moving" + indexIndicator;
                newMoving.NameArea = "Prime";

                for (int i2 = numberPattern - Length, i3 = 0; i2 < numberPattern; i2++)
                {
                    lockal = valuesToChart[indexIndicator][i2] / divider; // может быть плюс i????
                    Sequence[indexIndicator][i3] = lockal + lockal*thisExpand;
                    i3++;
                    Sequence[indexIndicator][i3] = lockal - lockal * thisExpand;
                    i3++;
                    newMoving.Values.Add(lockal);
                }
                Indicators.Add(newMoving);
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
            Expand = array[3].ToDecimal();

            Enum.TryParse(array[4], out SearchType);

            Sequence = GetSequenceFromString(array[5]);

            SequenceCandlePosition = GetSequenceFromString(array[6]);

            Indicators = new List<IIndicator>();

            for (int i = 0; i < Sequence.Length; i++)
            {
                MovingAverage newMoving = new MovingAverage("moving" + i, true);
                newMoving.Values = new List<decimal>();
                newMoving.NameSeries = "moving" + i;
                newMoving.NameArea = "Prime";

                for (int i2 = 0; i2 < Sequence[i].Length; i2 += 2)
                {
                    newMoving.Values.Add((Sequence[i][i2] + Sequence[i][i2+1]) / 2);
                }
                Indicators.Add(newMoving);
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

            string saveStr = PatternType.Indicators + "^";

            saveStr += Length + "^";

            saveStr += Weigth + "^";

            saveStr += Expand + "^";

            saveStr += SearchType + "^";

            saveStr += GetSequenceString(Sequence) + "^";

            saveStr += GetSequenceString(SequenceCandlePosition) + "^";

            return saveStr;
        }

        private string GetSequenceString(decimal[][] sequence)
        {
            // delimiters on previous levels: # *? %
            // разделители на предыдущих уровнях: # * ? % ^
            string saveStr = "";

            for (int i = 0; i < sequence.Length; i++) //we run on the first imitation/бежим по первому измирению
            {
                if (i != 0)
                {
                    saveStr += ")";
                }

                for (int ii = 0; ii < sequence[i].Length; ii++) // run on the second/бежим по второму
                {
                    saveStr += Convert.ToString(sequence[i][ii], CultureInfo.InvariantCulture);
                    saveStr += ";";
                }
            }

            return saveStr;
        }

        private decimal[][] GetSequenceFromString(string str)
        {
            string [] seqFirst = str.Split(')');

            decimal[][] sequence = new decimal[seqFirst.Length][];

            for (int i = 0; i < sequence.Length; i++)
            {
                string[] seqSec = seqFirst[i].Split(';');
                sequence[i] = new decimal[seqSec.Length-1];

                for (int i2 = 0; i2 < seqSec.Length - 1; i2++)
                {
                    sequence[i][i2] = seqSec[i2].ToDecimal();
                }
            }

            return sequence;
        }

        /// <summary>
        /// take a copy
        /// взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternIndicators pattern = new PatternIndicators();

            string save = GetSaveString();
            pattern.Load(save);

            return pattern;
        }
    }

    /// <summary>
    /// indicator pattern recognition types
    /// типы распознавания индикаторного паттерна
    /// </summary>
    public enum PatternIndicatorSearchType
    {
        /// <summary>
        /// angle of indicators
        /// угол индикаторов
        /// </summary>
        IndicatorsAngle,

        /// <summary>
        /// position of the last candle relative to the indicator
        /// позиция последней свечи относительно индикатора
        /// </summary>
        CandlePosition
    }
}
