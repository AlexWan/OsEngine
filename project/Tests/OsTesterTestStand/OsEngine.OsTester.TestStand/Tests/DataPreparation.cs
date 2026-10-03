/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Подготовка данных для стенда тестера: если нужного сета нет — качаем его сами через OsData.
    /// Не ставит PASS/FAIL (это делают проверки модуля Data), только логирует прогресс.
    /// </summary>
    public class DataPreparation : OsTesterTestsBase
    {
        private sealed class SetSpec
        {
            public string Name = string.Empty;
            public string Source = string.Empty;
            public string[] Timeframes = Array.Empty<string>();
            public string[] Securities = Array.Empty<string>();
            public string DateFrom = string.Empty;
            public string DateTo = string.Empty;
        }

        private static readonly List<SetSpec> RequiredSets = new List<SetSpec>
        {
            new SetSpec { Name = "TesterSBER", Source = "MoexDataServer", Timeframes = new[] { "Min30" }, Securities = new[] { "SBER" }, DateFrom = "2021-10-01T00:00:00", DateTo = "2026-10-01T00:00:00" },
            new SetSpec { Name = "Tester10Liq", Source = "MoexDataServer", Timeframes = new[] { "Min30" }, Securities = new[] { "SBER", "LKOH", "GAZP", "GMKN", "YDEX", "TATN", "ROSN", "SNGS", "VTBR", "NVTK" }, DateFrom = "2021-10-01T00:00:00", DateTo = "2026-10-01T00:00:00" },
            new SetSpec { Name = "TesterTicks", Source = "TDataHistory", Timeframes = new[] { "Tick" }, Securities = new[] { "ROSN", "TATN", "SNGS" }, DateFrom = "2026-09-21T00:00:00", DateTo = "2026-09-30T00:00:00" },
            new SetSpec { Name = "TesterDepth", Source = "QscalpMarketDepth", Timeframes = new[] { "MarketDepthHistory" }, Securities = new[] { "VTBR", "SBER", "GAZP" }, DateFrom = "2026-09-23T00:00:00", DateTo = "2026-09-30T00:00:00" }
        };

        public DataPreparation(TestContext context) : base(context, "Data")
        {
        }

        /// <summary>
        /// Переключиться в OsData, докачать недостающие сеты, вернуться в тестер.
        /// </summary>
        public void PrepareAllSets()
        {
            // переключаемся в OsData (terminal_open_mode работает только из MainWindow)
            _context.RestartOsEngine(string.Empty);
            ToolsCall("terminal_open_mode", new { mode = "data" });

            if (!WaitForOsDataReady())
            {
                Console.WriteLine("[Data] Режим OsData не поднялся — подготовка данных пропущена.");
                _context.RestartOsEngine("-testerlight");
                return;
            }

            foreach (SetSpec spec in RequiredSets)
            {
                EnsureSet(spec);
            }

            // возвращаемся в тестер
            _context.RestartOsEngine("-testerlight");
        }

        private void EnsureSet(SetSpec spec)
        {
            decimal percent = GetSetPercentLoad(spec.Name);

            // 100% достижим не всегда (выходные, неторговые дни) — считаем полным от 50%.
            if (percent >= 50m)
            {
                Console.WriteLine($"[Data] Сет {spec.Name} уже есть ({percent}%).");
                return;
            }

            if (percent >= 0m)
            {
                Console.WriteLine($"[Data] Сет {spec.Name} неполный ({percent}%) — удаляю и перекачиваю.");
                ToolsCall("data_delete_set", new { name = spec.Name });
            }

            DownloadSet(spec);
        }

        /// <summary>
        /// Процент загрузки сета из data_get_sets.
        /// -1 — сета нет; 0..100 — сет есть, с такой полнотой.
        /// </summary>
        private decimal GetSetPercentLoad(string name)
        {
            string response = QuietCall("data_get_sets", new { });

            if (!TryParseJson(response, out JsonElement root) || root.ValueKind != JsonValueKind.Array)
            {
                return -1m;
            }

            foreach (JsonElement item in root.EnumerateArray())
            {
                string setName = item.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                if (setName != name && setName != "Set_" + name)
                {
                    continue;
                }

                if (item.TryGetProperty("percent_load", out JsonElement percentElement)
                    && percentElement.ValueKind == JsonValueKind.Number)
                {
                    return percentElement.GetDecimal();
                }

                return 0m;
            }

            return -1m;
        }

        private void DownloadSet(SetSpec spec)
        {
            Console.WriteLine($"[Data] Скачиваю сет {spec.Name} ({spec.Source}).");

            ToolsCall("server_management_activate", new { type = spec.Source });
            System.Threading.Thread.Sleep(2000);
            ToolsCall("server_instance_connect", new { type = spec.Source, number = 0 });

            if (!WaitForServerSecurities(spec.Source))
            {
                Console.WriteLine($"[Data] Сервер {spec.Source} не отдал список бумаг — сет {spec.Name} не скачан.");
                return;
            }

            string create = ToolsCall("data_create_set", new
            {
                name = spec.Name,
                source = spec.Source,
                source_name = spec.Source,
                timeframes = spec.Timeframes,
                date_from = spec.DateFrom,
                date_to = spec.DateTo
            });

            if (IsError(create))
            {
                Console.WriteLine($"[Data] Сет {spec.Name} не создался: {GetErrorText(create)}");
                return;
            }

            List<object> securities = new List<object>();

            foreach (string securityName in spec.Securities)
            {
                securities.Add(new { name = securityName });
            }

            string add = ToolsCall("data_set_securities_add", new { name = spec.Name, securities = securities });

            if (IsError(add))
            {
                Console.WriteLine($"[Data] Бумаги в сет {spec.Name} не добавились: {GetErrorText(add)}");
                return;
            }

            ToolsCall("data_set_on", new { name = spec.Name });

            bool loaded = WaitForLoad(spec.Name);

            ToolsCall("data_set_off", new { name = spec.Name });

            if (loaded)
            {
                Console.WriteLine($"[Data] Сет {spec.Name} скачан (100%).");
            }
            else
            {
                Console.WriteLine($"[Data] Сет {spec.Name} загрузился не полностью.");
            }
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
            DateTime deadline = DateTime.Now.AddSeconds(600);
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

                if (zeroSeconds >= 45)
                {
                    return false;
                }

                System.Threading.Thread.Sleep(3000);
            }

            return false;
        }
    }
}
