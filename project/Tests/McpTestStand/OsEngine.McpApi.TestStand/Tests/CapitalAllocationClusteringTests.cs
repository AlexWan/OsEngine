/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Тесты CorrelationClustering-алгоритма риск-паритета: настройки (min_correlation),
    /// расчёт и результат (кластеры + веса, сумма весов ≈ 100, покрытие ботов).
    /// Запускается после CapitalAllocationCommonTests (режим открыт, все 50 ботов загружены).
    /// </summary>
    public class CapitalAllocationClusteringTests
    {
        private const string Module = "CAPITAL_ALLOCATION";

        private readonly TestContext _context;

        public CapitalAllocationClusteringTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            TestCorrelationClusteringSettingsGet();
            TestCorrelationClusteringSettingsSet();
            TestCorrelationClusteringSettingsClamp();
            TestCorrelationClusteringSettingsRestore();
            TestCorrelationClusteringResultBeforeCalc();
            TestCorrelationClusteringCalculate();
            TestCorrelationClusteringResult();
        }

        private void TestCorrelationClusteringSettingsGet()
        {
            const string method = "allocation_clustering_settings_get";
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

                if (!config.TryGetProperty("min_correlation", out JsonElement minCorrelation))
                {
                    _context.RecordFail(Module, method, "min_correlation missing");
                    return;
                }

                double value = minCorrelation.GetDouble();

                if (Math.Abs(value - 0.4) > 0.0001)
                {
                    _context.RecordFail(Module, method, $"min_correlation is not 0.4 (got {value})");
                    return;
                }

                _context.RecordPass(Module, method, "min_correlation=0.4");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCorrelationClusteringSettingsSet()
        {
            const string method = "allocation_clustering_settings_set";
            object request = new { min_correlation = 0.5 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("min_correlation", out JsonElement minCorrelation)
                    || Math.Abs(minCorrelation.GetDouble() - 0.5) > 0.0001)
                {
                    _context.RecordFail(Module, method, "min_correlation is not 0.5");
                    return;
                }

                _context.RecordPass(Module, method, "min_correlation=0.5");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCorrelationClusteringSettingsClamp()
        {
            const string method = "allocation_clustering_settings_set";
            object request = new { min_correlation = 1.5 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("min_correlation", out JsonElement minCorrelation)
                    || Math.Abs(minCorrelation.GetDouble() - 1.0) > 0.0001)
                {
                    _context.RecordFail(Module, method, "min_correlation is not clamped to 1");
                    return;
                }

                _context.RecordPass(Module, method, "min_correlation clamped to 1");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCorrelationClusteringSettingsRestore()
        {
            const string method = "allocation_clustering_settings_set";
            object request = new { min_correlation = 0.4 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("min_correlation", out JsonElement minCorrelation)
                    || Math.Abs(minCorrelation.GetDouble() - 0.4) > 0.0001)
                {
                    _context.RecordFail(Module, method, "min_correlation is not 0.4");
                    return;
                }

                _context.RecordPass(Module, method, "min_correlation restored to 0.4");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCorrelationClusteringResultBeforeCalc()
        {
            const string method = "allocation_clustering_result_get";

            ExpectToolError(method, new { }, "Run allocation_clustering_calculate first");
        }

        private void TestCorrelationClusteringCalculate()
        {
            const string method = "allocation_clustering_calculate";
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

        private void TestCorrelationClusteringResult()
        {
            const string method = "allocation_clustering_result_get";
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

                if (!config.TryGetProperty("clusters", out JsonElement clusters)
                    || clusters.ValueKind != JsonValueKind.Array
                    || clusters.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "clusters is empty");
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
                foreach (JsonElement weight in weights.EnumerateArray())
                {
                    if (!weight.TryGetProperty("weight_percent", out JsonElement weightPercent))
                    {
                        _context.RecordFail(Module, method, "weight_percent missing");
                        return;
                    }

                    weightSum += weightPercent.GetDouble();
                }

                if (Math.Abs(weightSum - 100.0) > 0.01)
                {
                    _context.RecordFail(Module, method, $"weights sum is not ~100 (got {weightSum})");
                    return;
                }

                var coveredBots = new HashSet<string>();
                foreach (JsonElement cluster in clusters.EnumerateArray())
                {
                    if (!cluster.TryGetProperty("bots", out JsonElement bots)
                        || bots.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, method, "cluster bots is not an array");
                        return;
                    }

                    foreach (JsonElement bot in bots.EnumerateArray())
                    {
                        if (bot.TryGetProperty("bot_name", out JsonElement botName))
                        {
                            coveredBots.Add(botName.GetString() ?? string.Empty);
                        }
                    }
                }

                var expectedBots = CapitalAllocationTestData.AllBotNames();

                if (!coveredBots.SetEquals(expectedBots))
                {
                    _context.RecordFail(Module, method, $"clusters do not cover all bots: covered={coveredBots.Count}, expected={expectedBots.Count}");
                    return;
                }

                _context.RecordPass(Module, method,
                    $"clusters={clusters.GetArrayLength()}, weights={weights.GetArrayLength()}, weights_sum={weightSum:F4}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
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
