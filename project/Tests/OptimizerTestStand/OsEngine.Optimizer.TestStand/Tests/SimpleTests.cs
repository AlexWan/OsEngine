/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 3 — BotTabSimple: штатный робот на простой вкладке, перебор параметра, прогон.
    /// </summary>
    public class SimpleTests : OsOptimizerTestsBase
    {
        public SimpleTests(TestContext context) : base(context, "Simple")
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
            if (!SelectDataSet())
            {
                RecordFail("DataSet", "не удалось выбрать сет");
                return false;
            }

            if (!WaitForStorageLoad(60))
            {
                RecordFail("Storage", "хранилище не загрузилось");
                return false;
            }

            if (!CreateBot("SmaTrendSample"))
            {
                RecordFail("CreateBot", "робот SmaTrendSample не загрузился");
                return false;
            }

            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName))
            {
                RecordFail("Tab", "нет вкладки Simple");
                return false;
            }

            if (!ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("ConfigureTab", "вкладка Simple не настроилась");
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
            if (!StartAndWait(600))
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
