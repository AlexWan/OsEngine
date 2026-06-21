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
    /// MCP API handlers for operations on a specific server instance.
    /// </summary>
    public class ServerInstanceApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

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
