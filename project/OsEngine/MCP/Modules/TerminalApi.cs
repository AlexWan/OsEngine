/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for terminal lifecycle commands.
    /// </summary>
    public class TerminalApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;
        private readonly Func<McpTerminalStatus> _getTerminalStatus;
        private readonly Action<string> _launchTerminal;
        private readonly Action _stopTerminal;
        private readonly Action _killTerminal;
        private readonly Action<string> _openMode;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public TerminalApi(
            Action<string, object> publishEvent,
            Func<McpTerminalStatus> getTerminalStatus,
            Action<string> launchTerminal,
            Action stopTerminal,
            Action killTerminal,
            Action<string> openMode = null)
        {
            _publishEvent = publishEvent;
            _getTerminalStatus = getTerminalStatus;
            _launchTerminal = launchTerminal;
            _stopTerminal = stopTerminal;
            _killTerminal = killTerminal;
            _openMode = openMode;
        }

        #endregion

        #region Public methods

        public McpJsonRpcResponse Handle(McpJsonRpcRequest request)
        {
            var response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "terminal_get_status":
                        response.Result = _getTerminalStatus != null
                            ? _getTerminalStatus()
                            : new McpTerminalStatus { Mode = "unknown" };
                        break;

                    case "terminal_launch":
                        response.Result = LaunchTerminalProgram(request.Params);
                        break;

                    case "terminal_stop":
                        response.Result = StopTerminalProgram();
                        break;

                    case "terminal_kill":
                        response.Result = KillTerminalProgram();
                        break;

                    case "terminal_open_mode":
                        response.Result = OpenTerminalMode(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in terminal API"
                        };
                        break;
                }
            }
            catch (Exception error)
            {
                response.Error = new McpJsonRpcError
                {
                    Code = -32603,
                    Message = error.Message
                };
            }

            return response;
        }

        public void SendTerminalStopped(string reason)
        {
            try
            {
                var payload = new
                {
                    Status = GetTerminalStatusSafe(),
                    Reason = reason,
                    Time = DateTime.Now
                };

                _publishEvent("terminal.stopped", payload);
            }
            catch (Exception error)
            {
                SendLog($"SendTerminalStopped failed: {error}", LogMessageType.Error);
            }
        }

        public void SendTerminalModeChanged(StartProgram mode)
        {
            try
            {
                var payload = new
                {
                    Mode = mode.ToString(),
                    Status = GetTerminalStatusSafe(),
                    Time = DateTime.Now
                };

                _publishEvent("terminal.mode_changed", payload);
            }
            catch (Exception error)
            {
                SendLog($"SendTerminalModeChanged failed: {error}", LogMessageType.Error);
            }
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "terminal_get_status", Description = "Get current terminal status", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "terminal_launch", Description = "Launch terminal in specified mode", InputSchema = new { type = "object", properties = new { mode = new { type = "string", description = "Mode: tester, testerlight, robots, robotslight, data, optimizer, converter" } }, required = new[] { "mode" } } },
                new McpTool { Name = "terminal_stop", Description = "Stop terminal gracefully", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "terminal_kill", Description = "Kill terminal process", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "terminal_open_mode", Description = "Open a mode window from the running MainWindow without restarting the process", InputSchema = new { type = "object", properties = new { mode = new { type = "string", description = "Mode: tester, testerlight, robots, robotslight, data, optimizer, converter" } }, required = new[] { "mode" } } }
            };
        }

        #endregion

        #region Private methods

        private string LaunchTerminalProgram(JsonElement parameters)
        {
            if (_launchTerminal == null)
            {
                return "Launch callback is not set";
            }

            string mode = ParseMode(parameters);

            if (string.IsNullOrEmpty(mode))
            {
                return "Unknown or unsupported mode";
            }

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    _launchTerminal(mode);
                }
                catch (Exception error)
                {
                    SendLog($"terminal_launch failed: {error}", LogMessageType.Error);
                }
            });

            return $"Launching terminal in mode {mode}";
        }

        private string StopTerminalProgram()
        {
            if (_stopTerminal == null)
            {
                return "Stop callback is not set";
            }

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    _stopTerminal();
                }
                catch (Exception error)
                {
                    SendLog($"terminal_stop failed: {error}", LogMessageType.Error);
                }
            });

            return "Stopping terminal";
        }

        private string KillTerminalProgram()
        {
            if (_killTerminal == null)
            {
                return "Kill callback is not set";
            }

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    _killTerminal();
                }
                catch (Exception error)
                {
                    SendLog($"terminal_kill failed: {error}", LogMessageType.Error);
                }
            });

            return "Killing terminal";
        }

        private string OpenTerminalMode(JsonElement parameters)
        {
            if (_openMode == null)
            {
                return "Open mode callback is not set";
            }

            string mode = ParseMode(parameters);

            if (string.IsNullOrEmpty(mode))
            {
                return "Unknown or unsupported mode";
            }

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    _openMode(mode);
                }
                catch (Exception error)
                {
                    SendLog($"terminal_open_mode failed: {error}", LogMessageType.Error);
                }
            });

            return $"Opening mode {mode}";
        }

        private string ParseMode(JsonElement parameters)
        {
            try
            {
                if (parameters.ValueKind == JsonValueKind.Object
                    && parameters.TryGetProperty("mode", out JsonElement modeElement))
                {
                    string mode = modeElement.GetString()?.ToLowerInvariant();

                    switch (mode)
                    {
                        case "tester":
                        case "testerlight":
                        case "robots":
                        case "robotslight":
                        case "data":
                        case "optimizer":
                        case "converter":
                            return mode;
                    }
                }
            }
            catch
            {
                // ignore, return empty
            }

            return string.Empty;
        }

        private McpTerminalStatus GetTerminalStatusSafe()
        {
            try
            {
                if (_getTerminalStatus != null)
                {
                    return _getTerminalStatus();
                }
            }
            catch (Exception error)
            {
                SendLog($"GetTerminalStatus failed: {error}", LogMessageType.Error);
            }

            return new McpTerminalStatus { Mode = "unknown" };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
