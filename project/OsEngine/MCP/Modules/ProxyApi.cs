/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Proxy;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for the proxy router (ProxyMaster): list, create, delete,
    /// per-proxy settings and status, ping.
    /// Passwords are always masked in responses.
    /// </summary>
    public class ProxyApi : IMcpToolProvider
    {
        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

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
                    case "proxy_get_list":
                        response.Result = GetList();
                        break;

                    case "proxy_create":
                        response.Result = Create(request.Params);
                        break;

                    case "proxy_delete":
                        response.Result = Delete(request.Params);
                        break;

                    case "proxy_get_settings":
                        response.Result = GetSettings(request.Params);
                        break;

                    case "proxy_set_settings":
                        response.Result = SetSettings(request.Params);
                        break;

                    case "proxy_get_status":
                        response.Result = GetStatus(request.Params);
                        break;

                    case "proxy_ping":
                        response.Result = Ping(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in proxy API"
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
                    Name = "proxy_get_list",
                    Description = "Get all proxies of the proxy router. Passwords are masked",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "proxy_create",
                    Description = "Create a new proxy. Number is assigned automatically. Duplicate by ip+port+login+password is rejected",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            is_on = new { type = "boolean", description = "Proxy participates in rotation (default false)" },
                            ip = new { type = "string" },
                            port = new { type = "integer", description = "1..65535" },
                            login = new { type = "string" },
                            password = new { type = "string" },
                            ping_web_address = new { type = "string", description = "http://ipinfo.io/ by default" }
                        },
                        required = new[] { "ip", "port" }
                    }
                },
                new McpTool
                {
                    Name = "proxy_delete",
                    Description = "Delete a proxy by number",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            number = new { type = "integer" }
                        },
                        required = new[] { "number" }
                    }
                },
                new McpTool
                {
                    Name = "proxy_get_settings",
                    Description = "Get settings of one proxy. Password is masked",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            number = new { type = "integer" }
                        },
                        required = new[] { "number" }
                    }
                },
                new McpTool
                {
                    Name = "proxy_set_settings",
                    Description = "Set settings of one proxy. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            number = new { type = "integer" },
                            is_on = new { type = "boolean" },
                            ip = new { type = "string" },
                            port = new { type = "integer", description = "1..65535" },
                            login = new { type = "string" },
                            password = new { type = "string" },
                            ping_web_address = new { type = "string" }
                        },
                        required = new[] { "number" }
                    }
                },
                new McpTool
                {
                    Name = "proxy_get_status",
                    Description = "Get status of one proxy: last ping status, location, usage count",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            number = new { type = "integer" }
                        },
                        required = new[] { "number" }
                    }
                },
                new McpTool
                {
                    Name = "proxy_ping",
                    Description = "Ping one proxy and return its updated status. Blocks up to 10 seconds on a dead proxy",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            number = new { type = "integer" }
                        },
                        required = new[] { "number" }
                    }
                }
            };
        }

        #endregion

        #region Private methods

        private object GetList()
        {
            ServerMaster.ActivateProxy();

            List<ProxyOsa> proxies = ServerMaster.GetAllProxies();
            List<object> result = new List<object>();

            if (proxies != null)
            {
                for (int i = 0; i < proxies.Count; i++)
                {
                    result.Add(BuildProxyResponse(proxies[i]));
                }
            }

            return new { proxies = result, count = result.Count };
        }

        private object Create(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            string ip = GetRequiredString(parameters, "ip");
            int port = GetPort(parameters);

            bool isOn = false;

            if (parameters.TryGetProperty("is_on", out JsonElement isOnElement)
                && (isOnElement.ValueKind == JsonValueKind.True || isOnElement.ValueKind == JsonValueKind.False))
            {
                isOn = isOnElement.GetBoolean();
            }

            string login = GetOptionalString(parameters, "login", string.Empty);
            string password = GetOptionalString(parameters, "password", string.Empty);
            string pingWebAddress = GetOptionalString(parameters, "ping_web_address", "http://ipinfo.io/");

            ServerMaster.ActivateProxy();

            bool created = ServerMaster.AddNewProxy(isOn, ip, port, login, password, pingWebAddress);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"Proxy {ip}:{port} was not created. Duplicate by ip+port+login+password");
            }

            ProxyOsa proxy = FindProxyByAddress(ip, port, login);

            return BuildProxyResponse(proxy);
        }

        private object Delete(JsonElement parameters)
        {
            int number = GetRequiredNumber(parameters);

            ServerMaster.ActivateProxy();

            bool deleted = ServerMaster.RemoveProxy(number);

            if (!deleted)
            {
                throw new ArgumentException($"Proxy number {number} not found");
            }

            return new { number = number, deleted = true };
        }

        private object GetSettings(JsonElement parameters)
        {
            int number = GetRequiredNumber(parameters);

            ServerMaster.ActivateProxy();

            ProxyOsa proxy = FindProxyRequired(number);

            return BuildProxyResponse(proxy);
        }

        private object SetSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            int number = GetRequiredNumber(parameters);

            ServerMaster.ActivateProxy();

            ProxyOsa proxy = FindProxyRequired(number);

            bool isOn = proxy.IsOn;

            if (parameters.TryGetProperty("is_on", out JsonElement isOnElement)
                && (isOnElement.ValueKind == JsonValueKind.True || isOnElement.ValueKind == JsonValueKind.False))
            {
                isOn = isOnElement.GetBoolean();
            }

            string ip = GetOptionalString(parameters, "ip", proxy.Ip);
            int port = proxy.Port;

            if (parameters.TryGetProperty("port", out JsonElement portElement))
            {
                port = GetPort(parameters);
            }

            string login = GetOptionalString(parameters, "login", proxy.Login);
            string password = GetOptionalString(parameters, "password", proxy.UserPassword);
            string pingWebAddress = GetOptionalString(parameters, "ping_web_address", proxy.PingWebAddress);

            bool updated = ServerMaster.UpdateProxy(number, isOn, ip, port, login, password, pingWebAddress);

            if (!updated)
            {
                throw new InvalidOperationException($"Proxy number {number} was not updated");
            }

            return BuildProxyResponse(FindProxyRequired(number));
        }

        private object GetStatus(JsonElement parameters)
        {
            int number = GetRequiredNumber(parameters);

            ServerMaster.ActivateProxy();

            ProxyOsa proxy = FindProxyRequired(number);

            return new
            {
                number = proxy.Number,
                is_on = proxy.IsOn,
                auto_ping_last_status = proxy.AutoPingLastStatus,
                location = proxy.Location,
                use_connection_count = proxy.UseConnectionCount
            };
        }

        private object Ping(JsonElement parameters)
        {
            int number = GetRequiredNumber(parameters);

            ServerMaster.ActivateProxy();

            ProxyOsa proxy = ServerMaster.UpdateStatusProxyAt(number);

            if (proxy == null)
            {
                throw new ArgumentException($"Proxy number {number} not found");
            }

            return new
            {
                number = proxy.Number,
                auto_ping_last_status = proxy.AutoPingLastStatus,
                location = proxy.Location
            };
        }

        private ProxyOsa FindProxyRequired(int number)
        {
            ProxyOsa proxy = ServerMaster.GetOneProxyAt(number);

            if (proxy == null)
            {
                throw new ArgumentException($"Proxy number {number} not found");
            }

            return proxy;
        }

        private ProxyOsa FindProxyByAddress(string ip, int port, string login)
        {
            List<ProxyOsa> proxies = ServerMaster.GetAllProxies();

            if (proxies != null)
            {
                for (int i = 0; i < proxies.Count; i++)
                {
                    if (proxies[i].Ip == ip
                        && proxies[i].Port == port
                        && proxies[i].Login == login)
                    {
                        return proxies[i];
                    }
                }
            }

            throw new InvalidOperationException($"Created proxy {ip}:{port} not found in the list");
        }

        private object BuildProxyResponse(ProxyOsa proxy)
        {
            return new
            {
                number = proxy.Number,
                is_on = proxy.IsOn,
                ip = proxy.Ip,
                port = proxy.Port,
                login = proxy.Login,
                password = MaskPassword(proxy.UserPassword),
                location = proxy.Location,
                auto_ping_last_status = proxy.AutoPingLastStatus,
                use_connection_count = proxy.UseConnectionCount,
                ping_web_address = proxy.PingWebAddress
            };
        }

        private string MaskPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return password;
            }

            if (password.Length <= 4)
            {
                return "********";
            }

            return password.Substring(0, 2) + "********" + password.Substring(password.Length - 2);
        }

        private int GetRequiredNumber(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("number", out JsonElement numberElement)
                || numberElement.ValueKind != JsonValueKind.Number
                || !numberElement.TryGetInt32(out int number))
            {
                throw new ArgumentException("number is required and must be an integer");
            }

            return number;
        }

        private int GetPort(JsonElement parameters)
        {
            if (!parameters.TryGetProperty("port", out JsonElement portElement)
                || portElement.ValueKind != JsonValueKind.Number
                || !portElement.TryGetInt32(out int port)
                || port < 1
                || port > 65535)
            {
                throw new ArgumentException("port is required and must be in range 1..65535");
            }

            return port;
        }

        private string GetRequiredString(JsonElement parameters, string name)
        {
            if (!parameters.TryGetProperty(name, out JsonElement element)
                || element.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(element.GetString()))
            {
                throw new ArgumentException($"{name} is required and must be a non-empty string");
            }

            return element.GetString();
        }

        private string GetOptionalString(JsonElement parameters, string name, string defaultValue)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            return defaultValue;
        }

        #endregion
    }
}
