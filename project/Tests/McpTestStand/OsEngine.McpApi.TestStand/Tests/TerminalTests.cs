/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;

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
            TestOpenMode();
            TestStop();
            TestKill();
            TestStopFromMode();
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

        private void TestOpenMode()
        {
            const string method = "terminal_open_mode";
            object request = new { mode = "data" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Opening mode data"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Opening mode data'");
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

        private void TestStopFromMode()
        {
            string[] modes = new[]
            {
                "tester",
                "testerlight",
                "robots",
                "robotslight",
                "data",
                "optimizer",
                "converter"
            };

            foreach (string mode in modes)
            {
                TestStopFromMode(mode);
            }
        }

        private void TestStopFromMode(string mode)
        {
            const string openMethod = "terminal_open_mode";
            const string stopMethod = "terminal_stop";
            object openRequest = new { mode };
            object stopRequest = new { };
            string testMethod = $"terminal_stop_from_mode_{mode}";

            try
            {
                _context.RestartOsEngine(string.Empty);

                _context.PrintRequest(Module, openMethod, openRequest);
                string openResponse = _context.Client.ToolsCall(openMethod, openRequest);
                _context.PrintResponse(openResponse);

                if (!openResponse.Contains($"Opening mode {mode}"))
                {
                    _context.RecordFail(Module, testMethod, $"terminal_open_mode response does not contain 'Opening mode {mode}'");
                    return;
                }

                Thread.Sleep(2000);

                _context.PrintRequest(Module, stopMethod, stopRequest);
                string stopResponse = _context.Client.ToolsCall(stopMethod, stopRequest);
                _context.PrintResponse(stopResponse);

                if (!stopResponse.Contains("Stopping terminal"))
                {
                    _context.RecordFail(Module, testMethod, "terminal_stop response does not contain 'Stopping terminal'");
                    return;
                }

                Process? process = _context.ProcessController.CurrentProcess;

                if (process == null)
                {
                    _context.RecordFail(Module, testMethod, "current process is not tracked");
                    return;
                }

                bool exited = process.WaitForExit(30000);

                if (!exited)
                {
                    _context.RecordFail(Module, testMethod, "process did not exit within 30 seconds");
                    return;
                }

                _context.RecordPass(Module, testMethod, $"process exited gracefully from {mode} mode window");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, testMethod, error.Message);
            }
        }
    }
}
