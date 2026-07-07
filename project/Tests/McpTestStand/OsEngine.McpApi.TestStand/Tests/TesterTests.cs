/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for Tester configuration tools (stages 10.1–10.5).
    /// Covers data, execution, portfolio configuration, robot management and parameters.
    /// </summary>
    public class TesterTests
    {
        private const string Module = "TESTER";
        private const string TestSetName = "McpReleaseSet";

        private readonly TestContext _context;

        public TesterTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestOpenMode();
            TestDataConfiguration();
            TestExecutionConfiguration();
            TestPortfolioConfiguration();
            TestRobotManagement();
            TestRunSimulation();
            TestJournalData();
            TestScreenerConfiguration();
            TestIndexArbitrage();
        }

        private void TestOpenMode()
        {
            const string method = "terminal_open_mode";
            object request = new { mode = "tester" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Opening mode tester"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Opening mode tester'");
                    return;
                }

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDataConfiguration()
        {
            if (!WaitForTesterServer())
            {
                return;
            }

            TestDataGetAvailableSets();
            TestDataGetConfig();
            TestDataSetConfig();
        }

        private void TestExecutionConfiguration()
        {
            TestExecutionGetConfig();
            TestExecutionSetConfig();
            CleanupExecutionConfig();
        }

        private void TestPortfolioConfiguration()
        {
            TestPortfolioGetConfig();
            TestPortfolioSetConfig();
        }

        private void TestRobotManagement()
        {
            TestGetBotsInitial();
            TestBotCreate();
            TestBotGetParams();
            TestBotSetParams();
            TestBotGetSources();
            TestBotConfigTabSimple();
            TestJournalSettings();
            TestGetBotsAfterCreate();
            TestBotDelete();
            TestGetBotsAfterDelete();
        }

        private bool WaitForTesterServer()
        {
            const string method = "tester_data_get_config";
            DateTime deadline = DateTime.Now.AddSeconds(10);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall(method, new { });

                    using (var document = JsonDocument.Parse(response))
                    {
                        JsonElement result = document.RootElement;

                        if (result.TryGetProperty("IsError", out JsonElement isError) && !isError.GetBoolean())
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // ignore and retry
                }

                Thread.Sleep(500);
            }

            _context.RecordFail(Module, method, "tester server is not available after open_mode");
            return false;
        }

        private void TestDataGetAvailableSets()
        {
            const string method = "tester_data_get_available_sets";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement sets))
                {
                    return;
                }

                if (sets.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "response is not an array");
                    return;
                }

                if (sets.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "available sets list is empty");
                    return;
                }

                bool foundTestSet = false;
                foreach (JsonElement item in sets.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() == TestSetName)
                    {
                        foundTestSet = true;
                        break;
                    }
                }

                if (!foundTestSet)
                {
                    _context.RecordFail(Module, method, $"expected set '{TestSetName}' not found");
                    return;
                }

                _context.RecordPass(Module, method, $"sets_count={sets.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDataGetConfig()
        {
            const string method = "tester_data_get_config";
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

                if (!config.TryGetProperty("source_type", out _))
                {
                    _context.RecordFail(Module, method, "source_type missing");
                    return;
                }

                if (!config.TryGetProperty("type_tester_data", out _))
                {
                    _context.RecordFail(Module, method, "type_tester_data missing");
                    return;
                }

                if (!config.TryGetProperty("date_from", out _))
                {
                    _context.RecordFail(Module, method, "date_from missing");
                    return;
                }

                if (!config.TryGetProperty("date_to", out _))
                {
                    _context.RecordFail(Module, method, "date_to missing");
                    return;
                }

                if (!config.TryGetProperty("delete_trades_from_memory", out _))
                {
                    _context.RecordFail(Module, method, "delete_trades_from_memory missing");
                    return;
                }

                _context.RecordPass(Module, method, "config received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDataSetConfig()
        {
            const string method = "tester_data_set_config";

            object request = new
            {
                source_type = "Set",
                set_name = TestSetName,
                type_tester_data = "Candle",
                date_from = "2024-01-01T00:00:00",
                date_to = "2024-06-30T00:00:00",
                delete_trades_from_memory = true
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

                if (!config.TryGetProperty("source_type", out JsonElement sourceType)
                    || sourceType.GetString() != "Set")
                {
                    _context.RecordFail(Module, method, "source_type mismatch");
                    return;
                }

                if (!config.TryGetProperty("set_name", out JsonElement setName)
                    || setName.GetString() != TestSetName)
                {
                    _context.RecordFail(Module, method, "set_name mismatch");
                    return;
                }

                if (!config.TryGetProperty("type_tester_data", out JsonElement typeTesterData)
                    || typeTesterData.GetString() != "Candle")
                {
                    _context.RecordFail(Module, method, "type_tester_data mismatch");
                    return;
                }

                if (!config.TryGetProperty("date_from", out JsonElement dateFrom))
                {
                    _context.RecordFail(Module, method, "date_from mismatch");
                    return;
                }

                string dateFromValue = dateFrom.GetString() ?? string.Empty;
                if (!dateFromValue.StartsWith("2024-01-01"))
                {
                    _context.RecordFail(Module, method, "date_from mismatch");
                    return;
                }

                if (!config.TryGetProperty("date_to", out JsonElement dateTo))
                {
                    _context.RecordFail(Module, method, "date_to mismatch");
                    return;
                }

                string dateToValue = dateTo.GetString() ?? string.Empty;
                if (!dateToValue.StartsWith("2024-06-30"))
                {
                    _context.RecordFail(Module, method, "date_to mismatch");
                    return;
                }

                if (!config.TryGetProperty("delete_trades_from_memory", out JsonElement deleteTrades)
                    || !deleteTrades.GetBoolean())
                {
                    _context.RecordFail(Module, method, "delete_trades_from_memory mismatch");
                    return;
                }

                _context.RecordPass(Module, method, "config set and verified");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestExecutionGetConfig()
        {
            const string method = "tester_execution_get_config";
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

                if (!config.TryGetProperty("slippage_to_simple_order", out _))
                {
                    _context.RecordFail(Module, method, "slippage_to_simple_order missing");
                    return;
                }

                if (!config.TryGetProperty("slippage_to_stop_order", out _))
                {
                    _context.RecordFail(Module, method, "slippage_to_stop_order missing");
                    return;
                }

                if (!config.TryGetProperty("order_execution_type", out _))
                {
                    _context.RecordFail(Module, method, "order_execution_type missing");
                    return;
                }

                if (!config.TryGetProperty("non_trade_periods", out _))
                {
                    _context.RecordFail(Module, method, "non_trade_periods missing");
                    return;
                }

                _context.RecordPass(Module, method, "config received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestExecutionSetConfig()
        {
            const string method = "tester_execution_set_config";

            object request = new
            {
                slippage_to_simple_order = 5,
                slippage_to_stop_order = 10,
                order_execution_type = "Intersection",
                non_trade_periods = new[]
                {
                    new
                    {
                        date_start = "2024-01-02T10:00:00",
                        date_end = "2024-01-02T11:00:00",
                        is_on = true
                    }
                }
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

                if (!config.TryGetProperty("slippage_to_simple_order", out JsonElement slippageSimple)
                    || slippageSimple.GetInt32() != 5)
                {
                    _context.RecordFail(Module, method, "slippage_to_simple_order mismatch");
                    return;
                }

                if (!config.TryGetProperty("slippage_to_stop_order", out JsonElement slippageStop)
                    || slippageStop.GetInt32() != 10)
                {
                    _context.RecordFail(Module, method, "slippage_to_stop_order mismatch");
                    return;
                }

                if (!config.TryGetProperty("order_execution_type", out JsonElement orderExecutionType)
                    || orderExecutionType.GetString() != "Intersection")
                {
                    _context.RecordFail(Module, method, "order_execution_type mismatch");
                    return;
                }

                if (!config.TryGetProperty("non_trade_periods", out JsonElement periods)
                    || periods.GetArrayLength() != 1)
                {
                    _context.RecordFail(Module, method, "non_trade_periods mismatch");
                    return;
                }

                _context.RecordPass(Module, method, "config set and verified");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void CleanupExecutionConfig()
        {
            const string method = "tester_execution_set_config";

            object request = new
            {
                slippage_to_simple_order = 0,
                slippage_to_stop_order = 0,
                order_execution_type = "Touch",
                non_trade_periods = new object[0]
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "cleanup failed: could not parse config response");
                    return;
                }

                if (!config.TryGetProperty("slippage_to_stop_order", out JsonElement slippageStop)
                    || slippageStop.GetInt32() != 0)
                {
                    _context.RecordFail(Module, method, "cleanup failed: slippage_to_stop_order not zero");
                    return;
                }

                if (!config.TryGetProperty("order_execution_type", out JsonElement orderExecutionType)
                    || orderExecutionType.GetString() != "Touch")
                {
                    _context.RecordFail(Module, method, "cleanup failed: order_execution_type not Touch");
                    return;
                }

                if (!config.TryGetProperty("non_trade_periods", out JsonElement periods)
                    || periods.GetArrayLength() != 0)
                {
                    _context.RecordFail(Module, method, "cleanup failed: non_trade_periods not empty");
                    return;
                }

                _context.RecordPass(Module, method, "execution config cleaned up");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestPortfolioGetConfig()
        {
            const string method = "tester_portfolio_get_config";
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

                if (!config.TryGetProperty("start_portfolio", out _))
                {
                    _context.RecordFail(Module, method, "start_portfolio missing");
                    return;
                }

                if (!config.TryGetProperty("portfolio_calculation_enabled", out _))
                {
                    _context.RecordFail(Module, method, "portfolio_calculation_enabled missing");
                    return;
                }

                _context.RecordPass(Module, method, "config received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestPortfolioSetConfig()
        {
            const string method = "tester_portfolio_set_config";

            object request = new
            {
                start_portfolio = 1000000m,
                portfolio_calculation_enabled = true
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

                if (!config.TryGetProperty("start_portfolio", out JsonElement startPortfolio)
                    || startPortfolio.GetDecimal() != 1000000m)
                {
                    _context.RecordFail(Module, method, "start_portfolio mismatch");
                    return;
                }

                if (!config.TryGetProperty("portfolio_calculation_enabled", out JsonElement portfolioCalc)
                    || !portfolioCalc.GetBoolean())
                {
                    _context.RecordFail(Module, method, "portfolio_calculation_enabled mismatch");
                    return;
                }

                _context.RecordPass(Module, method, "config set and verified");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private const string TestStrategyName = "TwoTimeFramesBot";
        private string _createdBotName = string.Empty;

        private void TestGetBotsInitial()
        {
            const string method = "bot_get_list";
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

                if (!config.TryGetProperty("bots", out JsonElement bots))
                {
                    _context.RecordFail(Module, method, "bots property missing");
                    return;
                }

                if (bots.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "bots is not an array");
                    return;
                }

                _context.RecordPass(Module, method, $"count={bots.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotCreate()
        {
            const string method = "bot_create";
            object request = new { strategy_name = TestStrategyName };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("number", out JsonElement number))
                {
                    _context.RecordFail(Module, method, "number missing");
                    return;
                }

                if (!config.TryGetProperty("class_name", out JsonElement className)
                    || className.GetString() != TestStrategyName)
                {
                    _context.RecordFail(Module, method, "class_name mismatch");
                    return;
                }

                if (!config.TryGetProperty("name", out JsonElement name))
                {
                    _context.RecordFail(Module, method, "name missing");
                    return;
                }

                _createdBotName = name.GetString() ?? string.Empty;
                _context.RecordPass(Module, method, $"number={number.GetInt32()}, name={_createdBotName}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotGetParams()
        {
            const string method = "bot_get_params";
            object request = new { bot_id = _createdBotName };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("parameters", out JsonElement parameters))
                {
                    _context.RecordFail(Module, method, "parameters property missing");
                    return;
                }

                if (parameters.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "parameters is not an array");
                    return;
                }

                _context.RecordPass(Module, method, $"count={parameters.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotSetParams()
        {
            const string method = "bot_set_params";
            const string paramName = "PC length";
            const int newValue = 25;

            object request = new
            {
                bot_id = _createdBotName,
                parameters = new Dictionary<string, object>
                {
                    { paramName, newValue }
                }
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

                if (!config.TryGetProperty("updated_count", out JsonElement updatedCount)
                    || updatedCount.GetInt32() != 1)
                {
                    _context.RecordFail(Module, method, "updated_count mismatch");
                    return;
                }

                // Verify the parameter was actually changed
                string verifyResponse = _context.Client.ToolsCall("bot_get_params", new { bot_id = _createdBotName });
                if (!TryParseConfig(verifyResponse, "bot_get_params", out JsonElement verifyConfig))
                {
                    _context.RecordFail(Module, method, "failed to verify parameter change");
                    return;
                }

                if (!verifyConfig.TryGetProperty("parameters", out JsonElement parameters))
                {
                    _context.RecordFail(Module, method, "parameters missing in verify response");
                    return;
                }

                bool found = false;
                foreach (JsonElement param in parameters.EnumerateArray())
                {
                    if (param.TryGetProperty("name", out JsonElement name)
                        && name.GetString() == paramName)
                    {
                        found = true;
                        if (!param.TryGetProperty("value", out JsonElement value)
                            || value.GetInt32() != newValue)
                        {
                            _context.RecordFail(Module, method, $"parameter '{paramName}' value mismatch");
                            return;
                        }
                        break;
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, method, $"parameter '{paramName}' not found");
                    return;
                }

                _context.RecordPass(Module, method, $"{paramName}={newValue}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotGetSources()
        {
            const string createMethod = "bot_create";
            const string getSourcesMethod = "bot_get_sources";
            const string deleteMethod = "bot_delete";
            const string allSourcesStrategy = "AllSourcesInOneSample";
            string? allSourcesBotName = null;

            try
            {
                // Create a robot with all source types
                object createRequest = new { strategy_name = allSourcesStrategy };
                _context.PrintRequest(Module, createMethod, createRequest);
                string createResponse = _context.Client.ToolsCall(createMethod, createRequest);
                _context.PrintResponse(createResponse);

                if (!TryParseConfig(createResponse, createMethod, out JsonElement createConfig))
                {
                    _context.RecordFail(Module, getSourcesMethod, "failed to create AllSourcesInOneSample");
                    return;
                }

                allSourcesBotName = createConfig.GetProperty("name").GetString() ?? string.Empty;

                // Get sources
                object request = new { bot_id = allSourcesBotName };
                _context.PrintRequest(Module, getSourcesMethod, request);
                string response = _context.Client.ToolsCall(getSourcesMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, getSourcesMethod, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("sources", out JsonElement sources))
                {
                    _context.RecordFail(Module, getSourcesMethod, "sources property missing");
                    return;
                }

                if (sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, getSourcesMethod, "sources is not an array");
                    return;
                }

                HashSet<string> expectedTypes = new HashSet<string>
                {
                    "Simple", "Index", "Pair", "Screener", "Polygon", "Cluster", "News"
                };

                HashSet<string> actualTypes = new HashSet<string>();
                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type))
                    {
                        actualTypes.Add(type.GetString() ?? string.Empty);
                    }
                }

                if (!expectedTypes.SetEquals(actualTypes))
                {
                    string missing = string.Join(", ", expectedTypes.Except(actualTypes));
                    string extra = string.Join(", ", actualTypes.Except(expectedTypes));
                    _context.RecordFail(Module, getSourcesMethod, $"types mismatch. Missing: [{missing}], Extra: [{extra}]");
                    return;
                }

                if (sources.GetArrayLength() != expectedTypes.Count)
                {
                    _context.RecordFail(Module, getSourcesMethod, $"expected {expectedTypes.Count} sources, got {sources.GetArrayLength()}");
                    return;
                }

                _context.RecordPass(Module, getSourcesMethod, $"count={sources.GetArrayLength()}, types=[{string.Join(", ", actualTypes)}]");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, getSourcesMethod, error.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(allSourcesBotName))
                {
                    try
                    {
                        object deleteRequest = new { bot_id = allSourcesBotName };
                        _context.PrintRequest(Module, deleteMethod, deleteRequest);
                        string deleteResponse = _context.Client.ToolsCall(deleteMethod, deleteRequest);
                        _context.PrintResponse(deleteResponse);
                    }
                    catch (Exception cleanupError)
                    {
                        _context.RecordFail(Module, getSourcesMethod, $"cleanup failed: {cleanupError.Message}");
                    }
                }
            }
        }

        private void TestBotConfigTabSimple()
        {
            const string getSourcesMethod = "bot_get_sources";
            const string getMethod = "bot_get_config_tab_simple";
            const string setMethod = "bot_set_config_tab_simple";

            try
            {
                // Find first Simple tab name
                object sourcesRequest = new { bot_id = _createdBotName };
                _context.PrintRequest(Module, getSourcesMethod, sourcesRequest);
                string sourcesResponse = _context.Client.ToolsCall(getSourcesMethod, sourcesRequest);
                _context.PrintResponse(sourcesResponse);

                if (!TryParseConfig(sourcesResponse, getSourcesMethod, out JsonElement sourcesConfig))
                {
                    _context.RecordFail(Module, getMethod, "failed to get sources");
                    return;
                }

                if (!sourcesConfig.TryGetProperty("sources", out JsonElement sources)
                    || sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, getMethod, "sources property missing");
                    return;
                }

                string tabName = string.Empty;
                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Simple"
                        && source.TryGetProperty("name", out JsonElement name))
                    {
                        tabName = name.GetString() ?? string.Empty;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(tabName))
                {
                    _context.RecordFail(Module, getMethod, "no Simple tab found");
                    return;
                }

                // Get current config
                object getRequest = new { bot_id = _createdBotName, tab_name = tabName };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement currentConfig))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "server_type", "portfolio_name", "emulator_is_on", "commission_type",
                    "commission_value", "security_class", "security_name",
                    "candle_market_data_type", "candle_create_method_type", "time_frame",
                    "save_trades_in_candles", "build_non_trading_candles"
                };

                foreach (string field in requiredFields)
                {
                    if (!currentConfig.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, getMethod, $"{field} missing");
                        return;
                    }
                }

                // Build a new config toggling values that don't depend on external securities
                string currentCommissionType = currentConfig.GetProperty("commission_type").GetString() ?? "None";
                decimal currentCommissionValue = currentConfig.GetProperty("commission_value").GetDecimal();
                bool currentSaveTrades = currentConfig.GetProperty("save_trades_in_candles").GetBoolean();
                bool currentBuildNonTrading = currentConfig.GetProperty("build_non_trading_candles").GetBoolean();
                string currentTimeFrame = currentConfig.GetProperty("time_frame").GetString() ?? "Min30";

                string newCommissionType = currentCommissionType == "Percent" ? "None" : "Percent";
                decimal newCommissionValue = currentCommissionValue == 0m ? 0.01m : 0m;
                bool newSaveTrades = !currentSaveTrades;
                bool newBuildNonTrading = !currentBuildNonTrading;
                string newTimeFrame = currentTimeFrame == "Min30" ? "Min15" : "Min30";

                object setRequest = new
                {
                    bot_id = _createdBotName,
                    tab_name = tabName,
                    commission_type = newCommissionType,
                    commission_value = newCommissionValue,
                    time_frame = newTimeFrame,
                    save_trades_in_candles = newSaveTrades,
                    build_non_trading_candles = newBuildNonTrading
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("commission_type").GetString() != newCommissionType)
                {
                    _context.RecordFail(Module, setMethod, "commission_type mismatch");
                    return;
                }

                if (setConfig.GetProperty("commission_value").GetDecimal() != newCommissionValue)
                {
                    _context.RecordFail(Module, setMethod, "commission_value mismatch");
                    return;
                }

                if (setConfig.GetProperty("time_frame").GetString() != newTimeFrame)
                {
                    _context.RecordFail(Module, setMethod, "time_frame mismatch");
                    return;
                }

                if (setConfig.GetProperty("save_trades_in_candles").GetBoolean() != newSaveTrades)
                {
                    _context.RecordFail(Module, setMethod, "save_trades_in_candles mismatch");
                    return;
                }

                if (setConfig.GetProperty("build_non_trading_candles").GetBoolean() != newBuildNonTrading)
                {
                    _context.RecordFail(Module, setMethod, "build_non_trading_candles mismatch");
                    return;
                }

                // Verify with get
                string verifyResponse = _context.Client.ToolsCall(getMethod, getRequest);
                if (!TryParseConfig(verifyResponse, getMethod, out JsonElement verifyConfig))
                {
                    _context.RecordFail(Module, setMethod, "failed to verify config");
                    return;
                }

                if (verifyConfig.GetProperty("commission_type").GetString() != newCommissionType
                    || verifyConfig.GetProperty("commission_value").GetDecimal() != newCommissionValue
                    || verifyConfig.GetProperty("time_frame").GetString() != newTimeFrame
                    || verifyConfig.GetProperty("save_trades_in_candles").GetBoolean() != newSaveTrades
                    || verifyConfig.GetProperty("build_non_trading_candles").GetBoolean() != newBuildNonTrading)
                {
                    _context.RecordFail(Module, setMethod, "verify config mismatch");
                    return;
                }

                _context.RecordPass(Module, setMethod, $"tab={tabName}, tf={newTimeFrame}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestJournalSettings()
        {
            const string method = "bot_journal_settings";
            const string getMethod = "bot_journal_get_settings";
            const string setMethod = "bot_journal_set_settings";

            try
            {
                // Get settings for all robots.
                object getAllRequest = new { };
                _context.PrintRequest(Module, getMethod, getAllRequest);
                string getAllResponse = _context.Client.ToolsCall(getMethod, getAllRequest);
                _context.PrintResponse(getAllResponse);

                if (!TryParseConfig(getAllResponse, getMethod, out JsonElement allConfig))
                {
                    _context.RecordFail(Module, method, "failed to get all settings");
                    return;
                }

                if (!allConfig.TryGetProperty("robots", out JsonElement robots)
                    || robots.ValueKind != JsonValueKind.Array
                    || robots.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "robots array missing or empty");
                    return;
                }

                JsonElement first = robots[0];
                string botName = first.GetProperty("bot_name").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(botName))
                {
                    _context.RecordFail(Module, method, "first robot name is empty");
                    return;
                }

                string group = first.GetProperty("group").GetString() ?? string.Empty;
                decimal mult = first.GetProperty("mult").GetDecimal();
                bool isOn = first.GetProperty("is_on").GetBoolean();

                if (group != string.Empty || mult != 100m || !isOn)
                {
                    _context.RecordFail(Module, method, $"unexpected defaults: group={group}, mult={mult}, is_on={isOn}");
                    return;
                }

                // Set custom settings for the first bot.
                object setRequest = new
                {
                    settings = new[]
                    {
                        new { bot_name = botName, group = "test", mult = 50m, is_on = false }
                    }
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setResult))
                {
                    _context.RecordFail(Module, method, "failed to set settings");
                    return;
                }

                if (!setResult.TryGetProperty("updated_count", out JsonElement updatedCount)
                    || updatedCount.GetInt32() != 1)
                {
                    _context.RecordFail(Module, method, "updated_count mismatch");
                    return;
                }

                // Get settings for a single bot and verify.
                object getOneRequest = new { bot_name = botName };
                _context.PrintRequest(Module, getMethod, getOneRequest);
                string getOneResponse = _context.Client.ToolsCall(getMethod, getOneRequest);
                _context.PrintResponse(getOneResponse);

                if (!TryParseConfig(getOneResponse, getMethod, out JsonElement oneConfig))
                {
                    _context.RecordFail(Module, method, "failed to get single bot settings");
                    return;
                }

                if (!oneConfig.TryGetProperty("robots", out JsonElement oneRobots)
                    || oneRobots.GetArrayLength() != 1)
                {
                    _context.RecordFail(Module, method, "single bot settings should contain exactly one robot");
                    return;
                }

                JsonElement one = oneRobots[0];
                string oneGroup = one.GetProperty("group").GetString() ?? string.Empty;
                decimal oneMult = one.GetProperty("mult").GetDecimal();
                bool oneIsOn = one.GetProperty("is_on").GetBoolean();

                if (oneGroup != "test" || oneMult != 50m || oneIsOn)
                {
                    _context.RecordFail(Module, method, $"set values not applied: group={oneGroup}, mult={oneMult}, is_on={oneIsOn}");
                    return;
                }

                // Restore defaults.
                object restoreRequest = new
                {
                    settings = new[]
                    {
                        new { bot_name = botName, group = string.Empty, mult = 100m, is_on = true }
                    }
                };

                _context.Client.ToolsCall(setMethod, restoreRequest);

                _context.RecordPass(Module, method, $"bot={botName}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetBotsAfterCreate()
        {
            const string method = "bot_get_list";
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

                if (!config.TryGetProperty("bots", out JsonElement bots))
                {
                    _context.RecordFail(Module, method, "bots property missing");
                    return;
                }

                bool found = false;
                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    if (bot.TryGetProperty("name", out JsonElement name)
                        && name.GetString() == _createdBotName)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, method, $"created bot '{_createdBotName}' not found");
                    return;
                }

                _context.RecordPass(Module, method, $"count={bots.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotDelete()
        {
            const string method = "bot_delete";
            object request = new { bot_id = _createdBotName };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("deleted", out JsonElement deleted)
                    || !deleted.GetBoolean())
                {
                    _context.RecordFail(Module, method, "deleted is false");
                    return;
                }

                _context.RecordPass(Module, method, $"bot '{_createdBotName}' deleted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetBotsAfterDelete()
        {
            const string method = "bot_get_list";
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

                if (!config.TryGetProperty("bots", out JsonElement bots))
                {
                    _context.RecordFail(Module, method, "bots property missing");
                    return;
                }

                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    if (bot.TryGetProperty("name", out JsonElement name)
                        && name.GetString() == _createdBotName)
                    {
                        _context.RecordFail(Module, method, $"deleted bot '{_createdBotName}' is still present");
                        return;
                    }
                }

                _context.RecordPass(Module, method, $"count={bots.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
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

        #region Run simulation test

        private void TestRunSimulation()
        {
            const string method = "tester_run_simulation";
            string botName = string.Empty;
            SseCollector? collector = null;

            try
            {
                if (!WaitForTesterServer())
                {
                    _context.RecordFail(Module, method, "tester server not available");
                    return;
                }

                // 1. Configure data set.
                if (!ConfigureDataSetForRun(method))
                {
                    return;
                }

                // 2. Wait for securities to load and get the first one.
                if (!WaitForTesterSecurities(method, out string securityName, out string securityClass))
                {
                    return;
                }

                // 3. Create robot.
                botName = CreateRobot(method, "ParabolicBollinger");
                if (string.IsNullOrWhiteSpace(botName))
                {
                    return;
                }

                // 4. Turn robot regime On.
                if (!SetRobotRegime(method, botName, "On"))
                {
                    return;
                }

                // 5. Configure first Simple tab.
                if (!ConfigureRobotTab(method, botName, securityName, securityClass))
                {
                    return;
                }

                WaitForTabSubscription(method);

                // 6. Start SSE collector before running tester.
                collector = new SseCollector(_context.Client, eventName => eventName.StartsWith("tester.test."));
                collector.Start();

                // 7. Start tester and enable fast forward.
                if (!StartTester(method))
                {
                    collector.Stop(TimeSpan.Zero);
                    return;
                }

                if (!EnableFastForward(method))
                {
                    collector.Stop(TimeSpan.Zero);
                    return;
                }

                // 8. Wait for completion and poll status.
                List<string> statusLog = new List<string>();
                if (!WaitForTesterCompletion(method, statusLog))
                {
                    collector.Stop(TimeSpan.Zero);
                    return;
                }

                // 9. Stop collector and read tester events.
                collector.Stop(TimeSpan.FromSeconds(5));
                List<string> events = collector.GetEvents();

                // 10. Verify events.
                string[] requiredEvents = new[] { "tester.test.started", "tester.test.progress", "tester.test.paused", "tester.test.finished" };
                List<string> missingEvents = new List<string>();
                foreach (string requiredEvent in requiredEvents)
                {
                    if (!events.Contains(requiredEvent))
                    {
                        missingEvents.Add(requiredEvent);
                    }
                }

                if (missingEvents.Count > 0)
                {
                    _context.RecordFail(Module, method, $"missing events: {string.Join(", ", missingEvents)}. Got: {string.Join(", ", events.Distinct())}");
                    return;
                }

                if (statusLog.Count < 2)
                {
                    _context.RecordFail(Module, method, "status was not polled multiple times");
                    return;
                }

                _context.RecordPass(Module, method, $"events=[{string.Join(", ", events.Distinct())}], statuses={statusLog.Count}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
            finally
            {
                collector?.Stop(TimeSpan.Zero);

                if (!string.IsNullOrWhiteSpace(botName))
                {
                    try
                    {
                        _context.Client.ToolsCall("bot_delete", new { bot_id = botName });
                    }
                    catch
                    {
                        // ignore cleanup errors
                    }
                }
            }
        }

        private void TestJournalData()
        {
            const string method = "tester_journal_data";
            string botName = string.Empty;

            try
            {
                _context.Client.ToolsCall("tester_stop", new { });

                if (!WaitForTesterServer())
                {
                    _context.RecordFail(Module, method, "tester server not available");
                    return;
                }

                if (!ConfigureDataSetForRun(method))
                {
                    return;
                }

                if (!WaitForTesterSecurities(method, out string securityName, out string securityClass))
                {
                    return;
                }

                botName = CreateRobot(method, "ParabolicBollinger");
                if (string.IsNullOrWhiteSpace(botName))
                {
                    return;
                }

                if (!SetRobotRegime(method, botName, "On"))
                {
                    return;
                }

                if (!ConfigureRobotTab(method, botName, securityName, securityClass))
                {
                    return;
                }

                WaitForTabSubscription(method);

                if (!StartTester(method))
                {
                    return;
                }

                if (!EnableFastForward(method))
                {
                    return;
                }

                List<string> statusLog = new List<string>();
                if (!WaitForTesterCompletion(method, statusLog))
                {
                    return;
                }

                if (!TestJournalForBot(method, null))
                {
                    return;
                }

                if (!TestJournalForBot(method, botName))
                {
                    return;
                }

                _context.RecordPass(Module, method, $"bot={botName}, statuses={statusLog.Count}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(botName))
                {
                    try
                    {
                        _context.Client.ToolsCall("bot_delete", new { bot_id = botName });
                    }
                    catch
                    {
                        // ignore cleanup errors
                    }
                }

                try
                {
                    _context.Client.ToolsCall("tester_stop", new { });
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        private bool TestJournalForBot(string method, string? botName)
        {
            string target = botName ?? "all";

            // Summary
            if (!CallJournalMethod("bot_journal_get_summary", new { bot_name = botName }, out JsonElement summary))
            {
                _context.RecordFail(Module, method, $"summary failed for {target}");
                return false;
            }

            if (!summary.TryGetProperty("total_profit_abs", out _)
                || !summary.TryGetProperty("total_profit_percent", out _)
                || !summary.TryGetProperty("period_start", out _)
                || !summary.TryGetProperty("period_end", out _))
            {
                _context.RecordFail(Module, method, $"summary fields missing for {target}");
                return false;
            }

            // Equity
            if (!CallJournalMethod("bot_journal_get_equity", new { bot_name = botName, chart_type = "DepositPercent" }, out JsonElement equity))
            {
                _context.RecordFail(Module, method, $"equity failed for {target}");
                return false;
            }

            if (!equity.TryGetProperty("points", out JsonElement equityPoints)
                || equityPoints.ValueKind != JsonValueKind.Array)
            {
                _context.RecordFail(Module, method, $"equity points missing for {target}");
                return false;
            }

            if (botName != null && equityPoints.GetArrayLength() == 0)
            {
                _context.RecordFail(Module, method, $"equity is empty for {target}");
                return false;
            }

            // Statistics
            if (!CallJournalMethod("bot_journal_get_statistics", new { bot_name = botName, side = "All" }, out JsonElement statistics))
            {
                _context.RecordFail(Module, method, $"statistics failed for {target}");
                return false;
            }

            string[] statFields = new[] { "net_profit", "net_profit_percent", "deals_count", "average_holding_time", "sharpe", "profit_factor", "recovery", "profitable_deals", "losing_deals", "max_drawdown_percent", "commission" };
            foreach (string field in statFields)
            {
                if (!statistics.TryGetProperty(field, out _))
                {
                    _context.RecordFail(Module, method, $"statistics field '{field}' missing for {target}");
                    return false;
                }
            }

            if (botName != null
                && (!statistics.TryGetProperty("deals_count", out JsonElement dealsCount)
                    || dealsCount.GetInt32() == 0))
            {
                _context.RecordFail(Module, method, $"no deals for {target}");
                return false;
            }

            // Drawdown
            if (!CallJournalMethod("bot_journal_get_drawdown", new { bot_name = botName }, out JsonElement drawdown))
            {
                _context.RecordFail(Module, method, $"drawdown failed for {target}");
                return false;
            }

            if (!drawdown.TryGetProperty("points", out JsonElement drawdownPoints)
                || drawdownPoints.ValueKind != JsonValueKind.Array)
            {
                _context.RecordFail(Module, method, $"drawdown points missing for {target}");
                return false;
            }

            // Volume
            if (!CallJournalMethod("bot_journal_get_volume", new { bot_name = botName }, out JsonElement volume))
            {
                _context.RecordFail(Module, method, $"volume failed for {target}");
                return false;
            }

            if (!volume.TryGetProperty("points", out JsonElement volumePoints)
                || volumePoints.ValueKind != JsonValueKind.Array)
            {
                _context.RecordFail(Module, method, $"volume points missing for {target}");
                return false;
            }

            // Open positions
            if (!CallJournalMethod("bot_journal_get_open_positions", new { bot_name = botName, limit = 100, offset = 0 }, out JsonElement openPositions))
            {
                _context.RecordFail(Module, method, $"open_positions failed for {target}");
                return false;
            }

            if (!openPositions.TryGetProperty("positions", out _)
                || !openPositions.TryGetProperty("count", out _))
            {
                _context.RecordFail(Module, method, $"open_positions fields missing for {target}");
                return false;
            }

            // Closed positions
            if (!CallJournalMethod("bot_journal_get_closed_positions", new { bot_name = botName, include_failed = false, limit = 100, offset = 0 }, out JsonElement closedPositions))
            {
                _context.RecordFail(Module, method, $"closed_positions failed for {target}");
                return false;
            }

            if (!closedPositions.TryGetProperty("positions", out JsonElement closedItems)
                || !closedPositions.TryGetProperty("count", out JsonElement closedCount))
            {
                _context.RecordFail(Module, method, $"closed_positions fields missing for {target}");
                return false;
            }

            if (botName != null && closedCount.GetInt32() == 0)
            {
                _context.RecordFail(Module, method, $"no closed positions for {target}");
                return false;
            }

            return true;
        }

        private bool CallJournalMethod(string method, object request, out JsonElement config)
        {
            config = default;

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);
                return TryParseConfig(response, method, out config);
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool ConfigureDataSetForRun(string method)
        {
            const string setMethod = "tester_data_set_config";

            object request = new
            {
                source_type = "Set",
                set_name = TestSetName,
                type_tester_data = "Candle",
                date_from = "2024-01-01T00:00:00",
                date_to = "2024-06-30T00:00:00",
                delete_trades_from_memory = true
            };

            try
            {
                _context.PrintRequest(Module, setMethod, request);
                string response = _context.Client.ToolsCall(setMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to configure data set");
                    return false;
                }

                if (!config.TryGetProperty("set_name", out JsonElement setName)
                    || setName.GetString() != TestSetName)
                {
                    _context.RecordFail(Module, method, "data set name mismatch");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"ConfigureDataSetForRun failed: {error.Message}");
                return false;
            }
        }

        private bool WaitForTesterSecurities(string method, out string securityName, out string securityClass)
        {
            securityName = string.Empty;
            securityClass = string.Empty;
            const string getMethod = "tester_get_securities";
            DateTime deadline = DateTime.Now.AddSeconds(60);

            try
            {
                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall(getMethod, new { });

                    if (!TryParseConfig(response, getMethod, out JsonElement config))
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (!config.TryGetProperty("securities", out JsonElement securities)
                        || securities.ValueKind != JsonValueKind.Array)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (securities.GetArrayLength() == 0)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    _context.PrintRequest(Module, getMethod, new { });
                    _context.PrintResponse(response);

                    JsonElement first = securities[0];
                    securityName = first.GetProperty("name").GetString() ?? string.Empty;
                    securityClass = first.GetProperty("class_name").GetString() ?? string.Empty;
                    return true;
                }

                _context.RecordFail(Module, method, "tester has no securities after timeout. Cannot run simulation.");
                return false;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"WaitForTesterSecurities failed: {error.Message}");
                return false;
            }
        }

        private string CreateRobot(string method, string strategyName)
        {
            const string createMethod = "bot_create";

            try
            {
                object request = new { strategy_name = strategyName };
                _context.PrintRequest(Module, createMethod, request);
                string response = _context.Client.ToolsCall(createMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, createMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to create robot");
                    return string.Empty;
                }

                if (!config.TryGetProperty("name", out JsonElement name))
                {
                    _context.RecordFail(Module, method, "created robot name missing");
                    return string.Empty;
                }

                return name.GetString() ?? string.Empty;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"CreateRobot failed: {error.Message}");
                return string.Empty;
            }
        }

        private bool SetRobotRegime(string method, string botName, string regime)
        {
            const string setParamsMethod = "bot_set_params";

            try
            {
                object request = new
                {
                    bot_id = botName,
                    parameters = new Dictionary<string, object> { { "Regime", regime } }
                };

                _context.PrintRequest(Module, setParamsMethod, request);
                string response = _context.Client.ToolsCall(setParamsMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setParamsMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to set robot regime");
                    return false;
                }

                if (!config.TryGetProperty("updated_count", out JsonElement updatedCount)
                    || updatedCount.GetInt32() != 1)
                {
                    _context.RecordFail(Module, method, "robot regime was not updated");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"SetRobotRegime failed: {error.Message}");
                return false;
            }
        }

        private bool SetScreenerTradeParameters(string method, string botName)
        {
            const string setParamsMethod = "bot_set_params";

            try
            {
                object request = new
                {
                    bot_id = botName,
                    parameters = new Dictionary<string, object>
                    {
                        { "Regime", "On" },
                        { "Volatility cluster to trade", 0 }
                    }
                };

                _context.PrintRequest(Module, setParamsMethod, request);
                string response = _context.Client.ToolsCall(setParamsMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setParamsMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to set screener parameters");
                    return false;
                }

                if (!config.TryGetProperty("updated_count", out JsonElement updatedCount)
                    || updatedCount.GetInt32() != 2)
                {
                    _context.RecordFail(Module, method, $"screener parameters were not updated (updated_count={updatedCount})");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"SetScreenerTradeParameters failed: {error.Message}");
                return false;
            }
        }

        private bool ConfigureRobotTab(string method, string botName, string securityName, string securityClass)
        {
            const string getSourcesMethod = "bot_get_sources";
            const string setConfigMethod = "bot_set_config_tab_simple";

            try
            {
                // Find first Simple tab.
                string sourcesResponse = _context.Client.ToolsCall(getSourcesMethod, new { bot_id = botName });
                if (!TryParseConfig(sourcesResponse, getSourcesMethod, out JsonElement sourcesConfig))
                {
                    _context.RecordFail(Module, method, "failed to get robot sources");
                    return false;
                }

                if (!sourcesConfig.TryGetProperty("sources", out JsonElement sources)
                    || sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "sources missing");
                    return false;
                }

                string tabName = string.Empty;
                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Simple"
                        && source.TryGetProperty("name", out JsonElement name))
                    {
                        tabName = name.GetString() ?? string.Empty;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(tabName))
                {
                    _context.RecordFail(Module, method, "no Simple tab found");
                    return false;
                }

                object request = new
                {
                    bot_id = botName,
                    tab_name = tabName,
                    server_type = "Tester",
                    portfolio_name = "GodMode",
                    security_name = securityName,
                    security_class = securityClass,
                    time_frame = "Min30"
                };

                _context.PrintRequest(Module, setConfigMethod, request);
                string response = _context.Client.ToolsCall(setConfigMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setConfigMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to configure robot tab");
                    return false;
                }

                if (config.GetProperty("security_name").GetString() != securityName)
                {
                    _context.RecordFail(Module, method, "security_name mismatch");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"ConfigureRobotTab failed: {error.Message}");
                return false;
            }
        }

        private void WaitForTabSubscription(string method)
        {
            // The connector subscribes to the tester server asynchronously.
            // Wait long enough for the subscription to complete and for the
            // 5-second "security just started" guard in TestingStart to expire.
            Thread.Sleep(6000);
        }

        private bool StartTester(string method)
        {
            const string startMethod = "tester_start";

            try
            {
                object request = new { fast_forward = true };
                _context.PrintRequest(Module, startMethod, request);
                string response = _context.Client.ToolsCall(startMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, startMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to start tester");
                    return false;
                }

                if (!config.TryGetProperty("regime", out JsonElement regime)
                    || (regime.GetString() ?? string.Empty) != "Play")
                {
                    _context.RecordFail(Module, method, $"unexpected regime after start: {regime.GetString()}");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"StartTester failed: {error.Message}");
                return false;
            }
        }

        private bool EnableFastForward(string method)
        {
            const string statusMethod = "tester_get_status";
            const string fastForwardMethod = "tester_fast_forward";
            DateTime deadline = DateTime.Now.AddSeconds(30);

            try
            {
                while (DateTime.Now < deadline)
                {
                    string statusResponse = _context.Client.ToolsCall(statusMethod, new { });
                    if (!TryParseConfig(statusResponse, statusMethod, out JsonElement status))
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    bool fastForward = status.GetProperty("fast_forward").GetBoolean();

                    if (fastForward)
                    {
                        return true;
                    }

                    _context.PrintRequest(Module, fastForwardMethod, new { });
                    string response = _context.Client.ToolsCall(fastForwardMethod, new { });
                    _context.PrintResponse(response);

                    Thread.Sleep(100);
                }

                _context.RecordFail(Module, method, "failed to enable tester fast forward");
                return false;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"EnableFastForward failed: {error.Message}");
                return false;
            }
        }

        private bool WaitForTesterCompletion(string method, List<string> statusLog)
        {
            const string statusMethod = "tester_get_status";
            DateTime deadline = DateTime.Now.AddSeconds(300);

            // Take an initial snapshot so very fast simulations still produce
            // more than one status log entry.
            try
            {
                string response = _context.Client.ToolsCall(statusMethod, new { });

                if (TryParseConfig(response, statusMethod, out JsonElement initialStatus))
                {
                    string regime = initialStatus.GetProperty("regime").GetString() ?? string.Empty;
                    string timeNow = initialStatus.GetProperty("time_now").GetString() ?? string.Empty;
                    double progress = initialStatus.GetProperty("progress_percent").GetDouble();
                    statusLog.Add($"{regime}|{timeNow}|{progress}%");
                }
            }
            catch
            {
                // ignore initial poll errors
            }

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall(statusMethod, new { });
                    if (!TryParseConfig(response, statusMethod, out JsonElement status))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    string regime = status.GetProperty("regime").GetString() ?? string.Empty;
                    string timeNow = status.GetProperty("time_now").GetString() ?? string.Empty;
                    string timeEnd = status.GetProperty("time_end").GetString() ?? string.Empty;
                    double progress = status.GetProperty("progress_percent").GetDouble();

                    string logEntry = $"{regime}|{timeNow}|{progress}%";
                    statusLog.Add(logEntry);

                    if (regime == "Pause"
                        && DateTime.TryParse(timeNow, out DateTime now)
                        && DateTime.TryParse(timeEnd, out DateTime end)
                        && now >= end)
                    {
                        return true;
                    }

                    if (regime == "NotActive")
                    {
                        _context.RecordFail(Module, method, "tester stopped unexpectedly");
                        return false;
                    }
                }
                catch
                {
                    // ignore and retry
                }

                Thread.Sleep(1000);
            }

            _context.RecordFail(Module, method, "tester did not complete within timeout");
            return false;
        }

        private void TestScreenerConfiguration()
        {
            const string method = "tester_screener_configuration";
            string botName = string.Empty;

            try
            {
                _context.Client.ToolsCall("tester_stop", new { });

                if (!WaitForTesterServer())
                {
                    return;
                }

                if (!ConfigureDataSetForRun(method))
                {
                    return;
                }

                List<(string Name, string Class)> securities = GetTesterSecuritiesList(method);

                if (securities == null || securities.Count == 0)
                {
                    _context.RecordPass(Module, method, "no tester securities available, screener test skipped");
                    return;
                }

                botName = CreateRobot(method, "AlgoStart1LinearRegression");
                if (string.IsNullOrWhiteSpace(botName))
                {
                    return;
                }

                string tabName = GetScreenerTabName(method, botName);
                if (string.IsNullOrWhiteSpace(tabName))
                {
                    return;
                }

                if (!ConfigureScreenerTab(method, botName, tabName, securities))
                {
                    return;
                }

                if (!WaitForScreenerTabs(method, botName, tabName, securities.Count))
                {
                    return;
                }

                // The default volatility cluster filter prevents trading when only
                // two securities are loaded. Disable it so the screener can trade.
                if (!SetScreenerTradeParameters(method, botName))
                {
                    return;
                }

                if (!StartTester(method))
                {
                    return;
                }

                if (!EnableFastForward(method))
                {
                    return;
                }

                List<string> statusLog = new List<string>();
                if (!WaitForTesterCompletion(method, statusLog))
                {
                    return;
                }

                object statisticsRequest = new { bot_name = botName, side = "All" };
                if (!CallJournalMethod("bot_journal_get_statistics", statisticsRequest, out JsonElement statistics))
                {
                    _context.RecordFail(Module, method, "failed to get journal statistics");
                    return;
                }

                if (!statistics.TryGetProperty("deals_count", out JsonElement dealsCount)
                    || dealsCount.GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "screener finished without trades");
                    return;
                }

                _context.RecordPass(Module, method, $"bot={botName}, securities={securities.Count}, deals={dealsCount.GetInt32()}");
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"TestScreenerConfiguration failed: {error.Message}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(botName))
                {
                    _context.Client.ToolsCall("bot_delete", new { bot_id = botName });
                }

                _context.Client.ToolsCall("tester_stop", new { });
            }
        }

        private List<(string Name, string Class)> GetTesterSecuritiesList(string method)
        {
            const string getMethod = "tester_get_securities";
            DateTime deadline = DateTime.Now.AddSeconds(60);

            try
            {
                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall(getMethod, new { });

                    if (!TryParseConfig(response, getMethod, out JsonElement config))
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (!config.TryGetProperty("securities", out JsonElement securities)
                        || securities.ValueKind != JsonValueKind.Array)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (securities.GetArrayLength() == 0)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    _context.PrintRequest(Module, getMethod, new { });
                    _context.PrintResponse(response);

                    List<(string, string)> result = new List<(string, string)>();

                    foreach (JsonElement item in securities.EnumerateArray())
                    {
                        string name = item.TryGetProperty("name", out JsonElement nameEl)
                            ? nameEl.GetString() ?? string.Empty
                            : string.Empty;

                        string className = item.TryGetProperty("class_name", out JsonElement classEl)
                            ? classEl.GetString() ?? string.Empty
                            : string.Empty;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Add((name, className));
                        }
                    }

                    return result;
                }

                _context.RecordFail(Module, method, "tester has no securities after timeout");
                return new List<(string, string)>();
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"GetTesterSecuritiesList failed: {error.Message}");
                return new List<(string, string)>();
            }
        }

        private string GetScreenerTabName(string method, string botName)
        {
            const string getSourcesMethod = "bot_get_sources";

            try
            {
                string sourcesResponse = _context.Client.ToolsCall(getSourcesMethod, new { bot_id = botName });
                if (!TryParseConfig(sourcesResponse, getSourcesMethod, out JsonElement sourcesConfig))
                {
                    _context.RecordFail(Module, method, "failed to get robot sources");
                    return string.Empty;
                }

                if (!sourcesConfig.TryGetProperty("sources", out JsonElement sources)
                    || sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "sources missing");
                    return string.Empty;
                }

                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Screener"
                        && source.TryGetProperty("name", out JsonElement name))
                    {
                        return name.GetString() ?? string.Empty;
                    }
                }

                _context.RecordFail(Module, method, "no Screener tab found");
                return string.Empty;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"GetScreenerTabName failed: {error.Message}");
                return string.Empty;
            }
        }

        private bool ConfigureScreenerTab(string method, string botName, string tabName, List<(string Name, string Class)> securities)
        {
            const string setConfigMethod = "bot_set_config_tab_screener";
            const string getConfigMethod = "bot_get_config_tab_screener";

            try
            {
                object[] securitiesArray = securities
                    .Select(s => new { name = s.Name, class_name = s.Class, is_on = true })
                    .ToArray();

                object request = new
                {
                    bot_id = botName,
                    tab_name = tabName,
                    server_type = "Tester",
                    server_name = "Tester",
                    portfolio_name = "GodMode",
                    emulator_is_on = true,
                    time_frame = "Min30",
                    securities = securitiesArray
                };

                _context.PrintRequest(Module, setConfigMethod, request);
                string response = _context.Client.ToolsCall(setConfigMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setConfigMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to configure screener tab");
                    return false;
                }

                if (!config.TryGetProperty("tabs_count", out JsonElement tabsCount)
                    || tabsCount.GetInt32() != securities.Count)
                {
                    // The tabs may not be created immediately. We will wait for them later.
                }

                // Verify that get_config returns the same data.
                _context.PrintRequest(Module, getConfigMethod, new { bot_id = botName, tab_name = tabName });
                string getResponse = _context.Client.ToolsCall(getConfigMethod, new { bot_id = botName, tab_name = tabName });
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getConfigMethod, out JsonElement getConfig))
                {
                    _context.RecordFail(Module, method, "failed to get screener tab config");
                    return false;
                }

                if (!getConfig.TryGetProperty("securities", out JsonElement storedSecurities)
                    || storedSecurities.GetArrayLength() != securities.Count)
                {
                    _context.RecordFail(Module, method, "screener securities count mismatch after get_config");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"ConfigureScreenerTab failed: {error.Message}");
                return false;
            }
        }

        private bool WaitForScreenerTabs(string method, string botName, string tabName, int expectedCount)
        {
            const string getConfigMethod = "bot_get_config_tab_screener";
            DateTime deadline = DateTime.Now.AddSeconds(60);

            try
            {
                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall(getConfigMethod, new { bot_id = botName, tab_name = tabName });

                    if (!TryParseConfig(response, getConfigMethod, out JsonElement config))
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (config.TryGetProperty("tabs_count", out JsonElement tabsCount)
                        && tabsCount.GetInt32() == expectedCount)
                    {
                        // Give the connectors a moment to finish subscribing.
                        Thread.Sleep(6000);
                        return true;
                    }

                    Thread.Sleep(500);
                }

                _context.RecordFail(Module, method, $"screener tabs did not reach expected count {expectedCount}");
                return false;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"WaitForScreenerTabs failed: {error.Message}");
                return false;
            }
        }

        #endregion

        #region Index arbitrage test

        private (string Name, string Class) FindSecurityByBaseName(List<(string Name, string Class)> securities, string baseName)
        {
            return securities.FirstOrDefault(s =>
            {
                string name = s.Name;

                if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - 4);
                }

                return name.Equals(baseName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void TestIndexArbitrage()
        {
            const string method = "tester_index_arbitrage";
            string botName = string.Empty;

            try
            {
                _context.Client.ToolsCall("tester_stop", new { });

                if (!WaitForTesterServer())
                {
                    _context.RecordFail(Module, method, "tester server not available");
                    return;
                }

                // The MOEX data set is prepared by the Data module.
                if (!ConfigureIndexArbitrageDataSet(method))
                {
                    return;
                }

                List<(string Name, string Class)> securities = GetTesterSecuritiesList(method);

                if (securities == null || securities.Count < 4)
                {
                    _context.RecordFail(Module, method, $"tester set has only {securities?.Count ?? 0} securities, need 4");
                    return;
                }

                (string Name, string Class) sber = FindSecurityByBaseName(securities, "SBER");
                (string Name, string Class) vtbr = FindSecurityByBaseName(securities, "VTBR");
                (string Name, string Class) gazp = FindSecurityByBaseName(securities, "GAZP");
                (string Name, string Class) lkoh = FindSecurityByBaseName(securities, "LKOH");

                if (string.IsNullOrWhiteSpace(sber.Name) || string.IsNullOrWhiteSpace(vtbr.Name)
                    || string.IsNullOrWhiteSpace(gazp.Name) || string.IsNullOrWhiteSpace(lkoh.Name))
                {
                    _context.RecordFail(Module, method, "required securities SBER/VTBR/GAZP/LKOH not found in tester set");
                    return;
                }

                // 3. Create robot and configure parameters.
                botName = CreateRobot(method, "IndexArbitrageClassic");
                if (string.IsNullOrWhiteSpace(botName))
                {
                    return;
                }

                if (!SetIndexArbitrageParams(method, botName))
                {
                    return;
                }

                List<string> indexTabs = GetIndexTabNames(method, botName);
                List<string> screenerTabs = GetScreenerTabNames(method, botName);

                if (indexTabs.Count < 2 || screenerTabs.Count < 2)
                {
                    _context.RecordFail(Module, method, $"robot tabs missing: index={indexTabs.Count}, screener={screenerTabs.Count}");
                    return;
                }

                // 4. Configure index tabs.
                if (!ConfigureIndexTab(method, botName, indexTabs[0], new[] { sber, vtbr }, "A0+A1"))
                {
                    return;
                }

                if (!ConfigureIndexTab(method, botName, indexTabs[1], new[] { gazp, lkoh }, "A0+A1"))
                {
                    return;
                }

                if (!WaitForIndexTabs(method, botName, indexTabs[0], 2)
                    || !WaitForIndexTabs(method, botName, indexTabs[1], 2))
                {
                    return;
                }

                // 5. Configure screener tabs.
                if (!ConfigureScreenerTab(method, botName, screenerTabs[0], new List<(string, string)> { sber, vtbr }))
                {
                    return;
                }

                if (!ConfigureScreenerTab(method, botName, screenerTabs[1], new List<(string, string)> { gazp, lkoh }))
                {
                    return;
                }

                if (!WaitForScreenerTabs(method, botName, screenerTabs[0], 2)
                    || !WaitForScreenerTabs(method, botName, screenerTabs[1], 2))
                {
                    return;
                }

                // 6. Run tester.
                if (!StartTester(method))
                {
                    return;
                }

                if (!EnableFastForward(method))
                {
                    return;
                }

                List<string> statusLog = new List<string>();
                if (!WaitForTesterCompletion(method, statusLog))
                {
                    return;
                }

                // 7. Verify closed positions.
                if (!CallJournalMethod("bot_journal_get_closed_positions", new { bot_name = botName, include_failed = false, limit = 100, offset = 0 }, out JsonElement closedPositions))
                {
                    _context.RecordFail(Module, method, "failed to get closed positions");
                    return;
                }

                if (!closedPositions.TryGetProperty("count", out JsonElement closedCount)
                    || closedCount.GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "no closed positions after index arbitrage test");
                    return;
                }

                _context.RecordPass(Module, method, $"bot={botName}, closed_positions={closedCount.GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"TestIndexArbitrage failed: {error.Message}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(botName))
                {
                    try
                    {
                        _context.Client.ToolsCall("bot_delete", new { bot_id = botName });
                    }
                    catch
                    {
                        // ignore cleanup errors
                    }
                }

                try
                {
                    _context.Client.ToolsCall("tester_stop", new { });
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }


        private bool ConfigureIndexArbitrageDataSet(string method)
        {
            const string setMethod = "tester_data_set_config";
            const string setName = TestSetName;

            object request = new
            {
                source_type = "Set",
                set_name = setName,
                type_tester_data = "Candle",
                date_from = "2024-01-01T00:00:00",
                date_to = "2024-06-30T00:00:00",
                delete_trades_from_memory = true
            };

            try
            {
                _context.PrintRequest(Module, setMethod, request);
                string response = _context.Client.ToolsCall(setMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to configure tester data set");
                    return false;
                }

                if (!config.TryGetProperty("set_name", out JsonElement setNameEl)
                    || setNameEl.GetString() != setName)
                {
                    _context.RecordFail(Module, method, "tester data set name mismatch");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, $"ConfigureIndexArbitrageDataSet failed: {error.Message}");
                return false;
            }
        }

        private List<string> GetIndexTabNames(string method, string botName)
        {
            const string getSourcesMethod = "bot_get_sources";
            List<string> result = new List<string>();

            try
            {
                string sourcesResponse = _context.Client.ToolsCall(getSourcesMethod, new { bot_id = botName });
                if (!TryParseConfig(sourcesResponse, getSourcesMethod, out JsonElement sourcesConfig))
                {
                    _context.RecordFail(Module, method, "failed to get robot sources for index tabs");
                    return result;
                }

                if (!sourcesConfig.TryGetProperty("sources", out JsonElement sources)
                    || sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "sources missing");
                    return result;
                }

                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Index"
                        && source.TryGetProperty("name", out JsonElement name))
                    {
                        result.Add(name.GetString() ?? string.Empty);
                    }
                }

                return result;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"GetIndexTabNames failed: {error.Message}");
                return result;
            }
        }

        private List<string> GetScreenerTabNames(string method, string botName)
        {
            const string getSourcesMethod = "bot_get_sources";
            List<string> result = new List<string>();

            try
            {
                string sourcesResponse = _context.Client.ToolsCall(getSourcesMethod, new { bot_id = botName });
                if (!TryParseConfig(sourcesResponse, getSourcesMethod, out JsonElement sourcesConfig))
                {
                    _context.RecordFail(Module, method, "failed to get robot sources for screener tabs");
                    return result;
                }

                if (!sourcesConfig.TryGetProperty("sources", out JsonElement sources)
                    || sources.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "sources missing");
                    return result;
                }

                foreach (JsonElement source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Screener"
                        && source.TryGetProperty("name", out JsonElement name))
                    {
                        result.Add(name.GetString() ?? string.Empty);
                    }
                }

                return result;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"GetScreenerTabNames failed: {error.Message}");
                return result;
            }
        }

        private bool SetIndexArbitrageParams(string method, string botName)
        {
            const string setParamsMethod = "bot_set_params";

            try
            {
                object request = new
                {
                    bot_id = botName,
                    parameters = new Dictionary<string, object>
                    {
                        { "Regime", "On" },
                        { "Regime Close Position", "Reverse signal" },
                        { "Correlatioin min value", 0.5m }
                    }
                };

                _context.PrintRequest(Module, setParamsMethod, request);
                string response = _context.Client.ToolsCall(setParamsMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setParamsMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, "failed to set index arbitrage parameters");
                    return false;
                }

                if (!config.TryGetProperty("updated_count", out JsonElement updatedCount)
                    || updatedCount.GetInt32() != 3)
                {
                    _context.RecordFail(Module, method, $"index arbitrage parameters were not updated (updated_count={updatedCount})");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"SetIndexArbitrageParams failed: {error.Message}");
                return false;
            }
        }

        private bool ConfigureIndexTab(string method, string botName, string tabName, (string Name, string Class)[] securities, string formula)
        {
            const string setConfigMethod = "bot_set_config_tab_index";
            const string getConfigMethod = "bot_get_config_tab_index";

            try
            {
                object[] securitiesArray = securities
                    .Select(s => new { name = s.Name, class_name = s.Class, is_on = true })
                    .ToArray();

                object request = new
                {
                    bot_id = botName,
                    tab_name = tabName,
                    server_type = "Tester",
                    server_name = "Tester",
                    portfolio_name = "GodMode",
                    emulator_is_on = true,
                    time_frame = "Min30",
                    user_formula = formula,
                    calculation_depth = 1000,
                    auto_formula = new
                    {
                        regime = "Off",
                        day_of_week = "Tuesday",
                        hour = 12,
                        sec_count = securities.Length,
                        days_look_back = 10,
                        sort_type = "FirstInArray",
                        mult_type = "EqualWeighted",
                        write_log_on_rebuild = false
                    },
                    securities = securitiesArray
                };

                _context.PrintRequest(Module, setConfigMethod, request);
                string response = _context.Client.ToolsCall(setConfigMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, setConfigMethod, out JsonElement config))
                {
                    _context.RecordFail(Module, method, $"failed to configure index tab '{tabName}'");
                    return false;
                }

                if (!config.TryGetProperty("tabs_count", out JsonElement tabsCount)
                    || tabsCount.GetInt32() != securities.Length)
                {
                    // Tabs may not be created immediately; we will wait for them later.
                }

                // Verify that get_config returns the same data.
                _context.PrintRequest(Module, getConfigMethod, new { bot_id = botName, tab_name = tabName });
                string getResponse = _context.Client.ToolsCall(getConfigMethod, new { bot_id = botName, tab_name = tabName });
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getConfigMethod, out JsonElement getConfig))
                {
                    _context.RecordFail(Module, method, $"failed to get index tab config for '{tabName}'");
                    return false;
                }

                if (!getConfig.TryGetProperty("securities", out JsonElement storedSecurities)
                    || storedSecurities.GetArrayLength() != securities.Length)
                {
                    _context.RecordFail(Module, method, $"index securities count mismatch after get_config for '{tabName}'");
                    return false;
                }

                if (!VerifyAutoFormulaConfig(getConfig, securities.Length))
                {
                    _context.RecordFail(Module, method, $"auto-formula config mismatch after get_config for '{tabName}'");
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"ConfigureIndexTab failed: {error.Message}");
                return false;
            }
        }

        private bool VerifyAutoFormulaConfig(JsonElement config, int expectedSecCount)
        {
            if (!config.TryGetProperty("auto_formula", out JsonElement autoFormula)
                || autoFormula.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string regime = autoFormula.TryGetProperty("regime", out JsonElement regimeElement)
                ? regimeElement.GetString() ?? string.Empty
                : string.Empty;

            if (regime != "Off")
            {
                return false;
            }

            string sortType = autoFormula.TryGetProperty("sort_type", out JsonElement sortTypeElement)
                ? sortTypeElement.GetString() ?? string.Empty
                : string.Empty;

            if (sortType != "FirstInArray")
            {
                return false;
            }

            string multType = autoFormula.TryGetProperty("mult_type", out JsonElement multTypeElement)
                ? multTypeElement.GetString() ?? string.Empty
                : string.Empty;

            if (multType != "EqualWeighted")
            {
                return false;
            }

            int secCount = autoFormula.TryGetProperty("sec_count", out JsonElement secCountElement)
                ? secCountElement.GetInt32()
                : 0;

            if (secCount != expectedSecCount)
            {
                return false;
            }

            return true;
        }

        private bool WaitForIndexTabs(string method, string botName, string tabName, int expectedCount)
        {
            const string getConfigMethod = "bot_get_config_tab_index";
            DateTime deadline = DateTime.Now.AddSeconds(60);

            try
            {
                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall(getConfigMethod, new { bot_id = botName, tab_name = tabName });

                    if (!TryParseConfig(response, getConfigMethod, out JsonElement config))
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (config.TryGetProperty("tabs_count", out JsonElement tabsCount)
                        && tabsCount.GetInt32() == expectedCount)
                    {
                        // Give the connectors a moment to finish subscribing.
                        Thread.Sleep(6000);
                        return true;
                    }

                    Thread.Sleep(500);
                }

                _context.RecordFail(Module, method, $"index tabs '{tabName}' did not reach expected count {expectedCount}");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"WaitForIndexTabs failed: {error.Message}");
                return false;
            }
        }

        #endregion

        #region SSE collector

        private class SseCollector
        {
            private readonly McpApiClient _client;
            private readonly Predicate<string> _filter;
            private readonly List<string> _events = new List<string>();
            private readonly object _locker = new object();
            private HttpResponseMessage? _response;
            private Stream? _stream;
            private StreamReader? _reader;
            private Thread? _thread;
            private bool _stopRequested;

            public SseCollector(McpApiClient client, Predicate<string> filter)
            {
                _client = client;
                _filter = filter;
            }

            public void Start()
            {
                _stopRequested = false;
                _thread = new Thread(ReadLoop);
                _thread.IsBackground = true;
                _thread.Start();
            }

            public void Stop(TimeSpan waitForRemaining)
            {
                // Give the reader a chance to pick up events that were in flight
                // before asking it to shut down.
                if (waitForRemaining > TimeSpan.Zero)
                {
                    Thread.Sleep(waitForRemaining);
                }

                _stopRequested = true;

                try
                {
                    _response?.Dispose();
                }
                catch
                {
                    // ignore
                }

                if (_thread != null && _thread.IsAlive)
                {
                    _thread.Join(TimeSpan.FromSeconds(1));
                }
            }

            public List<string> GetEvents()
            {
                lock (_locker)
                {
                    return new List<string>(_events);
                }
            }

            private void ReadLoop()
            {
                try
                {
                    _response = _client.GetSseResponse();
                    _stream = _response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                    _reader = new StreamReader(_stream, Encoding.UTF8);

                    string eventName = string.Empty;
                    string data = string.Empty;

                    while (!_stopRequested)
                    {
                        if (_reader.EndOfStream)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        string? line = _reader.ReadLine();
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
                            if (!string.IsNullOrEmpty(eventName) && _filter(eventName))
                            {
                                lock (_locker)
                                {
                                    _events.Add(eventName);
                                }
                            }

                            eventName = string.Empty;
                            data = string.Empty;
                        }
                    }
                }
                catch
                {
                    // ignore read errors
                }
            }
        }

        #endregion
    }
}
