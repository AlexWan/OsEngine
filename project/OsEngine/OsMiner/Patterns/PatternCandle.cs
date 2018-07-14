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
        /// вес паттерна во время поиска входа и выхода
        /// </summary>
        public decimal Weigth { get; set; }

        /// <summary>
        /// тип паттерна
        /// </summary>
        public PatternType Type { get; set; }

        /// <summary>
        /// длина паттерна
        /// </summary>
        public int Length;

        /// <summary>
        /// узнаваемость паттерна.  100  % - максимальная
        /// </summary>
        public decimal Expand { get; set; }

        /// <summary>
        /// тип идентификации свечного паттерна
        /// </summary>
        public TypeWatchCandlePattern TypeWatch;

        /// <summary>
        /// точки паттерна
        /// </summary>
        public decimal[][] Sequence;

        /// <summary>
        /// является ли текущая формация нашим паттерном
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="indicators">индикаторы</param>
        /// <param name="numberPattern">индекс по которому мы смотрим паттерн</param>
        public bool ThisIsIt(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern)
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
                for (int i = 0; i < Sequence.Length; i++) // здесь копать
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
                for (int i = 0; i < Sequence.Length; i++) // здесь копать
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
        /// установить паттерн с текущих данных
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="indicators">индикаторы</param>
        /// <param name="numberPattern">индекс по которому мы с мотрим паттерн</param>
        public void SetFromIndex(List<Candle> candles, List<IIndicatorCandle> indicators, int numberPattern)
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

            //Активация переменных
            decimal thisExpand = (100-Expand) /100;
            decimal divider = candles[numberPattern].Open / 100; // делитель, применяется во всём вычислении, измеряется один раз. Показывает сколько весит один процент первого входа
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
        /// взять паттерн в виде свечек
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
        /// загрузить паттерн из строки сохранения
        /// </summary>
        public void Load(string saveString)
        {
            string [] array = saveString.Split('^');

            Length = Convert.ToInt32(array[1]);
            Weigth = Convert.ToDecimal(array[2].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Expand = Convert.ToDecimal(array[3].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

            Enum.TryParse(array[4], out TypeWatch);

            if (array.Length < 5)
            {
                return;
            }

            Sequence = new decimal[Length][];

            

            for (int i = 0; i < Length; i++)
            {
                string[] lockal = array[5+i].Split(';');

                Sequence[i] = new decimal[8];
                //Open:
                Sequence[i][0] = Convert.ToDecimal(lockal[0].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Sequence[i][1] = Convert.ToDecimal(lockal[1].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                //High:
                Sequence[i][2] = Convert.ToDecimal(lockal[2].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Sequence[i][3] = Convert.ToDecimal(lockal[3].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                //Low:
                Sequence[i][4] = Convert.ToDecimal(lockal[4].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Sequence[i][5] = Convert.ToDecimal(lockal[5].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                //Close:
                Sequence[i][6] = Convert.ToDecimal(lockal[6].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Sequence[i][7] = Convert.ToDecimal(lockal[7].Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// взять строку для сохранения паттерна
        /// </summary>
        public string GetSaveString()
        {
            // разделители на предыдущих уровнях: # * ? % ^ ;

            string saveStr = PatternType.Candle + "^";

            saveStr += Length + "^";

            saveStr += Weigth + "^";

            saveStr += Expand + "^";

            saveStr += TypeWatch + "^";

            if (Sequence != null)
            {
                for (int i = 0; i < Sequence.Length; i++) //бежим по первому измирению
                {
                    if (i != 0)
                    {
                        saveStr += "^";
                    }

                    for (int ii = 0; ii < Sequence[i].Length; ii++)// бежим по второму
                    {
                        saveStr += Convert.ToString(Sequence[i][ii],CultureInfo.InvariantCulture);
                        saveStr += ";";
                    }

                }
            }

            return saveStr;
        }

        /// <summary>
        /// взять копию
        /// </summary>
        public IPattern GetCopy()
        {
            PatternCandle pattern = new PatternCandle();
            pattern.Length = Length;
            pattern.Expand = Expand;
            pattern.Sequence = Sequence;
            pattern.TypeWatch = TypeWatch;
            pattern.Weigth = Weigth;

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
