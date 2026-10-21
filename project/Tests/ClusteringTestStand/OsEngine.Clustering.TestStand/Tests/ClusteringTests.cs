/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Модуль 2. Кластеризация по корреляции: настройки, расчёт, результат.
    /// </summary>
    public class ClusteringTests : ClusteringTestsBase
    {
        private readonly int _expectedBots;

        public ClusteringTests(TestContext context) : base(context, "Clustering")
        {
            _expectedBots = GetAllBotFiles().Length;
        }

        public void RunAll()
        {
            if (!EnsureSetup())
            {
                RecordFail("setup", "режим не открыт или боты не загружены");
                return;
            }

            TestSettingsGet();
            TestSettingsSet();
            TestCalculate();
            TestResultGet();
            ShowClusters();
        }

        private void ShowClusters()
        {
            string response = QuietCall("allocation_clustering_result_get", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("clusters", out JsonElement clusters)
                || clusters.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            Console.WriteLine("  --- кластеры (min_correlation=0.3) ---");

            foreach (JsonElement cluster in clusters.EnumerateArray())
            {
                int clusterNumber = cluster.TryGetProperty("cluster_number", out JsonElement num) && num.ValueKind == JsonValueKind.Number
                    ? num.GetInt32()
                    : 0;

                List<string> names = new List<string>();

                if (cluster.TryGetProperty("bots", out JsonElement bots) && bots.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement bot in bots.EnumerateArray())
                    {
                        string name = bot.TryGetProperty("bot_name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? "?"
                            : "?";

                        names.Add(name);
                    }
                }

                Console.WriteLine($"  Cluster {clusterNumber}: {string.Join(", ", names)}");
            }

            Console.WriteLine($"  Всего кластеров: {clusters.GetArrayLength()}");
            Console.WriteLine();
        }

        private void TestSettingsGet()
        {
            const string method = "allocation_clustering_settings_get";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("min_correlation", out JsonElement minCorrelation)
                || minCorrelation.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет min_correlation в ответе");
                return;
            }

            RecordPass(method, $"min_correlation по умолчанию = {minCorrelation.GetDouble()}");
        }

        private void TestSettingsSet()
        {
            const string method = "allocation_clustering_settings_set";

            string response = ToolsCall(method, new { min_correlation = 0.3 });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("min_correlation", out JsonElement minCorrelation)
                || minCorrelation.ValueKind != JsonValueKind.Number)
            {
                RecordFail(method, "нет min_correlation в ответе");
                return;
            }

            if (minCorrelation.GetDouble() < 0.29 || minCorrelation.GetDouble() > 0.31)
            {
                RecordFail(method, $"min_correlation={minCorrelation.GetDouble()}, ожидалось ~0.3");
                return;
            }

            RecordPass(method, "min_correlation=0.3 выставлен");
        }

        private void TestCalculate()
        {
            const string method = "allocation_clustering_calculate";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("calculated", out JsonElement calculated)
                || calculated.ValueKind != JsonValueKind.True)
            {
                RecordFail(method, "calculated не true");
                return;
            }

            int bots = root.TryGetProperty("bots_count", out JsonElement botsCount) && botsCount.ValueKind == JsonValueKind.Number
                ? botsCount.GetInt32()
                : 0;

            RecordPass(method, $"рассчитано, ботов={bots}");
        }

        private void TestResultGet()
        {
            const string method = "allocation_clustering_result_get";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail(method, "нет результата");
                return;
            }

            if (!root.TryGetProperty("clusters", out JsonElement clusters) || clusters.ValueKind != JsonValueKind.Array)
            {
                RecordFail(method, "clusters не массив");
                return;
            }

            if (!root.TryGetProperty("weights", out JsonElement weights) || weights.ValueKind != JsonValueKind.Array)
            {
                RecordFail(method, "weights не массив");
                return;
            }

            if (weights.GetArrayLength() != _expectedBots)
            {
                RecordFail(method, $"weights={weights.GetArrayLength()}, ожидалось {_expectedBots}");
                return;
            }

            RecordPass(method, $"кластеров={clusters.GetArrayLength()}, весов={weights.GetArrayLength()}");
        }
    }
}
