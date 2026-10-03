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
    /// База для модулей «Тип данных …» (TickAllCandleState / MarketDepthAllCandleState):
    /// прогон на свечах, собранных из тиков/стаканов, тремя типами роботов —
    /// BotTabSimple, BotTabScreener и робот с сеткой.
    /// </summary>
    public abstract class DataTypeTestsBase : OsTesterTestsBase
    {
        private readonly string _setName;
        private readonly string _typeTesterData;
        private readonly string _candleMarketDataType;
        private readonly bool _includeGrid;

        private string _bot1 = string.Empty;
        private string _bot2 = string.Empty;
        private string _tab1 = string.Empty;
        private string _tab2 = string.Empty;

        private string _screener1 = string.Empty;
        private string _screener2 = string.Empty;
        private string _screenerTab1 = string.Empty;
        private string _screenerTab2 = string.Empty;

        private string _gridBot = string.Empty;
        private string _gridTab = string.Empty;

        private TestMetrics _baseline;

        protected DataTypeTestsBase(TestContext context, string module, string setName, string typeTesterData, bool includeGrid)
            : base(context, module)
        {
            _setName = setName;
            _typeTesterData = typeTesterData;
            _includeGrid = includeGrid;
            _candleMarketDataType = typeTesterData.StartsWith("Tick", StringComparison.OrdinalIgnoreCase)
                ? "Tick"
                : "MarketDepth";
        }

        public void RunAll()
        {
            if (!SelectDataSetAndType())
            {
                RecordFail("DataSet", $"не удалось выбрать сет {_setName} / тип {_typeTesterData}");
                return;
            }

            RunBotTabSimple();
            RunBotTabScreener();

            if (_includeGrid)
            {
                RunGrid();
            }
        }

        #region Setup

        private bool SelectDataSetAndType()
        {
            WaitForTesterReady(60);
            ResetTesterDefaults();
            DeleteAllRobots();

            string setResponse = ToolsCall("tester_data_set_config", new { set_name = _setName });
            if (IsError(setResponse))
            {
                return false;
            }

            string typeResponse = ToolsCall("tester_data_set_config", new { type_tester_data = _typeTesterData });
            if (IsError(typeResponse))
            {
                return false;
            }

            return WaitForSecurities(30);
        }

        #endregion

        #region BotTabSimple

        private void RunBotTabSimple()
        {
            DeleteAllRobots();

            _bot1 = CreateOne("SmaTrendSample", "simple_1");
            _bot2 = CreateOne("BollingerRevers", "simple_2");

            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("SimpleCreate", $"bot1='{_bot1}', bot2='{_bot2}'");
                return;
            }

            RecordPass("SimpleCreate", $"{_bot1} (SmaTrendSample), {_bot2} (BollingerRevers)");

            EnableTrading(_bot1, _bot2);

            string security = GetFirstSecurityName();
            _tab1 = GetTabName(_bot1, "Simple");
            _tab2 = GetTabName(_bot2, "Simple");

            if (string.IsNullOrEmpty(security) || string.IsNullOrEmpty(_tab1) || string.IsNullOrEmpty(_tab2))
            {
                RecordFail("SimpleConfigure", $"security='{security}', tab1='{_tab1}', tab2='{_tab2}'");
                return;
            }

            bool ok1 = ConfigureSimple(_bot1, _tab1, security);
            bool ok2 = ConfigureSimple(_bot2, _tab2, security);

            WaitForReconnect();

            if (ok1 && ok2)
            {
                RecordPass("SimpleConfigure", $"бумага {security}");
            }
            else
            {
                RecordFail("SimpleConfigure", $"ok1={ok1}, ok2={ok2}");
                return;
            }

            RunSimpleBaseline();
            RunSimpleCommission();
            RunSimpleVolume();
            RunSimpleIndicators();
        }

        private void RunSimpleBaseline()
        {
            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null || metrics.Trades <= 0)
            {
                RecordFail("SimpleBaseline", metrics == null ? "нет метрик" : "роботы не торговали (trades=0)");
                return;
            }

            _baseline = metrics;

            RecordPass("SimpleBaseline", $"trades={metrics.Trades}, profit={metrics.ProfitAbs}, volume={metrics.Volume}");
        }

        private void RunSimpleCommission()
        {
            if (_baseline == null)
            {
                RecordFail("SimpleCommission", "нет базового прогона");
                return;
            }

            SetCommission("Percent", 0.04m);

            TestMetrics metrics = RunAndGetMetrics();

            SetCommission("None", 0m);

            if (metrics == null)
            {
                RecordFail("SimpleCommission", "нет метрик");
                return;
            }

            if (metrics.Commission > 0m)
            {
                RecordPass("SimpleCommission", $"комиссия учтена: {metrics.Commission}");
            }
            else
            {
                RecordFail("SimpleCommission", $"комиссия не учтена: {metrics.Commission}");
            }
        }

        private void RunSimpleVolume()
        {
            if (_baseline == null)
            {
                RecordFail("SimpleVolume", "нет базового прогона");
                return;
            }

            SetVolumePercent(10m);

            TestMetrics metrics = RunAndGetMetrics();

            SetVolumePercent(20m);

            if (metrics == null)
            {
                RecordFail("SimpleVolume", "нет метрик");
                return;
            }

            if (metrics.Volume < _baseline.Volume * 0.9m)
            {
                RecordPass("SimpleVolume", $"объём изменился: {_baseline.Volume} -> {metrics.Volume}");
            }
            else
            {
                RecordFail("SimpleVolume", $"объём не изменился: baseline={_baseline.Volume}, now={metrics.Volume}");
            }
        }

        private void RunSimpleIndicators()
        {
            if (_baseline == null)
            {
                RecordFail("SimpleIndicators", "нет базового прогона");
                return;
            }

            SetIndicatorParams();

            TestMetrics metrics = RunAndGetMetrics();

            RestoreIndicatorParams();

            if (metrics == null)
            {
                RecordFail("SimpleIndicators", "нет метрик");
                return;
            }

            if (metrics.Trades != _baseline.Trades)
            {
                RecordPass("SimpleIndicators", $"позиции изменились: {_baseline.Trades} -> {metrics.Trades}");
            }
            else
            {
                RecordFail("SimpleIndicators", $"позиции не изменились: {_baseline.Trades}");
            }
        }

        #endregion

        #region BotTabScreener

        private void RunBotTabScreener()
        {
            DeleteAllRobots();

            _screener1 = CreateOne("SmaScreener", "screener_1");
            _screener2 = CreateOne("BollingerMomentumScreener", "screener_2");

            if (string.IsNullOrEmpty(_screener1) || string.IsNullOrEmpty(_screener2))
            {
                RecordFail("ScreenerCreate", $"screener1='{_screener1}', screener2='{_screener2}'");
                return;
            }

            RecordPass("ScreenerCreate", $"{_screener1} (SmaScreener), {_screener2} (BollingerMomentumScreener)");

            EnableTrading(_screener1, _screener2);

            _screenerTab1 = GetTabName(_screener1, "Screener");
            _screenerTab2 = GetTabName(_screener2, "Screener");

            if (string.IsNullOrEmpty(_screenerTab1) || string.IsNullOrEmpty(_screenerTab2))
            {
                RecordFail("ScreenerConfigure", $"tab1='{_screenerTab1}', tab2='{_screenerTab2}'");
                return;
            }

            List<object> securities = GetScreenerSecurities();

            if (securities.Count == 0)
            {
                RecordFail("ScreenerConfigure", "нет бумаг для скринера");
                return;
            }

            bool ok1 = ConfigureScreener(_screener1, _screenerTab1, securities);
            bool ok2 = ConfigureScreener(_screener2, _screenerTab2, securities);

            WaitForReconnect();

            if (ok1 && ok2)
            {
                RecordPass("ScreenerConfigure", $"бумаг {securities.Count}");
            }
            else
            {
                RecordFail("ScreenerConfigure", $"ok1={ok1}, ok2={ok2}");
                return;
            }

            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null || metrics.Trades <= 0)
            {
                RecordFail("ScreenerBaseline", metrics == null ? "нет метрик" : "роботы не торговали (trades=0)");
                return;
            }

            RecordPass("ScreenerBaseline", $"trades={metrics.Trades}, profit={metrics.ProfitAbs}, volume={metrics.Volume}");
        }

        #endregion

        #region Grid

        private void RunGrid()
        {
            DeleteAllRobots();

            _gridBot = CreateOne("GridBollinger", "grid_1");

            if (string.IsNullOrEmpty(_gridBot))
            {
                RecordFail("GridCreate", "робот не создан");
                return;
            }

            RecordPass("GridCreate", $"{_gridBot} (GridBollinger)");

            EnableTrading(_gridBot);

            string security = GetFirstSecurityName();
            _gridTab = GetTabName(_gridBot, "Simple");

            if (string.IsNullOrEmpty(security) || string.IsNullOrEmpty(_gridTab))
            {
                RecordFail("GridRun", $"security='{security}', tab='{_gridTab}'");
                return;
            }

            bool ok = ConfigureSimple(_gridBot, _gridTab, security);

            WaitForReconnect();

            if (!ok)
            {
                RecordFail("GridRun", "не настроился");
                return;
            }

            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null || metrics.Trades <= 0)
            {
                RecordFail("GridRun", metrics == null ? "нет метрик" : "не торговал (trades=0)");
                return;
            }

            RecordPass("GridRun", $"trades={metrics.Trades}, profit={metrics.ProfitAbs}, volume={metrics.Volume}");
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
                candle_market_data_type = _candleMarketDataType,
                security_name = security,
                time_frame = "Min30"
            });

            return !IsError(response);
        }

        private bool ConfigureScreener(string botId, string tabName, List<object> securities)
        {
            string response = ToolsCall("bot_set_config_tab_screener", new
            {
                bot_id = botId,
                tab_name = tabName,
                server_type = "Tester",
                server_name = "Tester",
                portfolio_name = "GodMode",
                emulator_is_on = true,
                candle_market_data_type = _candleMarketDataType,
                time_frame = "Min30",
                securities = securities
            });

            return !IsError(response);
        }

        private void EnableTrading(params string[] botIds)
        {
            foreach (string botId in botIds)
            {
                ToolsCall("bot_set_params", new
                {
                    bot_id = botId,
                    parameters = new Dictionary<string, object> { ["Regime"] = "On" }
                });
            }
        }

        private void SetCommission(string type, decimal value)
        {
            ToolsCall("bot_set_config_tab_simple", new
            {
                bot_id = _bot1,
                tab_name = _tab1,
                commission_type = type,
                commission_value = value
            });

            ToolsCall("bot_set_config_tab_simple", new
            {
                bot_id = _bot2,
                tab_name = _tab2,
                commission_type = type,
                commission_value = value
            });
        }

        private void SetVolumePercent(decimal value)
        {
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Volume"] = value }
            });

            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Volume"] = value }
            });
        }

        private void SetIndicatorParams()
        {
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Sma length"] = 10 }
            });

            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Bollinger Length"] = 6 }
            });
        }

        private void RestoreIndicatorParams()
        {
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Sma length"] = 30 }
            });

            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Bollinger Length"] = 12 }
            });
        }

        private List<object> GetScreenerSecurities()
        {
            List<object> securities = new List<object>();

            WaitForSecuritiesStable(30);

            string response = QuietCall("tester_get_securities", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("securities", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return securities;
            }

            foreach (JsonElement item in list.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                string className = item.TryGetProperty("class_name", out JsonElement classElement)
                    ? classElement.GetString() ?? string.Empty
                    : string.Empty;

                securities.Add(new { name = name, class_name = className, is_on = true });
            }

            return securities;
        }

        #endregion
    }
}
