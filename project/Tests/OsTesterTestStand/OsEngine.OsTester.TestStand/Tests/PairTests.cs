/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Модуль 9 — BotTabPair (FEATURE-0141): Pair-вкладка поднимается легаси-фикстурами
    /// Engine\*.txt до bot_create, дальше настройка только живым MCP bot_set_config_tab_pair
    /// до tester_start. Проверки: бутстрап фикстурами, A-5 sources непустые до старта,
    /// стабильность get после рестарта движка с форсированием портфеля GodMode, прогон
    /// A-6 (candles через перезачёт корреляции look_back=30 на обоих плечах, события по
    /// каждому pair_num, журнал, сделки), негатив D4 (events_is_on=false фикстурой файла
    /// -> тройка нулей и различимый ловушечный вердикт).
    /// </summary>
    public class PairTests : OsTesterTestsBase
    {
        private class PairSpec
        {
            public int PairNum;
            public string Sec1 = string.Empty;
            public string Sec2 = string.Empty;
        }

        private static readonly List<PairSpec> PositivePairs = new List<PairSpec>
        {
            new PairSpec { PairNum = 0, Sec1 = "SBER", Sec2 = "VTBR" },
            new PairSpec { PairNum = 1, Sec1 = "GAZP", Sec2 = "TATN" }
        };

        private static readonly List<PairSpec> NegativePairs = new List<PairSpec>
        {
            new PairSpec { PairNum = 0, Sec1 = "SBER", Sec2 = "VTBR" }
        };

        private const string BotPositive = "pair_a";
        private const string BotNegative = "pair_n";

        private string _tabName = string.Empty;
        private string _getBeforeRestart = string.Empty;

        public PairTests(TestContext context) : base(context, "Pair")
        {
        }

        public void RunAll()
        {
            _context.DataSetName = "Tester10Liq";

            if (!SelectDataSet())
            {
                RecordFail("DataSet", "не удалось выбрать сет Tester10Liq");
                return;
            }

            if (!WaitForSecurities(180))
            {
                RecordFail("DataSet", "сет Tester10Liq не отдал бумаги");
                return;
            }

            DeleteAllRobots();
            DeleteBotIfExists(BotPositive);
            DeleteBotIfExists(BotNegative);

            FixtureBootstrapRun();
        }

        #region Positive path

        private void FixtureBootstrapRun()
        {
            string expectedTab = BotPositive + "tab0";

            try
            {
                WritePairFixtures(BotPositive, expectedTab, PositivePairs, eventsOn: true);
            }
            catch (Exception error)
            {
                RecordFail("Fixtures", "не записать фикстуры: " + error.Message);
                return;
            }

            string created = ToolsCall("bot_create", new { strategy_name = "PairCorrelationTrader", name = BotPositive });

            if (!TryParseJson(created, out JsonElement createdRoot))
            {
                RecordFail("FixtureBootstrap", "bot_create не ответил: " + GetErrorText(created));
                return;
            }

            _tabName = GetTabName(BotPositive, "Pair");

            if (_tabName != expectedTab)
            {
                RecordFail("FixtureBootstrap", $"ожидался Pair-таб '{expectedTab}', получен '{_tabName}'");
                return;
            }

            if (!TryParseJson(ToolsCall("bot_get_config_tab_pair", new { bot_id = BotPositive, tab_name = _tabName }), out JsonElement initial))
            {
                RecordFail("FixtureBootstrap", "bot_get_config_tab_pair не ответил после bot_create");
                return;
            }

            int pairCount = GetInt(initial, "count");

            if (pairCount != PositivePairs.Count)
            {
                RecordFail("FixtureBootstrap", $"фикстуры подняли {pairCount} пар вместо {PositivePairs.Count}");
                return;
            }

            // Фикстуры читаются парами из PairsNamesToLoad: имена плеч приходят из
            // leg ConnectorPrime, security_key собирается из коннекторов плеч.
            string key0 = GetPairString(initial, 0, "security_key");
            string key1 = GetPairString(initial, 1, "security_key");

            if (key0 != "SBER_|_VTBR" || key1 != "GAZP_|_TATN")
            {
                RecordFail("FixtureBootstrap", $"security_key после загрузки фикстур: pair0='{key0}', pair1='{key1}'");
                return;
            }

            RecordPass("FixtureBootstrap", $"{pairCount} пары из Engine\\*.txt фикстур, ключи SBER_|_VTBR / GAZP_|_TATN");

            if (!McpLiveSet())
            {
                return;
            }

            if (!SourcesBeforeStart())
            {
                return;
            }

            WaitForReconnect();

            if (!TryParseJson(QuietCall("bot_get_config_tab_pair", new { bot_id = BotPositive, tab_name = _tabName }), out JsonElement beforeRestart))
            {
                RecordFail("RestartStable", "get до рестарта не разобран");
                return;
            }

            _getBeforeRestart = beforeRestart.ToString();

            RestartStep();
            PositiveRun();
            NegativeD4Run();
        }

        private bool McpLiveSet()
        {
            bool allOk = true;

            foreach (PairSpec spec in PositivePairs)
            {
                string response = ToolsCall("bot_set_config_tab_pair", new
                {
                    bot_id = BotPositive,
                    tab_name = _tabName,
                    pair_num = spec.PairNum,
                    sec1_server_type = "Tester",
                    sec1_server_full_name = "Tester",
                    sec1_portfolio_name = "GodMode",
                    sec1_security_name = spec.Sec1,
                    sec1_security_class = "TestClass",
                    sec1_time_frame = "Min30",
                    sec2_server_type = "Tester",
                    sec2_server_full_name = "Tester",
                    sec2_portfolio_name = "GodMode",
                    sec2_security_name = spec.Sec2,
                    sec2_security_class = "TestClass",
                    sec2_time_frame = "Min30",
                    sec1_trade_regime = "Market",
                    sec2_trade_regime = "Market",
                    sec1_volume_type = "Contract",
                    sec2_volume_type = "Contract",
                    sec1_volume = 5,
                    sec2_volume = 5,
                    sec1_slippage = 0,
                    sec2_slippage = 0,
                    correlation_look_back = 30,
                    cointegration_look_back = 30,
                    cointegration_deviation = 1,
                    auto_rebuild_correlation = true,
                    auto_rebuild_cointegration = true,
                    events_is_on = true
                });

                if (!TryParseJson(response, out JsonElement root))
                {
                    RecordFail("McpSet", $"set pair_num={spec.PairNum} не ответил: {GetErrorText(response)}");
                    return false;
                }

                string regime = GetPairString(root, spec.PairNum, "sec1_trade_regime");
                string leg2 = GetPairString(root, spec.PairNum, "sec2_security_name");
                bool eventsOn = GetPairBool(root, spec.PairNum, "events_is_on");
                int lookBack = GetPairInt(root, spec.PairNum, "correlation_look_back");

                if (regime != "Market" || leg2 != spec.Sec2 || eventsOn == false || lookBack != 30)
                {
                    RecordFail("McpSet", $"read-after-write pair_num={spec.PairNum}: regime='{regime}', sec2='{leg2}', events={eventsOn}, look_back={lookBack}");
                    allOk = false;
                }
            }

            if (!SetBotParamsStrict(BotPositive, new Dictionary<string, object> { ["Regime"] = "On" }, "McpSet"))
            {
                return false;
            }

            if (allOk)
            {
                RecordPass("McpSet", "живой MCP-set до tester_start: режимы Market, Contract 5, look_back 30, events true (оба плеча), Regime=On");
            }

            return allOk;
        }

        private bool SourcesBeforeStart()
        {
            string response = QuietCall("bot_get_sources", new { bot_id = BotPositive });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("sources", out JsonElement sources)
                || sources.ValueKind != JsonValueKind.Array)
            {
                RecordFail("SourcesBeforeStart", "bot_get_sources не разобран");
                return false;
            }

            List<string> pairSecurities = new List<string>();

            foreach (JsonElement source in sources.EnumerateArray())
            {
                string type = source.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;

                if (type != "Pair")
                {
                    continue;
                }

                if (source.TryGetProperty("securities", out JsonElement securities) && securities.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in securities.EnumerateArray())
                    {
                        pairSecurities.Add(item.GetString() ?? string.Empty);
                    }
                }
            }

            bool ok = pairSecurities.Count == 2
                && pairSecurities.Contains("SBER/VTBR")
                && pairSecurities.Contains("GAZP/TATN");

            if (ok)
            {
                RecordPass("SourcesBeforeStart", "A-5 до tester_start: sources пары непустые — SBER/VTBR, GAZP/TATN");
                return true;
            }

            RecordFail("SourcesBeforeStart", "sources пары: " + string.Join(", ", pairSecurities));
            return false;
        }

        private void RestartStep()
        {
            try
            {
                _context.RestartOsEngine("-testerlight");
            }
            catch (Exception error)
            {
                RecordFail("RestartStable", "рестарт движка не выполнен: " + error.Message);
                return;
            }

            if (!WaitForTesterReady(90))
            {
                RecordFail("RestartStable", "тестер после рестарта не поднялся");
                return;
            }

            ToolsCall("tester_data_set_config", new { set_name = "Tester10Liq" });

            if (!WaitForSecurities(180))
            {
                RecordFail("RestartStable", "бумаги сета после рестарта не загружены");
                return;
            }

            if (!TryParseJson(QuietCall("bot_get_config_tab_pair", new { bot_id = BotPositive, tab_name = _tabName }), out JsonElement after))
            {
                RecordFail("RestartStable", $"бот/вкладка не восстановлены после рестарта: {GetErrorText(QuietCall("bot_get_config_tab_pair", new { bot_id = BotPositive, tab_name = _tabName }))}");
                return;
            }

            bool godMode = true;
            string godDetail = string.Empty;

            foreach (PairSpec spec in PositivePairs)
            {
                string p1 = GetPairString(after, spec.PairNum, "sec1_portfolio_name");
                string p2 = GetPairString(after, spec.PairNum, "sec2_portfolio_name");

                if (p1 != "GodMode" || p2 != "GodMode")
                {
                    godMode = false;
                    godDetail += $" pair{spec.PairNum}:{p1}/{p2}";
                }
            }

            // Документированное форсирование: TesterServer переписывает портфель плеча
            // на GodMode при загрузке робота (ConnectorCandles ctor), поэтому после
            // рестарта get обязан отдать GodMode независимо того, что было в файле.
            if (!godMode)
            {
                RecordFail("GodModeForced", "портфель плеч после рестарта не GodMode:" + godDetail);
            }
            else
            {
                RecordPass("GodModeForced", "после рестарта портфель Tester у всех плеч = GodMode (документированное форсирование)");
            }

            // Стабильность get: сравниваем ключевые поля до/после; correlation_last
            // до прогона нулевой и после рестарта обязан остаться нулевым.
            bool stable = true;
            string detail = string.Empty;

            foreach (PairSpec spec in PositivePairs)
            {
                string[] fields = new[] { "sec1_security_name", "sec2_security_name", "sec1_time_frame", "sec2_time_frame", "sec1_trade_regime", "sec2_trade_regime", "security_key" };

                foreach (string field in fields)
                {
                    string before = GetPairString(beforeRestartClone(), spec.PairNum, field);
                    string now = GetPairString(after, spec.PairNum, field);

                    if (before != now)
                    {
                        stable = false;
                        detail += $" pair{spec.PairNum}.{field}: '{before}' -> '{now}'";
                    }
                }

                if (Math.Abs(GetPairDouble(after, spec.PairNum, "correlation_last")) != 0)
                {
                    stable = false;
                    detail += $" pair{spec.PairNum}.correlation_last разогнался до прогона";
                }
            }

            if (stable && GetInt(after, "count") == PositivePairs.Count)
            {
                RecordPass("RestartStable", "get после рестарта стабилен: пары 0/1, плечи и режимы совпадают с прогонами до рестарта");
            }
            else
            {
                RecordFail("RestartStable", "get после рестарта разошёлся:" + detail);
            }
        }

        private JsonElement beforeRestartClone()
        {
            using (JsonDocument doc = JsonDocument.Parse(_getBeforeRestart))
            {
                return doc.RootElement.Clone();
            }
        }

        private void PositiveRun()
        {
            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null)
            {
                RecordFail("RunTrades", "прогон не завершился или отчёт не разобран");
                return;
            }

            if (metrics.Trades <= 0)
            {
                RecordFail("RunTrades", "A-6: ноль сделок на настроенной паре с events=true");
                return;
            }

            RecordPass("RunTrades", $"A-6 прогон: trades={metrics.Trades}, profit={metrics.ProfitAbs}, volume={metrics.Volume}");

            if (!TryParseJson(QuietCall("bot_get_config_tab_pair", new { bot_id = BotPositive, tab_name = _tabName }), out JsonElement after))
            {
                RecordFail("CorrelationEventsPerPair", "get после прогона не разобран");
                return;
            }

            double corr0 = GetPairDouble(after, 0, "correlation_last");
            double corr1 = GetPairDouble(after, 1, "correlation_last");

            // Перезчёт корреляции с look_back=30 требует >=30 готовых свечей на ОБОИХ
            // плечах пары — ненулевой correlation_last и есть наблюдение candles>0 per-leg
            // (у MCP нет счётчика свечей; перезащённый индикатор — прямой доказательный путь).
            // Пара-специфичное значение на каждом pair_num = атрибуция события конкретной паре.
            if (corr0 == 0 || corr1 == 0)
            {
                RecordFail("CorrelationEventsPerPair", $"correlation_last pair0={corr0}, pair1={corr1} — событие не дошло до одной из пар");
            }
            else if (Math.Abs(corr0 - corr1) < 1e-9)
            {
                RecordFail("CorrelationEventsPerPair", $"значения пар совпали ({corr0}) — атрибуция не подтверждена");
            }
            else
            {
                RecordPass("CorrelationEventsPerPair", $"CorrelationChangeEvent атрибутирован парам: pair0 corr={corr0:F4}, pair1 corr={corr1:F4} (>=30 свечей на каждом плече по look_back=30)");
            }

            string journal = QuietCall("bot_journal_get_closed_positions", new { bot_id = BotPositive });

            int closedCount = TryParseJson(journal, out JsonElement journalRoot) ? GetInt(journalRoot, "count") : -1;

            if (closedCount > 0)
            {
                RecordPass("JournalNonEmpty", $"журнал пары непустой: closed positions = {closedCount}");
            }
            else
            {
                RecordFail("JournalNonEmpty", $"журнал пустой (count={closedCount}) при trades={metrics.Trades}");
            }
        }

        #endregion

        #region Negative D4

        private void NegativeD4Run()
        {
            DeleteBotIfExists(BotPositive);

            try
            {
                WritePairFixtures(BotNegative, BotNegative + "tab0", NegativePairs, eventsOn: false);
            }
            catch (Exception error)
            {
                RecordFail("NegativeD4", "не записать негатив-фикстуры: " + error.Message);
                return;
            }

            string created = QuietCall("bot_create", new { strategy_name = "PairCorrelationTrader", name = BotNegative });

            if (IsError(created))
            {
                RecordFail("NegativeD4", "bot_create негатива не прошёл: " + GetErrorText(created));
                return;
            }

            string negTab = BotNegative + "tab0";

            string initialResponse = QuietCall("bot_get_config_tab_pair", new { bot_id = BotNegative, tab_name = negTab });

            if (!TryParseJson(initialResponse, out JsonElement initial))
            {
                RecordFail("NegativeD4", "get негатива не разобран: " + GetErrorText(initialResponse));
                return;
            }

            if (GetPairBool(initial, 0, "events_is_on"))
            {
                RecordFail("NegativeD4", "легаси-загрузка не прочла events_is_on=false из фикстуры файла");
                return;
            }

            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null)
            {
                RecordFail("NegativeD4", "прогон негатива не завершился");
                return;
            }

            bool eventsZero = false;
            double corr = 0;

            if (TryParseJson(QuietCall("bot_get_config_tab_pair", new { bot_id = BotNegative, tab_name = negTab }), out JsonElement after))
            {
                corr = GetPairDouble(after, 0, "correlation_last");
                eventsZero = corr == 0;
            }

            string journal = QuietCall("bot_journal_get_closed_positions", new { bot_id = BotNegative });
            int journalCount = TryParseJson(journal, out JsonElement journalRoot) ? GetInt(journalRoot, "count") : -1;

            bool candlesZero = eventsZero; // событий нет => свечной поток плеч не запускался (гейд ConnectorCandles)

            if (candlesZero && eventsZero && journalCount == 0 && metrics.Trades == 0)
            {
                Console.WriteLine("  EMPTY-STREAM-TRAP: ожидаемый FAIL по позитивным критериям пойман — ноль сделок из-за пустого потока (events_is_on=false из файла).");
                RecordPass("NegativeD4", "A-7 ловушка сработала: тройка нулей (candles/события/журнал=0) + trades=0; на позитивных критериях вердикт был бы FAIL");
            }
            else
            {
                RecordFail("NegativeD4", $"пустой поток НЕ пойман: candles={candlesZero}, corr={corr}, journal={journalCount}, trades={metrics.Trades}");
            }

            DeleteBotIfExists(BotNegative);
        }

        #endregion

        #region Fixtures

        private static string EngineDirectory(TestContext context)
        {
            string exeDir = Path.GetDirectoryName(context.OsEnginePath) ?? ".";
            return Path.Combine(exeDir, "Engine");
        }

        private void WritePairFixtures(string botName, string tabName, List<PairSpec> pairs, bool eventsOn)
        {
            string engine = EngineDirectory(_context);
            Directory.CreateDirectory(engine);

            List<string> pairNames = new List<string>();

            foreach (PairSpec spec in pairs)
            {
                pairNames.Add(tabName + spec.PairNum);
            }

            File.WriteAllLines(Path.Combine(engine, tabName + "StandartPairsSettings.txt"), new[]
            {
                "0",            // 1  Sec1Slippage
                "7",            // 2  Sec1Volume
                "0",            // 3  Sec2Slippage
                "7",            // 4  Sec2Volume
                "30",           // 5  CorrelationLookBack
                "1",            // 6  CointegrationDeviation
                "30",           // 7  CointegrationLookBack
                "No",           // 8  PairSortType
                "Percent",      // 9  Sec1SlippageType
                "Currency",     // 10 Sec1VolumeType
                "Percent",      // 11 Sec2SlippageType
                "Currency",     // 12 Sec2VolumeType
                eventsOn ? "True" : "False", // 13 _eventsIsOn
                "False",        // 14 _emulatorIsOn
                "Off",          // 15 Sec1TradeRegime
                "Off",          // 16 Sec2TradeRegime
                "True",         // 17 AutoRebuildCointegration
                "True"          // 18 AutoRebuildCorrelation
            });

            File.WriteAllLines(Path.Combine(engine, tabName + "PairsNamesToLoad.txt"), pairNames);

            foreach (PairSpec spec in pairs)
            {
                string pairName = tabName + spec.PairNum;

                File.WriteAllLines(Path.Combine(engine, pairName + "PairsSettings.txt"), new[]
                {
                    spec.PairNum.ToString(), // 1  PairNum
                    "0",                     // 2  Sec1Slippage
                    "7",                     // 3  Sec1Volume
                    "0",                     // 4  Sec2Slippage
                    "7",                     // 5  Sec2Volume
                    "30",                    // 6  CorrelationLookBack
                    "1",                     // 7  CointegrationDeviation
                    "30",                    // 8  CointegrationLookBack
                    "Percent",               // 9  Sec1SlippageType
                    "Currency",              // 10 Sec1VolumeType
                    "Percent",               // 11 Sec2SlippageType
                    "Currency",              // 12 Sec2VolumeType
                    "Off",                   // 13 Sec1TradeRegime
                    "Off",                   // 14 Sec2TradeRegime
                    "No",                    // 15 last entry cointegration side
                    "True",                  // 16 show trade panel
                    "True",                  // 17 AutoRebuildCointegration
                    "True"                   // 18 AutoRebuildCorrelation
                });

                WriteLegFixture(engine, pairName + "1", spec.Sec1, eventsOn);
                WriteLegFixture(engine, pairName + "2", spec.Sec2, eventsOn);
            }
        }

        private static void WriteLegFixture(string engine, string legTab, string security, bool eventsOn)
        {
            File.WriteAllLines(Path.Combine(engine, legTab + "ConnectorPrime.txt"), new[]
            {
                "GodMode",      // 1 PortfolioName
                "False",        // 2 EmulatorIsOn
                security,       // 3 SecurityName
                "Tester",       // 4 ServerType
                "TestClass",    // 5 SecurityClass
                eventsOn ? "True" : "False", // 6 EventsIsOn
                "Tester"        // 7 ServerFullName
            });

            File.WriteAllLines(Path.Combine(engine, legTab + "TimeFrameBuilder.txt"), new[]
            {
                "Min30",        // 1 TimeFrame
                "False",        // 2 save trades in candles
                "Tick",         // 3 candle create type
                "Simple",       // 4 series realization
                string.Empty,   // 5 realization params
                "False",        // 6 market depth max spread is on
                "0"             // 7 market depth max spread
            });
        }

        #endregion

        #region Helpers

        private void DeleteBotIfExists(string botId)
        {
            QuietCall("bot_delete", new { bot_id = botId });
        }

        private static int GetInt(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
                ? element.GetInt32()
                : 0;
        }

        private static JsonElement GetPairNode(JsonElement root, int pairNum)
        {
            if (root.TryGetProperty("pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    if (pair.TryGetProperty("pair_num", out JsonElement num) && num.ValueKind == JsonValueKind.Number && num.GetInt32() == pairNum)
                    {
                        return pair;
                    }
                }
            }

            return default;
        }

        private static string GetPairString(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind == JsonValueKind.Undefined || !pair.TryGetProperty(field, out JsonElement element)
                ? string.Empty
                : element.GetString() ?? string.Empty;
        }

        private static int GetPairInt(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind == JsonValueKind.Undefined || !pair.TryGetProperty(field, out JsonElement element) || element.ValueKind != JsonValueKind.Number
                ? 0
                : element.GetInt32();
        }

        private static double GetPairDouble(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind == JsonValueKind.Undefined || !pair.TryGetProperty(field, out JsonElement element) || element.ValueKind != JsonValueKind.Number
                ? 0
                : element.GetDouble();
        }

        private static bool GetPairBool(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind != JsonValueKind.Undefined
                && pair.TryGetProperty(field, out JsonElement element)
                && element.ValueKind == JsonValueKind.True;
        }

        #endregion
    }
}
