/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Text.Json;

namespace OsEngine.IndicatorMarketDepth.TestStand.Tests
{
    public class DataPreparation : IndicatorTestsBase
    {
        private const string DepthSource = "QscalpMarketDepth";

        private const string TickSource = "TDataHistory";

        private string TickSetName => _context.DataSetName + "Ticks";

        public DataPreparation(TestContext context) : base(context, "Data")
        {
        }

        public bool PrepareSet()
        {
            _context.RestartOsEngine(string.Empty);
            ToolsCall("terminal_open_mode", new { mode = "data" });

            if (!WaitForOsDataReady())
            {
                RecordFail("OpenOsData", "режим OsData не поднялся");
                _context.RestartOsEngine("-testerlight");
                return false;
            }

            bool depthOk = EnsureDepthSet();
            bool ticksOk = EnsureTickSet();

            _context.RestartOsEngine("-testerlight");
            return depthOk && ticksOk;
        }

        private bool EnsureDepthSet()
        {
            if (DepthSetIsComplete())
            {
                RecordPass("EnsureDepthSet", $"сет {_context.DataSetName} уже готов");
                return true;
            }

            QuietCall("data_delete_set", new { name = _context.DataSetName });

            if (!ActivateAndConnect(DepthSource))
            {
                RecordFail("DepthServer", $"сервер {DepthSource} не отдал список бумаг");
                return false;
            }

            string create = ToolsCall("data_create_set", new
            {
                name = _context.DataSetName,
                source = DepthSource,
                source_name = DepthSource,
                timeframes = new[] { "MarketDepthHistory" },
                date_from = _context.DateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                date_to = _context.DateTo.ToString("yyyy-MM-ddTHH:mm:ss")
            });

            if (IsError(create))
            {
                RecordFail("DepthCreateSet", GetErrorText(create));
                return false;
            }

            if (!AddSecurity(_context.DataSetName))
            {
                return false;
            }

            QuietCall("data_set_on", new { name = _context.DataSetName });

            bool loaded = WaitForLoad(_context.DataSetName);

            QuietCall("data_set_off", new { name = _context.DataSetName });

            if (loaded == false)
            {
                RecordFail("DepthDownload", $"сет {_context.DataSetName} загрузился не полностью");
                return false;
            }

            RecordPass("DepthDownload", $"сет {_context.DataSetName} скачан");
            return true;
        }

        private bool EnsureTickSet()
        {
            if (TickSetIsComplete())
            {
                RecordPass("EnsureTickSet", $"сет {TickSetName} уже готов");
                return true;
            }

            DateTime from = _context.DateFrom.Date;
            DateTime to = _context.DateTo.Date;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (attempt > 0)
                {
                    from = from.AddDays(-7);
                    to = to.AddDays(-7);
                    Console.WriteLine($"[Data] Сет {TickSetName} не скачан — повтор с {from:yyyy-MM-dd}..{to:yyyy-MM-dd}.");
                }

                if (DownloadTickSet(from, to))
                {
                    return true;
                }
            }

            RecordFail("TickDownload", $"сет {TickSetName} не скачан ни для одного диапазона");
            return false;
        }

        private bool DownloadTickSet(DateTime from, DateTime to)
        {
            QuietCall("data_delete_set", new { name = TickSetName });

            if (!ActivateAndConnect(TickSource))
            {
                RecordFail("TickServer", $"сервер {TickSource} не отдал список бумаг");
                return false;
            }

            string create = ToolsCall("data_create_set", new
            {
                name = TickSetName,
                source = TickSource,
                source_name = TickSource,
                timeframes = new[] { "Tick" },
                date_from = from.ToString("yyyy-MM-ddTHH:mm:ss"),
                date_to = to.ToString("yyyy-MM-ddTHH:mm:ss")
            });

            if (IsError(create))
            {
                RecordFail("TickCreateSet", GetErrorText(create));
                return false;
            }

            if (!AddSecurity(TickSetName))
            {
                return false;
            }

            QuietCall("data_set_on", new { name = TickSetName });

            bool loaded = WaitForLoad(TickSetName);

            QuietCall("data_set_off", new { name = TickSetName });

            if (loaded == false)
            {
                Console.WriteLine($"[Data] Сет {TickSetName} за {from:yyyy-MM-dd}..{to:yyyy-MM-dd} загрузился не полностью.");
                return false;
            }

            RecordPass("TickDownload", $"сет {TickSetName} скачан за {from:yyyy-MM-dd}..{to:yyyy-MM-dd}");
            return true;
        }

