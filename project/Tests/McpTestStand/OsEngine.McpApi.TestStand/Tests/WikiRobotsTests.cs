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
    /// Tests for robot wiki reference methods.
    /// </summary>
    public class WikiRobotsTests
    {
        private const string Module = "WIKI_ROBOTS";
        private const string KnownIncludeRobot = "Engine";
        private const string KnownComplexRobot = "AlgoStart1LinearRegression";
        private const string UnknownRobot = "ThisRobotDoesNotExist12345";

        private readonly TestContext _context;

        public WikiRobotsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestToolsListContainsWikiMethods();
            TestListRobots();
            TestListFilterInclude();
            TestListFilterScript();
            TestRobotInfoEngine();
            TestRobotInfoNotFound();
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

                    if (!toolNames.Contains("wiki_robots_list"))
                    {
                        _context.RecordFail(Module, method, "wiki_robots_list tool is missing");
                        return;
                    }

                    if (!toolNames.Contains("wiki_robot_info"))
                    {
                        _context.RecordFail(Module, method, "wiki_robot_info tool is missing");
                        return;
                    }

                    _context.RecordPass(Module, method, "wiki robots tools registered");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestListRobots()
        {
            const string method = "wiki_robots_list";
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

                    if (!root.TryGetProperty("robots", out JsonElement robots) || robots.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "robots array is empty or missing");
                        return;
                    }

                    bool foundEngine = false;
                    foreach (JsonElement robot in robots.EnumerateArray())
                    {
                        string className = robot.GetProperty("class_name").GetString() ?? string.Empty;
                        if (className == KnownIncludeRobot)
                        {
                            foundEngine = true;
                            break;
                        }
                    }

                    if (!foundEngine)
                    {
                        _context.RecordFail(Module, method, $"expected robot '{KnownIncludeRobot}' not found");
                        return;
                    }

                    _context.RecordPass(Module, method, $"robots count={robots.GetArrayLength()}");
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
            const string method = "wiki_robots_list";
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

                    if (!root.TryGetProperty("robots", out JsonElement robots))
                    {
                        _context.RecordFail(Module, method, "robots array is missing");
                        return;
                    }

                    foreach (JsonElement robot in robots.EnumerateArray())
                    {
                        string location = robot.GetProperty("location").GetString() ?? string.Empty;
                        if (location != "Include")
                        {
                            _context.RecordFail(Module, method, $"unexpected location '{location}' in Include filter");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"include robots count={robots.GetArrayLength()}");
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
            const string method = "wiki_robots_list";
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

                    if (!root.TryGetProperty("robots", out JsonElement robots))
                    {
                        _context.RecordFail(Module, method, "robots array is missing");
                        return;
                    }

                    foreach (JsonElement robot in robots.EnumerateArray())
                    {
                        string location = robot.GetProperty("location").GetString() ?? string.Empty;
                        if (location != "Script")
                        {
                            _context.RecordFail(Module, method, $"unexpected location '{location}' in Script filter");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"script robots count={robots.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestRobotInfoEngine()
        {
            const string method = "wiki_robot_info";

            string engineSummary;
            string complexSummary;

            if (!TryGetRobotInfo(method, KnownIncludeRobot, false, out engineSummary))
            {
                return;
            }

            if (!TryGetRobotInfo(method, KnownComplexRobot, true, out complexSummary))
            {
                return;
            }

            _context.RecordPass(Module, method, $"{engineSummary}; {complexSummary}");
        }

        private bool TryGetRobotInfo(string method, string className, bool expectParameters, out string summary)
        {
            summary = string.Empty;
            object request = new { class_name = className };

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
                    if (actualClassName != className)
                    {
                        _context.RecordFail(Module, method, $"unexpected class_name '{actualClassName}' for {className}");
                        return false;
                    }

                    if (!root.TryGetProperty("description", out _))
                    {
                        _context.RecordFail(Module, method, $"description missing for {className}");
                        return false;
                    }

                    if (!root.TryGetProperty("sources", out JsonElement sources))
                    {
                        _context.RecordFail(Module, method, $"sources missing for {className}");
                        return false;
                    }

                    if (!root.TryGetProperty("indicators", out _))
                    {
                        _context.RecordFail(Module, method, $"indicators missing for {className}");
                        return false;
                    }

                    if (!root.TryGetProperty("parameters", out JsonElement parameters))
                    {
                        _context.RecordFail(Module, method, $"parameters missing for {className}");
                        return false;
                    }

                    int parameterCount = parameters.GetArrayLength();
                    if (expectParameters && parameterCount == 0)
                    {
                        _context.RecordFail(Module, method, $"expected parameters for {className} but got none");
                        return false;
                    }

                    summary = $"{className}: sources={sources.GetArrayLength()}, parameters={parameterCount}";
                    return true;
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"{className}: {error.Message}");
                return false;
            }
        }

        private void TestRobotInfoNotFound()
        {
            const string method = "wiki_robot_info";
            object request = new { class_name = UnknownRobot };

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
                        _context.RecordFail(Module, method, "expected IsError=true for unknown robot");
                        return;
                    }

                    _context.RecordPass(Module, method, "unknown robot returned error");
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
