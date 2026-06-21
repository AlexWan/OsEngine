/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for Server-Sent Events endpoint.
    /// </summary>
    public class SseTests
    {
        private const string Module = "SSE";
        private readonly TestContext _context;

        public SseTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestConnect();
        }

        private void TestConnect()
        {
            const string method = "GET /api/v1/events";

            try
            {
                _context.PrintRequest(Module, method, new { });

                using (HttpResponseMessage response = _context.Client.GetSseResponse())
                {
                    string status = $"HTTP {(int)response.StatusCode}, Content-Type: {response.Content.Headers.ContentType}";
                    _context.PrintResponse(status);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        _context.RecordFail(Module, method, $"expected HTTP 200, got {status}");
                        return;
                    }

                    string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    if (!contentType.Contains("text/event-stream"))
                    {
                        _context.RecordFail(Module, method, $"unexpected Content-Type: {contentType}");
                        return;
                    }

                    using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        bool gotLaunched = false;
                        bool gotHeartbeat = false;
                        DateTime deadline = DateTime.Now.AddSeconds(10);

                        string eventName = string.Empty;
                        string data = string.Empty;

                        while (DateTime.Now < deadline)
                        {
                            if (reader.EndOfStream)
                            {
                                Thread.Sleep(50);
                                continue;
                            }

                            string? line = reader.ReadLine();

                            if (line == null)
                            {
                                break;
                            }

                            if (line.StartsWith("event: "))
                            {
                                eventName = line.Substring("event: ".Length).Trim();
                            }
                            else if (line.StartsWith("data: "))
                            {
                                data = line.Substring("data: ".Length).Trim();
                            }
                            else if (string.IsNullOrEmpty(line))
                            {
                                if (!string.IsNullOrEmpty(eventName) && !string.IsNullOrEmpty(data))
                                {
                                    if (eventName == "terminal.launched")
                                    {
                                        gotLaunched = ValidateLaunchedEvent(data, out string launchedError);
                                        if (!gotLaunched)
                                        {
                                            _context.RecordFail(Module, $"{method} terminal.launched", launchedError);
                                            return;
                                        }
                                    }
                                    else if (eventName == "heartbeat")
                                    {
                                        gotHeartbeat = true;
                                    }

                                    if (gotLaunched && gotHeartbeat)
                                    {
                                        break;
                                    }
                                }

                                eventName = string.Empty;
                                data = string.Empty;
                            }
                        }

                        if (!gotLaunched)
                        {
                            _context.RecordFail(Module, method, "terminal.launched event not received");
                            return;
                        }

                        if (!gotHeartbeat)
                        {
                            _context.RecordFail(Module, method, "heartbeat event not received within 10 seconds");
                            return;
                        }

                        _context.RecordPass(Module, method, "received terminal.launched and heartbeat");
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private static bool ValidateLaunchedEvent(string data, out string error)
        {
            error = string.Empty;

            try
            {
                using (var document = JsonDocument.Parse(data))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("event", out JsonElement eventElement) || eventElement.GetString() != "terminal.launched")
                    {
                        error = "event field missing or invalid";
                        return false;
                    }

                    if (!root.TryGetProperty("payload", out JsonElement payload))
                    {
                        error = "payload missing";
                        return false;
                    }

                    if (!payload.TryGetProperty("mode", out _))
                    {
                        error = "mode missing in payload";
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = $"failed to parse event data: {ex.Message}";
                return false;
            }
        }
    }
}