        private bool AddSecurity(string setName)
        {
            string add = ToolsCall("data_set_securities_add", new
            {
                name = setName,
                securities = new[] { new { name = _context.Security } }
            });

            if (IsError(add))
            {
                RecordFail("AddSecurity", GetErrorText(add));
                return false;
            }

            return true;
        }

        private bool ActivateAndConnect(string source)
        {
            QuietCall("server_management_activate", new { type = source });
            System.Threading.Thread.Sleep(2000);
            QuietCall("server_instance_connect", new { type = source, number = 0 });

            return WaitForServerSecurities(source);
        }

        private bool DepthSetIsComplete()
        {
            if (SetExists(_context.DataSetName) == false)
            {
                return false;
            }

            return CountLocalQshFiles() >= ExpectedTradingDays();
        }

        private bool TickSetIsComplete()
        {
            if (SetExists(TickSetName) == false)
            {
                return false;
            }

            string filePath = GetTickFilePath();

            try
            {
                return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool SetExists(string name)
        {
            string response = QuietCall("data_get_sets", new { });

            if (!TryParseJson(response, out JsonElement root) || root.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement item in root.EnumerateArray())
            {
                string setName = item.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                if (setName == name || setName == "Set_" + name)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountLocalQshFiles()
        {
            try
            {
                string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
                string mdFolder = Path.Combine(dataDir, "Set_" + _context.DataSetName, _context.Security, "MarketDepth");

                if (Directory.Exists(mdFolder) == false)
                {
                    return 0;
                }

                return Directory.GetFiles(mdFolder, "*.qsh").Length;
            }
            catch
            {
                return 0;
            }
        }

        private string GetTickFilePath()
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
            return Path.Combine(dataDir, "Set_" + TickSetName, _context.Security, "Tick", _context.Security + ".txt");
        }

        private int ExpectedTradingDays()
        {
            int days = 0;
            DateTime cursor = _context.DateFrom.Date;

            while (cursor <= _context.DateTo.Date)
            {
                if (cursor.DayOfWeek != DayOfWeek.Saturday && cursor.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }

                cursor = cursor.AddDays(1);
            }

            return Math.Max(3, days);
        }

        private bool WaitForOsDataReady()
        {
            DateTime deadline = DateTime.Now.AddSeconds(120);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("data_get_sets", new { });

                if (TryParseJson(response, out JsonElement root) && root.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        private bool WaitForServerSecurities(string source)
        {
            DateTime deadline = DateTime.Now.AddSeconds(120);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("server_instance_get_securities", new { type = source });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("securities", out JsonElement list)
                    && list.ValueKind == JsonValueKind.Array
                    && list.GetArrayLength() > 0)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(2000);
            }

            return false;
        }

        private bool WaitForLoad(string setName)
        {
            DateTime deadline = DateTime.Now.AddSeconds(900);
            int zeroSeconds = 0;

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("data_get_set_status", new { name = setName });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("percent_load", out JsonElement percentElement)
                    && percentElement.ValueKind == JsonValueKind.Number)
                {
                    decimal percent = percentElement.GetDecimal();

                    if (percent >= 100m)
                    {
                        return true;
                    }

                    if (percent > 0m)
                    {
                        zeroSeconds = 0;
                    }
                    else
                    {
                        zeroSeconds += 3;
                    }
                }

                if (zeroSeconds >= 60)
                {
                    return false;
                }

                System.Threading.Thread.Sleep(3000);
            }

            return false;
        }
    }
}
