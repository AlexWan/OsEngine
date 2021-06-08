using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    public static class Extensions
    {
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
                .Replace("*", "")
                .Replace(":", "")
                .Replace(";", "")
                .Replace(x.ToString(), "");// это для того чтобы из названия бумаги удалять кавычки (правка @cibermax).;

            return value;

        }

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

        public static double ToDouble(this string value)
        {
            return Convert.ToDouble(value.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
        }

        public static string ToStringWithNoEndZero(this decimal value)
        {
            string result = value.ToString(CultureInfo.GetCultureInfo("ru-RU"));

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

        public static int DecimalsCount(this string value)
        {
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
        /// получить точность шкалы на основании количества знаков после запятой
        /// </summary>
        /// <param name="value">количество знаков после запятой</param>
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
                default:
                    return 0;
            }
        }

        public static List<Candle> Merge(this List<Candle> oldCandles, List<Candle> candlesToMerge)
        {
            if (candlesToMerge == null ||
                candlesToMerge.Count == 0)
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
                Candle lastCandle = candlesToMerge.Find(c => c.TimeStart == newCandles[newCandles.Count - 1].TimeStart);

                if (lastCandle != null)
                {
                    newCandles[newCandles.Count - 1] = lastCandle;
                }
            }

            // вставляем новые свечи в середину объединённого массива

            for (int i = indexLastInsertCandle; i < candlesToMerge.Count; i++)
            {
                Candle candle = candlesToMerge[i];

                for (int i2 = 1; i2 < newCandles.Count - 1; i2++)
                {
                    if (candle.TimeStart > newCandles[i2].TimeStart &&
                        candle.TimeStart < newCandles[i2 - 1].TimeStart)
                    {
                        newCandles.Insert(i2 + 1, candle);
                        break;
                    }
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

            oldCandles.Clear();
            oldCandles.AddRange(newCandles);

            return oldCandles;
        }

        public static string ToFormatString(this DataGridViewRow row)
        {
            string result = "";

            for(int i = 0; row.Cells != null && i < row.Cells.Count;i++)
            {
                if(row.Cells[i].Value == null)
                {
                    result +=  ",";
                    continue;
                }
                result += row.Cells[i].Value.ToString().Replace("\n"," ").Replace("\r"," ").Replace(",",".") + ",";
            }

            return result;
        }

    }
}
