/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Модуль 3. Иерархический риск-паритет (HRP): настройки, расчёт, результат.
    /// </summary>
    public class HrpTests : ClusteringTestsBase
    {
        private readonly int _expectedBots;

        public HrpTests(TestContext context) : base(context, "Hrp")
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
        }

        private void TestSettingsGet()
        {
            const string method = "allocation_hrp_settings_get";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("linkage", out JsonElement linkage)
                || !root.TryGetProperty("covariance", out JsonElement covariance)
                || !root.TryGetProperty("correlation", out JsonElement correlation))
            {
                RecordFail(method, "нет linkage/covariance/correlation в ответе");
                return;
            }

            RecordPass(method, $"linkage={linkage.GetString()}, covariance={covariance.GetString()}, correlation={correlation.GetString()}");
        }

        private void TestSettingsSet()
        {
            const string method = "allocation_hrp_settings_set";

            string response = ToolsCall(method, new { linkage = "complete", covariance = "sample", correlation = "pearson" });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("linkage", out JsonElement linkage)
                || linkage.GetString() != "complete")
            {
                RecordFail(method, "linkage не выставился в complete");
                return;
            }

            RecordPass(method, "настройки HRP выставлены");
        }

        private void TestCalculate()
        {
            const string method = "allocation_hrp_calculate";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("calculated", out JsonElement calculated)
                || calculated.ValueKind != JsonValueKind.True)
            {
                RecordFail(method, "calculated не true");
                return;
            }

            RecordPass(method, "HRP рассчитан");
        }

        private void TestResultGet()
        {
            const string method = "allocation_hrp_result_get";

            string response = ToolsCall(method, new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                RecordFail(method, "нет результата");
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

            if (!root.TryGetProperty("allocation_merges", out JsonElement merges) || merges.ValueKind != JsonValueKind.Array)
            {
                RecordFail(method, "allocation_merges не массив");
                return;
            }

            if (merges.GetArrayLength() != _expectedBots - 1)
            {
                RecordFail(method, $"allocation_merges={merges.GetArrayLength()}, ожидалось {_expectedBots - 1}");
                return;
            }

            if (!root.TryGetProperty("allocation_leaf_order", out JsonElement leafOrder) || leafOrder.ValueKind != JsonValueKind.Array)
            {
                RecordFail(method, "allocation_leaf_order не массив");
                return;
            }

            if (leafOrder.GetArrayLength() != _expectedBots)
            {
                RecordFail(method, $"allocation_leaf_order={leafOrder.GetArrayLength()}, ожидалось {_expectedBots}");
                return;
            }

            RecordPass(method, $"весов={weights.GetArrayLength()}, merges={merges.GetArrayLength()}, leaf_order={leafOrder.GetArrayLength()}");
        }
    }
}
