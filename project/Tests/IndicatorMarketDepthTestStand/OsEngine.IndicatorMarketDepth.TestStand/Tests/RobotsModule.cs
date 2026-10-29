/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OsEngine.IndicatorMarketDepth.TestStand.Tests
{
    /// <summary>
    /// Модуль smoke-теста торгового робота по стаканному индикатору.
    /// Деплоит робота в тестере на стаканных данных (MarketDepthAllCandleState),
    /// прогоняет и проверяет работоспособность: робот совершил сделки.
    /// Тип таба (Simple/Screener) определяется автоматически по bot_get_sources.
    /// </summary>
    public class RobotsModule : IndicatorTestsBase
    {
        private readonly string _robotStrategy;

        private readonly Dictionary<string, object> _botParams;

        public RobotsModule(TestContext context, string robotStrategy, Dictionary<string, object> botParams) : base(context, "Smoke")
        {
            _robotStrategy = robotStrategy;
            _botParams = botParams ?? new Dictionary<string, object>();
        }

        public void Run()
        {
            _context.PrintModuleHeader(Module + " (" + _robotStrategy + ")");

            if (WaitForTesterReady(60) == false)
            {
                RecordFail("TesterReady", "тестер не поднялся");
                return;
            }

            ResetTesterDefaults();
            QuietCall("tester_portfolio_set_config", new { portfolio_calculation_enabled = true });
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
                RecordFail("BotCreate", "робот '" + _robotStrategy + "' не создался");
                return;
            }

            if (_botParams.Count > 0)
            {
                SetBotParams(botId);
            }

            string tabType = DetectTabType(botId);

            if (string.IsNullOrEmpty(tabType))
            {
                RecordFail("BotTab", "не удалось определить тип таба (Simple/Screener)");
                DeleteBot(botId);
                return;
            }

            string tabName = GetTabName(botId, tabType);

            if (string.IsNullOrEmpty(tabName))
            {
                RecordFail("BotTab", "не удалось получить tab_name");
                DeleteBot(botId);
                return;
            }

            bool configured = tabType == "Screener"
                ? ConfigureScreener(botId, tabName)
                : ConfigureSimple(botId, tabName);

            if (configured == false)
            {
                RecordFail("BotConfig", "не удалось настроить таб '" + tabType + "'");
                DeleteBot(botId);
                return;
            }

            WaitForReconnect();

            if (StartTestAndWait(900) == false)
            {
                RecordFail("TestRun", "прогон тестера не завершился");
                DeleteBot(botId);
                return;
            }

            int closedPositions = GetClosedPositionsCount(botId);

            if (closedPositions < 0)
            {
                RecordFail("Trades", "не удалось получить закрытые сделки");
            }
            else if (closedPositions > 0)
            {
                RecordPass("Trades", "закрытых сделок " + closedPositions);

                decimal profitPercent = GetProfitPercent(botId);
                decimal profitAbs = GetProfitAbs(botId);
                decimal netProfit = GetNetProfit(botId);
                string averageHoldingTime = GetAverageHoldingTime(botId);

                RecordPass("Profit", "прибыль " + profitPercent.ToString("F2") + "% (абс " + profitAbs.ToString("F2") + ", net " + netProfit.ToString("F2") + ")");
                RecordPass("HoldingTime", "среднее время удержания " + averageHoldingTime);
                RecordPass("Long", GetSideSummary(botId, "Long"));
                RecordPass("Short", GetSideSummary(botId, "Short"));
            }
            else
            {
                RecordFail("Trades", "робот не совершил ни одной сделки");
            }

            DeleteBot(botId);
        }

        private string CreateBot()
        {
            string response = ToolsCall("bot_create", new { strategy_name = _robotStrategy, name = "MDISmoke_" + _robotStrategy });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            return root.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
        }

        private void SetBotParams(string botId)
        {
            QuietCall("bot_set_params", new { bot_id = botId, parameters = _botParams });
        }

        private string DetectTabType(string botId)
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
                string type = source.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == "Screener" || type == "Simple")
                {
                    return type;
                }
            }

            return string.Empty;
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

        private bool ConfigureSimple(string botId, string tabName)
        {
            string response = ToolsCall("bot_set_config_tab_simple", new
            {
                bot_id = botId,
                tab_name = tabName,
                server_type = "Tester",
                server_name = "Tester",
                portfolio_name = "GodMode",
                emulator_is_on = true,
                candle_market_data_type = "MarketDepth",
                security_name = _context.Security,
                time_frame = "Min1"
            });

            return !IsError(response);
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

        private int GetClosedPositionsCount(string botId)
        {
            string response = QuietCall("bot_journal_get_closed_positions", new { bot_name = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("count", out JsonElement countElement))
            {
                return -1;
            }

            return countElement.ValueKind == JsonValueKind.Number ? countElement.GetInt32() : -1;
        }

        private decimal GetProfitPercent(string botId)
        {
            string response = QuietCall("bot_journal_get_summary", new { bot_name = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("total_profit_percent", out JsonElement profitElement)
                || profitElement.ValueKind != JsonValueKind.Number)
            {
                return 0m;
            }

            return profitElement.GetDecimal();
        }

        private decimal GetProfitAbs(string botId)
        {
            string response = QuietCall("bot_journal_get_summary", new { bot_name = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("total_profit_abs", out JsonElement profitElement)
                || profitElement.ValueKind != JsonValueKind.Number)
            {
                return 0m;
            }

            return profitElement.GetDecimal();
        }

        private decimal GetNetProfit(string botId)
        {
            string response = QuietCall("bot_journal_get_statistics", new { bot_name = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("net_profit", out JsonElement profitElement)
                || profitElement.ValueKind != JsonValueKind.Number)
            {
                return 0m;
            }

            return profitElement.GetDecimal();
        }

        private string GetAverageHoldingTime(string botId)
        {
            string response = QuietCall("bot_journal_get_statistics", new { bot_name = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("average_holding_time", out JsonElement timeElement)
                || timeElement.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return timeElement.GetString() ?? string.Empty;
        }

        private string GetSideSummary(string botId, string side)
        {
            string response = QuietCall("bot_journal_get_statistics", new { bot_name = botId, side = side });

            if (!TryParseJson(response, out JsonElement root))
            {
                return side + ": нет данных";
            }

            int dealsCount = 0;

            if (root.TryGetProperty("deals_count", out JsonElement dealsElement) && dealsElement.ValueKind == JsonValueKind.Number)
            {
                dealsCount = dealsElement.GetInt32();
            }

            decimal netProfitPercent = 0m;

            if (root.TryGetProperty("net_profit_percent", out JsonElement percentElement) && percentElement.ValueKind == JsonValueKind.Number)
            {
                netProfitPercent = percentElement.GetDecimal();
            }

            decimal netProfit = 0m;

            if (root.TryGetProperty("net_profit", out JsonElement profitElement) && profitElement.ValueKind == JsonValueKind.Number)
            {
                netProfit = profitElement.GetDecimal();
            }

            return side + ": сделок " + dealsCount + ", прибыль " + netProfitPercent.ToString("F2") + "% (абс " + netProfit.ToString("F2") + ")";
        }

        private void DeleteBot(string botId)
        {
            QuietCall("bot_delete", new { bot_id = botId });
        }
    }
}
