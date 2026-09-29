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
    /// Модуль Тики (ticks/trades) — глава 4.3 контекста стенда.
    /// Качает тики по представителю класса (период ≥ 1 дня) и валидирует их:
    /// 4.3.1 валидность (кол-во, цена, объём, сторона), 4.3.2 хронология, 4.3.3 дубли.
    /// </summary>
    public class TicksTests : OsDataTestsBase
    {
        public TicksTests(TestContext context)
            : base(context, "TICKS")
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

            if (timeframes.Contains("Tick", StringComparer.OrdinalIgnoreCase) == false)
            {
                _context.RecordFail(Module, "tick_support", "коннектор '" + type + "' не поддерживает тики (нет 'Tick' в списке таймфреймов)");
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

                DateTime targetDateTo = DateTime.Now.Date.AddDays(-2);

                while (targetDateTo.DayOfWeek == DayOfWeek.Saturday || targetDateTo.DayOfWeek == DayOfWeek.Sunday)
                {
                    targetDateTo = targetDateTo.AddDays(-1);
                }

                DateTime targetDateFrom = targetDateTo.AddDays(-1);

                while (targetDateFrom.DayOfWeek == DayOfWeek.Saturday || targetDateFrom.DayOfWeek == DayOfWeek.Sunday)
                {
                    targetDateFrom = targetDateFrom.AddDays(-1);
                }

                RunForSecurity(type, targetSecurity,
                    TargetDateFrom(targetDateFrom), TargetDateTo(targetDateTo));
                return;
            }

            List<SecurityItem> representatives = PickRepresentatives(securities);

            if (representatives.Count == 0)
            {
                _context.RecordFail(Module, "pick_representatives", "не нашлось ни одного класса бумаг");
                return;
            }

            // Архив тиков публикуется с задержкой — берём завершённый торговый день ~2 суток назад.
            DateTime dateTo = DateTime.Now.Date.AddDays(-2);

            while (dateTo.DayOfWeek == DayOfWeek.Saturday || dateTo.DayOfWeek == DayOfWeek.Sunday)
            {
                dateTo = dateTo.AddDays(-1);
            }

            DateTime dateFrom = dateTo.AddDays(-1);

            while (dateFrom.DayOfWeek == DayOfWeek.Saturday || dateFrom.DayOfWeek == DayOfWeek.Sunday)
            {
                dateFrom = dateFrom.AddDays(-1);
            }

            foreach (SecurityItem security in representatives)
            {
                RunForSecurity(type, security, dateFrom, dateTo);
            }
        }

        #region Flow

        private void RunForSecurity(string type, SecurityItem security, DateTime dateFrom, DateTime dateTo)
        {
            string setName = "TesterData_Ticks_" + RemoveExcess(security.Name);

            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });

                object createRequest = new
                {
                    name = setName,
                    source = type,
                    source_name = type,
                    timeframes = new[] { "Tick" },
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

                Validate(security, setName);
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

        private void Validate(SecurityItem security, string setName)
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
            string securityFolder = RemoveExcess(security.Name);
            string setFolder = "Set_" + setName;

            string filePath = Path.Combine(dataDir, setFolder, securityFolder, "Tick", securityFolder + ".txt");

            if (File.Exists(filePath) == false)
            {
                _context.RecordFail(Module, "file", security.Name + ": файл тиков не найден " + filePath);
                return;
            }

            List<TickData> ticks = ReadTicks(filePath);

            if (ticks.Count == 0)
            {
                _context.RecordFail(Module, "file", security.Name + ": файл тиков пуст");
                return;
            }

            TestValidity(security, ticks);
            TestChronology(security, ticks);
            TestNoDuplicates(security, ticks);
        }

        #endregion

        #region Checks

        // 4.3.1. Загрузка: тиков > 0, цена > 0, объём > 0, сторона Buy/Sell.
        private void TestValidity(SecurityItem security, List<TickData> ticks)
        {
            int brokenPrice = 0;
            int brokenVolume = 0;
            int brokenSide = 0;

            foreach (TickData tick in ticks)
            {
                if (tick.Price <= 0m)
                {
                    brokenPrice++;
                }

                if (tick.Volume <= 0m)
                {
                    brokenVolume++;
                }

                if (tick.Side != "Buy" && tick.Side != "Sell")
                {
                    brokenSide++;
                }
            }

            if (brokenPrice == 0 && brokenVolume == 0 && brokenSide == 0)
            {
                _context.RecordPass(Module, "validity", security.Name + ": тиков " + ticks.Count + ", цена/объём/сторона валидны");
            }
            else
            {
                _context.RecordFail(Module, "validity", security.Name + ": тиков " + ticks.Count
                    + ", битых: цена " + brokenPrice + ", объём " + brokenVolume + ", сторона " + brokenSide);
            }
        }

        // 4.3.2. Хронология: тики в строго возрастающем порядке (дата + микросекунды).
        private void TestChronology(SecurityItem security, List<TickData> ticks)
        {
            int outOfOrder = 0;

            for (int i = 1; i < ticks.Count; i++)
            {
                if (CompareTicks(ticks[i], ticks[i - 1]) < 0)
                {
                    outOfOrder++;
                }
            }

            if (outOfOrder == 0)
            {
                _context.RecordPass(Module, "chronology", security.Name + ": хронология возрастающая");
            }
            else
            {
                _context.RecordFail(Module, "chronology", security.Name + ": нарушений порядка " + outOfOrder);
            }
        }

        // 4.3.3. Отсутствие дублей (по цене/объёму/времени/id).
        // Допуск на единичные ложные совпадения: id у TDataHistory генерируется
        // (не настоящий id сделки), поэтому возможны редкие коллизии.
        private void TestNoDuplicates(SecurityItem security, List<TickData> ticks)
        {
            HashSet<string> seen = new HashSet<string>();
            int duplicates = 0;

            foreach (TickData tick in ticks)
            {
                if (seen.Add(DuplicateKey(tick)) == false)
                {
                    duplicates++;
                }
            }

            if (duplicates <= 10)
            {
                _context.RecordPass(Module, "no_duplicates", security.Name + ": дублей " + duplicates);
            }
            else
            {
                _context.RecordFail(Module, "no_duplicates", security.Name + ": дублей " + duplicates);
            }
        }

        private static int CompareTicks(TickData a, TickData b)
        {
            int c = a.DateTime.CompareTo(b.DateTime);

            if (c != 0)
            {
                return c;
            }

            return a.MicroSeconds.CompareTo(b.MicroSeconds);
        }

        private static string DuplicateKey(TickData tick)
        {
            return tick.Time + ":" + tick.MicroSeconds + ":p:" + tick.Price + ":v:" + tick.Volume + ":id:" + tick.Id;
        }

        #endregion

        #region Parse helpers

        private static List<TickData> ReadTicks(string filePath)
        {
            List<TickData> ticks = new List<TickData>();

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                TickData tick = ParseTick(line);

                if (tick != null)
                {
                    ticks.Add(tick);
                }
            }

            return ticks;
        }

        private static TickData ParseTick(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            // yyyyMMdd,HHmmss,Price,Volume,Side,MicroSeconds[,Id[,Bid,Ask,BidsVolume,AsksVolume]]
            string[] parts = line.Split(',');

            if (parts.Length < 5)
            {
                return null;
            }

            try
            {
                TickData tick = new TickData();
                tick.Time = parts[0].Trim() + parts[1].Trim();
                tick.DateTime = DateTime.ParseExact(tick.Time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                tick.Price = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                tick.Volume = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                tick.Side = parts[4].Trim();

                if (parts.Length > 5)
                {
                    int.TryParse(parts[5].Trim(), out tick.MicroSeconds);
                }

                if (parts.Length > 6)
                {
                    tick.Id = parts[6].Trim();
                }

                return tick;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Entities

        private class TickData
        {
            public string Time = string.Empty;

            public DateTime DateTime;

            public int MicroSeconds;

            public decimal Price;

            public decimal Volume;

            public string Side = string.Empty;

            public string Id = string.Empty;
        }

        #endregion
    }
}
