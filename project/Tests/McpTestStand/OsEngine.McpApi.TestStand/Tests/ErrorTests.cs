/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for error handling and authorization.
    /// </summary>
    public class ErrorTests
    {
        private const string Module = "ERRORS";
        private readonly TestContext _context;

        public ErrorTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestUnauthorized();
            TestMethodNotFound();
            TestToolNotFound();
            TestInvalidParameters();
        }

        private void TestUnauthorized()
        {
            const string method = "POST /api/v1/mcp without key";

            try
            {
                _context.PrintRequest(Module, method, new { });

                using (var client = new HttpClient())
                {
                    var request = new
                    {
                        jsonrpc = "2.0",
                        method = "initialize",
                        @params = new { },
                        id = "1"
                    };

                    string json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = client.PostAsync($"{_context.Client.BaseUrl}/api/v1/mcp", content).GetAwaiter().GetResult())
                    {
                        string status = $"HTTP {(int)response.StatusCode}";
                        _context.PrintResponse(status);

                        if (response.StatusCode != HttpStatusCode.Unauthorized)
                        {
                            _context.RecordFail(Module, method, $"expected HTTP 401, got {status}");
                            return;
                        }

                        _context.RecordPass(Module, method, status);
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMethodNotFound()
        {
            const string method = "direct terminal_get_status";

            try
            {
                _context.PrintRequest(Module, method, new { });
                string response = _context.Client.SendRaw("terminal_get_status", new { });
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("error", out JsonElement errorElement) || errorElement.ValueKind == JsonValueKind.Null)
                    {
                        _context.RecordFail(Module, method, "expected error object");
                        return;
                    }

                    int code = errorElement.GetProperty("code").GetInt32();

                    if (code != -32601)
                    {
                        _context.RecordFail(Module, method, $"expected error code -32601, got {code}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"code={code}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestToolNotFound()
        {
            const string method = "tools/call unknown_tool";

            try
            {
                _context.PrintRequest(Module, method, new { name = "unknown_tool", arguments = new { } });
                string response = _context.Client.ToolsCall("unknown_tool", new { });
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || !isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "expected IsError == true");
                        return;
                    }

                    _context.RecordPass(Module, method, "IsError is true");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestInvalidParameters()
        {
            const string method = "tools/call without name";

            try
            {
                _context.PrintRequest(Module, method, new { arguments = new { } });
                string response = _context.Client.SendRaw("tools/call", new { arguments = new { } });
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("error", out JsonElement errorElement) || errorElement.ValueKind == JsonValueKind.Null)
                    {
                        _context.RecordFail(Module, method, "expected error object");
                        return;
                    }

                    _context.RecordPass(Module, method, "error returned");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }
    }
}
