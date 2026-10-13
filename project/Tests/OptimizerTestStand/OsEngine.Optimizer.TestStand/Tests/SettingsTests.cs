/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 7 — Настройки: комиссия и депозит меняют результат (меняем → прогоняем → сверяем → возвращаем).
    /// </summary>
    public class SettingsTests : OsOptimizerTestsBase
    {
        public SettingsTests(TestContext context) : base(context, "Settings")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            ReportSummary baseline = RunAndSummarize();

            if (baseline == null)
            {
                return;
            }

            RunCommission(baseline);
            RunCommissionOneLotFix(baseline);
            RunDeposit(baseline);
            RunSlippage(baseline);
            DividendsGet();
            TradeSettingsGet();
            CacheGet();
            ClearingGet();
            NonTradePeriodsGet();
            NonTradePeriodsAffects();
        }

        private bool Setup()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return false;
            }

            ResetTradeSettings();

            if (!CreateBot("SmaTrendSample"))
            {
                RecordFail("CreateBot", "робот не загрузился");
                return false;
            }

            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("ConfigureTab", "вкладка Simple не настроилась");
                return false;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("Enumeration", "не удалось задать перебор");
                return false;
            }

            return true;
        }

        private ReportSummary RunAndSummarize()
        {
            if (!StartAndWait(600))
            {
                RecordFail("Run", "прогон не завершился за отведённое время");
                return null;
            }

            return GetReportSummary();
        }

        private void RunCommission(ReportSummary baseline)
        {
            ToolsCall("optimizer_trade_settings_set", new { commission_type = "Percent", commission_value = 0.1m });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_trade_settings_set", new { commission_type = "None", commission_value = 0m });

            if (summary == null)
            {
                RecordFail("Commission", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("Commission", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("Commission", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void RunDeposit(ReportSummary baseline)
        {
            ToolsCall("optimizer_trade_settings_set", new { start_deposit = 10000m });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_trade_settings_set", new { start_deposit = 100000m });

            if (summary == null)
            {
                RecordFail("Deposit", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("Deposit", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("Deposit", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void RunCommissionOneLotFix(ReportSummary baseline)
        {
            ToolsCall("optimizer_trade_settings_set", new { commission_type = "OneLotFix", commission_value = 10m });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_trade_settings_set", new { commission_type = "None", commission_value = 0m });

            if (summary == null)
            {
                RecordFail("CommissionOneLotFix", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("CommissionOneLotFix", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("CommissionOneLotFix", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void RunSlippage(ReportSummary baseline)
        {
            ToolsCall("optimizer_trade_settings_set", new { slippage_to_simple_order = 10 });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_trade_settings_set", new { slippage_to_simple_order = 0 });

            if (summary == null)
            {
                RecordFail("Slippage", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("Slippage", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("Slippage", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void DividendsGet()
        {
            ToolsCall("optimizer_dividends_set_config", new
            {
                dividends_is_on = true,
                taxes_is_on = true,
                margin_regime = "Summ"
            });

            string get = ToolsCall("optimizer_dividends_get_config", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root))
            {
                bool dividendsOn = root.TryGetProperty("dividends_is_on", out JsonElement dividendsElement)
                    && dividendsElement.ValueKind == JsonValueKind.True;

                bool taxesOn = root.TryGetProperty("taxes_is_on", out JsonElement taxesElement)
                    && taxesElement.ValueKind == JsonValueKind.True;

                string margin = root.TryGetProperty("margin_regime", out JsonElement marginElement)
                    ? marginElement.GetString() ?? string.Empty
                    : string.Empty;

                ok = dividendsOn && taxesOn && margin == "Summ";
            }

            ToolsCall("optimizer_dividends_set_config", new
            {
                dividends_is_on = false,
                taxes_is_on = false,
                margin_regime = "Off"
            });

            if (ok)
            {
                RecordPass("DividendsGet", "get вернул то, что установил set (дивиденды/налоги/маржа)");
            }
            else
            {
                RecordFail("DividendsGet", "get не совпал с set");
            }
        }

        private void TradeSettingsGet()
        {
            ToolsCall("optimizer_trade_settings_set", new
            {
                commission_type = "Percent",
                commission_value = 0.1m,
                order_execution_type = "Intersection",
                slippage_to_simple_order = 5
            });

            string get = ToolsCall("optimizer_trade_settings_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root))
            {
                string type = root.TryGetProperty("commission_type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                decimal value = root.TryGetProperty("commission_value", out JsonElement valueElement)
                    && valueElement.ValueKind == JsonValueKind.Number
                    ? valueElement.GetDecimal()
                    : -1m;

                string execution = root.TryGetProperty("order_execution_type", out JsonElement executionElement)
                    ? executionElement.GetString() ?? string.Empty
                    : string.Empty;

                int slippage = root.TryGetProperty("slippage_to_simple_order", out JsonElement slippageElement)
                    && slippageElement.ValueKind == JsonValueKind.Number
                    ? slippageElement.GetInt32()
                    : -1;

                ok = type == "Percent" && value == 0.1m && execution == "Intersection" && slippage == 5;
            }

            ToolsCall("optimizer_trade_settings_set", new
            {
                commission_type = "None",
                commission_value = 0m,
                order_execution_type = "Touch",
                slippage_to_simple_order = 0
            });

            if (ok)
            {
                RecordPass("TradeSettingsGet", "get вернул то, что установил set (комиссия/исполнение/проскальзывание)");
            }
            else
            {
                RecordFail("TradeSettingsGet", "get не совпал с set");
            }
        }

        private void CacheGet()
        {
            ToolsCall("optimizer_cache_set", new { cache_indicators_is_on = true });

            string get = ToolsCall("optimizer_cache_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root))
            {
                ok = root.TryGetProperty("cache_indicators_is_on", out JsonElement element) && element.ValueKind == JsonValueKind.True;
            }

            ToolsCall("optimizer_cache_set", new { cache_indicators_is_on = false });

            if (ok)
            {
                RecordPass("CacheGet", "кеширование индикаторов включается/выключается");
            }
            else
            {
                RecordFail("CacheGet", "кеширование не включилось");
            }
        }

        private void ClearingGet()
        {
            ToolsCall("optimizer_trade_settings_set", new
            {
                clearing = new object[] { new { time = "19:00", is_on = true } }
            });

            string get = ToolsCall("optimizer_trade_settings_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root)
                && root.TryGetProperty("clearing", out JsonElement clearing)
                && clearing.ValueKind == JsonValueKind.Array
                && clearing.GetArrayLength() == 1)
            {
                JsonElement item = clearing[0];

                string time = item.TryGetProperty("time", out JsonElement timeElement)
                    ? timeElement.GetString() ?? string.Empty
                    : string.Empty;

                bool isOn = item.TryGetProperty("is_on", out JsonElement onElement) && onElement.ValueKind == JsonValueKind.True;

                ok = time == "19:00" && isOn;
            }

            ToolsCall("optimizer_trade_settings_set", new { clearing = new object[0] });

            if (ok)
            {
                RecordPass("ClearingGet", "отзыв ордеров (19:00) устанавливается/читается");
            }
            else
            {
                RecordFail("ClearingGet", "отзыв ордеров не установился");
            }
        }

        private void NonTradePeriodsGet()
        {
            ToolsCall("optimizer_trade_settings_set", new
            {
                non_trade_periods = new object[] { new { date_start = "2024-01-01T00:00:00", date_end = "2024-01-31T00:00:00", is_on = true } }
            });

            string get = ToolsCall("optimizer_trade_settings_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root)
                && root.TryGetProperty("non_trade_periods", out JsonElement periods)
                && periods.ValueKind == JsonValueKind.Array
                && periods.GetArrayLength() == 1)
            {
                JsonElement period = periods[0];

                bool isOn = period.TryGetProperty("is_on", out JsonElement onElement) && onElement.ValueKind == JsonValueKind.True;

                ok = isOn;
            }

            ToolsCall("optimizer_trade_settings_set", new { non_trade_periods = new object[0] });

            if (ok)
            {
                RecordPass("NonTradePeriodsGet", "неторговые периоды устанавливаются/читаются");
            }
            else
            {
                RecordFail("NonTradePeriodsGet", "неторговые периоды не установились");
            }
        }

        private void NonTradePeriodsAffects()
        {
            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("NonTradePeriodsAffects", "вкладка Simple не настроилась");
                return;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("NonTradePeriodsAffects", "не удалось задать перебор");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("NonTradePeriodsAffects", "базовый прогон не завершился");
                return;
            }

            ReportSummary baseline = GetReportSummary();

            ToolsCall("optimizer_trade_settings_set", new
            {
                non_trade_periods = new object[] { new { date_start = TestDateFrom, date_end = TestDateTo, is_on = true } }
            });

            if (!StartAndWait(600))
            {
                RecordFail("NonTradePeriodsAffects", "прогон с периодом не завершился");
                return;
            }

            ReportSummary noTrade = GetReportSummary();

            ToolsCall("optimizer_trade_settings_set", new { non_trade_periods = new object[0] });

            if (baseline.FirstProfit != 0m && noTrade.FirstProfit == 0m)
            {
                RecordPass("NonTradePeriodsAffects", $"неторговый период: прибыль {baseline.FirstProfit} -> {noTrade.FirstProfit}");
            }
            else
            {
                RecordFail("NonTradePeriodsAffects", $"baseline={baseline.FirstProfit}, noTrade={noTrade.FirstProfit}");
            }
        }
    }
}
