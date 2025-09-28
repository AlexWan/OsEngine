/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    public static class Extensions
    {
        private static CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>
        /// remove dangerous characters from the name of the security
        /// </summary>
        public static string RemoveExcessFromSecurityName(this string value)
        {
            if (value == null)
            {
                return null;
            }
            
            // это для того чтобы из названия бумаги удалять кавычки (правка @cibermax).
            // К примеру ПАО ЛУКОЙЛ, АДР tiker LKOD@GS не получалось создать папку выдавало исключение
            char x = '"';

            value = value
                .Replace("/", "")
                .Replace("\\", "")
                .Replace("*", "")
                .Replace(":", "")
                .Replace("@", "")
                .Replace(";", "")
                .Replace(x.ToString(), "");// это для того чтобы из названия бумаги удалять кавычки (правка @cibermax).;

            return value;

        }

        /// <summary>
        /// whether the string includes dangerous symbols.
        /// </summary>
        public static bool HaveExcessInString(this string value)
        {
            if (value == null)
            {
                return false;
            }

            int len = value.Length;

            char x = '"';

            value = value
                .Replace("*", "")
                .Replace("@", "")
                .Replace(x.ToString(), "");


            if(len != value.Length)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// culture-neutral conversion of string to Decimal type
        /// </summary>
        public static decimal ToDecimal(this string value)
        {
            if(value == null)
            {
                return 0;
            }
            if (value.Contains("E"))
            {
                return Convert.ToDecimal(value.ToDouble());
            }
            try
            {
                return Convert.ToDecimal(value.Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return Convert.ToDecimal(value.ToDouble());
            }
        }

        /// <summary>
        /// culture-neutral conversion of string to Double type
        /// </summary>
        public static double ToDouble(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            if (value.Contains("E"))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
                return 0;
            }
            try
            {
                return Convert.ToDouble(value.Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                if (double.TryParse(value, out double result))
                {
                    return result;
                }
                return 0;
            }
        }

        /// <summary>
        /// conversion of double to Decimal type
        /// </summary>
        public static decimal ToDecimal(this double value)
        {
            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// remove zeros from the decimal value at the end
        /// </summary>
        public static string ToStringWithNoEndZero(this decimal value)
        {
            string result = value.ToString(_culture);

            if(result.Contains(","))
            {
                result = result.TrimEnd('0');

                if(result.EndsWith(","))
                {
                    result = result.TrimEnd(',');
                }
            }

            return result;
        }

        /// <summary>
        /// remove zeros from the double value at the end
        /// </summary>
        public static string ToStringWithNoEndZero(this double value)
        {
            string result = value.ToString(_culture);

            if (result.Contains(","))
            {
                result = result.TrimEnd('0');

                if (result.EndsWith(","))
                {
                    result = result.TrimEnd(',');
                }
            }

            return result;
        }

        /// <summary>
        /// get decimal point from double ro decimal values
        /// </summary>
        public static int DecimalsCount(this string value)
        {
            if (value.Contains("E"))
            {
                value = value.ToDecimal().ToString();
            }

            value = value.Replace(",", ".");

            while (value.Length > 0 &&
                   value.EndsWith("0"))
            {
                value = value.Remove(value.Length - 1);
            }

            if (value.Split('.').Length == 1)
            {
                return 0;
            }

            return value.Split('.')[1].Length;
        }

        /// <summary>
        /// get scale accuracy based on the number of decimal places / 
        /// получить точность шкалы на основании количества знаков после запятой
        /// </summary>
        /// <param name="value">decimal point / количество знаков после запятой</param>
        public static decimal GetValueByDecimals(this int value)
        {
            switch (value)
            {
                case 0:
                    return 1;
                case 1:
                    return 0.1m;
                case 2:
                    return 0.01m;
                case 3:
                    return 0.001m;
                case 4:
                    return 0.0001m;
                case 5:
                    return 0.00001m;
                case 6:
                    return 0.000001m;
                case 7:
                    return 0.0000001m;
                case 8:
                    return 0.00000001m;
                case 9:
                    return 0.000000001m;
                case 10:
                    return 0.0000000001m;
                case 11:
                    return 0.00000000001m;
                case 12:
                    return 0.000000000001m;
                case 13:
                    return 0.0000000000001m;
                case 14:
                    return 0.00000000000001m;
                case 15:
                    return 0.000000000000001m;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// merge two candlestick data arrays
        /// </summary>
        public static List<Candle> Merge(this List<Candle> oldCandles, List<Candle> candlesToMerge)
        {
            if (candlesToMerge == null ||
                candlesToMerge.Count == 0)
            {
                return oldCandles;
            }

            // костыль от наличия null свечек в массиве

            for(int i = 0;i < candlesToMerge.Count;i++)
            {
                if (candlesToMerge[i] == null)
                {
                    candlesToMerge.RemoveAt(i);
                    i--;
                }
            }

            if(candlesToMerge.Count == 0)
            {
                return oldCandles;
            }

            if (oldCandles.Count == 0)
            {
                oldCandles.AddRange(candlesToMerge);
                return oldCandles;
            }

            if (candlesToMerge[0].TimeStart < oldCandles[0].TimeStart &&
                candlesToMerge[candlesToMerge.Count - 1].TimeStart >= oldCandles[oldCandles.Count - 1].TimeStart)
            {
                // начало массива в новых свечках раньше. Конец позже. Перезаписываем полностью 
                oldCandles.Clear();
                oldCandles.AddRange(candlesToMerge);
                return oldCandles;
            }

            // смотрим более ранние свечи в новой серии

            List<Candle> newCandles = new List<Candle>();

            int indexLastInsertCandle = 0;

            for (int i = 0; i < candlesToMerge.Count; i++)
            {
                if (candlesToMerge[i].TimeStart < oldCandles[0].TimeStart)
                {
                    newCandles.Add(candlesToMerge[i]);
                }
                else
                {
                    indexLastInsertCandle = i;
                    break;
                }
            }

            newCandles.AddRange(oldCandles);

            // обновляем последнюю свечку в старых данных

            if (newCandles.Count != 0)
            {
                Candle lastCandle = null;

                for(int i = 0;i < candlesToMerge.Count;i++)
                {
                    if (candlesToMerge[i].TimeStart == newCandles[newCandles.Count - 1].TimeStart)
                    {
                        lastCandle = candlesToMerge[i]; 
                        break;
                    }
                }

                if (lastCandle != null)
                {
                    newCandles[newCandles.Count - 1] = lastCandle;
                }
            }

            // вставляем новые свечи в середину объединённого массива. Смотрим последние 500 свечек, не более

            int indxStart = newCandles.Count - 500;

            if(indxStart < 0)
            {
                indxStart = 0;
            }

            for (int i = indexLastInsertCandle; i < candlesToMerge.Count; i++)
            {
                Candle candle = candlesToMerge[i];

                bool candleInsertInOldArray = false;

                for (int i2 = indxStart; i2 < newCandles.Count - 2; i2++)
                {
                    if (candle.TimeStart > newCandles[i2].TimeStart &&
                        candle.TimeStart < newCandles[i2 + 1].TimeStart)
                    {
                        newCandles.Insert(i2 + 1, candle);
                        candleInsertInOldArray = true;
                        break;
                    }
                }

                if(candleInsertInOldArray == false)
                {
                    i += 10;
                }
            }

            // вставляем новые свечи в конец объединённого массива

            for (int i = 0; i < candlesToMerge.Count; i++)
            {
                Candle candle = candlesToMerge[i];

                if (candle.TimeStart > newCandles[newCandles.Count - 1].TimeStart)
                {
                    newCandles.Add(candle);
                }
            }

            return newCandles;
        }

        /// <summary>
        /// merge two trades data arrays
        /// </summary>
        public static List<Trade> Merge(this List<Trade> oldTrades, List<Trade> tradesToMerge)
        {
            if (tradesToMerge == null ||
                tradesToMerge.Count == 0)
            {
                return oldTrades;
            }

            if (oldTrades.Count == 0)
            {
                oldTrades.AddRange(tradesToMerge);
                return oldTrades;
            }

            if (tradesToMerge[0].Time < oldTrades[0].Time &&
                tradesToMerge[tradesToMerge.Count - 1].Time >= oldTrades[oldTrades.Count - 1].Time)
            {
                // начало массива в новых свечках раньше. Конец позже. Перезаписываем полностью 
                oldTrades.Clear();
                oldTrades.AddRange(tradesToMerge);
                return oldTrades;
            }

            if (oldTrades[oldTrades.Count - 1].Time < tradesToMerge[0].Time)
            {
                oldTrades.AddRange(tradesToMerge);
                return oldTrades;
            }

            // смотрим более ранние свечи в новой серии

            List<Trade> newTrades = new List<Trade>();

            int indexLastInsertCandle = 0;

            for (int i = 0; i < tradesToMerge.Count; i++)
            {
                if (tradesToMerge[i].Time < oldTrades[0].Time)
                {
                    newTrades.Add(tradesToMerge[i]);
                }
                else
                {
                    indexLastInsertCandle = i;
                    break;
                }
            }

            newTrades.AddRange(oldTrades);

            // обновляем последнюю свечку в старых данных

            if (newTrades.Count != 0)
            {
                Trade lastTrade = tradesToMerge.Find(c => c.Time == newTrades[newTrades.Count - 1].Time);

                if (lastTrade != null)
                {
                    newTrades[newTrades.Count - 1] = lastTrade;
                }
            }

            // вставляем новые свечи в середину объединённого массива. Смотрим последние 500 свечек, не более

            int indxStart = newTrades.Count - 500;

            if (indxStart < 0)
            {
                indxStart = 0;
            }

            for (int i = indexLastInsertCandle; i < tradesToMerge.Count; i++)
            {
                Trade trade = tradesToMerge[i];

                bool tradesInsertInOldArray = false;

                for (int i2 = indxStart; i2 < newTrades.Count - 2; i2++)
                {
                    if (trade.Time > newTrades[i2].Time &&
                        trade.Time < newTrades[i2 + 1].Time)
                    {
                        newTrades.Insert(i2 + 1, trade);
                        tradesInsertInOldArray = true;
                        break;
                    }
                }

                if (tradesInsertInOldArray == false)
                {
                    i += 10;
                }
            }

            // вставляем новые свечи в конец объединённого массива

            for (int i = 0; i < tradesToMerge.Count; i++)
            {
                Trade tradeNew = tradesToMerge[i];

                if (tradeNew.Time >= newTrades[newTrades.Count - 1].Time)
                {
                    newTrades.Add(tradeNew);
                }
            }

            return newTrades;
        }

        /// <summary>
        /// merge the candle with the updated version of itself
        /// </summary>
        public static Candle Merge(this Candle oldCandle, Candle candleToMerge)
        {
            Candle res = new Candle();

            Candle firstCandle = oldCandle;
            Candle secondCandle = candleToMerge;

            if (oldCandle.TimeStart < candleToMerge.TimeStart)
            {
                res.TimeStart = oldCandle.TimeStart;
            }
            else if (oldCandle.TimeStart > candleToMerge.TimeStart)
            {
                res.TimeStart = candleToMerge.TimeStart;
                firstCandle = candleToMerge;
                secondCandle = oldCandle;
            }
            else
            {
                res.TimeStart = oldCandle.TimeStart;
            }

            res.Volume = oldCandle.Volume + candleToMerge.Volume;

            res.Open = firstCandle.Open;
            res.Close = secondCandle.Close;
            res.High = Math.Max(firstCandle.High, secondCandle.High);
            res.Low = Math.Min(firstCandle.Low, secondCandle.Low);

            return res;
        }

        /// <summary>
        /// convert a row in a table to a string representation
        /// </summary>
        public static string ToFormatString(this DataGridViewRow row)
        {
            string result = "";

            for(int i = 0; row.Cells != null && i < row.Cells.Count;i++)
            {
                if(row.Cells[i].Value == null)
                {
                    result +=  ";";
                    continue;
                }
                result += row.Cells[i].Value.ToString().Replace("\n"," ").Replace("\r"," ").Replace(",",".") + ";";
            }

            return result;
        }

    }

    public static class DateTimeParseHelper
    {
        /// <summary>
        /// Converts date-time from two strings, a date string and a time string.
        /// </summary>
        /// <param name="dateString">Date string in the format "YYYYMMDD".</param>
        /// <param name="timeString">Time string in the format  "HHmmSS".</param>
        public static DateTime ParseFromTwoStrings(string dateString, string timeString)
        {
            ParseDateOrTimeString(dateString, out int year, out int month, out int day);
            ParseDateOrTimeString(timeString, out int hour, out int minute, out int second);
            return new DateTime(year, month, day, hour, minute, second);
        }

        /// <summary>
        /// Converts a date or time string to the output variables year-month-day
        /// (if a date string) or hour-minute-second (if a time string).
        /// </summary>
        public static void ParseDateOrTimeString(string dateOrTimeString, out int yearHour, out int monthMinute, out int daySecond)
        {
            int dateOrTimeInt = Convert.ToInt32(dateOrTimeString);
            yearHour = dateOrTimeInt / 10000;
            monthMinute = dateOrTimeInt / 100 % 100;
            daySecond = dateOrTimeInt % 100;
        }
    }
}