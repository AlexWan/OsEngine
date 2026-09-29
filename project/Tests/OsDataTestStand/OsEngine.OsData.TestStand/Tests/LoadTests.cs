/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Модуль Нагрузка (глава 4.6 контекста стенда).
    /// 4.6.1 сет 10 инструментов; 4.6.2 смешанные таймфреймы в одном сете;
    /// 4.6.3 повторное включение сета (off → on) без дублей.
    /// </summary>
    public class LoadTests : OsDataTestsBase
    {
        public LoadTests(TestContext context)
            : base(context, "LOAD")
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

            DateTime dateTo = DateTime.Now.Date;

            while (dateTo.DayOfWeek == DayOfWeek.Saturday || dateTo.DayOfWeek == DayOfWeek.Sunday)
            {
                dateTo = dateTo.AddDays(-1);
            }

            DateTime dateFrom = dateTo.AddDays(-5);

            Test10Instruments(type, securities, timeframes, dateFrom, dateTo);
            TestMixedTimeframes(type, securities, timeframes, dateFrom, dateTo);
            TestReenable(type, securities, timeframes, dateFrom, dateTo);
        }

        #region 4.6.1

        private void Test10Instruments(string type, List<SecurityItem> securities, List<string> timeframes,
            DateTime dateFrom, DateTime dateTo)
        {
            List<SecurityItem> ten = PickStocks(securities, 10);

            if (ten.Count < 10)
            {
                _context.RecordFail(Module, "pick_10", "не удалось выбрать 10 акций");
                return;
            }

            string timeframe = PickTimeframe(timeframes, "Day", "Min5", "Min1");
            string setName = "TesterData_Load10";

            if (CreateAndLoadSet(type, setName, new[] { timeframe }, ten, dateFrom, dateTo, 300) == false)
            {
                _context.RecordFail(Module, "load10", "загрузка сета из 10 инструментов не завершилась");
                CleanupSet(setName);
                return;
            }

            int missing = 0;

            foreach (SecurityItem security in ten)
            {
                if (CandleFileNonEmpty(setName, security.Name, timeframe) == false)
                {
                    missing++;
                }
            }

            if (missing == 0)
            {
                _context.RecordPass(Module, "load10", "10 инструментов загружены (ТФ " + timeframe + ")");
            }
            else
            {
                _context.RecordFail(Module, "load10", "не загрузились " + missing + " инструментов");
            }

            CleanupSet(setName);
        }

        #endregion

        #region 4.6.2

        private void TestMixedTimeframes(string type, List<SecurityItem> securities, List<string> timeframes,
            DateTime dateFrom, DateTime dateTo)
        {
            List<SecurityItem> one = PickSecurities(securities, 1);

            if (one.Count == 0)
            {
                _context.RecordFail(Module, "pick_one", "не удалось выбрать инструмент");
                return;
            }

            SecurityItem security = one[0];

            List<string> mixed = new List<string>();

            foreach (string candidate in new[] { "Min1", "Min5", "Hour1", "Day" })
            {
                string found = timeframes.Find(t => t.Equals(candidate, StringComparison.OrdinalIgnoreCase));

                if (found != null && mixed.Contains(found) == false)
                {
                    mixed.Add(found);
                }
            }

            if (mixed.Count < 2)
            {
                _context.RecordFail(Module, "mixed", "недостаточно таймфреймов для смешанного сета");
                return;
            }

            string setName = "TesterData_LoadMixed";

            if (CreateAndLoadSet(type, setName, mixed.ToArray(), one, dateFrom, dateTo, 180) == false)
            {
                _context.RecordFail(Module, "mixed", "загрузка смешанных таймфреймов не завершилась");
                CleanupSet(setName);
                return;
            }

            int missing = 0;

            foreach (string tf in mixed)
            {
                if (CandleFileNonEmpty(setName, security.Name, tf) == false)
                {
                    missing++;
                }
            }

            if (missing == 0)
            {
                _context.RecordPass(Module, "mixed", security.Name + ": смешанные ТФ " + string.Join("/", mixed) + " загружены");
            }
            else
            {
                _context.RecordFail(Module, "mixed", security.Name + ": не загрузились ТФ " + missing);
            }

            CleanupSet(setName);
        }

        #endregion

        #region 4.6.3

        private void TestReenable(string type, List<SecurityItem> securities, List<string> timeframes,
            DateTime dateFrom, DateTime dateTo)
        {
            List<SecurityItem> one = PickSecurities(securities, 1);

            if (one.Count == 0)
            {
                _context.RecordFail(Module, "pick_one", "не удалось выбрать инструмент");
                return;
            }

            SecurityItem security = one[0];
            string timeframe = PickTimeframe(timeframes, "Min5", "Min1");
            string setName = "TesterData_LoadReenable";

            if (CreateAndLoadSet(type, setName, new[] { timeframe }, one, dateFrom, dateTo, 180) == false)
            {
                _context.RecordFail(Module, "reenable", "первичная загрузка не завершилась");
                CleanupSet(setName);
                return;
            }

            // повторное включение (off → on): дозагрузка не должна дать дублей
            try
            {
                _context.Client.ToolsCall("data_set_on", new { name = setName });

                if (WaitForLoad(setName, 120) == false)
                {
                    _context.Client.ToolsCall("data_set_off", new { name = setName });
                    _context.RecordFail(Module, "reenable", "повторная загрузка не завершилась");
                    CleanupSet(setName);
                    return;
                }

                _context.Client.ToolsCall("data_set_off", new { name = setName });
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "reenable", error.Message);
                CleanupSet(setName);
                return;
            }

            string filePath = CandleFilePath(setName, security.Name, timeframe);
            int duplicates = CountDuplicateCandleTimes(filePath);

            if (duplicates == 0)
            {
                _context.RecordPass(Module, "reenable", security.Name + "/" + timeframe + ": повторное включение без дублей");
            }
            else
            {
                _context.RecordFail(Module, "reenable", security.Name + "/" + timeframe + ": дублей после повторного включения " + duplicates);
            }

            CleanupSet(setName);
        }

        #endregion

        #region Helpers

        private bool CreateAndLoadSet(string type, string setName, string[] timeframes, List<SecurityItem> securities,
            DateTime dateFrom, DateTime dateTo, int timeoutSeconds)
        {
            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });

                object createRequest = new
                {
                    name = setName,
                    source = type,
                    source_name = type,
                    timeframes = timeframes,
                    date_from = dateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                    date_to = dateTo.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                _context.Client.ToolsCall("data_create_set", createRequest);

                object[] securityNames = new object[securities.Count];

                for (int i = 0; i < securities.Count; i++)
                {
                    securityNames[i] = new { name = securities[i].Name };
                }

                object addRequest = new
                {
                    name = setName,
                    securities = securityNames
                };

                string addResponse = _context.Client.ToolsCall("data_set_securities_add", addRequest);

                if (IsError(addResponse))
                {
                    _context.RecordFail(Module, "add_securities", GetErrorText(addResponse));
                    return false;
                }

                _context.Client.ToolsCall("data_set_on", new { name = setName });

                bool loaded = WaitForLoad(setName, timeoutSeconds);

                _context.Client.ToolsCall("data_set_off", new { name = setName });

                return loaded;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "create_set", error.Message);
                return false;
            }
        }

        private List<SecurityItem> PickStocks(List<SecurityItem> securities, int count)
        {
            List<SecurityItem> stocks = new List<SecurityItem>();

            foreach (SecurityItem security in securities)
            {
                if (security.NameClass.IndexOf("TQBR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    stocks.Add(security);
                }
            }

            List<SecurityItem> result = new List<SecurityItem>();
            HashSet<string> used = new HashSet<string>();

            foreach (string preferred in PreferredTickers)
            {
                foreach (SecurityItem security in stocks)
                {
                    if (used.Contains(security.Name))
                    {
                        continue;
                    }

                    if (security.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                        || security.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(security);
                        used.Add(security.Name);
                        break;
                    }
                }

                if (result.Count >= count)
                {
                    break;
                }
            }

            if (result.Count < count)
            {
                foreach (SecurityItem security in stocks)
                {
                    if (used.Contains(security.Name))
                    {
                        continue;
                    }

                    result.Add(security);
                    used.Add(security.Name);

                    if (result.Count >= count)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private List<SecurityItem> PickSecurities(List<SecurityItem> securities, int count)
        {
            List<SecurityItem> result = new List<SecurityItem>();
            HashSet<string> used = new HashSet<string>();

            foreach (string preferred in PreferredTickers)
            {
                foreach (SecurityItem security in securities)
                {
                    if (used.Contains(security.Name))
                    {
                        continue;
                    }

                    if (security.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                        || security.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(security);
                        used.Add(security.Name);
                        break;
                    }
                }

                if (result.Count >= count)
                {
                    break;
                }
            }

            if (result.Count < count)
            {
                foreach (SecurityItem security in securities)
                {
                    if (used.Contains(security.Name))
                    {
                        continue;
                    }

                    result.Add(security);
                    used.Add(security.Name);

                    if (result.Count >= count)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private static string PickTimeframe(List<string> timeframes, params string[] preferred)
        {
            foreach (string p in preferred)
            {
                string found = timeframes.Find(t => t.Equals(p, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    return found;
                }
            }

            return timeframes.Count > 0 ? timeframes[0] : null;
        }

        private string CandleFilePath(string setName, string securityName, string timeframe)
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
            string securityFolder = RemoveExcess(securityName);
            return Path.Combine(dataDir, "Set_" + setName, securityFolder, timeframe, securityFolder + ".txt");
        }

        private bool CandleFileNonEmpty(string setName, string securityName, string timeframe)
        {
            string filePath = CandleFilePath(setName, securityName, timeframe);

            if (File.Exists(filePath) == false)
            {
                return false;
            }

            return new FileInfo(filePath).Length > 0;
        }

        private static int CountDuplicateCandleTimes(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return 0;
            }

            HashSet<string> times = new HashSet<string>();
            int duplicates = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',');

                if (parts.Length < 2)
                {
                    continue;
                }

                string time = parts[0].Trim() + parts[1].Trim();

                if (times.Add(time) == false)
                {
                    duplicates++;
                }
            }

            return duplicates;
        }

        #endregion
    }
}
