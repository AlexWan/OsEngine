/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for exchange connector management via ServerMaster.
    /// </summary>
    public class ServerApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public ServerApi(Action<string, object> publishEvent)
        {
            _publishEvent = publishEvent;
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
                    case "server_get_list":
                        response.Result = GetServerList();
                        break;

                    case "server_activate":
                        response.Result = ActivateServer(request.Params);
                        break;

                    case "server_get_params":
                        response.Result = GetServerParams(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in server API"
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

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool
                {
                    Name = "server_get_list",
                    Description = "Get list of deployed exchange connectors",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "server_activate",
                    Description = "Activate server type: load saved instances and create primary instance (emulates double-click in ServerMaster window)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, BinanceSpot, MoexDataServer"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_get_params",
                    Description = "Get parameters of a specific server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, BinanceSpot, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            }
                        },
                        required = new[] { "type" }
                    }
                }
            };
        }

        #endregion

        #region Private methods

        private List<object> GetServerList()
        {
            var result = new List<object>();
            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null)
            {
                return result;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                AServer server = servers[i];

                result.Add(new
                {
                    name = server.ServerNameAndPrefix,
                    type = server.ServerType.ToString(),
                    status = server.ServerStatus.ToString(),
                    number = server.ServerNum
                });
            }

            return result;
        }

        private List<object> ActivateServer(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);

            ServerMaster.CreateServer(serverType, false, 0);

            SendLog($"Activated server type {serverType}", LogMessageType.System);

            return GetServerListByType(serverType);
        }

        private static List<object> GetServerListByType(ServerType serverType)
        {
            var result = new List<object>();
            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null)
            {
                return result;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                AServer server = servers[i];

                if (server.ServerType != serverType)
                {
                    continue;
                }

                result.Add(new
                {
                    name = server.ServerNameAndPrefix,
                    type = server.ServerType.ToString(),
                    status = server.ServerStatus.ToString(),
                    number = server.ServerNum
                });
            }

            return result;
        }

        private static List<object> GetServerParams(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            return ConvertServerParameters(server.ServerParameters);
        }

        private static AServer FindServer(ServerType serverType, int serverNumber)
        {
            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null)
            {
                return null;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                AServer server = servers[i];

                if (server.ServerType == serverType
                    && server.ServerNum == serverNumber)
                {
                    return server;
                }
            }

            return null;
        }

        private static List<object> ConvertServerParameters(List<IServerParameter> parameters)
        {
            var result = new List<object>();

            if (parameters == null)
            {
                return result;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                IServerParameter parameter = parameters[i];

                if (parameter.Type == ServerParameterType.Button)
                {
                    continue;
                }

                object value = GetParameterValue(parameter);
                bool isSecret = parameter.Type == ServerParameterType.Password;

                result.Add(new
                {
                    name = parameter.Name,
                    type = parameter.Type.ToString(),
                    value = isSecret ? MaskSecret(value) : value,
                    comment = parameter.Comment
                });
            }

            return result;
        }

        private static object GetParameterValue(IServerParameter parameter)
        {
            switch (parameter.Type)
            {
                case ServerParameterType.String:
                    return ((ServerParameterString)parameter).Value;

                case ServerParameterType.Password:
                    return ((ServerParameterPassword)parameter).Value;

                case ServerParameterType.Path:
                    return ((ServerParameterPath)parameter).Value;

                case ServerParameterType.Int:
                    return ((ServerParameterInt)parameter).Value;

                case ServerParameterType.Decimal:
                    return ((ServerParameterDecimal)parameter).Value;

                case ServerParameterType.Bool:
                    return ((ServerParameterBool)parameter).Value;

                case ServerParameterType.Enum:
                    return ((ServerParameterEnum)parameter).Value;

                default:
                    return null;
            }
        }

        private static string MaskSecret(object value)
        {
            string text = value?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.Length <= 4)
            {
                return "****";
            }

            return text.Substring(0, 2) + new string('*', text.Length - 4) + text.Substring(text.Length - 2);
        }

        private static int ParseServerNumber(JsonElement parameters)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("number", out JsonElement numberElement)
                && numberElement.ValueKind == JsonValueKind.Number
                && numberElement.TryGetInt32(out int number))
            {
                return number;
            }

            return 0;
        }

        private static ServerType ParseServerType(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("type", out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Parameter 'type' is required and must be a string");
            }

            string typeName = typeElement.GetString();

            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Parameter 'type' cannot be empty");
            }

            try
            {
                return (ServerType)Enum.Parse(typeof(ServerType), typeName, true);
            }
            catch
            {
                throw new ArgumentException($"Unknown server type '{typeName}'");
            }
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
