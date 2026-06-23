/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for indicator wiki reference methods.
    /// </summary>
    public class WikiIndicatorsTests
    {
        private const string Module = "WIKI_INDICATORS";
        private const string KnownIncludeIndicator = "Sma";
        private const string KnownComplexIndicator = "MACD";
        private const string UnknownIndicator = "ThisIndicatorDoesNotExist12345";

        private readonly TestContext _context;

        public WikiIndicatorsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestToolsListContainsWikiMethods();
            TestListIndicators();
            TestListFilterInclude();
            TestListFilterScript();
            TestIndicatorInfoSma();
            TestIndicatorInfoMacd();
            TestIndicatorInfoNotFound();
        }

        private void TestToolsListContainsWikiMethods()
        {
            const string method = "tools/list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.SendRequest(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("Tools", out JsonElement tools) || tools.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Tools array is empty or missing");
                        return;
                    }

                    string[] toolNames = tools.EnumerateArray()
                        .Select(t => t.GetProperty("name").GetString() ?? string.Empty)
                        .ToArray();

                    if (!toolNames.Contains("wiki_indicators_list"))
                    {
                        _context.RecordFail(Module, method, "wiki_indicators_list tool is missing");
                        return;
                    }

                    if (!toolNames.Contains("wiki_indicator_info"))
                    {
                        _context.RecordFail(Module, method, "wiki_indicator_info tool is missing");
                        return;
                    }

                    _context.RecordPass(Module, method, "wiki indicators tools registered");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestListIndicators()
        {
            const string method = "wiki_indicators_list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("indicators", out JsonElement indicators) || indicators.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "indicators array is empty or missing");
                        return;
                    }

                    bool foundSma = false;
                    foreach (JsonElement indicator in indicators.EnumerateArray())
                    {
                        string className = indicator.GetProperty("class_name").GetString() ?? string.Empty;
                        if (className == KnownIncludeIndicator)
                        {
                            foundSma = true;
                            break;
                        }
                    }

                    if (!foundSma)
                    {
                        _context.RecordFail(Module, method, $"expected indicator '{KnownIncludeIndicator}' not found");
                        return;
                    }

                    _context.RecordPass(Module, method, $"indicators count={indicators.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestListFilterInclude()
        {
            const string method = "wiki_indicators_list";
            object request = new { location = "Include" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("indicators", out JsonElement indicators))
                    {
                        _context.RecordFail(Module, method, "indicators array is missing");
                        return;
                    }

                    foreach (JsonElement indicator in indicators.EnumerateArray())
                    {
                        string location = indicator.GetProperty("location").GetString() ?? string.Empty;
                        if (location != "Include")
                        {
                            _context.RecordFail(Module, method, $"unexpected location '{location}' in Include filter");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"include indicators count={indicators.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestListFilterScript()
        {
            const string method = "wiki_indicators_list";
            object request = new { location = "Script" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("indicators", out JsonElement indicators))
                    {
                        _context.RecordFail(Module, method, "indicators array is missing");
                        return;
                    }

                    foreach (JsonElement indicator in indicators.EnumerateArray())
                    {
                        string location = indicator.GetProperty("location").GetString() ?? string.Empty;
                        if (location != "Script")
                        {
                            _context.RecordFail(Module, method, $"unexpected location '{location}' in Script filter");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"script indicators count={indicators.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestIndicatorInfoSma()
        {
            const string method = "wiki_indicator_info";
            object request = new { class_name = KnownIncludeIndicator };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    string actualClassName = root.GetProperty("class_name").GetString() ?? string.Empty;
                    if (actualClassName != KnownIncludeIndicator)
                    {
                        _context.RecordFail(Module, method, $"unexpected class_name '{actualClassName}'");
                        return;
                    }

                    if (!root.TryGetProperty("description", out _))
                    {
                        _context.RecordFail(Module, method, "description missing");
                        return;
                    }

                    if (!root.TryGetProperty("parameters", out JsonElement parameters))
                    {
                        _context.RecordFail(Module, method, "parameters missing");
                        return;
                    }

                    if (!root.TryGetProperty("series", out JsonElement series))
                    {
                        _context.RecordFail(Module, method, "series missing");
                        return;
                    }

                    int parameterCount = parameters.GetArrayLength();
                    int seriesCount = series.GetArrayLength();

                    if (parameterCount < 2)
                    {
                        _context.RecordFail(Module, method, $"expected at least 2 parameters, got {parameterCount}");
                        return;
                    }

                    if (seriesCount < 1)
                    {
                        _context.RecordFail(Module, method, $"expected at least 1 series, got {seriesCount}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"{KnownIncludeIndicator}: parameters={parameterCount}, series={seriesCount}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestIndicatorInfoMacd()
        {
            const string method = "wiki_indicator_info";
            object request = new { class_name = KnownComplexIndicator };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    string actualClassName = root.GetProperty("class_name").GetString() ?? string.Empty;
                    if (actualClassName != KnownComplexIndicator)
                    {
                        _context.RecordFail(Module, method, $"unexpected class_name '{actualClassName}'");
                        return;
                    }

                    if (!root.TryGetProperty("parameters", out JsonElement parameters))
                    {
                        _context.RecordFail(Module, method, "parameters missing");
                        return;
                    }

                    if (!root.TryGetProperty("series", out JsonElement series))
                    {
                        _context.RecordFail(Module, method, "series missing");
                        return;
                    }

                    int parameterCount = parameters.GetArrayLength();
                    int seriesCount = series.GetArrayLength();

                    if (parameterCount < 3)
                    {
                        _context.RecordFail(Module, method, $"expected at least 3 parameters, got {parameterCount}");
                        return;
                    }

                    if (seriesCount < 3)
                    {
                        _context.RecordFail(Module, method, $"expected at least 3 series, got {seriesCount}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"{KnownComplexIndicator}: parameters={parameterCount}, series={seriesCount}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestIndicatorInfoNotFound()
        {
            const string method = "wiki_indicator_info";
            object request = new { class_name = UnknownIndicator };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("IsError", out JsonElement isError) || isError.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "expected IsError=true for unknown indicator");
                        return;
                    }

                    if (!root.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "expected error content for unknown indicator");
                        return;
                    }

                    JsonElement textElement = content[0];
                    if (!textElement.TryGetProperty("Text", out JsonElement text)
                        || text.ValueKind != JsonValueKind.String)
                    {
                        _context.RecordFail(Module, method, "expected error text for unknown indicator");
                        return;
                    }

                    string message = text.GetString() ?? string.Empty;
                    if (!message.Contains($"'{UnknownIndicator}' not found"))
                    {
                        _context.RecordFail(Module, method, $"unexpected error message: {message}");
                        return;
                    }

                    _context.RecordPass(Module, method, "unknown indicator rejected");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private string ExtractToolResult(string response)
        {
            using (JsonDocument document = JsonDocument.Parse(response))
            {
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("Content", out JsonElement content)
                    && content.GetArrayLength() > 0)
                {
                    JsonElement first = content[0];
                    if (first.TryGetProperty("Text", out JsonElement text)
                        && text.ValueKind == JsonValueKind.String)
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }

                return response;
            }
        }
    }
}
