/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Text.Json;
using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Модуль 1 — Данные: наличие сетов и бумаг, выбор сета.
    /// </summary>
    public class DataTests : OsTesterTestsBase
    {
        public DataTests(TestContext context) : base(context, "Data")
        {
        }

        public void RunAll()
        {
            // Подготовка данных: если сетов нет — качаем сами через OsData.
            new DataPreparation(_context).PrepareAllSets();

            if (!WaitForTesterReady(60))
            {
                RecordFail("TesterReady", "тестер-сервер не поднялся за отведённое время");
                return;
            }

            ResetTesterDefaults();

            DataSetsAvailable();
            DataSetSelect();
            DataSecurities();
            TicksAvailable();
            DepthAvailable();
        }

        private void DataSetsAvailable()
        {
            string response = ToolsCall("tester_data_get_available_sets", new { });

            if (!TryParseJson(response, out JsonElement root) || root.ValueKind != JsonValueKind.Array)
            {
                RecordFail("DataSetsAvailable", "не разобрать список сетов");
                return;
            }

            bool hasSingle = false;
            bool hasTen = false;

            foreach (JsonElement item in root.EnumerateArray())
            {
                string name = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;

                if (SetMatches(name, _context.DataSetName))
                {
                    hasSingle = true;
                }

                if (SetMatches(name, "Tester10Liq"))
                {
                    hasTen = true;
                }
            }

            if (hasSingle && hasTen)
            {
                RecordPass("DataSetsAvailable", $"{_context.DataSetName} и Tester10Liq доступны");
            }
            else
            {
                RecordFail("DataSetsAvailable", $"не хватает сетов (single={hasSingle}, ten={hasTen})");
            }
        }

        private void DataSetSelect()
        {
            string response = ToolsCall("tester_data_set_config", new { set_name = _context.DataSetName });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("DataSetSelect", "нет ответа");
                return;
            }

            string setName = root.TryGetProperty("set_name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            if (SetMatches(setName, _context.DataSetName))
            {
                RecordPass("DataSetSelect", $"выбран сет {setName}");
            }
            else
            {
                RecordFail("DataSetSelect", $"set_name = '{setName}'");
            }
        }

        private void DataSecurities()
        {
            if (!WaitForSecurities(30))
            {
                RecordFail("DataSecurities", "бумаги не загрузились за отведённое время");
                return;
            }

            string response = ToolsCall("tester_get_securities", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("DataSecurities", "нет ответа");
                return;
            }

            int count = root.TryGetProperty("count", out JsonElement countElement) && countElement.ValueKind == JsonValueKind.Number
                ? countElement.GetInt32()
                : 0;

            if (count > 0)
            {
                RecordPass("DataSecurities", $"загружено бумаг: {count}");
            }
            else
            {
                RecordFail("DataSecurities", "нет бумаг");
            }
        }

        private void TicksAvailable()
        {
            if (SetInTester("TesterTicks"))
            {
                RecordPass("TicksAvailable", "сет тиков доступен");
            }
            else
            {
                RecordFail("TicksAvailable", "сета тиков нет");
            }
        }

        private void DepthAvailable()
        {
            if (SetInTester("TesterDepth"))
            {
                RecordPass("DepthAvailable", "сет стаканов доступен");
            }
            else
            {
                RecordFail("DepthAvailable", "сета стаканов нет");
            }
        }

        private bool SetInTester(string name)
        {
            string response = QuietCall("tester_data_get_available_sets", new { });

            if (!TryParseJson(response, out JsonElement root) || root.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement item in root.EnumerateArray())
            {
                string setName = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;

                if (SetMatches(setName, name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SetMatches(string name, string expected)
        {
            string actual = name ?? string.Empty;
            return actual == expected
                || actual == "Set_" + expected
                || actual.EndsWith("_" + expected);
        }
    }
}
