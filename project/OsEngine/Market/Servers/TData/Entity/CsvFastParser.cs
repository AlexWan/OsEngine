/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace OsEngine.Market.Servers.TData.Entity
{
    public class CsvFastParser
    {
        public List<Trade> GetTradesFromCsv(string csvFilePath, Security security)
        {
            List<Trade> trades = new List<Trade>(10000); 

            if (!File.Exists(csvFilePath))
                return trades;

            string targetSec = security.Name + "_" + security.NameClass;

            const string targetTradeSource = "EXCHANGE";

            using (StreamReader reader = new StreamReader(csvFilePath, Encoding.UTF8, false, 65536))
            {
                // Пропускаем заголовок
                reader.ReadLine();

                char[] buffer = new char[512];

                StringBuilder lineBuilder = new StringBuilder(256);

                while (reader.ReadLine() is string line)
                {
                    if (line.Length == 0)
                        continue;

                    // Быстрая проверка инструмента
                    if (!ContainsInstrumentFast(line, targetSec))
                        continue;

                    // Загружаем только биржевые сделки
                    if (!ContainsTradeSourceFast(line, targetTradeSource))
                        continue;

                    Trade trade = ParseTradeLineFast(line, security); // 2026-03-11T03:59:31.542593Z,MOEX_TQBR,SELL,177,5,EXCHANGE,5e1c2634-afc4-4e50-ad6d-f78fc14a539a
                    trades.Add(trade);
                }
            }

            return trades;
        }

        private bool ContainsInstrumentFast(string line, string targetInstrument)
        {
            // Находим позицию первого разделителя
            int firstComma = line.IndexOf(',');
            if (firstComma == -1) return false;

            // Находим позицию второго разделителя
            int secondComma = line.IndexOf(',', firstComma + 1);
            if (secondComma == -1) return false;

            // Проверяем, что есть место для второго поля
            int start = firstComma + 1;
            int length = secondComma - start;

            // Проверка границ
            if (start < 0 || start + length > line.Length) return false;

            // Быстрая проверка длины
            if (length != targetInstrument.Length) return false;

            // Посимвольное сравнение
            for (int i = 0; i < length; i++)
            {
                if (line[start + i] != targetInstrument[i])
                    return false;
            }

            return true;
        }

        private bool ContainsTradeSourceFast(string line, string targetSource)
        {
            // Ищем 5-ю и 6-ю запятую, между ними расположено поле источника сделки
            int commaCount = 0;
            int comma5 = -1;
            int comma6 = -1;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != ',')
                    continue;

                commaCount++;

                if (commaCount == 5)
                {
                    comma5 = i;
                }
                else if (commaCount == 6)
                {
                    comma6 = i;
                    break;
                }
            }

            if (comma5 == -1 || comma6 == -1 || comma6 <= comma5 + 1)
                return false;

            int start = comma5 + 1;
            int length = comma6 - start;

            if (length != targetSource.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (line[start + i] != targetSource[i])
                    return false;
            }

            return true;
        }

        private Trade ParseTradeLineFast(string line, Security security)
        {
            Trade trade = new Trade
            {
                SecurityNameCode = security.Name
            };

            try
            {
                ReadOnlySpan<char> span = line.AsSpan();

                // Находим позиции всех запятых с проверкой границ
                int comma1 = span.IndexOf(',');
                if (comma1 == -1) return trade;

                int comma2 = span.Slice(comma1 + 1).IndexOf(',');
                if (comma2 == -1) return trade;
                comma2 += comma1 + 1;

                int comma3 = span.Slice(comma2 + 1).IndexOf(',');
                if (comma3 == -1) return trade;
                comma3 += comma2 + 1;

                int comma4 = span.Slice(comma3 + 1).IndexOf(',');
                if (comma4 == -1) return trade;
                comma4 += comma3 + 1;

                int comma5 = span.Slice(comma4 + 1).IndexOf(',');
                if (comma5 == -1) return trade;
                comma5 += comma4 + 1;

                int comma6 = span.Slice(comma5 + 1).IndexOf(',');
                if (comma6 == -1) return trade;
                comma6 += comma5 + 1;

                // Проверяем границы перед парсингом
                if (comma1 <= span.Length && comma2 <= span.Length && comma3 <= span.Length &&
                    comma4 <= span.Length && comma5 <= span.Length && comma6 <= span.Length)
                {
                    trade.Time = ParseDateTimeFast(span.Slice(0, comma1));
                    trade.Side = ParseSideFast(span.Slice(comma2 + 1, comma3 - comma2 - 1));

                    ReadOnlySpan<char> priceSlice = span.Slice(comma3 + 1, comma4 - comma3 - 1);
                    string priceString = priceSlice.ToString();
                    trade.Price = security.SecurityType == SecurityType.Bond ? priceString.ToDecimal() * 10 : priceString.ToDecimal();

                    ReadOnlySpan<char> volSlice = span.Slice(comma4 + 1, comma5 - comma4 - 1);
                    string volString = volSlice.ToString();
                    trade.Volume = Math.Abs(volString.ToDecimal()) * security.Lot;

                    trade.Id = (DateTime.UtcNow.Ticks + trade.Time.Millisecond).ToString();
                }
            }
            catch
            {
                return trade;
            }

            return trade;
        }

        private DateTime ParseDateTimeFast(ReadOnlySpan<char> dateTimeSpan)
        {
            if (dateTimeSpan.Length < 19)
            {
                return ParseDateTimeSafe(dateTimeSpan);
            }

            try
            {
                int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                int millisecond = 0;

                // Парсим год (0-3)
                for (int i = 0; i < 4; i++)
                    year = year * 10 + (dateTimeSpan[i] - '0');

                // Парсим месяц (5-6)
                for (int i = 5; i < 7; i++)
                    month = month * 10 + (dateTimeSpan[i] - '0');

                // Парсим день (8-9)
                for (int i = 8; i < 10; i++)
                    day = day * 10 + (dateTimeSpan[i] - '0');

                // Парсим час (11-12)
                for (int i = 11; i < 13; i++)
                    hour = hour * 10 + (dateTimeSpan[i] - '0');

                // Парсим минуту (14-15)
                for (int i = 14; i < 16; i++)
                    minute = minute * 10 + (dateTimeSpan[i] - '0');

                // Парсим секунду (17-18)
                for (int i = 17; i < 19; i++)
                    second = second * 10 + (dateTimeSpan[i] - '0');

                // Проверяем наличие миллисекунд
                if (dateTimeSpan.Length > 19 && dateTimeSpan[19] == '.')
                {
                    int startMs = 20;
                    int endMs = dateTimeSpan.Length - 1; // пропускаем 'Z'

                    if (startMs < endMs && startMs < dateTimeSpan.Length)
                    {
                        int msDigits = 0;
                        for (int i = startMs; i < endMs && i < startMs + 6; i++)
                        {
                            if (dateTimeSpan[i] >= '0' && dateTimeSpan[i] <= '9')
                            {
                                millisecond = millisecond * 10 + (dateTimeSpan[i] - '0');
                                msDigits++;
                            }
                        }

                        // Нормализуем до 3 цифр
                        if (msDigits < 3)
                        {
                            for (int i = 0; i < 3 - msDigits; i++)
                                millisecond *= 10;
                        }
                        else if (msDigits > 3)
                        {
                            for (int i = 0; i < msDigits - 3; i++)
                                millisecond /= 10;
                        }
                    }
                }

                DateTime dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                return dateTime.AddHours(3).AddMilliseconds(millisecond);
            }
            catch
            {
                return ParseDateTimeSafe(dateTimeSpan);
            }
        }

        private DateTime ParseDateTimeSafe(ReadOnlySpan<char> dateTimeSpan)
        {
            string dateStr = dateTimeSpan.ToString();

            string[] formats = {
                                "yyyy-MM-ddTHH:mm:ss.ffffffZ",
                                "yyyy-MM-ddTHH:mm:ss.fffZ",
                                "yyyy-MM-ddTHH:mm:ssZ",
                                "yyyy-MM-ddTHH:mm:ss.fffffffZ"
                                };

            foreach (string format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
                {
                    return result.AddHours(3);
                }
            }

            // Если ничего не подошло, пробуем стандартный парсинг
            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime fallback))
            {
                return fallback.AddHours(3);
            }

            return DateTime.UtcNow.AddHours(3);
        }

        private Side ParseSideFast(ReadOnlySpan<char> sideSpan)
        {
            // Быстрое сравнение "BUY" (3 символа)
            if (sideSpan.Length == 3 &&
                sideSpan[0] == 'B' &&
                sideSpan[1] == 'U' &&
                sideSpan[2] == 'Y')
            {
                return Side.Buy;
            }

            return Side.Sell;
        }     
    }
}
