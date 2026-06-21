/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for Anthropic MCP protocol handshake and tool discovery.
    /// </summary>
    public class ProtocolTests
    {
        private const string Module = "PROTOCOL";
        private readonly TestContext _context;

        public ProtocolTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestInitialize();
            TestNotificationsInitialized();
            TestToolsList();
        }

        private void TestInitialize()
        {
            const string method = "initialize";
            object request = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-stand", version = "1.0.0" }
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.SendRequest(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("protocolVersion", out JsonElement version))
                    {
                        _context.RecordFail(Module, method, "protocolVersion missing");
                        return;
                    }

                    if (!result.TryGetProperty("serverInfo", out JsonElement serverInfo))
                    {
                        _context.RecordFail(Module, method, "serverInfo missing");
                        return;
                    }

                    string versionText = version.GetString() ?? string.Empty;
                    string serverName = serverInfo.GetProperty("name").GetString() ?? string.Empty;

                    if (versionText != "2024-11-05")
                    {
                        _context.RecordFail(Module, method, $"unexpected protocolVersion: {versionText}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"version={versionText}, server={serverName}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestNotificationsInitialized()
        {
            const string method = "notifications/initialized";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);

                using (HttpResponseMessage response = _context.Client.SendNotificationRaw(method, request))
                {
                    string status = $"HTTP {(int)response.StatusCode}";
                    _context.PrintResponse(status);

                    if (response.StatusCode != HttpStatusCode.Accepted)
                    {
                        _context.RecordFail(Module, method, $"expected HTTP 202, got {status}");
                        return;
                    }

                    _context.RecordPass(Module, method, status);
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestToolsList()
        {
            const string method = "tools/list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.SendRequest(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("Tools", out JsonElement tools) || tools.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Tools array is empty or missing");
                        return;
                    }

                    string[] toolNames = tools.EnumerateArray()
                        .Select(t => t.GetProperty("name").GetString() ?? string.Empty)
                        .ToArray();

                    if (!toolNames.Contains("ping"))
                    {
                        _context.RecordFail(Module, method, "ping tool is missing");
                        return;
                    }

                    if (!toolNames.Contains("terminal_get_status"))
                    {
                        _context.RecordFail(Module, method, "terminal_get_status tool is missing");
                        return;
                    }

                    _context.RecordPass(Module, method, $"tools count={tools.GetArrayLength()}");
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
