/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for compare positions tools (compare_positions_*).
    /// Synchronization is tested only on error paths with fake names —
    /// real orders must never be sent from the test stand.
    /// </summary>
    public class ComparePositionsTests
    {
        private const string Module = "COMPAREPOSITIONS";
        private readonly TestContext _context;
        private string _serverType = string.Empty;

        private string _originalPeriod = string.Empty;
        private int _originalDelay;
        private JsonElement _originalWatch;
        private JsonElement _originalIgnored;

        public ComparePositionsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            _serverType = !string.IsNullOrWhiteSpace(_context.Secrets.ConnectorType)
                ? _context.Secrets.ConnectorType
                : "Binance";

            ActivateConnector();

            try
            {
                if (!TestGetSettings())
                {
                    return;
                }

                TestGet();
                TestSetSettings();
                TestSetIgnored();
                TestSyncErrors();
            }
            finally
            {
                RestoreSettings();
            }
        }

        private void ActivateConnector()
        {
            const string method = "server_management_activate";
            object request = new { type = _serverType };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGet()
        {
            const string method = "compare_positions_get";
            object request = new { server_type = _serverType, number = 0 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("portfolios", out JsonElement portfolios)
                    || portfolios.ValueKind != JsonValueKind.Array
                    || !config.TryGetProperty("count", out _))
                {
                    _context.RecordFail(Module, method, "portfolios array missing");
                    return;
                }

                _context.RecordPass(Module, method, $"portfolios={config.GetProperty("count").GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool TestGetSettings()
        {
            const string method = "compare_positions_get_settings";
            object request = new { server_type = _serverType, number = 0 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                string[] requiredFields = new[]
                {
                    "verification_period", "time_delay_seconds", "portfolios_to_watch", "ignored_securities"
                };

                foreach (string field in requiredFields)
                {
                    if (!config.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, method, $"{field} missing");
                        return false;
                    }
                }

                _originalPeriod = config.GetProperty("verification_period").GetString() ?? "Min5";
                _originalDelay = config.GetProperty("time_delay_seconds").GetInt32();
                _originalWatch = config.GetProperty("portfolios_to_watch").Clone();
                _originalIgnored = config.GetProperty("ignored_securities").Clone();

                _context.RecordPass(Module, method, "settings received");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void TestSetSettings()
        {
            const string method = "compare_positions_set_settings";
            object request = new
            {
                server_type = _serverType,
                number = 0,
                verification_period = "Min10",
                time_delay_seconds = 42
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

                if (config.GetProperty("verification_period").GetString() != "Min10"
                    || config.GetProperty("time_delay_seconds").GetInt32() != 42)
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

        private void TestSetIgnored()
        {
            const string method = "compare_positions_set_ignored";
            object request = new
            {
                server_type = _serverType,
                number = 0,
                securities = new[] { "SBER", "GAZP" }
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

                if (config.GetProperty("ignored_securities").GetArrayLength() != 2)
                {
                    _context.RecordFail(Module, method, "ignored list was not applied");
                    return;
                }

                _context.RecordPass(Module, method, "ignored list updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSyncErrors()
        {
            const string syncAllMethod = "compare_positions_sync_all";
            const string syncThisMethod = "compare_positions_sync_this";

            try
            {
                // несуществующий портфель — должна быть ошибка, ордеров не будет
                object syncAllRequest = new
                {
                    server_type = _serverType,
                    number = 0,
                    portfolio_name = "MCP_FAKE_PORTFOLIO"
                };

                _context.PrintRequest(Module, syncAllMethod, syncAllRequest);
                string syncAllResponse = _context.Client.ToolsCall(syncAllMethod, syncAllRequest);
                _context.PrintResponse(syncAllResponse);

                if (!ExpectIsError(syncAllResponse, syncAllMethod))
                {
                    return;
                }

                object syncThisRequest = new
                {
                    server_type = _serverType,
                    number = 0,
                    portfolio_name = "MCP_FAKE_PORTFOLIO",
                    security_name = "MCP_FAKE_SECURITY"
                };

                _context.PrintRequest(Module, syncThisMethod, syncThisRequest);
                string syncThisResponse = _context.Client.ToolsCall(syncThisMethod, syncThisRequest);
                _context.PrintResponse(syncThisResponse);

                if (!ExpectIsError(syncThisResponse, syncThisMethod))
                {
                    return;
                }

                // несуществующий тип сервера
                object badServerRequest = new
                {
                    server_type = "MCP_FAKE_SERVER",
                    portfolio_name = "MCP_FAKE_PORTFOLIO"
                };

                string badServerResponse = _context.Client.ToolsCall(syncAllMethod, badServerRequest);

                if (!ExpectIsError(badServerResponse, syncAllMethod))
                {
                    return;
                }

                _context.RecordPass(Module, syncAllMethod, "sync error paths rejected with IsError");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, syncAllMethod, error.Message);
            }
        }

        private void RestoreSettings()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_serverType) || string.IsNullOrWhiteSpace(_originalPeriod))
                {
                    return;
                }

                JsonElement watch = _originalWatch;
                object[] watchArray = new object[watch.GetArrayLength()];

                for (int i = 0; i < watchArray.Length; i++)
                {
                    watchArray[i] = watch[i].GetString();
                }

                object settingsRequest = new
                {
                    server_type = _serverType,
                    number = 0,
                    verification_period = _originalPeriod,
                    time_delay_seconds = _originalDelay,
                    portfolios_to_watch = watchArray
                };

                _context.Client.ToolsCall("compare_positions_set_settings", settingsRequest);

                JsonElement ignored = _originalIgnored;
                object[] ignoredArray = new object[ignored.GetArrayLength()];

                for (int i = 0; i < ignoredArray.Length; i++)
                {
                    ignoredArray[i] = ignored[i].GetString();
                }

                object ignoredRequest = new
                {
                    server_type = _serverType,
                    number = 0,
                    securities = ignoredArray
                };

                _context.Client.ToolsCall("compare_positions_set_ignored", ignoredRequest);
            }
            catch
            {
                // восстановление настроек не должно ронять модуль
            }
        }

        private bool ExpectIsError(string response, string method)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || !isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "expected IsError, got success");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"ExpectIsError failed: {error.Message}");
                return false;
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
