/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 13 — BotTabPair в оптимизаторе (FEATURE-0141).
    /// Живой MCP-set пары делается на -testerlight (бот называется ровно так, как его
    /// сконструирует OptimizerMaster.CreateBot: "OptimizerBot" + стратегия + сет), после
    /// перезапуска на -optimizer тот же бот поднимает пару из записанных файлов.
    /// Проверки: BotTabPair не промолчан (серии, отчёты, позиции >= 1 — позиции PairCorrelationTrader
    /// открывает только по CorrelationChangeEvent), отказ bot_*-инструментов на оптимизаторе,
    /// неперсистирование настроек пар оптимизатором (описание инструмента обещает).
    /// Null-серии — FAIL, не SKIP: это стоп-сигнал на ядро по ТЗ.
    /// </summary>
    public class PairTests : OsOptimizerTestsBase
    {
        private const string Strategy = "PairCorrelationTrader";

        // OptimizerMaster.CreateBot: botName = "OptimizerBot" + strategy + последняя
        // часть ActiveSet.Split('_'). Для сета Tester10Liq это "OptimizerBotPairCorrelationTraderTester10Liq".
        private const string OptimizerBotName = "OptimizerBot" + Strategy + "Tester10Liq";

        private static readonly string[] LegSberVtbr = { "SBER", "VTBR" };
        private static readonly string[] LegGazpTatn = { "GAZP", "TATN" };

        private string _tabName = string.Empty;
        private readonly List<string> _pairNames = new List<string>();
        private Dictionary<string, string[]> _snapshot = new Dictionary<string, string[]>();

        public PairTests(TestContext context) : base(context, "Pair")
        {
        }

        public void RunAll()
        {
            if (!LiveMcpSetOnTester())
            {
                return;
            }

            PairOnOptimizer();
        }

        #region Phase A: live MCP set on testerlight

        private bool LiveMcpSetOnTester()
        {
            try
            {
                _context.RestartOsEngine("-testerlight");
            }
            catch (Exception error)
            {
                RecordFail("LiveMcpSet", "перезапуск на -testerlight не выполнен: " + error.Message);
                return false;
            }

            if (!WaitForTesterReadyLocal(90))
            {
                RecordFail("LiveMcpSet", "тестер-сервер на -testerlight не поднялся");
                return false;
            }

            DeleteAllBotsLocal();

            string created = ToolsCall("bot_create", new { strategy_name = Strategy, name = OptimizerBotName });

            if (IsError(created))
            {
                RecordFail("LiveMcpSet", "bot_create не прошёл: " + GetErrorText(created));
                return false;
            }

            _tabName = FindPairTabLocal();

            if (string.IsNullOrEmpty(_tabName))
            {
                RecordFail("LiveMcpSet", "Pair-вкладка у созданного бота не найдена");
                return false;
            }

            bool ok = CreatePairViaMcp(LegSberVtbr, 0) && CreatePairViaMcp(LegGazpTatn, 1);

            if (!ok)
            {
                return false;
            }

            _snapshot = SnapshotPairFiles();

            int filesFound = 0;

            foreach (KeyValuePair<string, string[]> entry in _snapshot)
            {
                if (entry.Value != null)
                {
                    filesFound++;
                }
            }

            if (filesFound == _snapshot.Count)
            {
                RecordPass("LiveMcpSet", $"живой MCP-set на -testerlight: пары 0/1 созданы, {filesFound} файлов конфигурации записаны");
                return true;
            }

            List<string> missing = new List<string>();

            foreach (KeyValuePair<string, string[]> entry in _snapshot)
            {
                if (entry.Value == null)
                {
                    missing.Add(entry.Key);
                }
            }

            RecordFail("LiveMcpSet", "записаны не все файлы: нет " + string.Join(", ", missing));
            return false;
        }

        private bool CreatePairViaMcp(string[] legs, int expectedIndex)
        {
            string response = ToolsCall("bot_set_config_tab_pair", new
            {
                bot_id = OptimizerBotName,
                tab_name = _tabName,
                create_pair = true,
                sec1_server_type = "Tester",
                sec1_server_full_name = "Tester",
                sec1_portfolio_name = "GodMode",
                sec1_security_name = legs[0],
                sec1_security_class = "TestClass",
                sec1_time_frame = "Min30",
                sec2_server_type = "Tester",
                sec2_server_full_name = "Tester",
                sec2_portfolio_name = "GodMode",
                sec2_security_name = legs[1],
                sec2_security_class = "TestClass",
                sec2_time_frame = "Min30",
                sec1_trade_regime = "Market",
                sec2_trade_regime = "Market",
                sec1_volume_type = "Contract",
                sec2_volume_type = "Contract",
                sec1_volume = 5,
                sec2_volume = 5,
                events_is_on = true,
                correlation_look_back = 30,
                cointegration_look_back = 30
            });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("LiveMcpSet", $"create_pair {legs[0]}/{legs[1]} не ответил: {GetErrorText(response)}");
                return false;
            }

            JsonElement last = default;
            int seen = 0;

            if (root.TryGetProperty("pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    last = pair;
                    seen++;
                }
            }

            if (seen != expectedIndex + 1 || last.ValueKind == JsonValueKind.Undefined)
            {
                RecordFail("LiveMcpSet", $"create_pair вернул {seen} пар, ожидалось {expectedIndex + 1}");
                return false;
            }

            string name = last.TryGetProperty("pair_name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                RecordFail("LiveMcpSet", "create_pair не вернул фактический pair_name");
                return false;
            }

            _pairNames.Add(name);
            return true;
        }

        #endregion

        #region Phase B: same bot on optimizer

        private void PairOnOptimizer()
        {
            try
            {
                _context.RestartOsEngine("-optimizer");
            }
            catch (Exception error)
            {
                RecordFail("SeriesNotSilent", "перезапуск на -optimizer не выполнен: " + error.Message);
                return;
            }

            if (!WaitForOptimizerReady(120))
            {
                RecordFail("SeriesNotSilent", "оптимизатор не поднялся");
                return;
            }

            if (!SelectDataSetByName("Tester10Liq"))
            {
                RecordFail("SeriesNotSilent", "сет Tester10Liq не выбран");
                return;
            }

            if (!WaitForStorageLoad(300))
            {
                RecordFail("SeriesNotSilent", "хранилище оптимизатора не загрузилось");
                return;
            }

            if (!CreateBot(Strategy))
            {
                RecordFail("SeriesNotSilent", "optimizer_bot_set не загрузил робота");
                return;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName))
            {
                RecordFail("SeriesNotSilent", "числовой параметр у pair-робота не найден");
                return;
            }

            if (!SetEnumeration(paramName, 1, 3, 2))
            {
                RecordFail("SeriesNotSilent", "перебор параметра не включён");
                return;
            }

            if (!StartAndWait(1500))
            {
                RecordFail("SeriesNotSilent", "прогон оптимизатора не завершился");
                return;
            }

            string report = ToolsCall("optimizer_get_report", new { });

            if (!TryParseJson(report, out JsonElement root))
            {
                RecordFail("SeriesNotSilent", "отчёт оптимизатора не разобран");
                return;
            }

            int reportsCount = root.TryGetProperty("reports_count", out JsonElement rc) && rc.ValueKind == JsonValueKind.Number ? rc.GetInt32() : 0;
            int fazesCount = root.TryGetProperty("fazes_count", out JsonElement fc) && fc.ValueKind == JsonValueKind.Number ? fc.GetInt32() : 0;

            int maxPositions = 0;
            string botName = string.Empty;
            decimal maxProfit = 0;

            if (root.TryGetProperty("fazes", out JsonElement fazes) && fazes.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement faze in fazes.EnumerateArray())
                {
                    if (!faze.TryGetProperty("reports", out JsonElement reports) || reports.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement rep in reports.EnumerateArray())
                    {
                        if (botName.Length == 0 && rep.TryGetProperty("bot_name", out JsonElement bn))
                        {
                            botName = bn.GetString() ?? string.Empty;
                        }

                        int positions = rep.TryGetProperty("positions_count", out JsonElement pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : 0;
                        decimal profit = rep.TryGetProperty("total_profit", out JsonElement tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetDecimal() : 0m;

                        if (positions > maxPositions)
                        {
                            maxPositions = positions;
                        }

                        if (Math.Abs(profit) > Math.Abs(maxProfit))
                        {
                            maxProfit = profit;
                        }
                    }
                }
            }

            if (reportsCount == 0 || fazesCount == 0)
            {
                RecordFail("SeriesNotSilent", $"BotTabPair промолчал: отчётов={reportsCount}, фаз={fazesCount} — null-серии, стоп-сигнал на ядро");
                return;
            }

            RecordPass("SeriesNotSilent", $"BotTabPair в оптимизаторе не промолчан: фаз={fazesCount}, отчётов={reportsCount}, bot_name='{botName}'");

            // PairCorrelationTrader открывает позицию только по CorrelationChangeEvent
            // (|correlation_last| >= 0.9 + сторона коинтеграции), поэтому позиции >= 1
            // в отчёте = как минимум одно событие корреляции дошло до робота.
            if (maxPositions >= 1)
            {
                RecordPass("PairTraded", $">=1 CorrelationChangeEvent: max позиций в отчёте = {maxPositions}, max прибыль = {maxProfit}");
            }
            else
            {
                RecordFail("PairTraded", $"бот без данных (null-серии): позиции=0 во всех {reportsCount} отчётах, прибыль={maxProfit} — стоп-сигнал на ядро по ТЗ (A-8)");
            }

            RejectedOnOptimizer();
            NotPersistedByOptimizer();
        }

        private void RejectedOnOptimizer()
        {
            string response = ToolsCall("bot_set_config_tab_pair", new
            {
                bot_id = OptimizerBotName,
                tab_name = _tabName,
                pair_num = 0,
                sec1_volume = 3
            });

            if (IsError(response))
            {
                RecordPass("RejectedOnOptimizer", "bot_set_config_tab_pair на -optimizer отвергнут: " + Trim(GetErrorText(response), 120));
                return;
            }

            RecordFail("RejectedOnOptimizer", "bot_* инструменты unexpectedly применимы на -optimizer — состояние надо перепроверить");
        }

        private void NotPersistedByOptimizer()
        {
            Dictionary<string, string[]> now = SnapshotPairFiles();
            List<string> changed = new List<string>();

            foreach (KeyValuePair<string, string[]> entry in _snapshot)
            {
                string[] before = IgnoreCointegrationSide(entry.Key, entry.Value);
                string[] after = IgnoreCointegrationSide(entry.Key, now.TryGetValue(entry.Key, out string[] value) ? value : null);

                if (!LinesEqual(before, after))
                {
                    changed.Add(entry.Key);
                }
            }

            if (changed.Count == 0)
            {
                RecordPass("NotPersistedByOptimizer", "оптимизатор не персистировал файлы пары (прогон + отвергнутый set): содержимое всех файлов совпало со снапшотом после MCP-set");
                return;
            }

            RecordFail("NotPersistedByOptimizer", "оптимизатор изменил файлы (обещание not-persisted нарушено): " + string.Join(", ", changed));
        }

        #endregion

        #region Snapshot / helpers

        private string EngineDirectory()
        {
            string exeDir = Path.GetDirectoryName(_context.OsEnginePath) ?? ".";
            return Path.Combine(exeDir, "Engine");
        }

        private Dictionary<string, string[]> SnapshotPairFiles()
        {
            Dictionary<string, string[]> snapshot = new Dictionary<string, string[]>(StringComparer.Ordinal);
            string engine = EngineDirectory();

            List<string> files = new List<string>
            {
                _tabName + "StandartPairsSettings.txt",
                _tabName + "PairsNamesToLoad.txt"
            };

            foreach (string pairName in _pairNames)
            {
                files.Add(pairName + "PairsSettings.txt");
                files.Add(pairName + "1ConnectorPrime.txt");
                files.Add(pairName + "1TimeFrameBuilder.txt");
                files.Add(pairName + "2ConnectorPrime.txt");
                files.Add(pairName + "2TimeFrameBuilder.txt");
            }

            foreach (string file in files)
            {
                string path = Path.Combine(engine, file);
                snapshot[file] = File.Exists(path) ? File.ReadAllLines(path) : null;
            }

            return snapshot;
        }

        private static string[] IgnoreCointegrationSide(string fileName, string[] lines)
        {
            // Строка 15 PairsSettings — сторона коинтеграции: рантаймовое состояние,
            // меняется в прогоне и не входит в обещание неперсистирования настроек.
            if (lines == null || !fileName.EndsWith("PairsSettings.txt") || lines.Length < 15)
            {
                return lines;
            }

            string[] copy = (string[])lines.Clone();
            copy[14] = "<runtime>";
            return copy;
        }

        private static bool LinesEqual(string[] a, string[] b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i].Trim(), b[i].Trim(), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private bool WaitForTesterReadyLocal(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                if (!IsError(QuietCall("tester_data_get_config", new { })))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        private void DeleteAllBotsLocal()
        {
            for (int pass = 0; pass < 20; pass++)
            {
                string list = QuietCall("bot_get_list", new { });

                if (!TryParseJson(list, out JsonElement root)
                    || !root.TryGetProperty("bots", out JsonElement bots)
                    || bots.ValueKind != JsonValueKind.Array
                    || bots.GetArrayLength() == 0)
                {
                    return;
                }

                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    string name = bot.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrEmpty(name))
                    {
                        QuietCall("bot_delete", new { bot_id = name });
                    }
                }
            }
        }

        private string FindPairTabLocal()
        {
            string response = QuietCall("bot_get_sources", new { bot_id = OptimizerBotName });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("sources", out JsonElement sources)
                || sources.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement source in sources.EnumerateArray())
            {
                string type = source.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;

                if (type == "Pair" && source.TryGetProperty("name", out JsonElement nameElement))
                {
                    return nameElement.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }

        #endregion
    }
}
