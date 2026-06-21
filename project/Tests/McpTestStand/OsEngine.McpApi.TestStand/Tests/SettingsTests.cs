/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for terminal prime settings tools.
    /// </summary>
    public class SettingsTests
    {
        private const string Module = "SETTINGS";
        private readonly TestContext _context;

        public SettingsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestPrimeSettingsGet();
            TestPrimeSettingsSet();
        }

        private void TestPrimeSettingsGet()
        {
            const string method = "prime_settings_get";
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

                    if (!text.Contains("\"ErrorLogMessageBoxIsActive\""))
                    {
                        _context.RecordFail(Module, method, "ErrorLogMessageBoxIsActive missing");
                        return;
                    }

                    _context.RecordPass(Module, method, "settings received");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestPrimeSettingsSet()
        {
            const string method = "prime_settings_set";

            bool originalValue = false;
            bool readSuccess = TryReadReportCriticalErrors(out originalValue);

            if (!readSuccess)
            {
                _context.RecordFail(Module, method, "failed to read current value");
                return;
            }

            bool newValue = !originalValue;
            object request = new { reportCriticalErrors = newValue };

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
                }

                bool actualValue = false;
                if (!TryReadReportCriticalErrors(out actualValue))
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
                object restoreRequest = new { reportCriticalErrors = originalValue };
                _context.Client.ToolsCall(method, restoreRequest);

                _context.RecordPass(Module, method, $"changed reportCriticalErrors to {newValue} and restored");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);

                try
                {
                    object restoreRequest = new { reportCriticalErrors = originalValue };
                    _context.Client.ToolsCall(method, restoreRequest);
                }
                catch
                {
                    // ignore restore error
                }
            }
        }

        private bool TryReadReportCriticalErrors(out bool value)
        {
            value = false;

            try
            {
                string response = _context.Client.ToolsCall("prime_settings_get", new { });

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
                        if (innerDocument.RootElement.TryGetProperty("ReportCriticalErrors", out JsonElement element))
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
