/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for exchange connector management via ServerMaster.
    /// </summary>
    public class ServerTests
    {
        private const string Module = "SERVER";
        private readonly TestContext _context;

        public ServerTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestGetList();
            TestActivate();
            TestGetParams();
        }

        private void TestGetList()
        {
            const string method = "server_get_list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    if (!ValidateServerListResult(resultElement, out error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    int count = resultElement.GetArrayLength();
                    _context.RecordPass(Module, method, $"returned {count} server(s)");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestActivate()
        {
            const string method = "server_activate";
            const string serverType = "TInvest";
            object request = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    if (!ValidateServerListResult(resultElement, out error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    bool found = false;

                    foreach (JsonElement serverElement in resultElement.EnumerateArray())
                    {
                        if (serverElement.TryGetProperty("type", out JsonElement typeElement)
                            && typeElement.ValueKind == JsonValueKind.String
                            && typeElement.GetString() == serverType)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _context.RecordFail(Module, method, $"activated server list does not contain type {serverType}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"activated {serverType} server");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetParams()
        {
            const string method = "server_get_params";
            const string serverType = "TInvest";
            object request = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    if (resultElement.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, method, "response result is not an array");
                        return;
                    }

                    foreach (JsonElement parameterElement in resultElement.EnumerateArray())
                    {
                        if (parameterElement.ValueKind != JsonValueKind.Object)
                        {
                            _context.RecordFail(Module, method, "parameter entry is not an object");
                            return;
                        }

                        if (!parameterElement.TryGetProperty("name", out _)
                            || !parameterElement.TryGetProperty("type", out _)
                            || !parameterElement.TryGetProperty("value", out _))
                        {
                            _context.RecordFail(Module, method, "parameter entry is missing required fields (name, type, value)");
                            return;
                        }
                    }

                    int count = resultElement.GetArrayLength();
                    _context.RecordPass(Module, method, $"returned {count} parameter(s) for {serverType}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private static bool TryExtractToolResult(JsonElement root, out JsonElement resultElement, out string error)
        {
            error = string.Empty;
            resultElement = default;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "response is not an object";
                return false;
            }

            if (!root.TryGetProperty("Content", out JsonElement contentElement)
                || contentElement.ValueKind != JsonValueKind.Array
                || contentElement.GetArrayLength() == 0)
            {
                error = "response Content is missing or empty";
                return false;
            }

            JsonElement textElement = contentElement[0];

            if (!textElement.TryGetProperty("Text", out JsonElement textProperty)
                || textProperty.ValueKind != JsonValueKind.String)
            {
                error = "response Content[0].Text is missing or not a string";
                return false;
            }

            string resultJson = textProperty.GetString() ?? string.Empty;

            try
            {
                using (var resultDocument = JsonDocument.Parse(resultJson))
                {
                    resultElement = resultDocument.RootElement.Clone();
                }
            }
            catch (Exception parseError)
            {
                error = $"failed to parse Content[0].Text as JSON: {parseError.Message}";
                return false;
            }

            return true;
        }

        private static bool ValidateServerListResult(JsonElement resultElement, out string error)
        {
            error = string.Empty;

            if (resultElement.ValueKind != JsonValueKind.Array)
            {
                error = "response result is not an array";
                return false;
            }

            foreach (JsonElement serverElement in resultElement.EnumerateArray())
            {
                if (serverElement.ValueKind != JsonValueKind.Object)
                {
                    error = "server entry is not an object";
                    return false;
                }

                if (!serverElement.TryGetProperty("name", out _)
                    || !serverElement.TryGetProperty("type", out _)
                    || !serverElement.TryGetProperty("status", out _)
                    || !serverElement.TryGetProperty("number", out _))
                {
                    error = "server entry is missing required fields (name, type, status, number)";
                    return false;
                }
            }

            return true;
        }
    }
}
