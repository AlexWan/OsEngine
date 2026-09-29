/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Модуль Свечи (candles) — глава 4.2 контекста стенда.
    /// Качает свечи по представителю класса на всех доступных таймфреймах и валидирует их.
    /// </summary>
    public class CandlesTests : OsDataTestsBase
    {
        public CandlesTests(TestContext context)
            : base(context, "CANDLES")
        {
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            string type = _context.ConnectorType;

            if (OpenOsDataMode() == false)
            {
                return;
            }

            DisableAllSets();

            List<SecurityItem> securities = LoadSecurities(type);

            if (securities == null)
            {
                return;
            }

            List<string> timeframes = GetTimeframes(type);

            if (timeframes == null || timeframes.Count == 0)
            {
                _context.RecordFail(Module, "get_timeframes", "не удалось получить таймфреймы коннектора");
                return;
            }

            timeframes.RemoveAll(t => t.Equals("Tick", StringComparison.OrdinalIgnoreCase));

            if (timeframes.Count == 0)
            {
                _context.RecordFail(Module, "get_timeframes", "у коннектора нет свечных таймфреймов");
                return;
            }

            // Точечный режим: тестируем одну бумагу из --security.
            if (IsTargeted(securities, out SecurityItem targetSecurity))
            {
                if (targetSecurity == null)
                {
                    _context.RecordFail(Module, "target", "бумага '" + _context.TargetSecurity + "' не найдена в списке коннектора");
                    return;
                }

                List<string> targetTimeframes = new List<string>();

                if (_context.TargetTimeframes == null || _context.TargetTimeframes.Count == 0)
                {
                    targetTimeframes.AddRange(timeframes);
                }
                else
                {
                    foreach (string tf in _context.TargetTimeframes)
                    {
                        if (tf.Equals("Tick", StringComparison.OrdinalIgnoreCase) == false
                            && targetTimeframes.Contains(tf) == false)
                        {
                            targetTimeframes.Add(tf);
                        }
                    }
                }

                if (targetTimeframes.Count == 0)
                {
                    _context.RecordFail(Module, "target", "не заданы свечные таймфреймы для точечного теста");
                    return;
                }

                DateTime targetDateTo = DateTime.Now.Date;

                while (targetDateTo.DayOfWeek == DayOfWeek.Saturday || targetDateTo.DayOfWeek == DayOfWeek.Sunday)
                {
                    targetDateTo = targetDateTo.AddDays(-1);
                }

                DateTime targetDateFrom = targetDateTo.AddMonths(-1);

                RunForSecurity(type, targetSecurity, targetTimeframes,
                    TargetDateFrom(targetDateFrom), TargetDateTo(targetDateTo));
                return;
            }

            List<SecurityItem> representatives = PickRepresentatives(securities);

            if (representatives.Count == 0)
            {
                _context.RecordFail(Module, "pick_representatives", "не нашлось ни одного класса бумаг");
                return;
            }

            DateTime dateTo = DateTime.Now.Date;

            while (dateTo.DayOfWeek == DayOfWeek.Saturday || dateTo.DayOfWeek == DayOfWeek.Sunday)
            {
                dateTo = dateTo.AddDays(-1);
            }

            DateTime dateFrom = dateTo.AddMonths(-1);

            foreach (SecurityItem security in representatives)
            {
                RunForSecurity(type, security, timeframes, dateFrom, dateTo);
            }
        }

        #region Flow

        private void RunForSecurity(string type, SecurityItem security, List<string> timeframes,
            DateTime dateFrom, DateTime dateTo)
        {
            string setName = "TesterData_Candles_" + RemoveExcess(security.Name);
            string sourceName = type;

            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });

                object createRequest = new
                {
                    name = setName,
                    source = type,
                    source_name = sourceName,
                    timeframes = timeframes,
                    date_from = dateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                    date_to = dateTo.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                _context.Client.ToolsCall("data_create_set", createRequest);

                object addRequest = new
                {
                    name = setName,
                    securities = new[] { new { name = security.Name } }
                };

                string addResponse = _context.Client.ToolsCall("data_set_securities_add", addRequest);

                if (IsError(addResponse))
                {
                    _context.RecordFail(Module, "add_security", security.Name + ": " + GetErrorText(addResponse));
                    CleanupSet(setName);
                    return;
                }

                _context.Client.ToolsCall("data_set_on", new { name = setName });

                bool loaded = WaitForLoad(setName);

                _context.Client.ToolsCall("data_set_off", new { name = setName });

                if (loaded == false)
                {
                    _context.RecordFail(Module, "download", security.Name + ": загрузка не завершилась");
                    CleanupSet(setName);
                    return;
                }

                Validate(security, timeframes, setName);
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "run", security.Name + ": " + error.Message);
            }
            finally
            {
                CleanupSet(setName);
            }
        }

        private void Validate(SecurityItem security, List<string> timeframes, string setName)
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
            string securityFolder = RemoveExcess(security.Name);
            string setFolder = "Set_" + setName;

            Dictionary<string, List<CandleData>> candlesByTimeframe = new Dictionary<string, List<CandleData>>();

            foreach (string timeframe in timeframes)
            {
                string filePath = Path.Combine(dataDir, setFolder, securityFolder, timeframe, securityFolder + ".txt");

                if (File.Exists(filePath) == false)
                {
                    _context.RecordFail(Module, "file", security.Name + "/" + timeframe + ": файл не найден " + filePath);
                    continue;
                }

                List<CandleData> candles = ReadCandles(filePath);

                if (candles.Count == 0)
                {
                    _context.RecordFail(Module, "file", security.Name + "/" + timeframe + ": файл пуст");
                    continue;
                }

                candlesByTimeframe[timeframe] = candles;
            }

            if (candlesByTimeframe.Count == 0)
            {
                return;
            }

            TestCountsDiffer(security, candlesByTimeframe);
            TestOhlc(security, candlesByTimeframe);
            TestDuplicates(security, candlesByTimeframe);
            TestTimeOrder(security, candlesByTimeframe);
            TestTimeframeAlignment(security, candlesByTimeframe);
            TestGaps(security, candlesByTimeframe);
        }

        #endregion

        #region Checks

        private void TestCountsDiffer(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            List<string> names = new List<string>(candlesByTimeframe.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            bool allEqual = true;

            for (int i = 1; i < names.Count; i++)
            {
                if (candlesByTimeframe[names[i]].Count != candlesByTimeframe[names[0]].Count)
                {
                    allEqual = false;
                    break;
                }
            }

            string summary = string.Join(", ", names.ConvertAll(n => n + "=" + candlesByTimeframe[n].Count));

            if (allEqual && names.Count > 1)
            {
                _context.RecordFail(Module, "counts_differ", security.Name + ": одинаковое кол-во во всех ТФ — " + summary);
            }
            else
            {
                _context.RecordPass(Module, "counts_differ", security.Name + ": " + summary);
            }
        }

        private void TestOhlc(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            int broken = 0;

            foreach (KeyValuePair<string, List<CandleData>> pair in candlesByTimeframe)
            {
                List<CandleData> candles = pair.Value;

                for (int i = 0; i < candles.Count; i++)
                {
                    CandleData candle = candles[i];

                    if (candle.High < candle.Open || candle.High < candle.Close
                        || candle.Low > candle.Open || candle.Low > candle.Close
                        || candle.High < candle.Low)
                    {
                        broken++;
                    }
                }
            }

            if (broken == 0)
            {
                _context.RecordPass(Module, "ohlc", security.Name + ": нарушений OHLC нет");
            }
            else
            {
                _context.RecordFail(Module, "ohlc", security.Name + ": битых свечей " + broken);
            }
        }

        private void TestDuplicates(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            int duplicates = 0;

            foreach (KeyValuePair<string, List<CandleData>> pair in candlesByTimeframe)
            {
                HashSet<string> seen = new HashSet<string>();

                foreach (CandleData candle in pair.Value)
                {
                    if (seen.Add(candle.Time) == false)
                    {
                        duplicates++;
                    }
                }
            }

            if (duplicates == 0)
            {
                _context.RecordPass(Module, "no_duplicates", security.Name + ": дублей по времени нет");
            }
            else
            {
                _context.RecordFail(Module, "no_duplicates", security.Name + ": дублей " + duplicates);
            }
        }

        private void TestTimeOrder(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            int outOfOrder = 0;

            foreach (KeyValuePair<string, List<CandleData>> pair in candlesByTimeframe)
            {
                List<CandleData> candles = pair.Value;

                for (int i = 1; i < candles.Count; i++)
                {
                    if (candles[i].DateTime < candles[i - 1].DateTime)
                    {
                        outOfOrder++;
                    }
                }
            }

            if (outOfOrder == 0)
            {
                _context.RecordPass(Module, "time_order", security.Name + ": хронология возрастающая");
            }
            else
            {
                _context.RecordFail(Module, "time_order", security.Name + ": нарушений порядка " + outOfOrder);
            }
        }

        // 4.2.6. Выравнивание по границе ТФ: 90% соседних свечей должны отстоять
        // друг от друга ровно на шаг таймфрейма (остальное — разрывы сессий и т.п.).
        private void TestTimeframeAlignment(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            foreach (KeyValuePair<string, List<CandleData>> pair in candlesByTimeframe)
            {
                int stepMinutes = TimeframeStepMinutes(pair.Key);

                if (stepMinutes <= 0)
                {
                    continue;
                }

                List<CandleData> candles = new List<CandleData>(pair.Value);
                candles.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));

                if (candles.Count < 2)
                {
                    continue;
                }

                int matched = 0;
                int total = 0;

                for (int i = 1; i < candles.Count; i++)
                {
                    // ночной разрыв (соседние свечи в разные календарные сутки) не считаем ошибкой
                    if (candles[i].DateTime.Date != candles[i - 1].DateTime.Date)
                    {
                        continue;
                    }

                    total++;

                    double diff = (candles[i].DateTime - candles[i - 1].DateTime).TotalMinutes;

                    if (Math.Abs(diff - stepMinutes) < 0.5)
                    {
                        matched++;
                    }
                }

                if (total == 0)
                {
                    continue;
                }

                double percent = (double)matched / total * 100.0;

                if (percent >= 90.0)
                {
                    _context.RecordPass(Module, "alignment_" + pair.Key, security.Name + "/" + pair.Key + ": шаг совпадает у " + percent.ToString("F0") + "%");
                }
                else
                {
                    _context.RecordFail(Module, "alignment_" + pair.Key, security.Name + "/" + pair.Key + ": шаг совпадает у " + percent.ToString("F0") + "% (нужно ≥90%)");
                }
            }
        }

        private static int TimeframeStepMinutes(string timeframe)
        {
            switch (timeframe.ToLowerInvariant())
            {
                case "min1": return 1;
                case "min2": return 2;
                case "min3": return 3;
                case "min5": return 5;
                case "min10": return 10;
                case "min15": return 15;
                case "min20": return 20;
                case "min30": return 30;
                case "min45": return 45;
                case "hour1": return 60;
                case "hour2": return 120;
                case "hour4": return 240;
                default: return 0;
            }
        }

        // 4.2.4. Отсутствие дыр — на дневном ТФ: между соседними торговыми днями
        // не должно быть пропусков больше выходных (сб/вс). Порог 4 дня покрывает
        // уикенд (пт→пн = 3 дня) и длинные выходные.
        private void TestGaps(SecurityItem security, Dictionary<string, List<CandleData>> candlesByTimeframe)
        {
            if (candlesByTimeframe.ContainsKey("Day") == false)
            {
                return;
            }

            List<CandleData> candles = candlesByTimeframe["Day"];
            candles.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));

            List<string> gaps = new List<string>();

            for (int i = 1; i < candles.Count; i++)
            {
                int days = (int)(candles[i].DateTime.Date - candles[i - 1].DateTime.Date).TotalDays;

                if (days > 4)
                {
                    gaps.Add(candles[i - 1].DateTime.ToString("yyyy-MM-dd") + "→" + candles[i].DateTime.ToString("yyyy-MM-dd") + " (" + days + "д)");
                }
            }

            if (gaps.Count == 0)
            {
                _context.RecordPass(Module, "no_gaps", security.Name + ": дыр по торговым дням нет");
            }
            else
            {
                _context.RecordFail(Module, "no_gaps", security.Name + ": дыры: " + string.Join("; ", gaps));
            }
        }

        #endregion

        #region Parse helpers

        private static List<CandleData> ReadCandles(string filePath)
        {
            List<CandleData> candles = new List<CandleData>();

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                CandleData candle = ParseCandle(line);

                if (candle != null)
                {
                    candles.Add(candle);
                }
            }

            return candles;
        }

        private static CandleData ParseCandle(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            string[] parts = line.Split(',');

            if (parts.Length < 7)
            {
                return null;
            }

            try
            {
                CandleData candle = new CandleData();
                candle.Time = parts[0].Trim() + parts[1].Trim();
                candle.Open = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                candle.High = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                candle.Low = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                candle.Close = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                candle.Volume = decimal.Parse(parts[6], CultureInfo.InvariantCulture);
                candle.DateTime = DateTime.ParseExact(candle.Time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                return candle;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Entities

        private class CandleData
        {
            public string Time = string.Empty;

            public DateTime DateTime;

            public decimal Open;

            public decimal High;

            public decimal Low;

            public decimal Close;

            public decimal Volume;
        }

        #endregion
    }
}
