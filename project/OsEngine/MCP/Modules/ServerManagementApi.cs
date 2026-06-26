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
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for global connector management via ServerMaster.
    /// </summary>
    public class ServerManagementApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public ServerManagementApi(Action<string, object> publishEvent)
        {
            _publishEvent = publishEvent;
        }

        #endregion

        #region Public methods

        public McpJsonRpcResponse Handle(McpJsonRpcRequest request)
        {
            McpJsonRpcResponse response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "server_management_get_list":
                        response.Result = GetServerList();
                        break;

                    case "server_management_activate":
                        response.Result = ActivateServer(request.Params);
                        break;

                    case "server_management_get_trade_connectors":
                        response.Result = GetTradeConnectors();
                        break;

                    case "server_management_get_data_connectors":
                        response.Result = GetDataConnectors();
                        break;

                    case "server_management_get_connector_permissions":
                        response.Result = GetConnectorPermissions(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in server management API"
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
                    Name = "server_management_get_list",
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
                    Name = "server_management_activate",
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
                    Name = "server_management_get_trade_connectors",
                    Description = "Get list of server types available for real trading",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "server_management_get_data_connectors",
                    Description = "Get full list of server types available for OsData market data download",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "server_management_get_connector_permissions",
                    Description = "Get OsEngine IServerPermission for a connector type (data feed timeframes, trade permissions, order lifetime, leverage, etc.)",
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
                }
            };
        }

        #endregion

        #region Private methods

        private List<object> GetServerList()
        {
            List<object> result = new List<object>();
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

        private static List<string> GetTradeConnectors()
        {
            List<ServerType> serverTypes = ServerMaster.ServersTypes;

            if (serverTypes == null)
            {
                return new List<string>();
            }

            List<string> result = new List<string>(serverTypes.Count);

            for (int i = 0; i < serverTypes.Count; i++)
            {
                result.Add(serverTypes[i].ToString());
            }

            result.Sort();
            return result;
        }

        private static List<string> GetDataConnectors()
        {
            List<ServerType> dataTypes = ServerMaster.ServersTypesToOsData;

            if (dataTypes == null)
            {
                return new List<string>();
            }

            List<string> result = new List<string>(dataTypes.Count);

            for (int i = 0; i < dataTypes.Count; i++)
            {
                result.Add(dataTypes[i].ToString());
            }

            result.Sort();
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
            List<object> result = new List<object>();
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

        private static object GetConnectorPermissions(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);

            IServerPermission permission = ServerMaster.GetServerPermission(serverType);

            if (permission == null)
            {
                return new
                {
                    type = serverType.ToString(),
                    permissionsAvailable = false,
                    message = $"No permissions registered for server type {serverType}"
                };
            }

            string permissionsJson = JsonSerializer.Serialize(permission, new JsonSerializerOptions
            {
                IncludeFields = true
            });

            using (JsonDocument document = JsonDocument.Parse(permissionsJson))
            {
                return new
                {
                    type = serverType.ToString(),
                    permissionsAvailable = true,
                    permissions = document.RootElement.Clone()
                };
            }
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
