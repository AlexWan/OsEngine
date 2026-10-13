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
    /// Модуль 6 — Этапы (фазы walk-forward) и фильтры отсева.
    /// </summary>
    public class PhasesTests : OsOptimizerTestsBase
    {
        public PhasesTests(TestContext context) : base(context, "Phases")
        {
        }

        public void RunAll()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return;
            }

            if (!CreateBot("SmaTrendSample"))
            {
                RecordFail("CreateBot", "робот не загрузился");
                return;
            }

            PhasesConfig();
            IterationCount();
            PercentOnFiltration();
            LastInSample();
            FiltersToggle();
            FilterAffects();
            MultiPeriodRun();
        }

        private void PhasesConfig()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            string timeStart = GetPhasesString("time_start");
            string timeEnd = GetPhasesString("time_end");
            int iteration = GetPhasesInt("iteration_count", -1);
            int percent = GetPhasesInt("percent_on_filtration", -1);
            bool lastInSample = GetPhasesBool("last_in_sample");
            int fazesCount = GetPhasesInt("fazes_count", -1);

            bool ok = !string.IsNullOrEmpty(timeStart)
                && !string.IsNullOrEmpty(timeEnd)
                && iteration == 1
                && percent == 30
                && !lastInSample
                && fazesCount == 2;

            if (ok)
            {
                RecordPass("PhasesConfig", $"период {timeStart.Substring(0, 10)}..{timeEnd.Substring(0, 10)}, итераций={iteration}, OOS={percent}%, фаз={fazesCount}");
            }
            else
            {
                RecordFail("PhasesConfig", $"period=({timeStart},{timeEnd}), iteration={iteration}, percent={percent}, last_in_sample={lastInSample}, fazes={fazesCount}");
            }
        }

        private void IterationCount()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 2,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            int fazesCount = GetPhasesInt("fazes_count", -1);

            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            if (fazesCount == 4)
            {
                RecordPass("IterationCount", $"2 итерации → {fazesCount} фаз");
            }
            else
            {
                RecordFail("IterationCount", $"ожидалось 4 фазы, получено {fazesCount}");
            }
        }

        private void PercentOnFiltration()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            int days30 = GetOutOfSampleDays();

            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 50,
                last_in_sample = false
            });

            int days50 = GetOutOfSampleDays();

            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            if (days50 > days30)
            {
                RecordPass("PercentOnFiltration", $"OOS: {days30} дн (30%) -> {days50} дн (50%)");
            }
            else
            {
                RecordFail("PercentOnFiltration", $"OOS 30%={days30}, 50%={days50}");
            }
        }

        private void LastInSample()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = true
            });

            bool lastInSample = GetPhasesBool("last_in_sample");
            int fazesCount = GetPhasesInt("fazes_count", -1);

            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            if (lastInSample && fazesCount == 1)
            {
                RecordPass("LastInSample", $"last_in_sample=true, фаз: {fazesCount}");
            }
            else
            {
                RecordFail("LastInSample", $"last_in_sample={lastInSample}, фаз={fazesCount}");
            }
        }

        private void FiltersToggle()
        {
            string[] filters = new[]
            {
                "filter_profit_is_on",
                "filter_max_draw_down_is_on",
                "filter_middle_profit_is_on",
                "filter_profit_factor_is_on",
                "filter_deals_count_is_on"
            };

            List<string> failed = new List<string>();

            foreach (string filter in filters)
            {
                ToolsCall("optimizer_filters_set", new Dictionary<string, object> { [filter] = true });

                string get = QuietCall("optimizer_filters_get", new { });

                bool isOn = false;

                if (TryParseJson(get, out JsonElement root))
                {
                    isOn = root.TryGetProperty(filter, out JsonElement element) && element.ValueKind == JsonValueKind.True;
                }

                ToolsCall("optimizer_filters_set", new Dictionary<string, object> { [filter] = false });

                if (!isOn)
                {
                    failed.Add(filter);
                }
            }

            if (failed.Count == 0)
            {
                RecordPass("FiltersToggle", $"все {filters.Length} фильтров включаются/выключаются");
            }
            else
            {
                RecordFail("FiltersToggle", $"не включились: {string.Join(", ", failed)}");
            }
        }

        private void FilterAffects()
        {
            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("FilterAffects", "вкладка Simple не настроилась");
                return;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("FilterAffects", "не удалось задать перебор");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("FilterAffects", "базовый прогон не завершился");
                return;
            }

            ReportSummary baseline = GetReportSummary();

            ToolsCall("optimizer_filters_set", new { filter_deals_count_is_on = true, filter_deals_count_value = 1000000 });

            if (!StartAndWait(600))
            {
                RecordFail("FilterAffects", "прогон с фильтром не завершился");
                return;
            }

            ReportSummary filtered = GetReportSummary();

            ToolsCall("optimizer_filters_set", new { filter_deals_count_is_on = false });

            if (filtered.ReportsCount < baseline.ReportsCount)
            {
                RecordPass("FilterAffects", $"фильтр сократил отчёты: {baseline.ReportsCount} -> {filtered.ReportsCount}");
            }
            else
            {
                RecordFail("FilterAffects", $"фильтр не подействовал: {baseline.ReportsCount} -> {filtered.ReportsCount}");
            }
        }

        private void MultiPeriodRun()
        {
            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("MultiPeriodRun", "вкладка Simple не настроилась");
                return;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("MultiPeriodRun", "не удалось задать перебор");
                return;
            }

            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 2,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            if (!StartAndWait(600))
            {
                RecordFail("MultiPeriodRun", "прогон не завершился");
                RestoreSingleIteration();
                return;
            }

            string response = QuietCall("optimizer_get_report", new { });

            bool ok = false;
            string phases = string.Empty;

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("fazes", out JsonElement fazes)
                && fazes.ValueKind == JsonValueKind.Array
                && fazes.GetArrayLength() == 4)
            {
                ok = true;
                List<string> types = new List<string>();

                for (int i = 0; i < fazes.GetArrayLength(); i++)
                {
                    JsonElement faze = fazes[i];

                    string type = faze.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString() ?? string.Empty
                        : string.Empty;

                    int reportsCount = faze.TryGetProperty("reports", out JsonElement reportsElement) && reportsElement.ValueKind == JsonValueKind.Array
                        ? reportsElement.GetArrayLength()
                        : 0;

                    types.Add(type);

                    if (reportsCount == 0)
                    {
                        ok = false;
                    }
                }

                phases = string.Join(",", types);

                if (types.Count != 4
                    || types[0] != "InSample"
                    || types[1] != "OutOfSample"
                    || types[2] != "InSample"
                    || types[3] != "OutOfSample")
                {
                    ok = false;
                }
            }

            RestoreSingleIteration();

            if (ok)
            {
                RecordPass("MultiPeriodRun", $"4 фазы ({phases}), в каждой есть отчёты");
            }
            else
            {
                RecordFail("MultiPeriodRun", $"фазы: {phases}");
            }
        }

        private void RestoreSingleIteration()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });
        }
    }
}
