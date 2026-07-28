/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for proxy router tools (proxy_*). Creates its own dead proxy
    /// (127.0.0.1) and deletes it at the end; existing proxies are not touched.
    /// </summary>
    public class ProxyTests
    {
        private const string Module = "PROXY";
        private readonly TestContext _context;
        private int _createdProxyNumber = -1;

        public ProxyTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            try
            {
                if (!TestCreate())
                {
                    return;
                }

                TestGetList();
                TestGetSettings();
                TestSetSettings();
                TestGetStatus();
                TestPing();
                TestGetSettingsNotFound();
                TestDelete();
            }
            finally
            {
                DeleteCreatedProxy();
            }
        }

        private bool TestCreate()
        {
            const string method = "proxy_create";
            object request = new
            {
                is_on = false,
                ip = "127.0.0.1",
                port = 9,
                login = "mcpuser",
                password = "mcpsecretpassword"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                if (!config.TryGetProperty("number", out JsonElement number))
                {
                    _context.RecordFail(Module, method, "created proxy number missing");
                    return false;
                }

                _createdProxyNumber = number.GetInt32();

                string password = config.GetProperty("password").GetString() ?? string.Empty;

                if (password == "mcpsecretpassword")
                {
                    _context.RecordFail(Module, method, "password is not masked");
                    return false;
                }

                _context.RecordPass(Module, method, $"number={_createdProxyNumber}");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void TestGetList()
        {
            const string method = "proxy_get_list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                bool found = false;

                foreach (JsonElement proxy in config.GetProperty("proxies").EnumerateArray())
                {
                    if (proxy.GetProperty("password").GetString() == "mcpsecretpassword")
                    {
                        _context.RecordFail(Module, method, "password is not masked in the list");
                        return;
                    }

                    if (proxy.GetProperty("number").GetInt32() == _createdProxyNumber)
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, method, "created proxy not found in the list");
                    return;
                }

                _context.RecordPass(Module, method, $"count={config.GetProperty("count").GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetSettings()
        {
            const string method = "proxy_get_settings";
            object request = new { number = _createdProxyNumber };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "number", "is_on", "ip", "port", "login", "password",
                    "location", "auto_ping_last_status", "use_connection_count", "ping_web_address"
                };

                foreach (string field in requiredFields)
                {
                    if (!config.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, method, $"{field} missing");
                        return;
                    }
                }

                if (config.GetProperty("ip").GetString() != "127.0.0.1"
                    || config.GetProperty("port").GetInt32() != 9
                    || config.GetProperty("login").GetString() != "mcpuser")
                {
                    _context.RecordFail(Module, method, "settings mismatch");
                    return;
                }

                _context.RecordPass(Module, method, "settings received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSetSettings()
        {
            const string method = "proxy_set_settings";
            object request = new
            {
                number = _createdProxyNumber,
                port = 10,
                login = "mcpuser2"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (config.GetProperty("port").GetInt32() != 10
                    || config.GetProperty("login").GetString() != "mcpuser2"
                    || config.GetProperty("ip").GetString() != "127.0.0.1")
                {
                    _context.RecordFail(Module, method, "settings were not applied");
                    return;
                }

                _context.RecordPass(Module, method, "settings updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetStatus()
        {
            const string method = "proxy_get_status";
            object request = new { number = _createdProxyNumber };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("auto_ping_last_status", out _)
                    || !config.TryGetProperty("location", out _)
                    || !config.TryGetProperty("use_connection_count", out _))
                {
                    _context.RecordFail(Module, method, "status response is incomplete");
                    return;
                }

                _context.RecordPass(Module, method, "status received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestPing()
        {
            const string method = "proxy_ping";
            object request = new { number = _createdProxyNumber };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                // прокси 127.0.0.1:10 гарантированно мёртв — пинг не должен пройти,
                // но ответ обязан иметь статус
                string status = config.GetProperty("auto_ping_last_status").GetString() ?? string.Empty;

                if (status.Length == 0 || status == "Connect")
                {
                    _context.RecordFail(Module, method, $"unexpected ping status on dead proxy: {status}");
                    return;
                }

                _context.RecordPass(Module, method, $"status={status}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetSettingsNotFound()
        {
            const string method = "proxy_get_settings";
            object request = new { number = 99999 };

            try
            {
                _context.PrintRequest(Module, $"{method}(not_found)", request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || !isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "expected IsError for unknown proxy");
                        return;
                    }
                }

                _context.RecordPass(Module, method, "unknown proxy rejected");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDelete()
        {
            const string method = "proxy_delete";
            object request = new { number = _createdProxyNumber };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (config.GetProperty("deleted").GetBoolean() != true)
                {
                    _context.RecordFail(Module, method, "proxy was not deleted");
                    return;
                }

                object listRequest = new { };
                string listResponse = _context.Client.ToolsCall("proxy_get_list", listRequest);

                if (TryParseConfig(listResponse, "proxy_get_list", out JsonElement listConfig))
                {
                    foreach (JsonElement proxy in listConfig.GetProperty("proxies").EnumerateArray())
                    {
                        if (proxy.GetProperty("number").GetInt32() == _createdProxyNumber)
                        {
                            _context.RecordFail(Module, method, "proxy remains in the list after delete");
                            return;
                        }
                    }
                }

                _createdProxyNumber = -1;

                _context.RecordPass(Module, method, "proxy deleted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void DeleteCreatedProxy()
        {
            try
            {
                if (_createdProxyNumber >= 0)
                {
                    _context.Client.ToolsCall("proxy_delete", new { number = _createdProxyNumber });
                }
            }
            catch
            {
                // очистка не должна ронять модуль
            }
        }

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            using (var document = JsonDocument.Parse(response))
            {
                JsonElement result = document.RootElement;

                if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                {
                    _context.RecordFail(Module, method, "IsError is true");
                    return false;
                }

                if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "Content is empty");
                    return false;
                }

                string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    _context.RecordFail(Module, method, "Content text is empty");
                    return false;
                }

                using (var innerDocument = JsonDocument.Parse(text))
                {
                    config = innerDocument.RootElement.Clone();
                    return true;
                }
            }
        }
    }
}
