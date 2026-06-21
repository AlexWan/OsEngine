/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for MCP host configuration tools.
    /// </summary>
    public class ConfigTests
    {
        private const string Module = "CONFIG";
        private readonly TestContext _context;

        public ConfigTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestMcpSettingsGet();
            TestMcpSettingsSetFullLog();
        }

        private void TestMcpSettingsGet()
        {
            const string method = "mcp_settings_get";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _context.RecordFail(Module, method, "Content text is empty");
                        return;
                    }

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement settings = innerDocument.RootElement;

                        if (!settings.TryGetProperty("Port", out _))
                        {
                            _context.RecordFail(Module, method, "Port missing");
                            return;
                        }

                        if (!settings.TryGetProperty("ApiKey", out _))
                        {
                            _context.RecordFail(Module, method, "ApiKey missing");
                            return;
                        }

                        if (!settings.TryGetProperty("IsEnabled", out _))
                        {
                            _context.RecordFail(Module, method, "IsEnabled missing");
                            return;
                        }

                        if (!settings.TryGetProperty("IsFullLogEnabled", out _))
                        {
                            _context.RecordFail(Module, method, "IsFullLogEnabled missing");
                            return;
                        }

                        _context.RecordPass(Module, method, "settings received");
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMcpSettingsSetFullLog()
        {
            const string method = "mcp_settings_set";

            bool originalValue = false;
            if (!TryReadFullLog(out originalValue))
            {
                _context.RecordFail(Module, method, "failed to read current IsFullLogEnabled");
                return;
            }

            bool newValue = !originalValue;
            object request = new { isFullLogEnabled = newValue };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    if (!text.Contains("\"Success\":true"))
                    {
                        _context.RecordFail(Module, method, "Success is not true");
                        return;
                    }

                    if (!text.Contains("\"RestartRequired\":false"))
                    {
                        _context.RecordFail(Module, method, "RestartRequired is not false");
                        return;
                    }
                }

                bool actualValue = false;
                if (!TryReadFullLog(out actualValue))
                {
                    _context.RecordFail(Module, method, "failed to verify new value");
                    return;
                }

                if (actualValue != newValue)
                {
                    _context.RecordFail(Module, method, $"value was not changed: expected {newValue}, got {actualValue}");
                    return;
                }

                // restore original value
                object restoreRequest = new { isFullLogEnabled = originalValue };
                _context.Client.ToolsCall(method, restoreRequest);

                _context.RecordPass(Module, method, $"changed IsFullLogEnabled to {newValue} and restored");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);

                try
                {
                    object restoreRequest = new { isFullLogEnabled = originalValue };
                    _context.Client.ToolsCall(method, restoreRequest);
                }
                catch
                {
                    // ignore restore error
                }
            }
        }

        private bool TryReadFullLog(out bool value)
        {
            value = false;

            try
            {
                string response = _context.Client.ToolsCall("mcp_settings_get", new { });

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        return false;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        if (innerDocument.RootElement.TryGetProperty("IsFullLogEnabled", out JsonElement element))
                        {
                            value = element.GetBoolean();
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }
}
