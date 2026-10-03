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
    /// Модуль 4 — BotTabScreener: два разных скринера на 10 бумагах,
    /// базовый прогон + сверка с прогонами с комиссией, другим объёмом и другими индикаторами.
    /// </summary>
    public class ScreenerTests : OsTesterTestsBase
    {
        private string _bot1 = string.Empty;   // SmaScreener
        private string _bot2 = string.Empty;   // BollingerMomentumScreener

        private string _tab1 = string.Empty;
        private string _tab2 = string.Empty;

        private TestMetrics _baseline;

        public ScreenerTests(TestContext context) : base(context, "Screener")
        {
        }

        public void RunAll()
        {
            if (!SelectDataSet10Liq())
            {
                RecordFail("DataSet", "не удалось выбрать сет Tester10Liq");
                return;
            }

            CreateRobots();
            EnableTrading();
            ConfigureScreener();
            RunBaseline();
            RunCommission();
            RunVolume();
            RunIndicators();
            RunCharges();
        }

        #region Steps

        private void CreateRobots()
        {
            _bot1 = CreateOne("SmaScreener", "screener_1");
            _bot2 = CreateOne("BollingerMomentumScreener", "screener_2");

            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("CreateRobots", $"bot1='{_bot1}', bot2='{_bot2}'");
                return;
            }

            RecordPass("CreateRobots", $"{_bot1} (SmaScreener), {_bot2} (BollingerMomentumScreener)");
        }

        private void ConfigureScreener()
        {
            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("ConfigureScreener", "роботы не созданы");
                return;
            }

            _tab1 = GetTabName(_bot1, "Screener");
            _tab2 = GetTabName(_bot2, "Screener");

            if (string.IsNullOrEmpty(_tab1) || string.IsNullOrEmpty(_tab2))
            {
                RecordFail("ConfigureScreener", $"tab1='{_tab1}', tab2='{_tab2}'");
                return;
            }

            List<object> securities = GetScreenerSecurities();

            if (securities.Count == 0)
            {
                RecordFail("ConfigureScreener", "нет бумаг для скринера");
                return;
            }

            bool ok1 = ConfigureScreener(_bot1, _tab1, securities);
            bool ok2 = ConfigureScreener(_bot2, _tab2, securities);

            WaitForReconnect();

            if (ok1 && ok2)
            {
                RecordPass("ConfigureScreener", $"бумаг {securities.Count}, ТФ Min30");
            }
            else
            {
                RecordFail("ConfigureScreener", $"ok1={ok1}, ok2={ok2}");
            }
        }

        private void RunBaseline()
        {
            TestMetrics metrics = RunAndGetMetrics();

            if (metrics == null)
            {
                RecordFail("Baseline", "не удалось получить метрики");
                return;
            }

            if (metrics.Trades <= 0)
            {
                RecordFail("Baseline", "роботы не торговали (trades=0)");
                return;
            }

            _baseline = metrics;

            RecordPass("Baseline", $"trades={metrics.Trades}, profit={metrics.ProfitAbs}, commission={metrics.Commission}, volume={metrics.Volume}");
        }

        private void RunCommission()
        {
            SetCommission("Percent", 0.04m);

            TestMetrics metrics = RunAndGetMetrics();

            SetCommission("None", 0m);

            if (metrics == null)
            {
                RecordFail("Commission", "нет метрик");
                return;
            }

            if (metrics.Commission > 0m)
            {
                RecordPass("Commission", $"комиссия учтена: {metrics.Commission}");
            }
            else
            {
                RecordFail("Commission", $"комиссия не учтена: {metrics.Commission}");
            }
        }

        private void RunVolume()
        {
            if (_baseline == null)
            {
                RecordFail("Volume", "нет базового прогона");
                return;
            }

            SetVolumePercent(10m);

            TestMetrics metrics = RunAndGetMetrics();

            SetVolumePercent(20m);

            if (metrics == null)
            {
                RecordFail("Volume", "нет метрик");
                return;
            }

            if (metrics.Volume < _baseline.Volume * 0.9m)
            {
                RecordPass("Volume", $"объём изменился: {_baseline.Volume} -> {metrics.Volume}");
            }
            else
            {
                RecordFail("Volume", $"объём не изменился: baseline={_baseline.Volume}, now={metrics.Volume}");
            }
        }

        private void RunIndicators()
        {
            if (_baseline == null)
            {
                RecordFail("Indicators", "нет базового прогона");
                return;
            }

            SetIndicatorParams();

            TestMetrics metrics = RunAndGetMetrics();

            RestoreIndicatorParams();

            if (metrics == null)
            {
                RecordFail("Indicators", "нет метрик");
                return;
            }

            if (metrics.Trades != _baseline.Trades)
            {
                RecordPass("Indicators", $"позиции изменились: {_baseline.Trades} -> {metrics.Trades}");
            }
            else
            {
                RecordFail("Indicators", $"позиции не изменились: {_baseline.Trades}");
            }
        }

        private void RunCharges()
        {
            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("Charges", "роботы не созданы");
                return;
            }

            // а) включить начисления (дивиденды + налоги + маржа)
            ToolsCall("tester_charges_set_config", new
            {
                dividends_is_on = true,
                taxes_is_on = true,
                margin_regime = "Summ"
            });

            // б) прогнать
            bool finished = StartTestAndWait(900);

            // в) отчёт журналов
            string report = QuietCall("tester_get_report", new { });

            // г) отключить начисления
            ToolsCall("tester_charges_set_config", new
            {
                dividends_is_on = false,
                taxes_is_on = false,
                margin_regime = "Off"
            });

            if (!finished)
            {
                RecordFail("Charges", "прогон не завершился");
                return;
            }

            if (!TryParseJson(report, out JsonElement root)
                || !root.TryGetProperty("cash_flows", out JsonElement cashFlows)
                || cashFlows.ValueKind != JsonValueKind.Array)
            {
                RecordFail("Charges", "нет cash_flows");
                return;
            }

            int count = cashFlows.GetArrayLength();
            bool hasDividend = false;
            bool hasTax = false;
            bool hasMargin = false;

            foreach (JsonElement entry in cashFlows.EnumerateArray())
            {
                string type = entry.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == "dividend")
                {
                    hasDividend = true;
                }
                else if (type == "tax")
                {
                    hasTax = true;
                }
                else if (type == "margin")
                {
                    hasMargin = true;
                }
            }

            if (count > 0)
            {
                RecordPass("Charges", $"начисления есть: {count} записей (dividend={hasDividend}, tax={hasTax}, margin={hasMargin})");
            }
            else
            {
                RecordFail("Charges", "начислений нет (cash_flows пуст)");
            }
        }

        #endregion

        #region Helpers

        private bool SelectDataSet10Liq()
        {
            WaitForTesterReady(60);
            ResetTesterDefaults();
            DeleteAllRobots();

            string response = ToolsCall("tester_data_set_config", new { set_name = "Tester10Liq" });
            return !IsError(response);
        }

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
                time_frame = "Min30",
                securities = securities
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

        private void SetCommission(string type, decimal value)
        {
            ToolsCall("bot_set_config_tab_screener", new
            {
                bot_id = _bot1,
                tab_name = _tab1,
                commission_type = type,
                commission_value = value
            });

            ToolsCall("bot_set_config_tab_screener", new
            {
                bot_id = _bot2,
                tab_name = _tab2,
                commission_type = type,
                commission_value = value
            });

            WaitForReconnect();
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
            // SmaScreener: Sma length 100 -> 50
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Sma length"] = 50 }
            });

            // BollingerMomentumScreener: Bollinger length 50 -> 25
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Bollinger length"] = 25 }
            });
        }

        private void RestoreIndicatorParams()
        {
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Sma length"] = 100 }
            });

            ToolsCall("bot_set_params", new
            {
                bot_id = _bot2,
                parameters = new Dictionary<string, object> { ["Bollinger length"] = 50 }
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
