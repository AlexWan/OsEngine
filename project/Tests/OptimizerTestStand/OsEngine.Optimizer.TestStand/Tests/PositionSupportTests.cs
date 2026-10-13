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
    /// Модуль 8 — Сопровождение позиции: стоп меняет результат прогона.
    /// </summary>
    public class PositionSupportTests : OsOptimizerTestsBase
    {
        public PositionSupportTests(TestContext context) : base(context, "PositionSupport")
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

            RunStop(baseline);
            RunProfit(baseline);
            PositionSupportGet();
        }

        private bool Setup()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return false;
            }

            ResetPositionSupport();

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

        private void RunStop(ReportSummary baseline)
        {
            ToolsCall("optimizer_position_support_set", new { stop_is_on = true, stop_distance = 1m });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_position_support_set", new { stop_is_on = false });

            if (summary == null)
            {
                RecordFail("Stop", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("Stop", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("Stop", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void RunProfit(ReportSummary baseline)
        {
            ToolsCall("optimizer_position_support_set", new { profit_is_on = true, profit_distance = 1m });

            ReportSummary summary = RunAndSummarize();

            ToolsCall("optimizer_position_support_set", new { profit_is_on = false });

            if (summary == null)
            {
                RecordFail("Profit", "нет результата");
                return;
            }

            if (summary.FirstProfit != baseline.FirstProfit)
            {
                RecordPass("Profit", $"прибыль изменилась: {baseline.FirstProfit} -> {summary.FirstProfit}");
            }
            else
            {
                RecordFail("Profit", $"прибыль не изменилась: {baseline.FirstProfit}");
            }
        }

        private void PositionSupportGet()
        {
            ToolsCall("optimizer_position_support_set", new { stop_is_on = true, stop_distance = 1m });

            string get = ToolsCall("optimizer_position_support_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root))
            {
                ok = root.TryGetProperty("stop_is_on", out JsonElement element)
                    && element.ValueKind == JsonValueKind.True;
            }

            ToolsCall("optimizer_position_support_set", new { stop_is_on = false });

            if (ok)
            {
                RecordPass("PositionSupportGet", "get вернул то, что установил set");
            }
            else
            {
                RecordFail("PositionSupportGet", "get не совпал с set");
            }
        }
    }
}
