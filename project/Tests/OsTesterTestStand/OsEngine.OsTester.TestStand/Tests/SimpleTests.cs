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
    /// Модуль 3 — BotTabSimple: два разных робота, базовый прогон + сверка с
    /// прогонами с комиссией, другим объёмом и другими параметрами индикаторов.
    /// </summary>
    public class SimpleTests : OsTesterTestsBase
    {
        private string _bot1 = string.Empty;   // SmaTrendSample
        private string _bot2 = string.Empty;   // BollingerRevers

        private string _tab1 = string.Empty;
        private string _tab2 = string.Empty;

        private TestMetrics _baseline;

        public SimpleTests(TestContext context) : base(context, "Simple")
        {
        }

        public void RunAll()
        {
            if (!SelectDataSet())
            {
                RecordFail("DataSet", "не удалось выбрать сет");
                return;
            }

            CreateRobots();
            EnableTrading();
            ConfigureSources();
            RunBaseline();
            RunCommission();
            RunVolume();
            RunIndicators();
            RunCharges();
        }

        #region Steps

        private void CreateRobots()
        {
            _bot1 = CreateOne("SmaTrendSample", "simple_1");
            _bot2 = CreateOne("BollingerRevers", "simple_2");

            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("CreateRobots", $"bot1='{_bot1}', bot2='{_bot2}'");
                return;
            }

            RecordPass("CreateRobots", $"{_bot1} (SmaTrendSample), {_bot2} (BollingerRevers)");
        }

        private void ConfigureSources()
        {
            if (string.IsNullOrEmpty(_bot1) || string.IsNullOrEmpty(_bot2))
            {
                RecordFail("ConfigureSources", "роботы не созданы");
                return;
            }

            string security = GetFirstSecurityName();

            if (string.IsNullOrEmpty(security))
            {
                RecordFail("ConfigureSources", "нет бумаги в сете");
                return;
            }

            _tab1 = GetTabName(_bot1, "Simple");
            _tab2 = GetTabName(_bot2, "Simple");

            if (string.IsNullOrEmpty(_tab1) || string.IsNullOrEmpty(_tab2))
            {
                RecordFail("ConfigureSources", $"tab1='{_tab1}', tab2='{_tab2}'");
                return;
            }

            bool ok1 = ConfigureSimple(_bot1, _tab1, security);
            bool ok2 = ConfigureSimple(_bot2, _tab2, security);

            WaitForReconnect();

            if (ok1 && ok2)
            {
                RecordPass("ConfigureSources", $"бумага {security}, ТФ Min30");
            }
            else
            {
                RecordFail("ConfigureSources", $"ok1={ok1}, ok2={ok2}");
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
            // SmaTrendSample: Sma length 30 -> 10
            ToolsCall("bot_set_params", new
            {
                bot_id = _bot1,
                parameters = new Dictionary<string, object> { ["Sma length"] = 10 }
            });

            // BollingerRevers: Bollinger Length 12 -> 6
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

        #endregion
    }
}
