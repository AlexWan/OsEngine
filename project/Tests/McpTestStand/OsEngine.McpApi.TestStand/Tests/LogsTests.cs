/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for log reading tools.
    /// </summary>
    public class LogsTests
    {
        private const string Module = "LOGS";
        private readonly TestContext _context;

        public LogsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestEmergencyLog();
            TestMcpLog();
            TestMcpLogWithCount();
        }

        private void TestEmergencyLog()
        {
            const string method = "log_get_emergency_log";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!ValidateLogResult(result, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    _context.RecordPass(Module, method, "returned log entries");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMcpLog()
        {
            const string method = "log_get_mcp_log";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!ValidateLogResult(result, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    _context.RecordPass(Module, method, "returned log entries");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMcpLogWithCount()
        {
            const string method = "log_get_mcp_log";
            object request = new { count = 5 };

            try
            {
                _context.PrintRequest(Module, $"{method}(count=5)", request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!ValidateLogResult(result, out string error))
                    {
                        _context.RecordFail(Module, $"{method}(count=5)", error);
                        return;
                    }

                    _context.RecordPass(Module, $"{method}(count=5)", "returned log entries");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, $"{method}(count=5)", error.Message);
            }
        }

        private static bool ValidateLogResult(JsonElement result, out string error)
        {
            error = string.Empty;

            if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
            {
                error = "IsError is true";
                return false;
            }

            if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
            {
                error = "Content is empty";
                return false;
            }

            return true;
        }
    }
}
