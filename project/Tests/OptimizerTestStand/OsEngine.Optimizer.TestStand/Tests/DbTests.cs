/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 12 — БД результатов: save → list → load (round-trip) → delete.
    /// Сценарии: 2 фазы (2 параметра, целостность данных), 4 фазы, 1 фаза (без OutOfSample).
    /// </summary>
    public class DbTests : OsOptimizerTestsBase
    {
        public DbTests(TestContext context) : base(context, "Db")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            RoundTrip();
            FourFazes();
            SingleFaze();
            ListFiltersAndCleanup();
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

            return true;
        }

        /// <summary>
        /// Перебор двух параметров: Sma length × Envelop length = 10 комбинаций.
        /// </summary>
        private bool SetTwoParamEnumeration()
        {
            List<EnumParam> list = new List<EnumParam>();
            list.Add(new EnumParam { Name = "Sma length", Start = 20, Stop = 24, Step = 1 });
            list.Add(new EnumParam { Name = "Envelop length", Start = 20, Stop = 21, Step = 1 });
            return SetEnumerationMany(list);
        }

        private void SetPhases(int iterationCount, bool lastInSample)
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = iterationCount,
                percent_on_filtration = 30,
                last_in_sample = lastInSample
            });
        }

        private void RoundTrip()
        {
            if (!SetTwoParamEnumeration())
            {
                RecordFail("RoundTrip", "не удалось задать перебор");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("RoundTrip", "прогон не завершился за отведённое время");
                return;
            }

            ReportInfo before = GetReportInfo(ToolsCall("optimizer_get_report", new { }));

            if (before.FazesCount != 2 || !before.HasInSample || !before.HasOutOfSample)
            {
                RecordFail("RoundTrip", $"ожидалось 2 фазы, получено fazes={before.FazesCount}");
                return;
            }

            long runId = SaveRun();

            if (runId <= 0)
            {
                return;
            }

            if (!Listed(runId))
            {
                RecordFail("RoundTrip", $"прогон {runId} не найден в списке");
                DeleteRun(runId);
                return;
            }

            string load = ToolsCall("optimizer_load_report_db", new { run_id = runId });

            if (IsError(load))
            {
                RecordFail("RoundTrip", "не удалось загрузить из БД: " + GetErrorText(load));
                DeleteRun(runId);
                return;
            }

            ReportInfo after = GetReportInfo(load);

            VerifyQuery(runId);

            bool deleted = DeleteRun(runId);

            bool ok = after.FazesCount == before.FazesCount
                && after.HasInSample && after.HasOutOfSample
                && after.FirstBotName == before.FirstBotName
                && Math.Abs(after.FirstProfit - before.FirstProfit) < 0.001m
                && after.HasSma && after.HasEnvelop
                && deleted;

            if (ok)
            {
                RecordPass("RoundTrip", $"целостность: бот='{after.FirstBotName}', прибыль={after.FirstProfit}, фазы=2, параметры Sma+Envelop на месте");
            }
            else
            {
                RecordFail("RoundTrip", $"целостность нарушена: бот до='{before.FirstBotName}' после='{after.FirstBotName}', прибыль до={before.FirstProfit} после={after.FirstProfit}, fazes={after.FazesCount}, Sma={after.HasSma}, Envelop={after.HasEnvelop}");
            }
        }

        private void FourFazes()
        {
            SetPhases(2, false);

            if (!SetTwoParamEnumeration())
            {
                RecordFail("FourFazes", "не удалось задать перебор");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("FourFazes", "прогон не завершился за отведённое время");
                return;
            }

            ReportInfo before = GetReportInfo(ToolsCall("optimizer_get_report", new { }));

            if (before.FazesCount != 4)
            {
                RecordFail("FourFazes", $"ожидалось 4 фазы, получено {before.FazesCount}");
                return;
            }

            long runId = SaveRun();

            if (runId <= 0)
            {
                return;
            }

            string load = ToolsCall("optimizer_load_report_db", new { run_id = runId });

            if (IsError(load))
            {
                RecordFail("FourFazes", "не удалось загрузить из БД: " + GetErrorText(load));
                DeleteRun(runId);
                return;
            }

            ReportInfo after = GetReportInfo(load);

            bool deleted = DeleteRun(runId);

            if (after.FazesCount == 4 && after.FirstBotName == before.FirstBotName && deleted)
            {
                RecordPass("FourFazes", $"4 фазы: fazes={after.FazesCount}, бот='{after.FirstBotName}' восстановлен");
            }
            else
            {
                RecordFail("FourFazes", $"fazes={after.FazesCount} (ожидалось 4), бот до='{before.FirstBotName}' после='{after.FirstBotName}', удалён={deleted}");
            }
        }

        private void SingleFaze()
        {
            SetPhases(1, true);

            if (!SetTwoParamEnumeration())
            {
                RecordFail("SingleFaze", "не удалось задать перебор");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("SingleFaze", "прогон не завершился за отведённое время");
                return;
            }

            ReportInfo before = GetReportInfo(ToolsCall("optimizer_get_report", new { }));

            if (before.FazesCount != 1 || !before.HasInSample || before.HasOutOfSample)
            {
                RecordFail("SingleFaze", $"ожидалась 1 фаза InSample, получено fazes={before.FazesCount}, InSample={before.HasInSample}, OutOfSample={before.HasOutOfSample}");
                return;
            }

            long runId = SaveRun();

            if (runId <= 0)
            {
                return;
            }

            string load = ToolsCall("optimizer_load_report_db", new { run_id = runId });

            if (IsError(load))
            {
                RecordFail("SingleFaze", "не удалось загрузить из БД: " + GetErrorText(load));
                DeleteRun(runId);
                return;
            }

            ReportInfo after = GetReportInfo(load);

            bool deleted = DeleteRun(runId);

            if (after.FazesCount == 1 && after.HasInSample && !after.HasOutOfSample && deleted)
            {
                RecordPass("SingleFaze", $"1 фаза InSample: fazes={after.FazesCount}, восстановлена без OutOfSample");
            }
            else
            {
                RecordFail("SingleFaze", $"fazes={after.FazesCount}, InSample={after.HasInSample}, OutOfSample={after.HasOutOfSample}, удалён={deleted}");
            }
        }

        private void VerifyQuery(long runId)
        {
            string query = ToolsCall("optimizer_query_report_db", new { run_id = runId, sort_by = "total_profit", order = "desc", limit = 5 });

            if (IsError(query))
            {
                RecordFail("QuerySort", "не удалось запросить БД: " + GetErrorText(query));
                return;
            }

            if (!TryParseJson(query, out JsonElement root)
                || !root.TryGetProperty("reports", out JsonElement reports)
                || reports.ValueKind != JsonValueKind.Array)
            {
                RecordFail("QuerySort", "не разобрать ответ");
                return;
            }

            int returned = root.TryGetProperty("returned", out JsonElement returnedElement) && returnedElement.ValueKind == JsonValueKind.Number
                ? returnedElement.GetInt32()
                : 0;
            int reportsCount = root.TryGetProperty("reports_count", out JsonElement reportsCountElement) && reportsCountElement.ValueKind == JsonValueKind.Number
                ? reportsCountElement.GetInt32()
                : 0;
            int fazesCount = root.TryGetProperty("fazes_count", out JsonElement fazesCountElement) && fazesCountElement.ValueKind == JsonValueKind.Number
                ? fazesCountElement.GetInt32()
                : 0;

            bool sorted = true;
            double previous = double.MaxValue;

            foreach (JsonElement report in reports.EnumerateArray())
            {
                double profit = report.TryGetProperty("total_profit", out JsonElement profitElement) && profitElement.ValueKind == JsonValueKind.Number
                    ? profitElement.GetDouble()
                    : 0;

                if (profit > previous)
                {
                    sorted = false;
                }

                previous = profit;
            }

            string queryIn = QuietCall("optimizer_query_report_db", new { run_id = runId, faze_type = "InSample" });

            bool allInSample = true;

            if (TryParseJson(queryIn, out JsonElement inRoot)
                && inRoot.TryGetProperty("reports", out JsonElement inReports)
                && inReports.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement report in inReports.EnumerateArray())
                {
                    string fazeType = report.TryGetProperty("faze_type", out JsonElement fazeTypeElement)
                        ? fazeTypeElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (fazeType != "InSample")
                    {
                        allInSample = false;
                    }
                }
            }
            else
            {
                allInSample = false;
            }

            if (sorted && returned > 0 && returned <= 5 && allInSample && reportsCount >= returned && fazesCount == 2)
            {
                RecordPass("QuerySort", $"сортировка desc + limit=5 + фильтр InSample: returned={returned}/{reportsCount}, fazes={fazesCount}");
            }
            else
            {
                RecordFail("QuerySort", $"returned={returned}, reports={reportsCount}, fazes={fazesCount}, отсортировано={sorted}, InSample={allInSample}");
            }
        }

        private void ListFiltersAndCleanup()
        {
            long runId = SaveRun();

            if (runId <= 0)
            {
                return;
            }

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string tomorrow = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            List<long> byRobot = ListRunIds("SmaTrendSample", null, null);
            List<long> byBadRobot = ListRunIds("NoSuchRobot", null, null);
            List<long> byToday = ListRunIds(null, today, null);
            List<long> byTomorrow = ListRunIds(null, tomorrow, null);

            bool robotOk = byRobot.Contains(runId) && byBadRobot.Count == 0;
            bool dateOk = byToday.Contains(runId) && !byTomorrow.Contains(runId);

            string cleanup = ToolsCall("optimizer_db_cleanup", new { before_date = tomorrow });

            int cleaned = 0;

            if (TryParseJson(cleanup, out JsonElement cleanupRoot)
                && cleanupRoot.TryGetProperty("cleaned", out JsonElement cleanedElement)
                && cleanedElement.ValueKind == JsonValueKind.Number)
            {
                cleaned = cleanedElement.GetInt32();
            }

            bool gone = !Listed(runId);

            if (robotOk && dateOk && cleaned > 0 && gone)
            {
                RecordPass("ListFiltersCleanup", $"фильтры robot/date работают, cleanup удалил {cleaned} прогонов");
            }
            else
            {
                RecordFail("ListFiltersCleanup", $"robot={robotOk}, date={dateOk}, cleaned={cleaned}, gone={gone}");
            }
        }

        private List<long> ListRunIds(string robot, string dateFrom, string dateTo)
        {
            List<long> ids = new List<long>();

            string list = QuietCall("optimizer_list_report_db", new { robot = robot, date_from = dateFrom, date_to = dateTo });

            if (TryParseJson(list, out JsonElement root)
                && root.TryGetProperty("runs", out JsonElement runs)
                && runs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement run in runs.EnumerateArray())
                {
                    if (run.TryGetProperty("run_id", out JsonElement runIdElement)
                        && runIdElement.ValueKind == JsonValueKind.Number)
                    {
                        ids.Add(runIdElement.GetInt64());
                    }
                }
            }

            return ids;
        }

        private long SaveRun()
        {
            string save = ToolsCall("optimizer_save_report_db", new { });

            if (IsError(save))
            {
                RecordFail("SaveDb", "не удалось сохранить в БД: " + GetErrorText(save));
                return 0;
            }

            if (TryParseJson(save, out JsonElement saveRoot))
            {
                return saveRoot.TryGetProperty("run_id", out JsonElement runIdElement) && runIdElement.ValueKind == JsonValueKind.Number
                    ? runIdElement.GetInt64()
                    : 0;
            }

            return 0;
        }

        private bool DeleteRun(long runId)
        {
            string del = ToolsCall("optimizer_delete_report_db", new { run_id = runId });

            if (IsError(del))
            {
                RecordFail("DeleteDb", "не удалось удалить прогон: " + GetErrorText(del));
                return false;
            }

            return !Listed(runId);
        }

        private bool Listed(long runId)
        {
            string list = QuietCall("optimizer_list_report_db", new { });

            if (!TryParseJson(list, out JsonElement root)
                || !root.TryGetProperty("runs", out JsonElement runs)
                || runs.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement run in runs.EnumerateArray())
            {
                if (run.TryGetProperty("run_id", out JsonElement runIdElement)
                    && runIdElement.ValueKind == JsonValueKind.Number
                    && runIdElement.GetInt64() == runId)
                {
                    return true;
                }
            }

            return false;
        }

        private ReportInfo GetReportInfo(string json)
        {
            ReportInfo info = new ReportInfo();

            if (!TryParseJson(json, out JsonElement root))
            {
                return info;
            }

            if (root.TryGetProperty("fazes_count", out JsonElement fazesCountElement)
                && fazesCountElement.ValueKind == JsonValueKind.Number)
            {
                info.FazesCount = fazesCountElement.GetInt32();
            }

            if (!root.TryGetProperty("fazes", out JsonElement fazes) || fazes.ValueKind != JsonValueKind.Array)
            {
                return info;
            }

            foreach (JsonElement faze in fazes.EnumerateArray())
            {
                string type = faze.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == "InSample")
                {
                    info.HasInSample = true;
                }

                if (type == "OutOfSample")
                {
                    info.HasOutOfSample = true;
                }

                if (info.FirstBotName.Length > 0)
                {
                    continue;
                }

                if (!faze.TryGetProperty("reports", out JsonElement reports)
                    || reports.ValueKind != JsonValueKind.Array
                    || reports.GetArrayLength() == 0)
                {
                    continue;
                }

                JsonElement first = reports[0];
                info.FirstBotName = first.TryGetProperty("bot_name", out JsonElement botNameElement)
                    ? botNameElement.GetString() ?? string.Empty
                    : string.Empty;
                info.FirstProfit = first.TryGetProperty("total_profit", out JsonElement profitElement) && profitElement.ValueKind == JsonValueKind.Number
                    ? profitElement.GetDecimal()
                    : 0;

                if (first.TryGetProperty("parameters", out JsonElement parameters) && parameters.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement parameter in parameters.EnumerateArray())
                    {
                        string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty;

                        if (name == "Sma length")
                        {
                            info.HasSma = true;
                        }

                        if (name == "Envelop length")
                        {
                            info.HasEnvelop = true;
                        }
                    }
                }
            }

            return info;
        }

        private class ReportInfo
        {
            public int FazesCount;
            public bool HasInSample;
            public bool HasOutOfSample;
            public string FirstBotName = string.Empty;
            public decimal FirstProfit;
            public bool HasSma;
            public bool HasEnvelop;
        }
    }
}
