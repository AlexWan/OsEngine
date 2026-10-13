/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 2 — Очистка: сбросить состояние оптимизатора (фильтры и фазы) со старых прогонов.
    /// </summary>
    public class CleanupTests : OsOptimizerTestsBase
    {
        public CleanupTests(TestContext context) : base(context, "Cleanup")
        {
        }

        public void RunAll()
        {
            if (!WaitForOptimizerReady(60))
            {
                RecordFail("OptimizerReady", "оптимизатор-сервер не поднялся за отведённое время");
                return;
            }

            ResetFilters();
            ResetPhases();
        }

        private void ResetFilters()
        {
            ToolsCall("optimizer_filters_set", new
            {
                filter_profit_is_on = false,
                filter_max_draw_down_is_on = false,
                filter_middle_profit_is_on = false,
                filter_profit_factor_is_on = false,
                filter_deals_count_is_on = false
            });

            string response = ToolsCall("optimizer_filters_get", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("ResetFilters", "нет ответа");
                return;
            }

            bool allOff = true;

            allOff &= !IsFilterOn(root, "filter_profit_is_on");
            allOff &= !IsFilterOn(root, "filter_max_draw_down_is_on");
            allOff &= !IsFilterOn(root, "filter_middle_profit_is_on");
            allOff &= !IsFilterOn(root, "filter_profit_factor_is_on");
            allOff &= !IsFilterOn(root, "filter_deals_count_is_on");

            if (allOff)
            {
                RecordPass("ResetFilters", "все фильтры выключены");
            }
            else
            {
                RecordFail("ResetFilters", "не все фильтры выключились");
            }
        }

        private void ResetPhases()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });

            string response = ToolsCall("optimizer_phases_get", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("ResetPhases", "нет ответа");
                return;
            }

            if (root.TryGetProperty("fazes_count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number
                && countElement.GetInt32() >= 2)
            {
                RecordPass("ResetPhases", $"фаз: {countElement.GetInt32()}");
            }
            else
            {
                RecordFail("ResetPhases", "фазы не построились (ожидалось InSample+OutOfSample)");
            }
        }

        private static bool IsFilterOn(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.True;
        }
    }
}
