/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for operations on a specific server instance.
    /// </summary>
    public class ServerInstanceTests
    {
        private const string Module = "SERVER_INSTANCE";
        private readonly TestContext _context;

        public ServerInstanceTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestGetParams();
            TestSetParams();
        }

        private void TestGetParams()
        {
            const string method = "server_instance_get_params";
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

        private void TestSetParams()
        {
            const string getMethod = "server_instance_get_params";
            const string setMethod = "server_instance_set_params";
            const string serverType = "TInvest";
            object getRequest = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                using (var document = JsonDocument.Parse(getResponse))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, setMethod, error);
                        return;
                    }

                    if (resultElement.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, setMethod, "get_params response result is not an array");
                        return;
                    }

                    string? targetName = null;
                    bool originalValue = false;

                    foreach (JsonElement parameterElement in resultElement.EnumerateArray())
                    {
                        if (parameterElement.TryGetProperty("name", out JsonElement nameElement)
                            && nameElement.ValueKind == JsonValueKind.String
                            && parameterElement.TryGetProperty("type", out JsonElement typeElement)
                            && typeElement.ValueKind == JsonValueKind.String
                            && typeElement.GetString() == "Bool"
                            && parameterElement.TryGetProperty("value", out JsonElement valueElement)
                            && (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False))
                        {
                            targetName = nameElement.GetString();
                            originalValue = valueElement.GetBoolean();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(targetName))
                    {
                        _context.RecordFail(Module, setMethod, "no bool parameter found to test set_params");
                        return;
                    }

                    bool newValue = !originalValue;

                    object setRequest = new
                    {
                        type = serverType,
                        parameters = new[]
                        {
                            new { name = targetName, value = newValue }
                        }
                    };

                    _context.PrintRequest(Module, setMethod, setRequest);
                    string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                    _context.PrintResponse(setResponse);

                    using (var setDocument = JsonDocument.Parse(setResponse))
                    {
                        JsonElement setRoot = setDocument.RootElement;

                        if (!TryExtractToolResult(setRoot, out JsonElement setResultElement, out error))
                        {
                            _context.RecordFail(Module, setMethod, error);
                            return;
                        }

                        if (!setResultElement.TryGetProperty("success", out JsonElement successElement)
                            || successElement.ValueKind != JsonValueKind.True)
                        {
                            _context.RecordFail(Module, setMethod, "set_params did not return success");
                            return;
                        }

                        if (!setResultElement.TryGetProperty("updated", out JsonElement updatedElement)
                            || updatedElement.ValueKind != JsonValueKind.Array
                            || updatedElement.GetArrayLength() != 1)
                        {
                            _context.RecordFail(Module, setMethod, "set_params did not return exactly one updated parameter");
                            return;
                        }
                    }

                    _context.PrintRequest(Module, getMethod, getRequest);
                    string verifyResponse = _context.Client.ToolsCall(getMethod, getRequest);
                    _context.PrintResponse(verifyResponse);

                    using (var verifyDocument = JsonDocument.Parse(verifyResponse))
                    {
                        JsonElement verifyRoot = verifyDocument.RootElement;

                        if (!TryExtractToolResult(verifyRoot, out JsonElement verifyResultElement, out error))
                        {
                            _context.RecordFail(Module, setMethod, error);
                            return;
                        }

                        bool verified = false;

                        foreach (JsonElement parameterElement in verifyResultElement.EnumerateArray())
                        {
                            if (parameterElement.TryGetProperty("name", out JsonElement nameElement)
                                && nameElement.ValueKind == JsonValueKind.String
                                && nameElement.GetString() == targetName
                                && parameterElement.TryGetProperty("value", out JsonElement valueElement)
                                && (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False)
                                && valueElement.GetBoolean() == newValue)
                            {
                                verified = true;
                                break;
                            }
                        }

                        if (!verified)
                        {
                            _context.RecordFail(Module, setMethod, $"parameter '{targetName}' was not updated to {newValue}");
                            return;
                        }
                    }

                    object restoreRequest = new
                    {
                        type = serverType,
                        parameters = new[]
                        {
                            new { name = targetName, value = originalValue }
                        }
                    };

                    _context.Client.ToolsCall(setMethod, restoreRequest);

                    _context.RecordPass(Module, setMethod, $"updated '{targetName}' from {originalValue} to {newValue} and restored");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
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
    }
}
