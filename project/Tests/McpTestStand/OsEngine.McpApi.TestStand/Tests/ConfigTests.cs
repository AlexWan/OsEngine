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
            TestMcpSettingsApiKeyMasked();
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

                        if (!settings.TryGetProperty("port", out _))
                        {
                            _context.RecordFail(Module, method, "port missing");
                            return;
                        }

                        if (!settings.TryGetProperty("apiKey", out _))
                        {
                            _context.RecordFail(Module, method, "apiKey missing");
                            return;
                        }

                        if (!settings.TryGetProperty("isEnabled", out _))
                        {
                            _context.RecordFail(Module, method, "isEnabled missing");
                            return;
                        }

                        if (!settings.TryGetProperty("isFullLogEnabled", out _))
                        {
                            _context.RecordFail(Module, method, "isFullLogEnabled missing");
                            return;
                        }

                        if (!settings.TryGetProperty("allowedIps", out JsonElement allowedIpsElement)
                            || allowedIpsElement.ValueKind != System.Text.Json.JsonValueKind.Array
                            || allowedIpsElement.GetArrayLength() == 0)
                        {
                            _context.RecordFail(Module, method, "allowedIps missing or empty");
                            return;
                        }

                        bool hasLocalhost = false;
                        foreach (JsonElement ipItem in allowedIpsElement.EnumerateArray())
                        {
                            if (ipItem.TryGetProperty("ip", out JsonElement ipValue)
                                && ipValue.GetString() == "127.0.0.1")
                            {
                                hasLocalhost = true;
                                break;
                            }
                        }

                        if (!hasLocalhost)
                        {
                            _context.RecordFail(Module, method, "Default localhost IP not found in allowedIps");
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

                    if (!text.Contains("\"success\":true"))
                    {
                        _context.RecordFail(Module, method, "success is not true");
                        return;
                    }

                    if (!text.Contains("\"restartRequired\":false"))
                    {
                        _context.RecordFail(Module, method, "restartRequired is not false");
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

        // BUG-0137: mcp_settings_get/set must never echo the API key in plaintext.
        // K2: an empty key stays empty (MaskSecret("") == "") — skipped, not failed.
        private void TestMcpSettingsApiKeyMasked()
        {
            const string method = "mcp_settings_get";

            try
            {
                _context.PrintRequest(Module, method, new { });
                string response = _context.Client.ToolsCall(method, new { });
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    if (!TryExtractSettings(document.RootElement, out JsonElement settings, out string error))
                    {
                        _context.RecordFail(Module, method + ".apiKey_masked", error);
                        return;
                    }

                    if (!settings.TryGetProperty("apiKey", out JsonElement apiKeyElement))
                    {
                        _context.RecordFail(Module, method + ".apiKey_masked", "apiKey field missing in mcp_settings_get (contract)");
                        return;
                    }

                    string apiKey = apiKeyElement.ValueKind == JsonValueKind.String
                        ? apiKeyElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (apiKey.Length == 0)
                    {
                        _context.RecordPass(Module, method + ".apiKey_masked", "SKIP: apiKey is empty, masking not applicable");
                        return;
                    }

                    if (!apiKey.Contains("*"))
                    {
                        _context.RecordFail(Module, method + ".apiKey_masked", $"apiKey is returned unmasked by mcp_settings_get (length {apiKey.Length}, no '*' in value)");
                        return;
                    }

                    // mcp_settings_set echoes the same settings object — check the masking there too.
                    // Same value is sent back, so nothing changes and no restart is triggered.
                    bool currentFullLog = false;
                    TryReadFullLog(out currentFullLog);

                    const string setMethod = "mcp_settings_set";
                    object request = new { isFullLogEnabled = currentFullLog };

                    _context.PrintRequest(Module, setMethod, request);
                    string setResponse = _context.Client.ToolsCall(setMethod, request);
                    _context.PrintResponse(setResponse);

                    using (var setDocument = JsonDocument.Parse(setResponse))
                    {
                        if (!TryExtractSettings(setDocument.RootElement, out JsonElement setSettings, out error))
                        {
                            _context.RecordFail(Module, setMethod + ".apiKey_masked", error);
                            return;
                        }

                        if (setSettings.TryGetProperty("settings", out JsonElement innerSettings))
                        {
                            setSettings = innerSettings;
                        }

                        if (!setSettings.TryGetProperty("apiKey", out JsonElement setApiKeyElement))
                        {
                            _context.RecordFail(Module, setMethod + ".apiKey_masked", "apiKey field missing in mcp_settings_set echo (contract)");
                            return;
                        }

                        string setApiKey = setApiKeyElement.ValueKind == JsonValueKind.String
                            ? setApiKeyElement.GetString() ?? string.Empty
                            : string.Empty;

                        if (setApiKey.Length > 0 && !setApiKey.Contains("*"))
                        {
                            _context.RecordFail(Module, setMethod + ".apiKey_masked", $"apiKey is echoed unmasked by mcp_settings_set (length {setApiKey.Length}, no '*' in value)");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method + ".apiKey_masked", "apiKey masked in mcp_settings_get and mcp_settings_set echo");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method + ".apiKey_masked", error.Message);
            }
        }

        private static bool TryExtractSettings(JsonElement root, out JsonElement settings, out string error)
        {
            settings = default;
            error = string.Empty;

            if (!root.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
            {
                error = "IsError is true";
                return false;
            }

            if (!root.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
            {
                error = "Content is empty";
                return false;
            }

            string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

            try
            {
                using (var innerDocument = JsonDocument.Parse(text))
                {
                    settings = innerDocument.RootElement.Clone();
                }
            }
            catch (Exception parseError)
            {
                error = $"failed to parse settings JSON: {parseError.Message}";
                return false;
            }

            return true;
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
                        if (innerDocument.RootElement.TryGetProperty("isFullLogEnabled", out JsonElement element))
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
