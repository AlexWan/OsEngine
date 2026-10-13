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
    /// Модуль 1 — Данные: наличие сетов, выбор сета, загрузка хранилища.
    /// </summary>
    public class DataTests : OsOptimizerTestsBase
    {
        public DataTests(TestContext context) : base(context, "Data")
        {
        }

        public void RunAll()
        {
            new DataPreparation(_context).PrepareAllSets();

            if (!WaitForOptimizerReady(60))
            {
                RecordFail("OptimizerReady", "оптимизатор-сервер не поднялся за отведённое время");
                return;
            }

            DataSetsAvailable();
            DataSetSelect();
            StorageLoad();
        }

        private void DataSetsAvailable()
        {
            string response = ToolsCall("optimizer_data_get_status", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("available_sets", out JsonElement sets)
                || sets.ValueKind != JsonValueKind.Array)
            {
                RecordFail("DataSetsAvailable", "не разобрать available_sets");
                return;
            }

            bool hasSingle = false;
            bool hasTen = false;

            foreach (JsonElement item in sets.EnumerateArray())
            {
                string name = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : string.Empty;

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
            string response = ToolsCall("optimizer_data_set_config", new
            {
                source_type = "Set",
                set_name = _context.DataSetName,
                date_from = TestDateFrom,
                date_to = TestDateTo
            });

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

        private void StorageLoad()
        {
            if (!WaitForStorageLoad(60))
            {
                RecordFail("StorageLoad", "хранилище не загрузилось за отведённое время");
                return;
            }

            string response = ToolsCall("optimizer_data_get_status", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail("StorageLoad", "нет ответа");
                return;
            }

            int count = root.TryGetProperty("securities_count", out JsonElement countElement) && countElement.ValueKind == JsonValueKind.Number
                ? countElement.GetInt32()
                : 0;

            if (count > 0)
            {
                RecordPass("StorageLoad", $"загружено бумаг: {count}");
            }
            else
            {
                RecordFail("StorageLoad", "нет бумаг в хранилище");
            }
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
