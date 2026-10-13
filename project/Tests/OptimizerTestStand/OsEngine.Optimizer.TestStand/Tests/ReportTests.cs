/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 9 — Отчёты: разделы отчёта на месте, save/load round-trip.
    /// </summary>
    public class ReportTests : OsOptimizerTestsBase
    {
        public ReportTests(TestContext context) : base(context, "Report")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("Run", "прогон не завершился за отведённое время");
                return;
            }

            ReportSections();
            SortByProfit();
            Limit();
            SaveLoad();
        }

        private bool Setup()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return false;
            }

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

        private void ReportSections()
        {
            string response = ToolsCall("optimizer_get_report", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("ReportSections", "не разобрать отчёт");
                return;
            }

            bool hasFazes = root.TryGetProperty("fazes", out JsonElement fazes) && fazes.ValueKind == JsonValueKind.Array;
            int fazesCount = root.TryGetProperty("fazes_count", out JsonElement fazesCountElement)
                && fazesCountElement.ValueKind == JsonValueKind.Number
                ? fazesCountElement.GetInt32()
                : 0;
            int reportsCount = root.TryGetProperty("reports_count", out JsonElement reportsCountElement)
                && reportsCountElement.ValueKind == JsonValueKind.Number
                ? reportsCountElement.GetInt32()
                : 0;

            bool hasMetrics = false;

            if (hasFazes && fazes.GetArrayLength() > 0)
            {
                JsonElement firstFaze = fazes[0];

                if (firstFaze.TryGetProperty("reports", out JsonElement reports)
                    && reports.ValueKind == JsonValueKind.Array
                    && reports.GetArrayLength() > 0)
                {
                    JsonElement firstReport = reports[0];
                    hasMetrics = firstReport.TryGetProperty("total_profit", out JsonElement profit)
                        && profit.ValueKind == JsonValueKind.Number;
                }
            }

            if (hasFazes && fazesCount > 0 && reportsCount > 0 && hasMetrics)
            {
                RecordPass("ReportSections", $"фаз={fazesCount}, отчётов={reportsCount}, метрики на месте");
            }
            else
            {
                RecordFail("ReportSections", $"fazes={fazesCount}, reports={reportsCount}, metrics={hasMetrics}");
            }
        }

        private void SortByProfit()
        {
            string response = ToolsCall("optimizer_get_report", new { sort_type = "TotalProfit" });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("fazes", out JsonElement fazes)
                || fazes.ValueKind != JsonValueKind.Array
                || fazes.GetArrayLength() == 0)
            {
                RecordFail("SortByProfit", "не разобрать отчёт");
                return;
            }

            JsonElement firstFaze = fazes[0];

            if (!firstFaze.TryGetProperty("reports", out JsonElement reports)
                || reports.ValueKind != JsonValueKind.Array
                || reports.GetArrayLength() < 2)
            {
                RecordFail("SortByProfit", "мало отчётов для сортировки");
                return;
            }

            decimal firstProfit = GetReportProfit(reports[0]);
            decimal maxProfit = firstProfit;

            foreach (JsonElement report in reports.EnumerateArray())
            {
                decimal profit = GetReportProfit(report);

                if (profit > maxProfit)
                {
                    maxProfit = profit;
                }
            }

            if (firstProfit >= maxProfit)
            {
                RecordPass("SortByProfit", $"сортировка по TotalProfit: первый = {firstProfit} (максимум)");
            }
            else
            {
                RecordFail("SortByProfit", $"первый = {firstProfit}, максимум = {maxProfit}");
            }
        }

        private void Limit()
        {
            string response = ToolsCall("optimizer_get_report", new { limit = 2 });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("fazes", out JsonElement fazes)
                || fazes.ValueKind != JsonValueKind.Array
                || fazes.GetArrayLength() == 0)
            {
                RecordFail("Limit", "не разобрать отчёт");
                return;
            }

            int firstFazeReports = fazes[0].TryGetProperty("reports", out JsonElement reports) && reports.ValueKind == JsonValueKind.Array
                ? reports.GetArrayLength()
                : -1;

            if (firstFazeReports >= 0 && firstFazeReports <= 2)
            {
                RecordPass("Limit", $"limit=2 → {firstFazeReports} отчётов в первой фазе");
            }
            else
            {
                RecordFail("Limit", $"ожидалось ≤2 отчётов, получено {firstFazeReports}");
            }
        }

        private static decimal GetReportProfit(JsonElement report)
        {
            if (report.TryGetProperty("total_profit", out JsonElement profitElement) && profitElement.ValueKind == JsonValueKind.Number)
            {
                return profitElement.GetDecimal();
            }

            return decimal.MinValue;
        }

        private void SaveLoad()
        {
            string path = Path.Combine(Path.GetTempPath(), "optimizer-stand-" + Guid.NewGuid().ToString("N") + ".txt");

            string save = ToolsCall("optimizer_save_report", new { path = path });

            if (IsError(save))
            {
                RecordFail("SaveLoad", "не удалось сохранить отчёт: " + GetErrorText(save));
                return;
            }

            if (!File.Exists(path))
            {
                RecordFail("SaveLoad", $"файл отчёта не создан: {path}");
                return;
            }

            string load = ToolsCall("optimizer_load_report", new { path = path });

            if (IsError(load))
            {
                RecordFail("SaveLoad", "не удалось загрузить отчёт: " + GetErrorText(load));
                DeleteFile(path);
                return;
            }

            int reportsCount = 0;

            if (TryParseJson(load, out JsonElement root)
                && root.TryGetProperty("reports_count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number)
            {
                reportsCount = countElement.GetInt32();
            }

            DeleteFile(path);

            if (reportsCount > 0)
            {
                RecordPass("SaveLoad", $"round-trip выполнен, отчётов={reportsCount}");
            }
            else
            {
                RecordFail("SaveLoad", $"после загрузки отчётов={reportsCount}");
            }
        }

        private static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // временный файл мог уже удалиться
            }
        }
    }
}
