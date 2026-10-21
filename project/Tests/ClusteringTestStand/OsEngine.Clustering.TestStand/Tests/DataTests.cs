/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Модуль 1. Данные: открытие режима, статус, загрузка ботов, общие настройки.
    /// </summary>
    public class DataTests : ClusteringTestsBase
    {
        private readonly int _expectedBots;

        public DataTests(TestContext context) : base(context, "Data")
        {
            _expectedBots = GetAllBotFiles().Length;
        }

        public void RunAll()
        {
            if (!OpenAllocationMode())
            {
                return;
            }

            TestStatus();
            TestBotsSet();
            TestBotsGet();
            TestBotsClear();
            TestBotsSetAll();
            TestSettings();
        }

        private void TestStatus()
        {
            const string method = "allocation_get_status";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("is_open", out JsonElement isOpen)
                || isOpen.ValueKind != JsonValueKind.True)
            {
                RecordFail(method, "is_open не true");
                return;
            }

            if (!root.TryGetProperty("bots_count", out JsonElement botsCount) || botsCount.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет bots_count");
                return;
            }

            RecordPass(method, $"режим открыт, ботов={botsCount.GetInt32()}");
        }

        private void TestBotsSet()
        {
            const string method = "allocation_bots_set";

            string[] files = GetAllBotFiles();

            if (files.Length == 0)
            {
                RecordFail(method, "нет входных файлов в папке данных");
                return;
            }

            string response = ToolsCall(method, new { files = files });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("bots_count", out JsonElement count)
                || count.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет bots_count в ответе");
                return;
            }

            if (count.GetInt32() != files.Length)
            {
                RecordFail(method, $"bots_count={count.GetInt32()}, ожидалось {files.Length}");
                return;
            }

            RecordPass(method, $"загружено ботов: {count.GetInt32()}");
        }

        private void TestBotsGet()
        {
            const string method = "allocation_bots_get";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("bots_count", out JsonElement count)
                || count.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет bots_count в ответе");
                return;
            }

            if (count.GetInt32() != _expectedBots)
            {
                RecordFail(method, $"bots_count={count.GetInt32()}, ожидалось {_expectedBots}");
                return;
            }

            RecordPass(method, $"ботов в списке: {count.GetInt32()}");
        }

        private void TestBotsClear()
        {
            const string method = "allocation_bots_clear";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("bots_count", out JsonElement count)
                || count.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет bots_count в ответе");
                return;
            }

            if (count.GetInt32() != 0)
            {
                RecordFail(method, $"bots_count={count.GetInt32()}, ожидалось 0");
                return;
            }

            RecordPass(method, "список очищен");
        }

        private void TestBotsSetAll()
        {
            const string method = "allocation_bots_set";

            string[] files = GetAllBotFiles();

            string response = ToolsCall(method, new { files = files });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("bots_count", out JsonElement count)
                || count.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет bots_count в ответе");
                return;
            }

            if (count.GetInt32() != files.Length)
            {
                RecordFail(method, $"bots_count={count.GetInt32()}, ожидалось {files.Length}");
                return;
            }

            RecordPass(method, $"боты загружены заново: {count.GetInt32()}");
        }

        private void TestSettings()
        {
            const string setMethod = "allocation_settings_set";
            const string getMethod = "allocation_settings_get";

            string set = ToolsCall(setMethod, new { period = "week" });

            if (!TryParseJson(set, out JsonElement setRoot)
                || !setRoot.TryGetProperty("period", out JsonElement setPeriod)
                || setPeriod.GetString() != "week")
            {
                RecordFail(setMethod, "не выставился period=week");
                return;
            }

            string get = ToolsCall(getMethod, new { });

            if (!TryParseJson(get, out JsonElement getRoot)
                || !getRoot.TryGetProperty("period", out JsonElement getPeriod)
                || getPeriod.GetString() != "week")
            {
                RecordFail(getMethod, "period не читается обратно (ожидалось week)");
                return;
            }

            RecordPass(setMethod, "настройки (period=week) выставляются и читаются");
        }
    }
}
