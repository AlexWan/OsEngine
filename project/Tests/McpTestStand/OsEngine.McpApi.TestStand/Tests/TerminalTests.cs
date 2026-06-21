/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for terminal lifecycle and status tools.
    /// </summary>
    public class TerminalTests
    {
        private const string Module = "TERMINAL";
        private readonly TestContext _context;

        public TerminalTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestPing();
            TestGetStatus();
            TestLaunch();
            TestStop();
            TestKill();
        }

        private void TestPing()
        {
            const string method = "ping";
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

                    if (text != "\"pong\"")
                    {
                        _context.RecordFail(Module, method, $"unexpected response: {text}");
                        return;
                    }

                    _context.RecordPass(Module, method, text);
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetStatus()
        {
            const string method = "terminal_get_status";
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
                        if (!innerDocument.RootElement.TryGetProperty("mode", out JsonElement mode))
                        {
                            _context.RecordFail(Module, method, "mode missing in response");
                            return;
                        }

                        _context.RecordPass(Module, method, $"mode={mode.GetString()}");
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestLaunch()
        {
            const string method = "terminal_launch";
            object request = new { mode = "tester" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Launching terminal"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Launching terminal'");
                    return;
                }

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestStop()
        {
            const string method = "terminal_stop";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Stopping terminal"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Stopping terminal'");
                    return;
                }

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestKill()
        {
            const string method = "terminal_kill";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Killing terminal"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Killing terminal'");
                    return;
                }

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }
    }
}
