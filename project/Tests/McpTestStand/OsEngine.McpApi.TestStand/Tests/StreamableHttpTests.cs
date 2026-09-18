/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for the standard Streamable HTTP endpoint (/api/v2/mcp):
    /// sessions, protocol version header, camelCase result keys, HTTP codes.
    /// Uses McpApiClient in raw mode (wire format, no wrapper normalization).
    /// </summary>
    public class StreamableHttpTests
    {
        private const string Module = "STREAMABLE_HTTP";
        private readonly TestContext _context;

        public StreamableHttpTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            using (McpApiClient client = new McpApiClient(_context.Client.BaseUrl, _context.ApiKey, streamableHttp: true))
            {
                TestInitialize(client);
                TestNotification(client);
                TestToolsList(client);
                TestToolsCall(client);
                TestSessionRequired(client);
                TestUnknownSession(client);
                TestInvalidVersionHeader(client);
                TestGetSse(client);
                TestDeleteSession(client);
                TestServerEventDelivery(client);
            }
        }

        private string _sessionId = string.Empty;

        private void TestInitialize(McpApiClient client)
        {
            const string method = "initialize";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"stand-v2\",\"version\":\"1.0\"}}}";

                _context.PrintRequest(Module, method, new { protocolVersion = "2025-06-18" });
                HttpResult result = client.PostRaw(body);
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 200)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 200, got {result.StatusCode}");
                    return;
                }

                if (string.IsNullOrEmpty(result.SessionId))
                {
                    _context.RecordFail(Module, method, "Mcp-Session-Id header is missing");
                    return;
                }

                using (JsonDocument document = JsonDocument.Parse(result.Body))
                {
                    if (!document.RootElement.TryGetProperty("result", out JsonElement r)
                        || !r.TryGetProperty("protocolVersion", out JsonElement version)
                        || version.GetString() != "2024-11-05")
                    {
                        _context.RecordFail(Module, method, "protocolVersion was not negotiated to 2024-11-05");
                        return;
                    }
                }

                _sessionId = result.SessionId;
                _context.RecordPass(Module, method, $"session assigned, negotiated version 2024-11-05");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestNotification(McpApiClient client)
        {
            const string method = "notifications/initialized";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";

                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.PostRaw(body, _sessionId);
                _context.PrintResponse("");

                if (result.StatusCode != 202)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 202, got {result.StatusCode}");
                    return;
                }

                _context.RecordPass(Module, method, "HTTP 202 Accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestToolsList(McpApiClient client)
        {
            const string method = "tools/list";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}";

                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.PostRaw(body, _sessionId, "2024-11-05");
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 200)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 200, got {result.StatusCode}");
                    return;
                }

                using (JsonDocument document = JsonDocument.Parse(result.Body))
                {
                    JsonElement r = document.RootElement.GetProperty("result");

                    if (!r.TryGetProperty("tools", out JsonElement tools) || tools.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "'tools' key (camelCase) missing or empty");
                        return;
                    }

                    _context.RecordPass(Module, method, $"tools count={tools.GetArrayLength()}, camelCase keys");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestToolsCall(McpApiClient client)
        {
            const string method = "tools/call";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"ping\",\"arguments\":{}}}";

                _context.PrintRequest(Module, method, new { name = "ping" });
                HttpResult result = client.PostRaw(body, _sessionId, "2024-11-05");
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 200)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 200, got {result.StatusCode}");
                    return;
                }

                using (JsonDocument document = JsonDocument.Parse(result.Body))
                {
                    JsonElement r = document.RootElement.GetProperty("result");

                    if (!r.TryGetProperty("content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "'content' key (camelCase) missing or empty");
                        return;
                    }

                    if (!r.TryGetProperty("isError", out _))
                    {
                        _context.RecordFail(Module, method, "'isError' key (camelCase) missing");
                        return;
                    }

                    string type = content[0].GetProperty("type").GetString();
                    string text = content[0].GetProperty("text").GetString();

                    _context.RecordPass(Module, method, $"content[0].type={type}, text={text}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSessionRequired(McpApiClient client)
        {
            const string method = "tools/list (no session)";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/list\"}";

                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.PostRaw(body);
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 400)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 400, got {result.StatusCode}");
                    return;
                }

                _context.RecordPass(Module, method, "HTTP 400 for missing session");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestUnknownSession(McpApiClient client)
        {
            const string method = "tools/list (unknown session)";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/list\"}";

                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.PostRaw(body, "no-such-session-12345", "2024-11-05");
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 404)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 404, got {result.StatusCode}");
                    return;
                }

                _context.RecordPass(Module, method, "HTTP 404 for unknown session");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestInvalidVersionHeader(McpApiClient client)
        {
            const string method = "tools/list (bad version)";

            try
            {
                string body = "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/list\"}";

                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.PostRaw(body, _sessionId, "9999-99-99");
                _context.PrintResponse(result.Body);

                if (result.StatusCode != 400)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 400, got {result.StatusCode}");
                    return;
                }

                _context.RecordPass(Module, method, "HTTP 400 for unsupported MCP-Protocol-Version");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetSse(McpApiClient client)
        {
            const string method = "GET";

            try
            {
                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.GetRaw(_sessionId);
                _context.PrintResponse($"HTTP {result.StatusCode}, Content-Type: {result.ContentType}");

                if (result.StatusCode != 200 || !result.ContentType.Contains("text/event-stream"))
                {
                    _context.RecordFail(Module, method, $"expected 200 text/event-stream, got {result.StatusCode} {result.ContentType}");
                    return;
                }

                _context.RecordPass(Module, method, "HTTP 200 text/event-stream");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestServerEventDelivery(McpApiClient client)
        {
            const string method = "server->client event";

            try
            {
                // отдельная сессия с заявленной поддержкой logging
                string initBody = "{\"jsonrpc\":\"2.0\",\"id\":20,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"logging\":{}},\"clientInfo\":{\"name\":\"stand-v2\",\"version\":\"1.0\"}}}";

                HttpResult init = client.PostRaw(initBody);

                if (init.StatusCode != 200 || string.IsNullOrEmpty(init.SessionId))
                {
                    _context.RecordFail(Module, method, "failed to initialize a logging-capable session");
                    return;
                }

                string session = init.SessionId;

                client.PostRaw("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", session);

                List<string> frames = new List<string>();
                object framesLocker = new object();
                Exception? readerError = null;
                ManualResetEvent ready = new ManualResetEvent(false);

                Thread reader = new Thread(() =>
                {
                    try
                    {
                        client.ReadSseData(session, TimeSpan.FromSeconds(8), ready, frame =>
                        {
                            lock (framesLocker)
                            {
                                frames.Add(frame);
                            }
                        });
                    }
                    catch (Exception error)
                    {
                        readerError = error;
                        ready.Set();
                    }
                });
                reader.IsBackground = true;
                reader.Start();

                if (!ready.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    _context.RecordFail(Module, method, "GET stream did not become ready in time");
                    return;
                }

                // тогглим настройку туда-обратно: хотя бы одно изменение гарантированно вызовет событие
                string callFalse = "{\"jsonrpc\":\"2.0\",\"id\":21,\"method\":\"tools/call\",\"params\":{\"name\":\"prime_settings_set\",\"arguments\":{\"reportCriticalErrors\":false}}}";
                string callTrue = "{\"jsonrpc\":\"2.0\",\"id\":22,\"method\":\"tools/call\",\"params\":{\"name\":\"prime_settings_set\",\"arguments\":{\"reportCriticalErrors\":true}}}";
                client.PostRaw(callFalse, session, "2024-11-05");
                client.PostRaw(callTrue, session, "2024-11-05");

                // даём событиям дойти до стрима
                Thread.Sleep(2500);

                bool found = false;

                lock (framesLocker)
                {
                    foreach (string frame in frames)
                    {
                        using (JsonDocument document = JsonDocument.Parse(frame))
                        {
                            if (document.RootElement.TryGetProperty("method", out JsonElement methodElement)
                                && methodElement.GetString() == "notifications/message"
                                && document.RootElement.TryGetProperty("params", out JsonElement parameters)
                                && parameters.TryGetProperty("data", out JsonElement data)
                                && data.TryGetProperty("event", out JsonElement eventName)
                                && eventName.GetString() == "prime_settings.changed")
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, method, $"no notifications/message for prime_settings.changed; frames={frames.Count}, readerError={readerError?.Message}");
                    return;
                }

                _context.RecordPass(Module, method, "notifications/message delivered on GET stream (event: message)");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDeleteSession(McpApiClient client)
        {
            const string method = "DELETE";

            try
            {
                _context.PrintRequest(Module, method, new { });
                HttpResult result = client.DeleteRaw(_sessionId);
                _context.PrintResponse($"HTTP {result.StatusCode}");

                if (result.StatusCode != 200)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 200, got {result.StatusCode}");
                    return;
                }

                string body = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/list\"}";
                HttpResult reuse = client.PostRaw(body, _sessionId, "2024-11-05");

                if (reuse.StatusCode != 404)
                {
                    _context.RecordFail(Module, method, $"expected HTTP 404 after delete, got {reuse.StatusCode}");
                    return;
                }

                _context.RecordPass(Module, method, "session terminated, reused id -> 404");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }
    }
}
