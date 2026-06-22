/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for operations on a specific server instance.
    /// </summary>
    public class ServerInstanceApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;
        private readonly Dictionary<(ServerType, int), Action<string>> _statusSubscriptions = new Dictionary<(ServerType, int), Action<string>>();
        private readonly Dictionary<(ServerType, int), Action<List<Security>>> _securitiesSubscriptions = new Dictionary<(ServerType, int), Action<List<Security>>>();
        private readonly Dictionary<(ServerType, int), Action<List<Portfolio>>> _portfolioSubscriptions = new Dictionary<(ServerType, int), Action<List<Portfolio>>>();
        private readonly Dictionary<(ServerType, int), Action<string, LogMessageType>> _logSubscriptions = new Dictionary<(ServerType, int), Action<string, LogMessageType>>();
        private readonly object _subscriptionsLocker = new object();

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public ServerInstanceApi(Action<string, object> publishEvent)
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
                    case "server_instance_get_params":
                        response.Result = GetServerParams(request.Params);
                        break;

                    case "server_instance_set_params":
                        response.Result = SetServerParams(request.Params);
                        break;

                    case "server_instance_create":
                        response.Result = CreateServerInstance(request.Params);
                        break;

                    case "server_instance_delete":
                        response.Result = DeleteServerInstance(request.Params);
                        break;

                    case "server_instance_connect":
                        response.Result = ConnectServerInstance(request.Params);
                        break;

                    case "server_instance_disconnect":
                        response.Result = DisconnectServerInstance(request.Params);
                        break;

                    case "server_instance_get_securities":
                        response.Result = GetServerSecurities(request.Params);
                        break;

                    case "server_instance_get_portfolios":
                        response.Result = GetServerPortfolios(request.Params);
                        break;

                    case "server_instance_get_status":
                        response.Result = GetServerStatus(request.Params);
                        break;

                    case "server_instance_get_log":
                        response.Result = GetServerLog(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in server instance API"
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
                    Name = "server_instance_get_params",
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
                },
                new McpTool
                {
                    Name = "server_instance_create",
                    Description = "Create a new instance of a server connector type",
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
                    Name = "server_instance_delete",
                    Description = "Delete a server instance by type and number",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0). Instance 0 cannot be deleted, only stopped."
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_get_status",
                    Description = "Get current connection status of a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_get_securities",
                    Description = "Get list of securities from a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            },
                            className = new
                            {
                                type = "string",
                                description = "Optional filter by security class"
                            },
                            filter = new
                            {
                                type = "string",
                                description = "Optional filter by security code/name substring"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_get_portfolios",
                    Description = "Get list of portfolios and positions from a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_get_log",
                    Description = "Get recent log messages from a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            },
                            count = new
                            {
                                type = "integer",
                                description = "Maximum number of log entries (default: 100)"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_connect",
                    Description = "Connect a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_disconnect",
                    Description = "Disconnect a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "Server type name, e.g. TInvest, Binance, MoexDataServer"
                            },
                            number = new
                            {
                                type = "integer",
                                description = "Server instance number (default: 0)"
                            }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "server_instance_set_params",
                    Description = "Set parameters of a specific server instance",
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
                            },
                            parameters = new
                            {
                                type = "array",
                                description = "Parameters to set",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        value = new { type = new[] { "string", "integer", "number", "boolean" } }
                                    },
                                    required = new[] { "name", "value" }
                                }
                            }
                        },
                        required = new[] { "type", "parameters" }
                    }
                }
            };
        }

        #endregion

        #region Private methods

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

        private static object SetServerParams(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);
            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("parameters", out JsonElement paramsArray)
                || paramsArray.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Parameter 'parameters' is required and must be an array");
            }

            var validatedParams = new List<Tuple<IServerParameter, object>>();

            foreach (JsonElement item in paramsArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("name", out JsonElement nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Each parameter must have a string 'name'");
                }

                if (!item.TryGetProperty("value", out JsonElement valueElement))
                {
                    throw new ArgumentException("Each parameter must have a 'value'");
                }

                string paramName = nameElement.GetString();
                IServerParameter parameter = FindParameter(server, paramName);

                if (parameter == null)
                {
                    throw new ArgumentException($"Parameter '{paramName}' not found on server {serverType}#{serverNumber}");
                }

                object convertedValue = ConvertParameterValue(parameter, valueElement);
                validatedParams.Add(new Tuple<IServerParameter, object>(parameter, convertedValue));
            }

            foreach (Tuple<IServerParameter, object> pair in validatedParams)
            {
                SetParameterValue(pair.Item1, pair.Item2);
            }

            return new
            {
                success = true,
                updated = validatedParams.ConvertAll(p => new
                {
                    name = p.Item1.Name,
                    type = p.Item1.Type.ToString(),
                    value = p.Item2
                })
            };
        }

        private static object CreateServerInstance(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);

            IServerPermission permission = ServerMaster.GetServerPermission(serverType);

            if (permission == null)
            {
                throw new ArgumentException($"No permissions registered for server type {serverType}");
            }

            if (!permission.IsSupports_MultipleInstances)
            {
                throw new ArgumentException($"Server type {serverType} does not support multiple instances");
            }

            List<AServer> servers = ServerMaster.GetAServers();
            int maxNumber = 0;

            if (servers != null)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    AServer server = servers[i];

                    if (server.ServerType == serverType
                        && server.ServerNum > maxNumber)
                    {
                        maxNumber = server.ServerNum;
                    }
                }
            }

            int newNumber = maxNumber + 1;

            ServerMaster.CreateServer(serverType, false, newNumber);
            ServerMaster.SaveServerInstanceByType(serverType);

            AServer createdServer = FindServer(serverType, newNumber);

            if (createdServer == null)
            {
                throw new InvalidOperationException($"Failed to create server instance {serverType}#{newNumber}");
            }

            return new
            {
                name = createdServer.ServerNameAndPrefix,
                type = createdServer.ServerType.ToString(),
                status = createdServer.ServerStatus.ToString(),
                number = createdServer.ServerNum
            };
        }

        private object DeleteServerInstance(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            if (serverNumber < 1)
            {
                throw new ArgumentException("Only instances with number >= 1 can be deleted. Instance 0 can only be stopped.");
            }

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            UnsubscribeFromServerEvents(server, serverType, serverNumber);
            ServerMaster.DeleteServer(serverType, serverNumber);

            AServer remainingServer = FindServer(serverType, serverNumber);

            if (remainingServer != null)
            {
                throw new InvalidOperationException($"Failed to delete server instance {serverType}#{serverNumber}");
            }

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                deleted = true
            };
        }

        private object ConnectServerInstance(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            SubscribeToServerEvents(server, serverType, serverNumber);
            server.StartServer();

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                command = "connect",
                status = server.ServerStatus.ToString()
            };
        }

        private object DisconnectServerInstance(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            server.StopServer();

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                command = "disconnect",
                status = server.ServerStatus.ToString()
            };
        }

        private void SubscribeToServerEvents(AServer server, ServerType serverType, int serverNumber)
        {
            lock (_subscriptionsLocker)
            {
                if (_statusSubscriptions.ContainsKey((serverType, serverNumber)))
                {
                    return;
                }

                Action<string> statusHandler = null;
                statusHandler = (status) =>
                {
                    _publishEvent("server_instance.status_changed", new
                    {
                        type = serverType.ToString(),
                        number = serverNumber,
                        status = status
                    });

                    if (status == "Disconnect")
                    {
                        UnsubscribeFromServerEvents(server, serverType, serverNumber);
                    }
                };

                Action<List<Security>> securitiesHandler = (list) =>
                {
                    _publishEvent("server_instance.security.updated", new
                    {
                        type = serverType.ToString(),
                        number = serverNumber,
                        count = list?.Count ?? 0
                    });
                };

                Action<List<Portfolio>> portfolioHandler = (list) =>
                {
                    _publishEvent("server_instance.portfolio.updated", new
                    {
                        type = serverType.ToString(),
                        number = serverNumber,
                        count = list?.Count ?? 0
                    });
                };

                Action<string, LogMessageType> logHandler = (message, type) =>
                {
                    _publishEvent("server_instance.log", new
                    {
                        type = serverType.ToString(),
                        number = serverNumber,
                        message = message,
                        messageType = type.ToString()
                    });
                };

                server.ConnectStatusChangeEvent += statusHandler;
                server.SecuritiesChangeEvent += securitiesHandler;
                server.PortfoliosChangeEvent += portfolioHandler;
                server.LogMessageEvent += logHandler;

                _statusSubscriptions[(serverType, serverNumber)] = statusHandler;
                _securitiesSubscriptions[(serverType, serverNumber)] = securitiesHandler;
                _portfolioSubscriptions[(serverType, serverNumber)] = portfolioHandler;
                _logSubscriptions[(serverType, serverNumber)] = logHandler;
            }
        }

        private void UnsubscribeFromServerEvents(AServer server, ServerType serverType, int serverNumber)
        {
            lock (_subscriptionsLocker)
            {
                if (_statusSubscriptions.TryGetValue((serverType, serverNumber), out Action<string> statusHandler))
                {
                    server.ConnectStatusChangeEvent -= statusHandler;
                    _statusSubscriptions.Remove((serverType, serverNumber));
                }

                if (_securitiesSubscriptions.TryGetValue((serverType, serverNumber), out Action<List<Security>> securitiesHandler))
                {
                    server.SecuritiesChangeEvent -= securitiesHandler;
                    _securitiesSubscriptions.Remove((serverType, serverNumber));
                }

                if (_portfolioSubscriptions.TryGetValue((serverType, serverNumber), out Action<List<Portfolio>> portfolioHandler))
                {
                    server.PortfoliosChangeEvent -= portfolioHandler;
                    _portfolioSubscriptions.Remove((serverType, serverNumber));
                }

                if (_logSubscriptions.TryGetValue((serverType, serverNumber), out Action<string, LogMessageType> logHandler))
                {
                    server.LogMessageEvent -= logHandler;
                    _logSubscriptions.Remove((serverType, serverNumber));
                }
            }
        }

        private static object GetServerStatus(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                status = server.ServerStatus.ToString()
            };
        }

        private static object GetServerSecurities(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            string classFilter = string.Empty;
            string codeFilter = string.Empty;

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("className", out JsonElement classElement)
                    && classElement.ValueKind == JsonValueKind.String)
                {
                    classFilter = classElement.GetString();
                }

                if (parameters.TryGetProperty("filter", out JsonElement filterElement)
                    && filterElement.ValueKind == JsonValueKind.String)
                {
                    codeFilter = filterElement.GetString();
                }
            }

            List<Security> securities = server.Securities;
            var result = new List<object>();

            if (securities != null)
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    Security security = securities[i];

                    if (!string.IsNullOrWhiteSpace(classFilter)
                    && security.NameClass != classFilter)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(codeFilter)
                        && !security.Name.Contains(codeFilter))
                    {
                        continue;
                    }

                    result.Add(ConvertSecurity(security));
                }
            }

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                count = result.Count,
                securities = result
            };
        }

        private static object ConvertSecurity(Security security)
        {
            return new
            {
                name = security.Name,
                nameFull = security.NameFull,
                nameClass = security.NameClass,
                nameId = security.NameId,
                exchange = security.Exchange,
                state = security.State.ToString(),
                securityType = security.SecurityType.ToString(),
                priceStep = security.PriceStep,
                priceStepCost = security.PriceStepCost,
                lot = security.Lot,
                decimals = security.Decimals,
                decimalsVolume = security.DecimalsVolume,
                volumeStep = security.VolumeStep,
                minTradeAmount = security.MinTradeAmount,
                minTradeAmountType = security.MinTradeAmountType.ToString(),
                marginBuy = security.MarginBuy,
                marginSell = security.MarginSell,
                priceLimitLow = security.PriceLimitLow,
                priceLimitHigh = security.PriceLimitHigh,
                underlyingAsset = security.UnderlyingAsset,
                optionType = security.OptionType.ToString(),
                strike = security.Strike,
                expiration = security.Expiration,
                nominalInitial = security.NominalInitial,
                nominalCurrent = security.NominalCurrent,
                maturityDate = security.MaturityDate,
                aciValue = security.AciValue
            };
        }

        private static object GetServerPortfolios(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            List<Portfolio> portfolios = server.Portfolios;
            var result = new List<object>();

            if (portfolios != null)
            {
                for (int i = 0; i < portfolios.Count; i++)
                {
                    result.Add(ConvertPortfolio(portfolios[i]));
                }
            }

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                count = result.Count,
                portfolios = result
            };
        }

        private static object ConvertPortfolio(Portfolio portfolio)
        {
            var positions = new List<object>();

            if (portfolio.PositionOnBoard != null)
            {
                for (int i = 0; i < portfolio.PositionOnBoard.Count; i++)
                {
                    PositionOnBoard position = portfolio.PositionOnBoard[i];

                    positions.Add(new
                    {
                        securityNameCode = position.SecurityNameCode,
                        securityNameClass = position.SecurityNameClass,
                        valueBegin = position.ValueBegin,
                        valueCurrent = position.ValueCurrent,
                        valueBlocked = position.ValueBlocked,
                        unrealizedPnl = position.UnrealizedPnl
                    });
                }
            }

            return new
            {
                number = portfolio.Number,
                valueBegin = portfolio.ValueBegin,
                valueCurrent = portfolio.ValueCurrent,
                valueBlocked = portfolio.ValueBlocked,
                unrealizedPnl = portfolio.UnrealizedPnl,
                serverType = portfolio.ServerType.ToString(),
                serverUniqueName = portfolio.ServerUniqueName,
                positions = positions
            };
        }

        private static object GetServerLog(JsonElement parameters)
        {
            ServerType serverType = ParseServerType(parameters);
            int serverNumber = ParseServerNumber(parameters);

            AServer server = FindServer(serverType, serverNumber);

            if (server == null)
            {
                throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
            }

            int count = 100;

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number
                && countElement.TryGetInt32(out int requestedCount)
                && requestedCount > 0)
            {
                count = requestedCount;
            }

            List<LogMessage> messages = server.Log?.LoadMessageFromLastDay() ?? new List<LogMessage>();

            if (messages.Count > count)
            {
                messages = messages.Skip(messages.Count - count).ToList();
            }

            var result = new List<object>(messages.Count);

            for (int i = 0; i < messages.Count; i++)
            {
                LogMessage message = messages[i];

                result.Add(new
                {
                    time = message.Time,
                    type = message.Type.ToString(),
                    message = message.Message
                });
            }

            return new
            {
                type = serverType.ToString(),
                number = serverNumber,
                count = result.Count,
                messages = result
            };
        }

        private static IServerParameter FindParameter(AServer server, string name)
        {
            List<IServerParameter> parameters = server.ServerParameters;

            if (parameters == null)
            {
                return null;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Name == name)
                {
                    return parameters[i];
                }
            }

            return null;
        }

        private static object ConvertParameterValue(IServerParameter parameter, JsonElement valueElement)
        {
            switch (parameter.Type)
            {
                case ServerParameterType.String:
                case ServerParameterType.Password:
                case ServerParameterType.Path:
                    if (valueElement.ValueKind != JsonValueKind.String)
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a string value");
                    }
                    return valueElement.GetString();

                case ServerParameterType.Enum:
                    if (valueElement.ValueKind != JsonValueKind.String)
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a string value");
                    }
                    string enumValue = valueElement.GetString();
                    var enumParameter = (ServerParameterEnum)parameter;
                    if (enumParameter.EnumValues != null && !enumParameter.EnumValues.Contains(enumValue))
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' value '{enumValue}' is not in allowed values");
                    }
                    return enumValue;

                case ServerParameterType.Int:
                    if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetInt32(out int intValue))
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires an integer value");
                    }
                    return intValue;

                case ServerParameterType.Decimal:
                    if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetDecimal(out decimal decimalValue))
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a decimal value");
                    }
                    return decimalValue;

                case ServerParameterType.Bool:
                    if (valueElement.ValueKind != JsonValueKind.True && valueElement.ValueKind != JsonValueKind.False)
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a boolean value");
                    }
                    return valueElement.GetBoolean();

                case ServerParameterType.Button:
                    throw new ArgumentException($"Parameter '{parameter.Name}' is a button and cannot be set");

                default:
                    throw new ArgumentException($"Parameter '{parameter.Name}' has unsupported type '{parameter.Type}'");
            }
        }

        private static void SetParameterValue(IServerParameter parameter, object value)
        {
            switch (parameter.Type)
            {
                case ServerParameterType.String:
                    ((ServerParameterString)parameter).Value = (string)value;
                    break;

                case ServerParameterType.Password:
                    ((ServerParameterPassword)parameter).Value = (string)value;
                    break;

                case ServerParameterType.Path:
                    ((ServerParameterPath)parameter).Value = (string)value;
                    break;

                case ServerParameterType.Enum:
                    ((ServerParameterEnum)parameter).Value = (string)value;
                    break;

                case ServerParameterType.Int:
                    ((ServerParameterInt)parameter).Value = (int)value;
                    break;

                case ServerParameterType.Decimal:
                    ((ServerParameterDecimal)parameter).Value = (decimal)value;
                    break;

                case ServerParameterType.Bool:
                    ((ServerParameterBool)parameter).Value = (bool)value;
                    break;
            }
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
