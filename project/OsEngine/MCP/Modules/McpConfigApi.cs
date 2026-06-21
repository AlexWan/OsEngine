/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for MCP host configuration.
    /// </summary>
    public class McpConfigApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action _restartHost;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public McpConfigApi(Action restartHost)
        {
            _restartHost = restartHost;
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
                    case "mcp_settings_get":
                        response.Result = GetSettings();
                        break;

                    case "mcp_settings_set":
                        response.Result = SetSettings(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in MCP config API"
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

        private object GetSettings()
        {
            return new
            {
                McpSettings.Port,
                McpSettings.ApiKey,
                McpSettings.IsEnabled,
                McpSettings.IsFullLogEnabled
            };
        }

        private object SetSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            int oldPort = McpSettings.Port;
            string oldApiKey = McpSettings.ApiKey;
            bool oldIsEnabled = McpSettings.IsEnabled;

            if (parameters.TryGetProperty("port", out var portElement)
                && portElement.ValueKind == JsonValueKind.Number
                && portElement.TryGetInt32(out int port))
            {
                McpSettings.Port = port;
            }

            if (parameters.TryGetProperty("apiKey", out var apiKeyElement)
                && apiKeyElement.ValueKind == JsonValueKind.String)
            {
                McpSettings.ApiKey = apiKeyElement.GetString();
            }

            if (parameters.TryGetProperty("isEnabled", out var isEnabledElement)
                && (isEnabledElement.ValueKind == JsonValueKind.True || isEnabledElement.ValueKind == JsonValueKind.False))
            {
                McpSettings.IsEnabled = isEnabledElement.GetBoolean();
            }

            if (parameters.TryGetProperty("isFullLogEnabled", out var isFullLogEnabledElement)
                && (isFullLogEnabledElement.ValueKind == JsonValueKind.True || isFullLogEnabledElement.ValueKind == JsonValueKind.False))
            {
                McpSettings.IsFullLogEnabled = isFullLogEnabledElement.GetBoolean();
            }

            bool needRestart = McpSettings.Port != oldPort
                || McpSettings.ApiKey != oldApiKey
                || McpSettings.IsEnabled != oldIsEnabled;

            if (needRestart && _restartHost != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(500);
                        _restartHost.Invoke();
                    }
                    catch (Exception error)
                    {
                        SendLog($"mcp_settings_set restart failed: {error}", LogMessageType.Error);
                    }
                });
            }

            return new
            {
                Success = true,
                RestartRequired = needRestart,
                Settings = GetSettings()
            };
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "mcp_settings_get", Description = "Get MCP host settings", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "mcp_settings_set", Description = "Set MCP host settings", InputSchema = new { type = "object", properties = new { }, required = new string[0] } }
            };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
