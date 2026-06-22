/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.MCP.Json;
using OsEngine.MCP.Modules;
using OsEngine.PrimeSettings;

namespace OsEngine.MCP
{
    /// <summary>
    /// Master host for MCP API.
    /// Accepts JSON-RPC requests and publishes events via Server-Sent Events.
    /// </summary>
    public class McpMaster
    {
        #region Fields

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenerTask;
        private readonly string _apiKey;
        private readonly int _port;
        private readonly List<SseClient> _sseClients = new List<SseClient>();
        private readonly object _sseClientsLocker = new object();

        private readonly TerminalApi _terminalApi;
        private readonly LogsApi _logsApi;
        private readonly SettingsApi _settingsApi;
        private readonly McpConfigApi _configApi;
        private readonly ServerManagementApi _serverManagementApi;
        private readonly ServerInstanceApi _serverInstanceApi;
        private readonly McpProtocolApi _protocolApi;

        private readonly Func<McpTerminalStatus> _getTerminalStatus;
        private readonly Action<StartProgram> _launchTerminal;
        private readonly Action _stopTerminal;
        private readonly Action _killTerminal;

        /// <summary>
        /// Standard OsEngine log for MCP events and requests.
        /// </summary>
        public Log Log { get; private set; }

        #endregion

        #region Constructors

        public McpMaster(
            int port,
            string apiKey,
            Action restartHost = null,
            Func<McpTerminalStatus> getTerminalStatus = null,
            Action<StartProgram> launchTerminal = null,
            Action stopTerminal = null,
            Action killTerminal = null)
        {
            _port = port;
            _apiKey = apiKey;
            _getTerminalStatus = getTerminalStatus;
            _launchTerminal = launchTerminal;
            _stopTerminal = stopTerminal;
            _killTerminal = killTerminal;

            Log = new Log("MCP", StartProgram.IsMainWindow);

            Action<string, object> publishEvent = (eventName, payload) => SendEvent(eventName, payload);

            _terminalApi = new TerminalApi(
                publishEvent,
                () => GetTerminalStatusSafe(),
                _launchTerminal,
                _stopTerminal,
                _killTerminal);
            _terminalApi.NewLogMessageEvent += TerminalApi_NewLogMessageEvent;

            _logsApi = new LogsApi(Log);
            _logsApi.NewLogMessageEvent += LogsApi_NewLogMessageEvent;

            _settingsApi = new SettingsApi(publishEvent);
            _settingsApi.NewLogMessageEvent += SettingsApi_NewLogMessageEvent;

            _configApi = new McpConfigApi(restartHost);
            _configApi.NewLogMessageEvent += ConfigApi_NewLogMessageEvent;

            _serverManagementApi = new ServerManagementApi(publishEvent);
            _serverManagementApi.NewLogMessageEvent += ServerManagementApi_NewLogMessageEvent;

            _serverInstanceApi = new ServerInstanceApi(publishEvent);
            _serverInstanceApi.NewLogMessageEvent += ServerInstanceApi_NewLogMessageEvent;

            _protocolApi = new McpProtocolApi(request => ExecuteTool(request));
            _protocolApi.NewLogMessageEvent += ProtocolApi_NewLogMessageEvent;

            _protocolApi.RegisterToolProvider(_terminalApi);
            _protocolApi.RegisterToolProvider(_logsApi);
            _protocolApi.RegisterToolProvider(_settingsApi);
            _protocolApi.RegisterToolProvider(_configApi);
            _protocolApi.RegisterToolProvider(_serverManagementApi);
            _protocolApi.RegisterToolProvider(_serverInstanceApi);
        }

        #endregion

        #region Public methods

