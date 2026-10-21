/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.IO;
using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Модуль 4. Аллокация: сохранение результата в файл и проверка файла.
    /// </summary>
    public class AllocationTests : ClusteringTestsBase
    {
        private readonly string _savePath;

        public AllocationTests(TestContext context) : base(context, "Allocation")
        {
            _savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allocation_result_test.txt");
        }

        public void RunAll()
        {
            if (!EnsureSetup())
            {
                RecordFail("setup", "режим не открыт или боты не загружены");
                return;
            }

            EnsureCalculated();
            TestResultSave();
            TestFileExists();
        }

        /// <summary>
        /// Если ещё нет результата (стенд запущен только с -m Allocation), считаем кластеризацию.
        /// </summary>
        private void EnsureCalculated()
        {
            QuietCall("allocation_clustering_calculate", new { });
        }

        private void TestResultSave()
        {
            const string method = "allocation_result_save";

            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }

            string response = ToolsCall(method, new { file_path = _savePath });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("saved", out JsonElement saved)
                || saved.ValueKind != JsonValueKind.True)
            {
                RecordFail(method, "saved не true");
                return;
            }

            int clusters = root.TryGetProperty("clusters_count", out JsonElement clustersCount) && clustersCount.ValueKind == JsonValueKind.Number
                ? clustersCount.GetInt32()
                : 0;

            RecordPass(method, $"сохранено, кластеров={clusters}");
        }

        private void TestFileExists()
        {
            const string method = "allocation_result_save.file";

            if (!File.Exists(_savePath))
            {
                RecordFail(method, "файл аллокации не создан: " + _savePath);
                return;
            }

            long length = new FileInfo(_savePath).Length;

            if (length == 0)
            {
                RecordFail(method, "файл аллокации пустой");
                return;
            }

            RecordPass(method, $"файл на месте, {length} байт");
        }
    }
}
