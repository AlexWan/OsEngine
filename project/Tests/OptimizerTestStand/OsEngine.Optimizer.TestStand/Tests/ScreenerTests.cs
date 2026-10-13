/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 4 — BotTabScreener: робот-скринер, бумаги массивом, перебор параметра, прогон.
    /// </summary>
    public class ScreenerTests : OsOptimizerTestsBase
    {
        public ScreenerTests(TestContext context) : base(context, "Screener")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            RunBaseline();
        }

        private bool Setup()
        {
            if (!SelectDataSetByName("Tester10Liq"))
            {
                RecordFail("DataSet", "не удалось выбрать сет Tester10Liq");
                return false;
            }

            if (!WaitForStorageLoad(60))
            {
                RecordFail("Storage", "хранилище не загрузилось");
                return false;
            }

            if (!CreateBot("SmaScreener"))
            {
                RecordFail("CreateBot", "робот SmaScreener не загрузился");
                return false;
            }

            string tabName = GetTabName("Screener");

            if (string.IsNullOrEmpty(tabName))
            {
                RecordFail("Tab", "нет вкладки Screener");
                return false;
            }

            if (!ConfigureScreenerTab(tabName, new[] { "SBER.txt", "GAZP.txt", "VTBR.txt" }, "Min30"))
            {
                RecordFail("ConfigureTab", "вкладка Screener не настроилась");
                return false;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName))
            {
                RecordFail("Param", "не найден числовой параметр для перебора");
                return false;
            }

            if (!SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("Enumeration", "не удалось задать перебор");
                return false;
            }

            return true;
        }

        private void RunBaseline()
        {
            if (!StartAndWait(900))
            {
                RecordFail("Run", "прогон не завершился за отведённое время");
                return;
            }

            ReportSummary summary = GetReportSummary();

            if (summary.ReportsCount > 0 && summary.FazesCount > 0)
            {
                RecordPass("Run", $"фаз={summary.FazesCount}, отчётов={summary.ReportsCount}, прибыль={summary.FirstProfit}");
            }
            else
            {
                RecordFail("Run", $"отчётов={summary.ReportsCount}, фаз={summary.FazesCount}");
            }
        }
    }
}