        public void Start()
        {
            if (_listener != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{_port}/");
                _listener.Start();
            }
            catch
            {
                // fallback to localhost if wildcard prefix requires elevated permissions
                // or if the previous listener became disposed after a failed Start()
                try
                {
                    _listener?.Close();
                }
                catch
                {
                    // ignore
                }

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
            }

            _listenerTask = Task.Run(() => ListenLoop(_cts.Token));
            Task.Run(() => HeartbeatLoop(_cts.Token));

            Log.ProcessMessage($"MCP API started on port {_port}", LogMessageType.System);

            SendEvent("terminal.launched", GetTerminalStatusSafe());
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _listener = null;

                lock (_sseClientsLocker)
                {
                    foreach (var client in _sseClients)
                    {
                        try
                        {
                            client.Response.Close();
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                    _sseClients.Clear();
                }

                Log.ProcessMessage("MCP API stopped", LogMessageType.System);
            }
            catch (Exception error)
            {
                Log.ProcessMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SendEventToClient(SseClient client, string eventName, object payload)
        {
            var message = new McpEvent
            {
                Event = eventName,
                Timestamp = DateTime.Now,
                Payload = payload
            };

            string json = JsonSerializer.Serialize(message, JsonOptions);
            string sseData = $"event: {eventName}\ndata: {json}\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseData);

            try
            {
                client.Response.OutputStream.Write(bytes, 0, bytes.Length);
                client.Response.OutputStream.Flush();
            }
            catch
            {
                try
                {
                    client.Response.Close();
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void TerminalApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void LogsApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void SettingsApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void ConfigApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void ServerManagementApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void ServerInstanceApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        private void ProtocolApi_NewLogMessageEvent(string message, LogMessageType type)
        {
            Log.ProcessMessage(message, type);
        }

        public void SendEvent(string eventName, object payload)
        {
            var message = new McpEvent
            {
                Event = eventName,
                Timestamp = DateTime.Now,
                Payload = payload
            };

            string json = JsonSerializer.Serialize(message, JsonOptions);
            string sseData = $"event: {eventName}\ndata: {json}\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseData);

            lock (_sseClientsLocker)
            {
                for (int i = _sseClients.Count - 1; i >= 0; i--)
                {
                    var client = _sseClients[i];
                    try
                    {
                        client.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        client.Response.OutputStream.Flush();
                    }
                    catch
                    {
                        try
                        {
                            client.Response.Close();
                        }
                        catch
                        {
                            // ignore
                        }
                        _sseClients.RemoveAt(i);
                    }
                }
            }
        }

        public void SendTerminalStopped(string reason)
        {
            _terminalApi?.SendTerminalStopped(reason);
        }

        public void SendTerminalModeChanged(StartProgram mode)
        {
            _terminalApi?.SendTerminalModeChanged(mode);
        }

        #endregion

        #region Private methods

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context), token);
                }
                catch (Exception error)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Log.ProcessMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
        }

        private async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token);
                    SendEvent("heartbeat", new { time = DateTime.Now });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception error)
                {
                    Log.ProcessMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = null;
            HttpListenerResponse response = null;
            string path = string.Empty;

            try
            {
                request = context.Request;
                response = context.Response;
                path = request.Url.AbsolutePath;

                if (McpSettings.IsFullLogEnabled)
                {
                    string ip = request.RemoteEndPoint?.Address?.ToString();
                    Log.ProcessMessage($"[FullLog] Request {request.HttpMethod} {path} from {ip}", LogMessageType.System);
                }

                if (!IsAuthorized(request))
                {
                    SendError(response, 401, "Unauthorized");
                    LogRequest(request, response, path);
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/api/v1/mcp")
                {
                    ProcessJsonRpc(request, response);
                }
                else if (request.HttpMethod == "GET" && path == "/api/v1/events")
                {
                    if (McpSettings.IsFullLogEnabled)
                    {
                        string ip = request.RemoteEndPoint?.Address?.ToString();
                        Log.ProcessMessage($"[FullLog] SSE client connected from {ip}", LogMessageType.System);
                    }
                    ProcessSse(response);
                }
                else
                {
                    SendError(response, 404, "Not found");
                }

                LogRequest(request, response, path);
            }
            catch (Exception error)
            {
                Log.ProcessMessage(error.ToString(), LogMessageType.Error);
                try
                {
                    if (response != null)
                    {
                        SendError(response, 500, "Internal server error");
                        LogRequest(request, response, path);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void LogRequest(HttpListenerRequest request, HttpListenerResponse response, string path)
        {
            if (request == null || response == null)
            {
                return;
            }

            try
            {
                string ip = request.RemoteEndPoint?.Address?.ToString();
                string message = $"{request.HttpMethod} {path} from {ip} -> {response.StatusCode}";
                Log.ProcessMessage(message, LogMessageType.System);
            }
            catch
            {
                // logging must not break request handling
            }
        }

        private bool IsAuthorized(HttpListenerRequest request)
        {
            string apiKey = request.Headers["X-Api-Key"];
            return apiKey == _apiKey;
        }

        private void ProcessJsonRpc(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            if (McpSettings.IsFullLogEnabled)
            {
                Log.ProcessMessage($"[FullLog] RPC request body: {body}", LogMessageType.System);
            }

            McpJsonRpcResponse rpcResponse = null;

            try
            {
                var rpcRequest = JsonSerializer.Deserialize<McpJsonRpcRequest>(body);

                if (rpcRequest == null)
                {
                    rpcResponse = new McpJsonRpcResponse
                    {
                        JsonRpc = "2.0",
                        Error = new McpJsonRpcError { Code = -32700, Message = "Parse error" }
                    };
                }
                else if (rpcRequest.Id == null)
                {
                    // JSON-RPC notification: no response required
                    _protocolApi.HandleNotification(rpcRequest);
                }
                else
                {
                    rpcResponse = HandleMethod(rpcRequest);
                }
            }
            catch (JsonException error)
            {
                rpcResponse = new McpJsonRpcResponse
                {
                    JsonRpc = "2.0",
                    Error = new McpJsonRpcError { Code = -32700, Message = error.Message }
                };
            }
            catch (Exception error)
            {
                rpcResponse = new McpJsonRpcResponse
                {
                    JsonRpc = "2.0",
                    Error = new McpJsonRpcError { Code = -32603, Message = error.Message }
                };
            }

            if (rpcResponse == null)
            {
                // notification: no response body
                response.StatusCode = 202;
                response.Close();
                return;
            }

            if (McpSettings.IsFullLogEnabled)
            {
                var logOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string responseJson = JsonSerializer.Serialize(rpcResponse, logOptions);
                Log.ProcessMessage($"[FullLog] RPC response: {responseJson}", LogMessageType.System);
            }

            SendJson(response, 200, rpcResponse);
        }

        private McpJsonRpcResponse HandleMethod(McpJsonRpcRequest request)
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
                    case "tools/list":
                    case "tools/call":
                        response = _protocolApi.Handle(request);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found. Use tools/call."
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

        private McpJsonRpcResponse ExecuteTool(McpJsonRpcRequest request)
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
                    case "ping":
                        response.Result = "pong";
                        break;

                    case "terminal_get_status":
                    case "terminal_launch":
                    case "terminal_stop":
                    case "terminal_kill":
                        response = _terminalApi.Handle(request);
                        break;

                    case "log_get_emergency_log":
                    case "log_get_mcp_log":
                        response = _logsApi.Handle(request);
                        break;

                    case "prime_settings_get":
                    case "prime_settings_set":
                        response = _settingsApi.Handle(request);
                        break;

                    case "mcp_settings_get":
                    case "mcp_settings_set":
                        response = _configApi.Handle(request);
                        break;

                    case "server_management_get_list":
                    case "server_management_activate":
                    case "server_management_get_trade_connectors":
                    case "server_management_get_data_connectors":
                    case "server_management_get_connector_permissions":
                        response = _serverManagementApi.Handle(request);
                        break;

                    case "server_instance_get_params":
                    case "server_instance_set_params":
                    case "server_instance_create":
                    case "server_instance_delete":
                    case "server_instance_connect":
                    case "server_instance_disconnect":
                    case "server_instance_get_securities":
                    case "server_instance_get_portfolios":
                    case "server_instance_get_status":
                    case "server_instance_get_log":
                        response = _serverInstanceApi.Handle(request);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Tool '{request.Method}' not found"
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
                Log.ProcessMessage($"GetTerminalStatus failed: {error}", LogMessageType.Error);
            }

            return new McpTerminalStatus { Mode = "unknown" };
        }

        private void ProcessSse(HttpListenerResponse response)
        {
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.StatusCode = 200;
            response.OutputStream.Flush();

            var client = new SseClient { Response = response };

            lock (_sseClientsLocker)
            {
                _sseClients.Add(client);
            }

            SendEventToClient(client, "terminal.launched", GetTerminalStatusSafe());

            // keep connection alive until host is stopped
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(1000);
                }
                catch
                {
                    break;
                }
            }

            lock (_sseClientsLocker)
            {
                _sseClients.Remove(client);
            }
        }

        private void SendJson<T>(HttpListenerResponse response, int statusCode, T data)
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private void SendError(HttpListenerResponse response, int statusCode, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        #endregion

        #region Nested types

        private class SseClient
        {
            public HttpListenerResponse Response;
        }

        #endregion
    }
}
