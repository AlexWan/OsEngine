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
    /// Модуль 8 — Отчёты: все 8 разделов присутствуют, robot_results и positions валидны.
    /// </summary>
    public class ReportTests : OsTesterTestsBase
    {
        public ReportTests(TestContext context) : base(context, "Report")
        {
        }

        public void RunAll()
        {
            WaitForTesterReady(60);
            ResetTesterDefaults();
            CheckReportComplete();
        }

        private void CheckReportComplete()
        {
            string response = ToolsCall("tester_get_report", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("ReportComplete", "нет JSON");
                return;
            }

            string[] required =
            {
                "date", "robots", "data_set", "tester_settings",
                "statistics_full", "robot_results", "positions", "cash_flows"
            };

            List<string> missing = new List<string>();

            foreach (string name in required)
            {
                if (!root.TryGetProperty(name, out _))
                {
                    missing.Add(name);
                }
            }

            if (missing.Count > 0)
            {
                RecordFail("ReportComplete", "не хватает: " + string.Join(",", missing));
                return;
            }

            bool robotResultsOk = root.TryGetProperty("robot_results", out JsonElement robotResults)
                && robotResults.ValueKind == JsonValueKind.Array;

            bool positionsOk = root.TryGetProperty("positions", out JsonElement positions)
                && positions.ValueKind == JsonValueKind.Array;

            if (robotResultsOk && positionsOk)
            {
                RecordPass("ReportComplete", "8 разделов, robot_results и positions валидны");
            }
            else
            {
                RecordFail("ReportComplete", $"robot_results={robotResultsOk}, positions={positionsOk}");
            }
        }
    }
}
