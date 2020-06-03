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
    /// candle pattern
    /// свечной паттерн
    /// </summary>
    public class PatternCandle : IPattern
    {

        public PatternCandle()
        {
            Weigth = 1;
            Type =  PatternType.Candle;
            Expand = 99.8m;
            Length = 2;
        }

        /// <summary>
        /// pattern weight while searching for entry and exit
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        public decimal Weigth { get; set; }

        /// <summary>
        /// pattern type
        /// тип паттерна
        /// </summary>
        public PatternType Type { get; set; }

        /// <summary>
        /// pattern length
        /// длина паттерна
        /// </summary>
        public int Length;

        /// <summary>
        /// pattern recognition. 100% - maximum
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        public decimal Expand { get; set; }

        /// <summary>
        /// Candle Pattern Identification Type
        /// тип идентификации свечного паттерна
        /// </summary>
        public TypeWatchCandlePattern TypeWatch;

        /// <summary>
        /// pattern points
        /// точки паттерна
        /// </summary>
        public decimal[][] Sequence;

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

            PatternCandle researched = new PatternCandle();
            researched.Length = Length;
            researched.Expand = Expand;
            //shadow
            //body
            //shadow+body

            researched.SetFromIndex(candles, null, numberPattern);

            List<Candle> incomPattern = researched.GetInCandle();

            if (TypeWatch == TypeWatchCandlePattern.ShadowAndBody)
            {
                for (int i = 0; i < Sequence.Length; i++) // здесь копать
                {
                    /*if (Sequence[i][0] < researched.Sequence[i][0] || Sequence[i][1] > researched.Sequence[i][0] || // опен
                        Sequence[i][2] < researched.Sequence[i][2] || Sequence[i][3] > researched.Sequence[i][2] || // хай
                        Sequence[i][4] < researched.Sequence[i][4] || Sequence[i][5] > researched.Sequence[i][4] || // лоу
                        Sequence[i][6] < researched.Sequence[i][6] || Sequence[i][7] > researched.Sequence[i][6]    // клос
                        )
                    {
                        return false;
                    }*/
                    if (Sequence[i][0] < incomPattern[i].Open || Sequence[i][1] > incomPattern[i].Open ||
                        // опен
                        Sequence[i][2] < incomPattern[i].High || Sequence[i][3] > incomPattern[i].High ||
                        // хай
                        Sequence[i][4] < incomPattern[i].Low || Sequence[i][5] > incomPattern[i].Low ||
                        // лоу
                        Sequence[i][6] < incomPattern[i].Close || Sequence[i][7] > incomPattern[i].Close // клос
                        )
                    {
                        return false;
                    }
                }
            }
            else if (TypeWatch == TypeWatchCandlePattern.Body)
            {
                for (int i = 0; i < Sequence.Length; i++) // dig here/здесь копать
                {
                    /*if (Sequence[i][0] < researched.Sequence[i][0] || Sequence[i][1] > researched.Sequence[i][0] ||
                        // опен
                        Sequence[i][6] < researched.Sequence[i][6] || Sequence[i][7] > researched.Sequence[i][6] // клос
                        )
                    {
                        return false;
                    }*/
                    if (Sequence[i][0] < incomPattern[i].Open || Sequence[i][1] > incomPattern[i].Open ||
                        // опен
                        Sequence[i][6] < incomPattern[i].Close || Sequence[i][7] > incomPattern[i].Close // клос
                        )
                    {
                        return false;
                    }
                }
            }

            else if (TypeWatch == TypeWatchCandlePattern.Shadow)
            {
                for (int i = 0; i < Sequence.Length; i++) // dig here/здесь копать
                {
                    /*if (Sequence[i][2] < researched.Sequence[i][2] || Sequence[i][3] > researched.Sequence[i][2] ||
                        // хай
                        Sequence[i][4] < researched.Sequence[i][4] || Sequence[i][5] > researched.Sequence[i][4] // лоу
                        )
                    {
                        return false;
                    }*/
                    if (Sequence[i][2] < incomPattern[i].High || Sequence[i][3] > incomPattern[i].High ||
                        // хай
                        Sequence[i][4] < incomPattern[i].Low || Sequence[i][5] > incomPattern[i].Low
                        )
                    {
                        return false;
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
        /// <param name="numberPattern">index on which we pattern pattern/индекс по которому мы с мотрим паттерн</param>
        public void SetFromIndex(List<Candle> candles, List<IIndicator> indicators, int numberPattern)
        {
            if (numberPattern - Length + 1 < 0)
            {
                return;
            }

            Sequence = new decimal[Length][];

            for (int i = 0; i < Sequence.Length; i++)
            {
                Sequence[i] = new decimal[8];
            }

            //Variable activation/Активация переменных
            decimal thisExpand = (100-Expand) /100;
            decimal divider = candles[numberPattern].Open / 100; // divider, applied in the whole calculation, measured once. Shows how much one percent of the first entry weighs./делитель, применяется во всём вычислении, измеряется один раз. Показывает сколько весит один процент первого входа
            decimal lockal;
            for (int i = 0; i < Length; i++)
            {
                //Open:
                lockal = candles[numberPattern - Length + 1 + i].Open / divider;// может быть плюс i????
                Sequence[i][0] = lockal + lockal * thisExpand;
                Sequence[i][1] = lockal - lockal * thisExpand;
                //High:
                lockal = candles[numberPattern - Length + 1 + i].High / divider;
                Sequence[i][2] = lockal + lockal * thisExpand;
                Sequence[i][3] = lockal - lockal * thisExpand;
                //Low:
                lockal = candles[numberPattern - Length + 1 + i].Low / divider;
                Sequence[i][4] = lockal + lockal * thisExpand;
                Sequence[i][5] = lockal - lockal * thisExpand;

                //Close:
                lockal = candles[numberPattern - Length + 1 + i].Close / divider;
                Sequence[i][6] = lockal + lockal * thisExpand;
                Sequence[i][7] = lockal - lockal * thisExpand;
            }
        }

        /// <summary>
        /// take a pattern in the form of candles/взять паттерн в виде свечек
        /// </summary>
        public List<Candle> GetInCandle()
        {
            if (Sequence == null || Sequence.Length == 0 || Sequence[0].Length == 0)
            {
                return null;
            }

            List<Candle> result = new List<Candle>();

            for (int i = 0; i < Sequence.Length; i++)
            {
                Candle newCandle = new Candle();

                newCandle.Open = Math.Round((Sequence[i][0] + Sequence[i][1]) / 2 ,3);
                newCandle.High = Math.Round((Sequence[i][2] + Sequence[i][3]) / 2 ,3);
                newCandle.Low = Math.Round((Sequence[i][4] + Sequence[i][5]) / 2 ,3);
                newCandle.Close = Math.Round((Sequence[i][6] + Sequence[i][7]) / 2 , 3);

                result.Add(newCandle);
            }

            return result;
        }

        /// <summary>
        /// load pattern from save line
        /// загрузить паттерн из строки сохранения
        /// </summary>
        public void Load(string saveString)
        {
            string [] array = saveString.Split('^');

            Length = Convert.ToInt32(array[1]);
            Weigth = array[2].ToDecimal();
            Expand = array[3].ToDecimal();

            Enum.TryParse(array[4], out TypeWatch);

            if (array.Length < 5)
            {
                return;
            }

            Sequence = new decimal[array.Length- 5][];

            Length = Sequence.Length;

            for (int i = 0; i < Sequence.Length; i++)
            {
                string[] lockal = array[5+i].Split(';');

                Sequence[i] = new decimal[8];
                //Open:
                Sequence[i][0] = lockal[0].ToDecimal();
                Sequence[i][1] = lockal[1].ToDecimal();
                //High:
                Sequence[i][2] = lockal[2].ToDecimal();
                Sequence[i][3] = lockal[3].ToDecimal();
                //Low:
                Sequence[i][4] = lockal[4].ToDecimal();
                Sequence[i][5] = lockal[5].ToDecimal();

                //Close:
                Sequence[i][6] = lockal[6].ToDecimal();
                Sequence[i][7] = lockal[7].ToDecimal();
            }
        }

        /// <summary>
        /// take a string to save the pattern
        /// взять строку для сохранения паттерна
        /// </summary>
        public string GetSaveString()
        {
            // delimiters on previous levels: # *? % ^;
            // разделители на предыдущих уровнях: # * ? % ^ ;


            string saveStr = PatternType.Candle + "^";

            saveStr += Length + "^";

            saveStr += Weigth + "^";

            saveStr += Expand + "^";

            saveStr += TypeWatch + "^";

            if (Sequence != null)
            {
                for (int i = 0; i < Sequence.Length; i++) //we run on the first imitation/бежим по первому измирению
                {
                    if (i != 0)
                    {
                        saveStr += "^";
                    }

                    for (int ii = 0; ii < Sequence[i].Length; ii++)// run on the second/ бежим по второму
                    {
                        saveStr += Convert.ToString(Sequence[i][ii],CultureInfo.InvariantCulture);
                        saveStr += ";";
                    }

                }
            }

            return saveStr;
        }

        /// <summary>
        /// take a copy
        /// взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternCandle pattern = new PatternCandle();

            string save = GetSaveString();
            pattern.Load(save);

            return pattern;
        }
    }

    public enum TypeWatchCandlePattern
    {
        ShadowAndBody,
        Body,
        Shadow
    }
}
