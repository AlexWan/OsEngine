/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// Anthropic MCP protocol handlers: initialize, notifications/initialized, tools/list, tools/call.
    /// </summary>
    public class McpProtocolApi
    {
        #region Fields

        private const string SupportedProtocolVersion = "2024-11-05";

        private readonly Func<McpJsonRpcRequest, McpJsonRpcResponse> _executeTool;
        private readonly List<IMcpToolProvider> _toolProviders = new List<IMcpToolProvider>();

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public McpProtocolApi(Func<McpJsonRpcRequest, McpJsonRpcResponse> executeTool)
        {
            _executeTool = executeTool ?? throw new ArgumentNullException(nameof(executeTool));
        }

        public void RegisterToolProvider(IMcpToolProvider provider)
        {
            if (provider != null && !_toolProviders.Contains(provider))
            {
                _toolProviders.Add(provider);
            }
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
                    case "initialize":
                        response.Result = Initialize(request.Params);
                        break;

                    case "tools/list":
                        response.Result = ToolsList();
                        break;

                    case "tools/call":
                        response.Result = ToolsCall(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in MCP protocol API"
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

        public void HandleNotification(McpJsonRpcRequest request)
        {
            try
            {
                switch (request.Method)
                {
                    case "notifications/initialized":
                        SendLog("MCP client initialized", LogMessageType.System);
                        break;

                    default:
                        SendLog($"Unknown notification: {request.Method}", LogMessageType.System);
                        break;
                }
            }
            catch (Exception error)
            {
                SendLog($"HandleNotification failed: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region Private methods

        private object Initialize(JsonElement parameters)
        {
            string protocolVersion = SupportedProtocolVersion;

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("protocolVersion", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.String)
            {
                protocolVersion = versionElement.GetString();
            }

            if (protocolVersion != SupportedProtocolVersion)
            {
                throw new ArgumentException($"Unsupported protocol version '{protocolVersion}'. Supported: {SupportedProtocolVersion}");
            }

            SendLog($"MCP initialize requested, protocolVersion={protocolVersion}", LogMessageType.System);

            return new
            {
                protocolVersion = SupportedProtocolVersion,
                capabilities = new
                {
                    tools = new { },
                    logging = new { }
                },
                serverInfo = new
                {
                    name = "osengine-mcp",
                    version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                }
            };
        }

        private object ToolsCall(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Parameter 'name' is required");
            }

            string name = nameElement.GetString();

            JsonElement arguments = default;
            if (parameters.TryGetProperty("arguments", out var argumentsElement)
                && argumentsElement.ValueKind == JsonValueKind.Object)
            {
                arguments = argumentsElement;
            }

            SendLog($"tools/call: {name}", LogMessageType.System);

            var innerRequest = new McpJsonRpcRequest
            {
                JsonRpc = "2.0",
                Method = name,
                Params = arguments,
                Id = Guid.NewGuid().ToString()
            };

            McpJsonRpcResponse innerResponse = _executeTool(innerRequest);

            if (innerResponse.Error != null)
            {
                return new
                {
                    Content = new[]
                    {
                        new { Type = "text", Text = innerResponse.Error.Message }
                    },
                    IsError = true
                };
            }

            string resultJson = innerResponse.Result != null
                ? JsonSerializer.Serialize(innerResponse.Result, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
                : "null";

            return new
            {
                Content = new[]
                {
                    new { Type = "text", Text = resultJson }
                },
                IsError = false
            };
        }

        private object ToolsList()
        {
            var tools = new List<McpTool>
            {
                new McpTool { Name = "ping", Description = "Check MCP API availability", InputSchema = new { type = "object", properties = new { }, required = new string[0] } }
            };

            for (int i = 0; i < _toolProviders.Count; i++)
            {
                tools.AddRange(_toolProviders[i].GetTools());
            }

            return new { Tools = tools };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
