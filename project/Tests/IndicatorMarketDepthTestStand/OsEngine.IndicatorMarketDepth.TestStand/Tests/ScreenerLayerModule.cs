/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Text.Json;

namespace OsEngine.IndicatorMarketDepth.TestStand.Tests
{
    /// <summary>
    /// Модуль теста роботного слоя создания стаканных индикаторов на BotTabScreener.
    /// Гоняет MarketDepthScreenerTestBot (на двух бумагах) и разбирает его отчёт.
    /// </summary>
    public class ScreenerLayerModule : IndicatorTestsBase
    {
        private const string BotStrategy = "MarketDepthScreenerTestBot";

        public ScreenerLayerModule(TestContext context) : base(context, "ScreenerLayer")
        {
        }

        public void Run()
        {
            _context.PrintModuleHeader(Module);

            if (WaitForTesterReady(60) == false)
            {
                RecordFail("TesterReady", "тестер не поднялся");
                return;
            }

            ResetTesterDefaults();
            DeleteAllRobots();

            string setResponse = QuietCall("tester_data_set_config", new { set_name = _context.DataSetName });

            if (IsError(setResponse))
            {
                RecordFail("DataSet", GetErrorText(setResponse));
                return;
            }

            string typeResponse = QuietCall("tester_data_set_config", new { type_tester_data = "MarketDepthAllCandleState" });

            if (IsError(typeResponse))
            {
                RecordFail("DataType", GetErrorText(typeResponse));
                return;
            }

            if (WaitForSecurities(30) == false)
            {
                RecordFail("Securities", "бумаги сета не загрузились");
                return;
            }

            string botId = CreateBot();

            if (string.IsNullOrEmpty(botId))
            {
                RecordFail("BotCreate", "робот не создался");
                return;
            }

            string tabName = GetTabName(botId, "Screener");

            if (string.IsNullOrEmpty(tabName))
            {
                RecordFail("BotTab", "не удалось получить tab_name");
                DeleteBot(botId);
                return;
            }

            if (ConfigureScreener(botId, tabName) == false)
            {
                RecordFail("BotConfig", "не удалось настроить скринер");
                DeleteBot(botId);
                return;
            }

            WaitForReconnect();

            if (Deploy(botId) == false)
            {
                RecordFail("Deploy", "не удалось развернуть индикатор");
                DeleteBot(botId);
                return;
            }

            if (StartTestAndWait(900) == false)
            {
                RecordFail("TestRun", "прогон тестера не завершился");
                DeleteBot(botId);
                return;
            }

            string report = WaitForReportJson(botId, 120);

            if (string.IsNullOrEmpty(report))
            {
                RecordFail("Report", "не удалось получить отчёт робота");
                DeleteBot(botId);
                return;
            }

            ParseReport(report);

            DeleteBot(botId);
        }

        private string CreateBot()
        {
            string response = ToolsCall("bot_create", new { strategy_name = BotStrategy, name = "MDIScreenerLayer" });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            return root.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
        }

        private string GetTabName(string botId, string type)
        {
            string response = QuietCall("bot_get_sources", new { bot_id = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("sources", out JsonElement sources)
                || sources.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement source in sources.EnumerateArray())
            {
                string sourceType = source.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (sourceType == type)
                {
                    return source.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            return string.Empty;
        }

        private bool ConfigureScreener(string botId, string tabName)
        {
            string response = ToolsCall("bot_set_config_tab_screener", new
            {
                bot_id = botId,
                tab_name = tabName,
                server_type = "Tester",
                server_name = "Tester",
                portfolio_name = "GodMode",
                emulator_is_on = true,
                candle_market_data_type = "MarketDepth",
                time_frame = "Min1",
                securities = new[]
                {
                    new { name = _context.Security },
                    new { name = _context.SecondSecurity }
                }
            });

            return !IsError(response);
        }

        private bool Deploy(string botId)
        {
            string response = ToolsCall("bot_click_param_button", new
            {
                bot_id = botId,
                param_name = "DeployIndicator"
            });

            return !IsError(response);
        }

        private void DeleteBot(string botId)
        {
            QuietCall("bot_delete", new { bot_id = botId });
        }
    }
}
