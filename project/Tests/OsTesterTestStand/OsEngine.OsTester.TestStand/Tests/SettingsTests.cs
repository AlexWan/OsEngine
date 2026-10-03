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
    /// Модуль 5 — Настройки тестера: каждая настройка проверяется реальным прогоном
    /// (меняем → прогоняем → сверяем с базовым → возвращаем).
    /// </summary>
    public class SettingsTests : OsTesterTestsBase
    {
        private string _bot1 = string.Empty;   // SmaTrendSample
        private string _bot2 = string.Empty;   // BollingerRevers

        private string _tab1 = string.Empty;
        private string _tab2 = string.Empty;

        private TestMetrics _baseline;

        public SettingsTests(TestContext context) : base(context, "Settings")
        {
        }

        public void RunAll()
        {
            Setup();
            RunBaseline();
            RunSlippage();
            RunPortfolio();
            RunExecutionType();
            RunNonTradePeriods();
        }

        #region Steps

        private void Setup()
        {
            if (!SelectDataSet())
            {
                RecordFail("DataSet", "не удалось выбрать сет");
                return;
            }

            _bot1 = CreateOne("SmaTrendSample", "settings_1");
            _bot2 = CreateOne("BollingerRevers", "settings_2");

            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("Setup", $"bot1='{_bot1}', bot2='{_bot2}'");
                return;
            }

            EnableTrading();

            string security = GetFirstSecurityName();
            _tab1 = GetTabName(_bot1, "Simple");
            _tab2 = GetTabName(_bot2, "Simple");

            if (string.IsNullOrEmpty(security) || string.IsNullOrEmpty(_tab1) || string.IsNullOrEmpty(_tab2))
            {
                RecordFail("Setup", $"security='{security}', tab1='{_tab1}', tab2='{_tab2}'");
                return;
            }

            ConfigureSimple(_bot1, _tab1, security);
            ConfigureSimple(_bot2, _tab2, security);

            WaitForReconnect();
        }

        private void RunBaseline()
        {
            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null || metrics.Trades <= 0)
            {
                RecordFail("Baseline", metrics == null ? "нет метрик" : "роботы не торговали (trades=0)");
                return;
            }

            _baseline = metrics;

            RecordPass("Baseline", $"trades={metrics.Trades}, profit={metrics.ProfitAbs}, volume={metrics.Volume}");
        }

        private void RunSlippage()
        {
            if (_baseline == null)
            {
                RecordFail("Slippage", "нет базового прогона");
                return;
            }

            SetSlippage(10);

            TestMetrics metrics = RunAndGetMetrics();

            SetSlippage(0);

            if (metrics == null)
            {
                RecordFail("Slippage", "нет метрик");
                return;
            }

            if (metrics.ProfitAbs != _baseline.ProfitAbs)
            {
                RecordPass("Slippage", $"прибыль изменилась: {_baseline.ProfitAbs} -> {metrics.ProfitAbs}");
            }
            else
            {
                RecordFail("Slippage", $"прибыль не изменилась: {_baseline.ProfitAbs}");
            }
        }

        private void RunPortfolio()
        {
            if (_baseline == null)
            {
                RecordFail("Portfolio", "нет базового прогона");
                return;
            }

            SetPortfolio(2000000m);

            TestMetrics metrics = RunAndGetMetrics();

            SetPortfolio(1000000m);

            if (metrics == null)
            {
                RecordFail("Portfolio", "нет метрик");
                return;
            }

            if (metrics.Volume > _baseline.Volume * 1.5m)
            {
                RecordPass("Portfolio", $"объём вырос: {_baseline.Volume} -> {metrics.Volume}");
            }
            else
            {
                RecordFail("Portfolio", $"объём не вырос: baseline={_baseline.Volume}, now={metrics.Volume}");
            }
        }

        private void RunExecutionType()
        {
            if (_baseline == null)
            {
                RecordFail("ExecutionType", "нет базового прогона");
                return;
            }

            SetExecutionType("Intersection");

            TestMetrics metrics = RunAndGetMetrics();

            SetExecutionType("Touch");

            if (metrics == null)
            {
                RecordFail("ExecutionType", "нет метрик");
                return;
            }

            if (metrics.ProfitAbs != _baseline.ProfitAbs)
            {
                RecordPass("ExecutionType", $"результат изменился: {_baseline.ProfitAbs} -> {metrics.ProfitAbs}");
            }
            else
            {
                RecordFail("ExecutionType", $"результат не изменился: {_baseline.ProfitAbs}");
            }
        }

        private void RunNonTradePeriods()
        {
            if (_baseline == null)
            {
                RecordFail("NonTradePeriods", "нет базового прогона");
                return;
            }

            AddNonTradePeriod("2023-01-01T00:00:00.0000000", "2024-01-01T00:00:00.0000000");

            TestMetrics metrics = RunAndGetMetrics();

            ClearNonTradePeriods();

            if (metrics == null)
            {
                RecordFail("NonTradePeriods", "нет метрик");
                return;
            }

            if (metrics.Trades != _baseline.Trades)
            {
                RecordPass("NonTradePeriods", $"сделки изменились: {_baseline.Trades} -> {metrics.Trades}");
            }
            else
            {
                RecordFail("NonTradePeriods", $"сделки не изменились: {_baseline.Trades}");
            }
        }

        #endregion

        #region Helpers

        private string CreateOne(string strategyName, string desiredName)
        {
            string response = ToolsCall("bot_create", new { strategy_name = strategyName, name = desiredName });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            return root.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
        }

        private bool ConfigureSimple(string botId, string tabName, string security)
        {
            string response = ToolsCall("bot_set_config_tab_simple", new
            {
                bot_id = botId,
                tab_name = tabName,
                server_type = "Tester",
                server_name = "Tester",
                portfolio_name = "GodMode",
                emulator_is_on = true,
                security_name = security,
                time_frame = "Min30"
            });

            return !IsError(response);
        }

        private void EnableTrading()
        {
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Regime"] = "On" }
            });

            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Regime"] = "On" }
            });
        }

        private void SetSlippage(int value)
        {
            ToolsCall("tester_execution_set_config", new { slippage_to_simple_order = value });
        }

        private void SetPortfolio(decimal value)
        {
            ToolsCall("tester_portfolio_set_config", new { start_portfolio = value });
        }

        private void SetExecutionType(string value)
        {
            ToolsCall("tester_execution_set_config", new { order_execution_type = value });
        }

        private void AddNonTradePeriod(string dateStart, string dateEnd)
        {
            ToolsCall("tester_execution_set_config", new
            {
                non_trade_periods = new[]
                {
                    new { date_start = dateStart, date_end = dateEnd, is_on = true }
                }
            });
        }

        private void ClearNonTradePeriods()
        {
            ToolsCall("tester_execution_set_config", new { non_trade_periods = new object[0] });
        }

        #endregion
    }
}
