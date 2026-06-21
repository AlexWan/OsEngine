/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for log reading commands.
    /// </summary>
    public class LogsApi : IMcpToolProvider
    {
        #region Fields

        private readonly Log _mcpLog;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public LogsApi(Log mcpLog)
        {
            _mcpLog = mcpLog;
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
                    case "log_get_emergency_log":
                        SendLog("log_get_emergency_log requested", LogMessageType.System);
                        response.Result = GetEmergencyLogEntries(request.Params);
                        break;

                    case "log_get_mcp_log":
                        SendLog("log_get_mcp_log requested", LogMessageType.System);
                        response.Result = GetMcpLogEntries(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in logs API"
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

        #endregion

        #region Private methods

        private List<McpLogEntry> GetEmergencyLogEntries(JsonElement parameters)
        {
            int count = ParseCount(parameters);

            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                return (List<McpLogEntry>)MainWindow.GetDispatcher.Invoke(
                    new Func<int, List<McpLogEntry>>(GetEmergencyLogEntriesInternal), count);
            }

            return GetEmergencyLogEntriesInternal(count);
        }

        private List<McpLogEntry> GetEmergencyLogEntriesInternal(int count)
        {
            List<LogMessage> messages = Log.GetLastErrorMessages(count);
            return ConvertToMcpEntries(messages);
        }

        private List<McpLogEntry> GetMcpLogEntries(JsonElement parameters)
        {
            int count = ParseCount(parameters);

            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                return (List<McpLogEntry>)MainWindow.GetDispatcher.Invoke(
                    new Func<int, List<McpLogEntry>>(GetMcpLogEntriesInternal), count);
            }

            return GetMcpLogEntriesInternal(count);
        }

        private List<McpLogEntry> GetMcpLogEntriesInternal(int count)
        {
            List<LogMessage> messages = _mcpLog.GetLastMessages(count);
            return ConvertToMcpEntries(messages);
        }

        private List<McpLogEntry> ConvertToMcpEntries(List<LogMessage> messages)
        {
            var result = new List<McpLogEntry>(messages.Count);

            for (int i = 0; i < messages.Count; i++)
            {
                LogMessage message = messages[i];

                result.Add(new McpLogEntry
                {
                    Time = message.Time,
                    Type = message.Type.ToString(),
                    Message = message.Message
                });
            }

            return result;
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "log_get_emergency_log", Description = "Read last emergency log entries", InputSchema = new { type = "object", properties = new { count = new { type = "integer", description = "Number of entries (1..1000)" } }, required = new string[0] } },
                new McpTool { Name = "log_get_mcp_log", Description = "Read last MCP log entries", InputSchema = new { type = "object", properties = new { count = new { type = "integer", description = "Number of entries (1..1000)" } }, required = new string[0] } }
            };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        private int ParseCount(JsonElement parameters)
        {
            try
            {
                if (parameters.ValueKind == JsonValueKind.Object
                    && parameters.TryGetProperty("count", out JsonElement countElement))
                {
                    if (countElement.TryGetInt32(out int count))
                    {
                        return Math.Max(1, Math.Min(count, 1000));
                    }
                }
            }
            catch
            {
                // ignore, use default
            }

            return 100;
        }

        #endregion
    }
}
