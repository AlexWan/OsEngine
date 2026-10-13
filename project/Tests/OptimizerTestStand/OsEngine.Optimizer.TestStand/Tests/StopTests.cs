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
    /// Модуль 10 — Досрочная остановка: stop прерывает прогон, отчёт приходит с is_partial=true и частичными результатами.
    /// </summary>
    public class StopTests : OsOptimizerTestsBase
    {
        public StopTests(TestContext context) : base(context, "Stop")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            StartAndStop();
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

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 10, 200, 1))
            {
                RecordFail("Enumeration", "не удалось задать широкий перебор");
                return false;
            }

            return true;
        }

        private void StartAndStop()
        {
            int fullPassCount = GetPassCount();

            string startResponse = ToolsCall("optimizer_start", new { });

            if (!TryParseJson(startResponse, out JsonElement startRoot)
                || !startRoot.TryGetProperty("started", out JsonElement startedElement)
                || startedElement.ValueKind != JsonValueKind.True)
            {
                RecordFail("StartAndStop", "optimizer_start не запустился");
                return;
            }

            if (!WaitForRunning(30))
            {
                RecordFail("StartAndStop", "прогон не стартовал (is_running=false)");
                return;
            }

            System.Threading.Thread.Sleep(500);

            string stopResponse = ToolsCall("optimizer_stop", new { });

            bool stopped = TryParseJson(stopResponse, out JsonElement stopRoot)
                && stopRoot.TryGetProperty("stopped", out JsonElement stoppedElement)
                && stoppedElement.ValueKind == JsonValueKind.True;

            if (!stopped)
            {
                RecordFail("StartAndStop", "optimizer_stop не остановил прогон");
                return;
            }

            WaitForFinish(60);

            ReportSummary summary = GetReportSummary();

            bool partialFlag = summary.IsPartial;
            bool interrupted = summary.ReportsCount > 0 && fullPassCount > 0 && summary.ReportsCount < fullPassCount;

            if (partialFlag && interrupted)
            {
                RecordPass("StartAndStop", $"прервано: is_partial=true, {summary.ReportsCount} отчётов из {fullPassCount} проходов");
            }
            else
            {
                RecordFail("StartAndStop", $"is_partial={partialFlag}, отчётов={summary.ReportsCount}, полных проходов={fullPassCount}");
            }
        }

        private bool WaitForRunning(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("optimizer_get_status", new { });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("is_running", out JsonElement runningElement)
                    && runningElement.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        private void WaitForFinish(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("optimizer_get_status", new { });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("is_running", out JsonElement runningElement)
                    && runningElement.ValueKind == JsonValueKind.False)
                {
                    return;
                }

                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
