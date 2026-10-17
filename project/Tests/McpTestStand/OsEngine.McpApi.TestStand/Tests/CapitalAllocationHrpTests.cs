/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Тесты HierarchicalRiskParity HRP-алгоритма риск-паритета: настройки (linkage/covariance/correlation),
    /// расчёт и результат (веса + дендрограмма merges + leaf_order).
    /// Запускается после CapitalAllocationCorrelationClusteringTests (режим открыт, все 50 ботов загружены).
    /// В конце — проверка ошибки расчёта без ботов.
    /// </summary>
    public class CapitalAllocationHrpTests
    {
        private const string Module = "CAPITAL_ALLOCATION";

        private readonly TestContext _context;

        public CapitalAllocationHrpTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            TestHierarchicalRiskParitySettingsGet();
            TestHierarchicalRiskParitySettingsSet();
            TestHierarchicalRiskParitySettingsInvalid();
            TestHierarchicalRiskParityResultBeforeCalc();
            TestHierarchicalRiskParityCalculate();
            TestHierarchicalRiskParityResult();
            TestCorrelationClusteringCalculateEmptyBots();
            TestHierarchicalRiskParityCalculateEmptyBots();
        }

        private void TestHierarchicalRiskParitySettingsGet()
        {
            const string method = "allocation_hrp_settings_get";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("linkage", out JsonElement linkage)
                    || linkage.GetString() != "single")
                {
                    _context.RecordFail(Module, method, "linkage is not single");
                    return;
                }

                if (!config.TryGetProperty("covariance", out JsonElement covariance)
                    || covariance.GetString() != "sample")
                {
                    _context.RecordFail(Module, method, "covariance is not sample");
                    return;
                }

                if (!config.TryGetProperty("correlation", out JsonElement correlation)
                    || correlation.GetString() != "pearson")
                {
                    _context.RecordFail(Module, method, "correlation is not pearson");
                    return;
                }

                _context.RecordPass(Module, method, "single/sample/pearson");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestHierarchicalRiskParitySettingsSet()
        {
            const string method = "allocation_hrp_settings_set";

            object request = new
            {
                linkage = "ward",
                covariance = "shrinkage",
                correlation = "spearman"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("linkage", out JsonElement linkage)
                    || linkage.GetString() != "ward")
                {
                    _context.RecordFail(Module, method, "linkage is not ward");
                    return;
                }

                if (!config.TryGetProperty("covariance", out JsonElement covariance)
                    || covariance.GetString() != "shrinkage")
                {
                    _context.RecordFail(Module, method, "covariance is not shrinkage");
                    return;
                }

                if (!config.TryGetProperty("correlation", out JsonElement correlation)
                    || correlation.GetString() != "spearman")
                {
                    _context.RecordFail(Module, method, "correlation is not spearman");
                    return;
                }

                _context.RecordPass(Module, method, "ward/shrinkage/spearman");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestHierarchicalRiskParitySettingsInvalid()
        {
            const string method = "allocation_hrp_settings_set";

            object request = new
            {
                linkage = "foo"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("linkage", out JsonElement linkage)
                    || linkage.GetString() != "ward")
                {
                    _context.RecordFail(Module, method, "linkage was not ignored (still ward expected)");
                    return;
                }

                _context.RecordPass(Module, method, "invalid linkage ignored, linkage=ward");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestHierarchicalRiskParityResultBeforeCalc()
        {
            const string method = "allocation_hrp_result_get";

            ExpectToolError(method, new { }, "Run allocation_hrp_calculate first");
        }

        private void TestHierarchicalRiskParityCalculate()
        {
            const string method = "allocation_hrp_calculate";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("calculated", out JsonElement calculated)
                    || calculated.ValueKind != JsonValueKind.True)
                {
                    _context.RecordFail(Module, method, "calculated is not true");
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() != CapitalAllocationTestData.BotCount())
                {
                    _context.RecordFail(Module, method, $"bots_count is not {CapitalAllocationTestData.BotCount()}");
                    return;
                }

                if (!config.TryGetProperty("periods_count", out JsonElement periodsCount)
                    || periodsCount.GetInt32() <= 0)
                {
                    _context.RecordFail(Module, method, "periods_count is not positive");
                    return;
                }

                _context.RecordPass(Module, method, $"bots={CapitalAllocationTestData.BotCount()}, periods={periodsCount.GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestHierarchicalRiskParityResult()
        {
            const string method = "allocation_hrp_result_get";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("weights", out JsonElement weights)
                    || weights.ValueKind != JsonValueKind.Array
                    || weights.GetArrayLength() != CapitalAllocationTestData.BotCount())
                {
                    _context.RecordFail(Module, method, $"weights array length is not {CapitalAllocationTestData.BotCount()}");
                    return;
                }

                double weightSum = 0;
                double weightMin = double.MaxValue;
                double weightMax = double.MinValue;
                foreach (JsonElement weight in weights.EnumerateArray())
                {
                    if (!weight.TryGetProperty("weight_percent", out JsonElement weightPercent))
                    {
                        _context.RecordFail(Module, method, "weight_percent missing");
                        return;
                    }

                    double wp = weightPercent.GetDouble();
                    weightSum += wp;
                    weightMin = Math.Min(weightMin, wp);
                    weightMax = Math.Max(weightMax, wp);
                }

                if (Math.Abs(weightSum - 100.0) > 0.01)
                {
                    _context.RecordFail(Module, method, $"weights sum is not ~100 (got {weightSum})");
                    return;
                }

                if (Math.Abs(weightMax - weightMin) < 0.0001)
                {
                    _context.RecordFail(Module, method, "weights are all equal (shrinkage degenerated to identity)");
                    return;
                }

                if (!config.TryGetProperty("allocation_merges", out JsonElement merges)
                    || merges.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "allocation_merges is not an array");
                    return;
                }

                int expectedMerges = CapitalAllocationTestData.BotCount() - 1;

                if (merges.GetArrayLength() != expectedMerges)
                {
                    _context.RecordFail(Module, method, $"allocation_merges count is not {expectedMerges} (got {merges.GetArrayLength()})");
                    return;
                }

                foreach (JsonElement merge in merges.EnumerateArray())
                {
                    if (!merge.TryGetProperty("left", out _)
                        || !merge.TryGetProperty("right", out _)
                        || !merge.TryGetProperty("distance", out _)
                        || !merge.TryGetProperty("size", out _))
                    {
                        _context.RecordFail(Module, method, "merge node fields missing (left/right/distance/size)");
                        return;
                    }
                }

                if (!config.TryGetProperty("allocation_leaf_order", out JsonElement leafOrder)
                    || leafOrder.ValueKind != JsonValueKind.Array
                    || leafOrder.GetArrayLength() != CapitalAllocationTestData.BotCount())
                {
                    _context.RecordFail(Module, method, $"allocation_leaf_order count is not {CapitalAllocationTestData.BotCount()}");
                    return;
                }

                _context.RecordPass(Module, method,
                    $"weights_sum={weightSum:F4}, merges={merges.GetArrayLength()}, leaf_order={leafOrder.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCorrelationClusteringCalculateEmptyBots()
        {
            const string clearMethod = "allocation_bots_clear";
            const string method = "allocation_clustering_calculate";

            try
            {
                _context.PrintRequest(Module, clearMethod, new { });
                string clearResponse = _context.Client.ToolsCall(clearMethod, new { });
                _context.PrintResponse(clearResponse);

                ExpectToolError(method, new { }, "No bot files loaded");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestHierarchicalRiskParityCalculateEmptyBots()
        {
            const string method = "allocation_hrp_calculate";

            ExpectToolError(method, new { }, "No bot files loaded");
        }

        private void ExpectToolError(string method, object request, string expectedText)
        {
            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("IsError", out JsonElement isError)
                        || isError.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "expected IsError=true");
                        return;
                    }

                    string text = string.Empty;

                    if (root.TryGetProperty("Content", out JsonElement content)
                        && content.GetArrayLength() > 0
                        && content[0].TryGetProperty("Text", out JsonElement textElement))
                    {
                        text = textElement.GetString() ?? string.Empty;
                    }

                    if (text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        _context.RecordFail(Module, method, $"error text does not contain '{expectedText}': {text}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"error as expected: {expectedText}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            try
            {
                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "IsError is true");
                        return false;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return false;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _context.RecordFail(Module, method, "Content text is empty");
                        return false;
                    }

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        config = innerDocument.RootElement.Clone();
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }
    }
}
