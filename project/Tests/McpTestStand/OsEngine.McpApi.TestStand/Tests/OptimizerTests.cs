/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for optimizer tools (optimizer_*): data config/status, dividends, trade settings,
    /// position support, phases, filters, bot selection, params, threads, start/stop, reports
    /// and a full E2E optimization run. The E2E run uses the persistent data set
    /// 'OptimizerToTestStend' (SBER, VTBR, GAZP, Min30): it is downloaded once via Os.Data
    /// and reused afterwards. Settings are restored after the run.
    /// </summary>
    public class OptimizerTests
    {
        private const string Module = "OPTIMIZER";
        private readonly TestContext _context;

        // сет данных для E2E-прогона оптимизации: качается один раз и остаётся на диске
        private const string DataSetName = "OptimizerToTestStend";
        private const string DataServerType = "MoexDataServer";
        private const string DataSetTimeFrame = "Min30";
        private static readonly string[] DataSetSecurities = new[] { "SBER", "VTBR", "GAZP" };

        private string _originalDateFrom = string.Empty;
        private string _originalDateTo = string.Empty;
        private string _originalSourceType = string.Empty;
        private string _originalSetName = string.Empty;
        private string _originalFolderPath = string.Empty;
        private bool _originalTaxesIsOn;
        private string _originalMarginRegime = "Off";
        private string _originalStrategy = string.Empty;
        private bool _originalIsScript;
        private JsonElement _originalTradeSettings;
        private JsonElement _originalPositionSupport;
        private JsonElement _originalPhases;
        private JsonElement _originalFilters;

        public OptimizerTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            if (!EnsureOptimizerDataSet())
            {
                return;
            }

            if (!WaitForOptimizer())
            {
                _context.RecordFail(Module, "optimizer_open", "optimizer master is not available after mode open");
                return;
            }

            try
            {
                if (!TestDataGetConfig())
                {
                    return;
                }

                TestDataSetConfig();
                TestDataGetStatus();
                TestDividendsConfig();
                TestTradeSettings();
                TestPositionSupport();
                TestPhases();
                TestFilters();
                TestBotGetSet();
                TestParams();
                TestPassCountAndThreads();
                TestStartStopStatus();
                TestReport();
                TestBotSetUnknown();
                TestOptimizerRun();
                TestOptimizerRunScreener();
            }
            finally
            {
                RestoreSettings();
            }
        }

        private bool TestDataGetConfig()
        {
            const string method = "optimizer_data_get_config";
            object request = new { };

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
                    "source_type", "set_name", "folder_path", "type_tester_data", "date_from", "date_to"
                };

                foreach (string field in requiredFields)
                {
                    if (!config.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, method, $"{field} missing");
                        return false;
                    }
                }

                _originalDateFrom = config.GetProperty("date_from").GetString() ?? string.Empty;
                _originalDateTo = config.GetProperty("date_to").GetString() ?? string.Empty;
                _originalSourceType = config.GetProperty("source_type").GetString() ?? string.Empty;
                _originalSetName = config.GetProperty("set_name").GetString() ?? string.Empty;
                _originalFolderPath = config.GetProperty("folder_path").GetString() ?? string.Empty;

                _context.RecordPass(Module, method, "config received");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void TestDataSetConfig()
        {
            const string method = "optimizer_data_set_config";

            object request = new
            {
                date_from = "2024-01-01T00:00:00",
                date_to = "2024-06-30T00:00:00"
            };

            try
            {
                _context.PrintRequest(Module, method, request);

                // хранилище перезаписывает даты мастера событиями загрузки —
                // применяем повторно, пока значения не закрепятся
                if (!ApplyDataConfigUntilStable(request, null, "2024-01-01", "2024-06-30"))
                {
                    _context.PrintResponse("");
                    _context.RecordFail(Module, method, "dates were not applied");
                    return;
                }

                string response = _context.Client.ToolsCall("optimizer_data_get_config", new { });
                _context.PrintResponse(response);

                _context.RecordPass(Module, method, "dates updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool ApplyDataConfigUntilStable(object request, string expectedSetName,
            string expectedDateFrom, string expectedDateTo)
        {
            // пока хранилище грузится, его события перезаписывают даты мастера.
            // ждём два стабильных чтения подряд; при откате применяем конфиг заново
            DateTime deadline = DateTime.Now.AddSeconds(180);
            int stableReads = 0;

            _context.Client.ToolsCall("optimizer_data_set_config", request);

            while (DateTime.Now < deadline)
            {
                Thread.Sleep(1500);

                string response = _context.Client.ToolsCall("optimizer_data_get_config", new { });

                if (!IsSuccessResponse(response, out string text) || string.IsNullOrEmpty(text))
                {
                    stableReads = 0;
                    continue;
                }

                try
                {
                    using (JsonDocument document = JsonDocument.Parse(text))
                    {
                        JsonElement config = document.RootElement;

                        bool datesMatch = config.GetProperty("date_from").GetString().StartsWith(expectedDateFrom)
                            && config.GetProperty("date_to").GetString().StartsWith(expectedDateTo);

                        bool setMatches = expectedSetName == null
                            || (config.TryGetProperty("set_name", out JsonElement setNameElement)
                                && setNameElement.GetString() == expectedSetName);

                        if (datesMatch && setMatches)
                        {
                            stableReads++;

                            if (stableReads >= 2)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            stableReads = 0;
                            _context.Client.ToolsCall("optimizer_data_set_config", request);
                        }
                    }
                }
                catch
                {
                    stableReads = 0;
                }
            }

            return false;
        }

        private void TestDataGetStatus()
        {
            const string method = "optimizer_data_get_status";
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

                string[] requiredFields = new[]
                {
                    "is_loaded", "securities_count", "time_min", "time_max", "available_sets"
                };

                foreach (string field in requiredFields)
                {
                    if (!config.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, method, $"{field} missing");
                        return;
                    }
                }

                _context.RecordPass(Module, method, $"securities={config.GetProperty("securities_count").GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDividendsConfig()
        {
            const string getMethod = "optimizer_dividends_get_config";
            const string setMethod = "optimizer_dividends_set_config";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                _originalTaxesIsOn = getConfig.GetProperty("taxes_is_on").GetBoolean();
                _originalMarginRegime = getConfig.GetProperty("margin_regime").GetString() ?? "Off";

                object setRequest = new { taxes_is_on = !_originalTaxesIsOn, margin_regime = "Percent" };
                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("taxes_is_on").GetBoolean() != !_originalTaxesIsOn
                    || setConfig.GetProperty("margin_regime").GetString() != "Percent")
                {
                    _context.RecordFail(Module, setMethod, "dividends config was not applied");
                    return;
                }

                _context.RecordPass(Module, setMethod, "dividends config updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestTradeSettings()
        {
            const string getMethod = "optimizer_trade_settings_get";
            const string setMethod = "optimizer_trade_settings_set";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "commission_type", "commission_value", "order_execution_type",
                    "slippage_to_simple_order", "slippage_to_stop_order", "start_deposit"
                };

                foreach (string field in requiredFields)
                {
                    if (!getConfig.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, getMethod, $"{field} missing");
                        return;
                    }
                }

                _originalTradeSettings = getConfig.Clone();

                object setRequest = new
                {
                    commission_type = "Percent",
                    commission_value = 0.05m,
                    order_execution_type = "Intersection",
                    slippage_to_simple_order = 2,
                    slippage_to_stop_order = 3,
                    start_deposit = 250000m
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("commission_type").GetString() != "Percent"
                    || setConfig.GetProperty("commission_value").GetDecimal() != 0.05m
                    || setConfig.GetProperty("order_execution_type").GetString() != "Intersection"
                    || setConfig.GetProperty("slippage_to_simple_order").GetInt32() != 2
                    || setConfig.GetProperty("slippage_to_stop_order").GetInt32() != 3
                    || setConfig.GetProperty("start_deposit").GetDecimal() != 250000m)
                {
                    _context.RecordFail(Module, setMethod, "trade settings were not applied");
                    return;
                }

                _context.RecordPass(Module, setMethod, "trade settings updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestPositionSupport()
        {
            const string getMethod = "optimizer_position_support_get";
            const string setMethod = "optimizer_position_support_set";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "stop_is_on", "stop_distance", "profit_is_on", "second_to_open",
                    "setback_to_open_position", "setback_to_close_position",
                    "double_exit_is_on", "values_type", "order_type_time", "limits_maker_only"
                };

                foreach (string field in requiredFields)
                {
                    if (!getConfig.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, getMethod, $"{field} missing");
                        return;
                    }
                }

                _originalPositionSupport = getConfig.Clone();

                object setRequest = new
                {
                    stop_is_on = true,
                    stop_distance = 42m,
                    second_to_open_is_on = true,
                    second_to_open = 77,
                    setback_to_open_is_on = true,
                    setback_to_open_position = 9m,
                    setback_to_close_is_on = true,
                    setback_to_close_position = 21m,
                    limits_maker_only = true
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("stop_is_on").GetBoolean() != true
                    || setConfig.GetProperty("stop_distance").GetDecimal() != 42m
                    || setConfig.GetProperty("second_to_open").GetDouble() != 77
                    || setConfig.GetProperty("setback_to_open_position").GetDecimal() != 9m
                    || setConfig.GetProperty("setback_to_close_position").GetDecimal() != 21m
                    || setConfig.GetProperty("limits_maker_only").GetBoolean() != true)
                {
                    _context.RecordFail(Module, setMethod, "position support was not applied");
                    return;
                }

                _context.RecordPass(Module, setMethod, "position support updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestPhases()
        {
            const string getMethod = "optimizer_phases_get";
            const string setMethod = "optimizer_phases_set";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "time_start", "time_end", "iteration_count", "percent_on_filtration",
                    "last_in_sample", "fazes"
                };

                foreach (string field in requiredFields)
                {
                    if (!getConfig.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, getMethod, $"{field} missing");
                        return;
                    }
                }

                _originalPhases = getConfig.Clone();

                object setRequest = new
                {
                    iteration_count = 2,
                    percent_on_filtration = 25m,
                    last_in_sample = false
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("iteration_count").GetInt32() != 2
                    || setConfig.GetProperty("percent_on_filtration").GetDecimal() != 25m
                    || setConfig.GetProperty("last_in_sample").GetBoolean() != false)
                {
                    _context.RecordFail(Module, setMethod, "phases were not applied");
                    return;
                }

                // при 2 итерациях и выключенном last_in_sample должно быть 4 фазы
                if (setConfig.GetProperty("fazes_count").GetInt32() != 4)
                {
                    _context.RecordFail(Module, setMethod, $"expected 4 rebuilt fazes, got {setConfig.GetProperty("fazes_count").GetInt32()}");
                    return;
                }

                _context.RecordPass(Module, setMethod, "phases updated and rebuilt");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestFilters()
        {
            const string getMethod = "optimizer_filters_get";
            const string setMethod = "optimizer_filters_set";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                string[] requiredFields = new[]
                {
                    "filter_profit_value", "filter_profit_is_on",
                    "filter_max_draw_down_value", "filter_max_draw_down_is_on",
                    "filter_middle_profit_value", "filter_profit_factor_value",
                    "filter_deals_count_value", "filter_deals_count_is_on"
                };

                foreach (string field in requiredFields)
                {
                    if (!getConfig.TryGetProperty(field, out _))
                    {
                        _context.RecordFail(Module, getMethod, $"{field} missing");
                        return;
                    }
                }

                _originalFilters = getConfig.Clone();

                object setRequest = new
                {
                    filter_profit_value = 15m,
                    filter_profit_is_on = true,
                    filter_deals_count_value = 7,
                    filter_deals_count_is_on = true
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("filter_profit_value").GetDecimal() != 15m
                    || setConfig.GetProperty("filter_profit_is_on").GetBoolean() != true
                    || setConfig.GetProperty("filter_deals_count_value").GetInt32() != 7
                    || setConfig.GetProperty("filter_deals_count_is_on").GetBoolean() != true)
                {
                    _context.RecordFail(Module, setMethod, "filters were not applied");
                    return;
                }

                _context.RecordPass(Module, setMethod, "filters updated");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestBotGetSet()
        {
            const string getMethod = "optimizer_bot_get";
            const string setMethod = "optimizer_bot_set";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                _originalStrategy = getConfig.GetProperty("strategy_name").GetString() ?? string.Empty;
                _originalIsScript = getConfig.GetProperty("is_script").GetBoolean();

                object setRequest = new { strategy_name = "TwoTimeFramesBot" };
                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("strategy_name").GetString() != "TwoTimeFramesBot"
                    || setConfig.GetProperty("is_loaded").GetBoolean() != true)
                {
                    _context.RecordFail(Module, setMethod, "robot was not selected");
                    return;
                }

                _context.RecordPass(Module, setMethod, "robot selected and loaded");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestParams()
        {
            const string getMethod = "optimizer_params_get";
            const string setMethod = "optimizer_params_set";
            const string resetMethod = "optimizer_params_reset";

            try
            {
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                JsonElement pcLength = default;
                bool found = false;

                foreach (JsonElement parameter in getConfig.GetProperty("parameters").EnumerateArray())
                {
                    if (parameter.GetProperty("name").GetString() == "PC length")
                    {
                        pcLength = parameter;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, getMethod, "parameter 'PC length' not found in TwoTimeFramesBot");
                    return;
                }

                _originalPcLength = pcLength.Clone();

                // set value, range and on-flag
                object setRequest = new
                {
                    parameters = new object[]
                    {
                        new { name = "PC length", value = 25, start = 10, stop = 40, step = 5, on = true }
                    }
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (!FindParam(setConfig, "PC length", out JsonElement changed)
                    || changed.GetProperty("value").GetInt32() != 25
                    || changed.GetProperty("start").GetInt32() != 10
                    || changed.GetProperty("stop").GetInt32() != 40
                    || changed.GetProperty("step").GetInt32() != 5
                    || changed.GetProperty("on").GetBoolean() != true)
                {
                    _context.RecordFail(Module, setMethod, "parameter was not applied");
                    return;
                }

                // start > stop must be rejected
                object badRange = new
                {
                    parameters = new object[]
                    {
                        new { name = "PC length", start = 50, stop = 10 }
                    }
                };

                string badRangeResponse = _context.Client.ToolsCall(setMethod, badRange);

                if (!ExpectIsError(badRangeResponse, setMethod))
                {
                    return;
                }

                // unknown parameter must be rejected
                object badName = new
                {
                    parameters = new object[]
                    {
                        new { name = "McpNoSuchParam", value = 1 }
                    }
                };

                string badNameResponse = _context.Client.ToolsCall(setMethod, badName);

                if (!ExpectIsError(badNameResponse, setMethod))
                {
                    return;
                }

                _context.RecordPass(Module, setMethod, "parameter updated, bad range and unknown name rejected");

                // reset to standard
                _context.PrintRequest(Module, resetMethod, new { });
                string resetResponse = _context.Client.ToolsCall(resetMethod, new { });
                _context.PrintResponse(resetResponse);

                if (!TryParseConfig(resetResponse, resetMethod, out JsonElement resetConfig))
                {
                    return;
                }

                if (!FindParam(resetConfig, "PC length", out JsonElement resetParam)
                    || resetParam.GetProperty("value").GetInt32() != 20
                    || resetParam.GetProperty("start").GetInt32() != 5
                    || resetParam.GetProperty("stop").GetInt32() != 50)
                {
                    _context.RecordFail(Module, resetMethod, "parameter was not reset to standard");
                    return;
                }

                _context.RecordPass(Module, resetMethod, "parameters reset to standard");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private bool FindParam(JsonElement config, string paramName, out JsonElement parameter)
        {
            parameter = default;

            foreach (JsonElement item in config.GetProperty("parameters").EnumerateArray())
            {
                if (item.GetProperty("name").GetString() == paramName)
                {
                    parameter = item;
                    return true;
                }
            }

            return false;
        }

        private JsonElement _originalPcLength;

        private void TestPassCountAndThreads()
        {
            const string passMethod = "optimizer_get_pass_count";
            const string getMethod = "optimizer_get_threads";
            const string setMethod = "optimizer_set_threads";

            try
            {
                object passRequest = new { };
                _context.PrintRequest(Module, passMethod, passRequest);
                string passResponse = _context.Client.ToolsCall(passMethod, passRequest);
                _context.PrintResponse(passResponse);

                if (!TryParseConfig(passResponse, passMethod, out JsonElement passConfig))
                {
                    return;
                }

                if (!passConfig.TryGetProperty("pass_count", out JsonElement passCount)
                    || passCount.GetInt32() < 0)
                {
                    _context.RecordFail(Module, passMethod, "pass_count missing or negative");
                    return;
                }

                _context.RecordPass(Module, passMethod, $"pass_count={passCount.GetInt32()}");

                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                _originalThreads = getConfig.GetProperty("threads_count").GetInt32();

                object setRequest = new { threads_count = 3 };
                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                if (!TryParseConfig(setResponse, setMethod, out JsonElement setConfig))
                {
                    return;
                }

                if (setConfig.GetProperty("threads_count").GetInt32() != 3)
                {
                    _context.RecordFail(Module, setMethod, "threads were not applied");
                    return;
                }

                string badZero = _context.Client.ToolsCall(setMethod, new { threads_count = 0 });

                if (!ExpectIsError(badZero, setMethod))
                {
                    return;
                }

                string badHundred = _context.Client.ToolsCall(setMethod, new { threads_count = 100 });

                if (!ExpectIsError(badHundred, setMethod))
                {
                    return;
                }

                _context.RecordPass(Module, setMethod, "threads updated, out-of-range rejected");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private int _originalThreads;

        private void TestStartStopStatus()
        {
            const string startMethod = "optimizer_start";
            const string stopMethod = "optimizer_stop";
            const string statusMethod = "optimizer_get_status";

            try
            {
                object statusRequest = new { };
                _context.PrintRequest(Module, statusMethod, statusRequest);
                string statusResponse = _context.Client.ToolsCall(statusMethod, statusRequest);
                _context.PrintResponse(statusResponse);

                if (!TryParseConfig(statusResponse, statusMethod, out JsonElement statusConfig))
                {
                    return;
                }

                if (statusConfig.GetProperty("is_running").GetBoolean() != false
                    || !statusConfig.TryGetProperty("prime_progress", out _)
                    || !statusConfig.TryGetProperty("threads", out _))
                {
                    _context.RecordFail(Module, statusMethod, "status response mismatch");
                    return;
                }

                object stopRequest = new { };
                _context.PrintRequest(Module, stopMethod, stopRequest);
                string stopResponse = _context.Client.ToolsCall(stopMethod, stopRequest);
                _context.PrintResponse(stopResponse);

                if (!TryParseConfig(stopResponse, stopMethod, out JsonElement stopConfig))
                {
                    return;
                }

                if (stopConfig.GetProperty("stopped").GetBoolean() != false)
                {
                    _context.RecordFail(Module, stopMethod, "stop while not running must return stopped=false");
                    return;
                }

                // в среде стенда вкладки робота без бумаг — старт должен отказать со списком ошибок
                object startRequest = new { };
                _context.PrintRequest(Module, startMethod, startRequest);
                string startResponse = _context.Client.ToolsCall(startMethod, startRequest);
                _context.PrintResponse(startResponse);

                if (!TryParseConfig(startResponse, startMethod, out JsonElement startConfig))
                {
                    return;
                }

                if (startConfig.GetProperty("started").GetBoolean() == true)
                {
                    // оптимизация реально пошла — глушим её, чтобы не мешала остальным тестам
                    _context.Client.ToolsCall(stopMethod, stopRequest);

                    DateTime stopDeadline = DateTime.Now.AddSeconds(60);

                    while (DateTime.Now < stopDeadline)
                    {
                        string waitResponse = _context.Client.ToolsCall(statusMethod, statusRequest);

                        if (IsSuccessResponse(waitResponse, out string waitText)
                            && !string.IsNullOrEmpty(waitText))
                        {
                            try
                            {
                                using (JsonDocument waitDocument = JsonDocument.Parse(waitText))
                                {
                                    if (waitDocument.RootElement.TryGetProperty("is_running", out JsonElement isRunningElement)
                                        && isRunningElement.GetBoolean() == false)
                                    {
                                        break;
                                    }
                                }
                            }
                            catch
                            {
                                // статус ещё не прочитался — ждём дальше
                            }
                        }

                        Thread.Sleep(1000);
                    }

                    _context.RecordPass(Module, startMethod, "optimization started in this environment, stopped");
                    return;
                }

                if (!startConfig.TryGetProperty("errors", out JsonElement errors)
                    || errors.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, startMethod, "start failed without errors list");
                    return;
                }

                string statusAfter = _context.Client.ToolsCall(statusMethod, statusRequest);

                if (!TryParseConfig(statusAfter, statusMethod, out JsonElement afterConfig)
                    || afterConfig.GetProperty("is_running").GetBoolean() != false)
                {
                    _context.RecordFail(Module, startMethod, "is_running must stay false after rejected start");
                    return;
                }

                _context.RecordPass(Module, startMethod, $"rejected with {errors.GetArrayLength()} readiness errors");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, startMethod, error.Message);
            }
        }

        private void TestReport()
        {
            const string getMethod = "optimizer_get_report";
            const string saveMethod = "optimizer_save_report";
            const string loadMethod = "optimizer_load_report";
            string reportFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "mcp-test-report.txt");

            try
            {
                if (System.IO.File.Exists(reportFile))
                {
                    System.IO.File.Delete(reportFile);
                }

                // пустой отчёт: структура ответа
                object getRequest = new { };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!TryParseConfig(getResponse, getMethod, out JsonElement getConfig))
                {
                    return;
                }

                if (!getConfig.TryGetProperty("is_partial", out _)
                    || !getConfig.TryGetProperty("fazes", out _)
                    || !getConfig.TryGetProperty("fazes_count", out _)
                    || !getConfig.TryGetProperty("reports_count", out _))
                {
                    _context.RecordFail(Module, getMethod, "report response is incomplete");
                    return;
                }

                // сохранение без результатов — ошибка
                string saveEmptyResponse = _context.Client.ToolsCall(saveMethod, new { path = reportFile });

                if (!ExpectIsError(saveEmptyResponse, saveMethod))
                {
                    return;
                }

                // загрузка несуществующего файла — ошибка
                string loadMissingResponse = _context.Client.ToolsCall(loadMethod, new { path = reportFile });

                if (!ExpectIsError(loadMissingResponse, loadMethod))
                {
                    return;
                }

                // фабрикованный файл с одной фазой без отчётов
                System.IO.File.WriteAllText(reportFile,
                    "InSample%01/01/2024 00:00:00%06/30/2024 00:00:00%180%^\r\n");

                _context.PrintRequest(Module, loadMethod, new { path = reportFile });
                string loadResponse = _context.Client.ToolsCall(loadMethod, new { path = reportFile });
                _context.PrintResponse(loadResponse);

                if (!TryParseConfig(loadResponse, loadMethod, out JsonElement loadConfig))
                {
                    return;
                }

                if (loadConfig.GetProperty("fazes_count").GetInt32() != 1
                    || loadConfig.GetProperty("reports_count").GetInt32() != 0)
                {
                    _context.RecordFail(Module, loadMethod, "loaded report mismatch");
                    return;
                }

                // теперь результаты есть — сохранение должно сработать
                _context.PrintRequest(Module, saveMethod, new { path = reportFile });
                string saveResponse = _context.Client.ToolsCall(saveMethod, new { path = reportFile });
                _context.PrintResponse(saveResponse);

                if (!TryParseConfig(saveResponse, saveMethod, out JsonElement saveConfig))
                {
                    return;
                }

                if (saveConfig.GetProperty("saved").GetBoolean() != true
                    || !System.IO.File.Exists(reportFile))
                {
                    _context.RecordFail(Module, saveMethod, "report was not saved");
                    return;
                }

                _context.RecordPass(Module, getMethod, "report get/load/save roundtrip works");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, getMethod, error.Message);
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(reportFile))
                    {
                        System.IO.File.Delete(reportFile);
                    }
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        private void TestOptimizerRun()
        {
            const string method = "optimizer_run_e2e";
            const string setName = DataSetName;
            SseCollector? collector = null;

            try
            {
                // сет гарантируется шагом data_ensure_set в начале модуля — здесь только проверяем
                string statusResponse = _context.Client.ToolsCall("optimizer_data_get_status", new { });

                if (!TryParseConfig(statusResponse, "optimizer_data_get_status", out JsonElement statusConfig))
                {
                    _context.RecordFail(Module, method, "failed to get optimizer data status");
                    return;
                }

                bool setPresent = false;

                foreach (JsonElement availableSet in statusConfig.GetProperty("available_sets").EnumerateArray())
                {
                    if (availableSet.GetString() == setName)
                    {
                        setPresent = true;
                        break;
                    }
                }

                if (!setPresent)
                {
                    _context.RecordFail(Module, method, $"data set '{setName}' not found after data_ensure_set");
                    return;
                }

                // 2. Конфигурация данных оптимизатора
                object dataRequest = new
                {
                    source_type = "Set",
                    set_name = setName,
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-03-31T00:00:00"
                };

                _context.PrintRequest(Module, "optimizer_data_set_config", dataRequest);

                if (!ApplyDataConfigUntilStable(dataRequest, setName, "2024-01-01", "2024-03-31"))
                {
                    _context.RecordFail(Module, method, "data config was not applied");
                    return;
                }

                if (!WaitForStorageLoaded(DataSetSecurities.Length))
                {
                    _context.RecordFail(Module, method, "optimizer storage was not loaded in time");
                    return;
                }

                // 4. Робот и вкладка
                string botResponse = _context.Client.ToolsCall("optimizer_bot_set", new { strategy_name = "TwoTimeFramesBot" });

                if (!TryParseConfig(botResponse, "optimizer_bot_set", out _))
                {
                    _context.RecordFail(Module, method, "failed to select robot");
                    return;
                }

                _context.PrintRequest(Module, "optimizer_bot_tab_get_config", new { });
                string tabsResponse = _context.Client.ToolsCall("optimizer_bot_tab_get_config", new { });
                _context.PrintResponse(tabsResponse);

                if (!TryParseConfig(tabsResponse, "optimizer_bot_tab_get_config", out JsonElement tabsConfig)
                    || tabsConfig.GetProperty("count").GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "no tabs in optimization robot");
                    return;
                }

                // вкладок у робота может быть несколько — бумага нужна в каждой,
                // иначе старт откажет с "No securities configured in robot tabs".
                // имена бумаг в хранилище оптимизатора — имена файлов с расширением ("SBER.txt")
                foreach (JsonElement tab in tabsConfig.GetProperty("tabs").EnumerateArray())
                {
                    string tabName = tab.GetProperty("tab_name").GetString() ?? string.Empty;

                    object tabSetRequest = new
                    {
                        tab_name = tabName,
                        security_name = "SBER.txt",
                        time_frame = "Min30"
                    };

                    _context.PrintRequest(Module, "optimizer_bot_tab_set_config", tabSetRequest);
                    string tabSetResponse = _context.Client.ToolsCall("optimizer_bot_tab_set_config", tabSetRequest);
                    _context.PrintResponse(tabSetResponse);

                    if (!TryParseConfig(tabSetResponse, "optimizer_bot_tab_set_config", out JsonElement tabSetConfig)
                        || tabSetConfig.GetProperty("security_name").GetString() != "SBER.txt")
                    {
                        _context.RecordFail(Module, method, $"failed to configure robot tab '{tabName}'");
                        return;
                    }
                }

                // 5. Параметры и фазы. Фильтры гасим явно: конфиг-тесты выше
                // включали свои, иначе отчёты E2E будут отфильтрованы
                _context.Client.ToolsCall("optimizer_filters_set", new
                {
                    filter_profit_is_on = false,
                    filter_max_draw_down_is_on = false,
                    filter_middle_profit_is_on = false,
                    filter_profit_factor_is_on = false,
                    filter_deals_count_is_on = false
                });

                _context.Client.ToolsCall("optimizer_params_set", new
                {
                    parameters = new object[]
                    {
                        new { name = "PC length", value = 20, start = 20, stop = 22, step = 1, on = true },
                        new { name = "Regime", value = "On" }
                    }
                });

                _context.Client.ToolsCall("optimizer_phases_set", new
                {
                    time_start = "2024-01-01T00:00:00",
                    time_end = "2024-03-31T00:00:00",
                    iteration_count = 1,
                    last_in_sample = false
                });

                string passResponse = _context.Client.ToolsCall("optimizer_get_pass_count", new { });

                if (!TryParseConfig(passResponse, "optimizer_get_pass_count", out JsonElement passConfig)
                    || passConfig.GetProperty("pass_count").GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "pass count is zero");
                    return;
                }

                // 6. Запуск с сбором SSE
                collector = new SseCollector(_context.Client, eventName => eventName.StartsWith("optimizer.test."));
                collector.Start();

                _context.PrintRequest(Module, "optimizer_start", new { });
                string startResponse = _context.Client.ToolsCall("optimizer_start", new { });
                _context.PrintResponse(startResponse);

                if (!TryParseConfig(startResponse, "optimizer_start", out JsonElement startConfig)
                    || startConfig.GetProperty("started").GetBoolean() != true)
                {
                    _context.RecordFail(Module, method, "optimization was not started");
                    return;
                }

                if (!WaitForOptimizationEnd())
                {
                    _context.RecordFail(Module, method, "optimization did not finish in time");
                    return;
                }

                collector.Stop(TimeSpan.FromSeconds(2));

                // 7. Отчёт
                _context.PrintRequest(Module, "optimizer_get_report", new { });
                string reportResponse = _context.Client.ToolsCall("optimizer_get_report", new { });
                _context.PrintResponse(reportResponse);

                if (!TryParseConfig(reportResponse, "optimizer_get_report", out JsonElement reportConfig))
                {
                    return;
                }

                int reportsCount = reportConfig.GetProperty("reports_count").GetInt32();

                if (reportsCount == 0)
                {
                    _context.RecordFail(Module, method, "report is empty after finished optimization");
                    return;
                }

                List<string> events = collector.GetEvents();
                bool finished = false;

                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] == "optimizer.test.finished")
                    {
                        finished = true;
                        break;
                    }
                }

                if (!finished)
                {
                    _context.RecordFail(Module, method, "optimizer.test.finished event was not received");
                    return;
                }

                // сводка по первой фазе для отчёта в лог
                JsonElement firstFaze = reportConfig.GetProperty("fazes")[0];
                JsonElement firstReport = firstFaze.GetProperty("reports")[0];
                string botName = firstReport.GetProperty("bot_name").GetString() ?? string.Empty;
                decimal profit = firstReport.GetProperty("total_profit").GetDecimal();
                int positions = firstReport.GetProperty("positions_count").GetInt32();

                _context.RecordPass(Module, method,
                    $"reports={reportsCount}, events={events.Count}, faze0: {botName}, profit={profit}, positions={positions}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"TestOptimizerRun failed: {error.Message}");
            }
            finally
            {
                collector?.Stop(TimeSpan.Zero);

                try
                {
                    // возвращаем вкладку и Regime в исходное состояние
                    _context.Client.ToolsCall("optimizer_params_set", new
                    {
                        parameters = new object[]
                        {
                            new { name = "Regime", value = "Off" }
                        }
                    });
                }
                catch
                {
                    // ignore restore errors
                }
            }
        }

        private void TestOptimizerRunScreener()
        {
            const string method = "optimizer_run_screener_e2e";
            const string setName = DataSetName;
            SseCollector? collector = null;
            JsonElement originalPcAdx = default;
            string originalRegime = string.Empty;
            List<JsonElement> allParams = new List<JsonElement>();

            try
            {
                // 1. данные: тот же сет, что и в простом E2E
                object dataRequest = new
                {
                    source_type = "Set",
                    set_name = setName,
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-03-31T00:00:00"
                };

                if (!ApplyDataConfigUntilStable(dataRequest, setName, "2024-01-01", "2024-03-31"))
                {
                    _context.RecordFail(Module, method, "data config was not applied");
                    return;
                }

                if (!WaitForStorageLoaded(DataSetSecurities.Length))
                {
                    _context.RecordFail(Module, method, "optimizer storage was not loaded in time");
                    return;
                }

                // фильтры гасим явно: конфиг-тесты выше включали свои
                _context.Client.ToolsCall("optimizer_filters_set", new
                {
                    filter_profit_is_on = false,
                    filter_max_draw_down_is_on = false,
                    filter_middle_profit_is_on = false,
                    filter_profit_factor_is_on = false,
                    filter_deals_count_is_on = false
                });

                // 2. робот со скринер-вкладкой
                string botResponse = _context.Client.ToolsCall("optimizer_bot_set", new { strategy_name = "AlgoStart3PriceChannel" });

                if (!TryParseConfig(botResponse, "optimizer_bot_set", out JsonElement botConfig)
                    || botConfig.GetProperty("is_loaded").GetBoolean() != true)
                {
                    _context.RecordFail(Module, method, "failed to select screener robot");
                    return;
                }

                // 3. скринер: три бумаги одним источником
                _context.PrintRequest(Module, "optimizer_bot_tab_get_config", new { });
                string tabsResponse = _context.Client.ToolsCall("optimizer_bot_tab_get_config", new { });
                _context.PrintResponse(tabsResponse);

                if (!TryParseConfig(tabsResponse, "optimizer_bot_tab_get_config", out JsonElement tabsConfig)
                    || tabsConfig.GetProperty("count").GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "no tabs in screener robot");
                    return;
                }

                string screenerTabName = string.Empty;

                foreach (JsonElement tab in tabsConfig.GetProperty("tabs").EnumerateArray())
                {
                    if (tab.GetProperty("tab_type").GetString() == "Screener")
                    {
                        screenerTabName = tab.GetProperty("tab_name").GetString() ?? string.Empty;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(screenerTabName))
                {
                    _context.RecordFail(Module, method, "no Screener tab in AlgoStart3PriceChannel");
                    return;
                }

                object tabSetRequest = new
                {
                    tab_name = screenerTabName,
                    time_frame = "Min30",
                    securities = new object[]
                    {
                        new { name = "SBER.txt" },
                        new { name = "VTBR.txt" },
                        new { name = "GAZP.txt" }
                    }
                };

                _context.PrintRequest(Module, "optimizer_bot_tab_set_config", tabSetRequest);
                string tabSetResponse = _context.Client.ToolsCall("optimizer_bot_tab_set_config", tabSetRequest);
                _context.PrintResponse(tabSetResponse);

                if (!TryParseConfig(tabSetResponse, "optimizer_bot_tab_set_config", out JsonElement tabSetConfig)
                    || tabSetConfig.GetProperty("securities_count").GetInt32() != 3
                    || tabSetConfig.GetProperty("tabs_count").GetInt32() != 3)
                {
                    _context.RecordFail(Module, method, "screener tab was not configured with 3 securities");
                    return;
                }

                // 4. параметры: запоминаем исходные, включаем перебор одного
                string paramsResponse = _context.Client.ToolsCall("optimizer_params_get", new { });

                if (!TryParseConfig(paramsResponse, "optimizer_params_get", out JsonElement paramsConfig))
                {
                    return;
                }

                if (!FindParam(paramsConfig, "Pc adx length", out JsonElement pcAdx))
                {
                    _context.RecordFail(Module, method, "parameter 'Pc adx length' not found in AlgoStart3PriceChannel");
                    return;
                }

                originalPcAdx = pcAdx.Clone();

                if (FindParam(paramsConfig, "Regime", out JsonElement regime))
                {
                    originalRegime = regime.GetProperty("value").GetString() ?? string.Empty;
                }

                // все остальные параметры гасим: иначе проходы перемножаются
                // с тем, что пользователь оставил включённым у этого робота
                foreach (JsonElement parameter in paramsConfig.GetProperty("parameters").EnumerateArray())
                {
                    allParams.Add(parameter.Clone());
                }

                List<object> setList = new List<object>();

                for (int i = 0; i < allParams.Count; i++)
                {
                    string paramName = allParams[i].GetProperty("name").GetString() ?? string.Empty;

                    if (paramName == "Pc adx length")
                    {
                        setList.Add(new { name = paramName, value = 50, start = 50, stop = 51, step = 1, on = true });
                    }
                    else if (paramName == "Regime")
                    {
                        setList.Add(new { name = paramName, value = "On" });
                    }
                    else
                    {
                        setList.Add(new { name = paramName, on = false });
                    }
                }

                _context.Client.ToolsCall("optimizer_params_set", new { parameters = setList.ToArray() });

                // 5. фазы
                _context.Client.ToolsCall("optimizer_phases_set", new
                {
                    time_start = "2024-01-01T00:00:00",
                    time_end = "2024-03-31T00:00:00",
                    iteration_count = 1,
                    last_in_sample = false
                });

                string passResponse = _context.Client.ToolsCall("optimizer_get_pass_count", new { });

                if (!TryParseConfig(passResponse, "optimizer_get_pass_count", out JsonElement passConfig)
                    || passConfig.GetProperty("pass_count").GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "pass count is zero");
                    return;
                }

                // 6. запуск с сбором SSE
                collector = new SseCollector(_context.Client, eventName => eventName.StartsWith("optimizer.test."));
                collector.Start();

                _context.PrintRequest(Module, "optimizer_start", new { });
                string startResponse = _context.Client.ToolsCall("optimizer_start", new { });
                _context.PrintResponse(startResponse);

                if (!TryParseConfig(startResponse, "optimizer_start", out JsonElement startConfig)
                    || startConfig.GetProperty("started").GetBoolean() != true)
                {
                    _context.RecordFail(Module, method, "screener optimization was not started");
                    return;
                }

                if (!WaitForOptimizationEnd())
                {
                    _context.RecordFail(Module, method, "screener optimization did not finish in time");
                    return;
                }

                collector.Stop(TimeSpan.FromSeconds(2));

                // 7. отчёт
                _context.PrintRequest(Module, "optimizer_get_report", new { });
                string reportResponse = _context.Client.ToolsCall("optimizer_get_report", new { });
                _context.PrintResponse(reportResponse);

                if (!TryParseConfig(reportResponse, "optimizer_get_report", out JsonElement reportConfig))
                {
                    return;
                }

                int reportsCount = reportConfig.GetProperty("reports_count").GetInt32();

                if (reportsCount == 0)
                {
                    _context.RecordFail(Module, method, "report is empty after finished screener optimization");
                    return;
                }

                List<string> events = collector.GetEvents();
                bool finished = false;

                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] == "optimizer.test.finished")
                    {
                        finished = true;
                        break;
                    }
                }

                if (!finished)
                {
                    _context.RecordFail(Module, method, "optimizer.test.finished event was not received");
                    return;
                }

                JsonElement firstFaze = reportConfig.GetProperty("fazes")[0];
                JsonElement firstReport = firstFaze.GetProperty("reports")[0];
                string botName = firstReport.GetProperty("bot_name").GetString() ?? string.Empty;
                decimal profit = firstReport.GetProperty("total_profit").GetDecimal();
                int positions = firstReport.GetProperty("positions_count").GetInt32();

                _context.RecordPass(Module, method,
                    $"reports={reportsCount}, events={events.Count}, faze0: {botName}, profit={profit}, positions={positions}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"TestOptimizerRunScreener failed: {error.Message}");
            }
            finally
            {
                collector?.Stop(TimeSpan.Zero);

                try
                {
                    // возвращаем параметры скринер-робота: on-флаги всех,
                    // Pc adx length целиком, Regime — значение
                    if (allParams.Count > 0)
                    {
                        List<object> restoreList = new List<object>();

                        for (int i = 0; i < allParams.Count; i++)
                        {
                            string paramName = allParams[i].GetProperty("name").GetString() ?? string.Empty;

                            if (paramName == "Pc adx length")
                            {
                                restoreList.Add(new
                                {
                                    name = paramName,
                                    value = originalPcAdx.GetProperty("value").GetInt32(),
                                    start = originalPcAdx.GetProperty("start").GetInt32(),
                                    stop = originalPcAdx.GetProperty("stop").GetInt32(),
                                    step = originalPcAdx.GetProperty("step").GetInt32(),
                                    on = originalPcAdx.GetProperty("on").GetBoolean()
                                });
                            }
                            else if (paramName == "Regime")
                            {
                                restoreList.Add(new
                                {
                                    name = paramName,
                                    value = string.IsNullOrEmpty(originalRegime) ? "Off" : originalRegime,
                                    on = allParams[i].GetProperty("on").GetBoolean()
                                });
                            }
                            else
                            {
                                restoreList.Add(new
                                {
                                    name = paramName,
                                    on = allParams[i].GetProperty("on").GetBoolean()
                                });
                            }
                        }

                        _context.Client.ToolsCall("optimizer_params_set", new { parameters = restoreList.ToArray() });
                    }

                    // возвращаем TwoTimeFramesBot, чтобы восстановление параметров модуля не споткнулось
                    _context.Client.ToolsCall("optimizer_bot_set", new { strategy_name = "TwoTimeFramesBot" });
                }
                catch
                {
                    // ignore restore errors
                }
            }
        }


        private bool EnsureOptimizerDataSet()
        {
            const string method = "data_ensure_set";

            try
            {
                // быстрый путь: сет уже на диске с данными по всем бумагам — ничего не качаем
                if (IsOptimizerDataSetOnDisk())
                {
                    _context.RecordPass(Module, method, $"set '{DataSetName}' found on disk, download skipped");
                    return true;
                }

                _context.PrintResponse($"set '{DataSetName}' is missing or incomplete, downloading via Os.Data");

                // в режиме оптимизатора Os.Data недоступен — перезапускаем процесс в главное окно
                _context.RestartOsEngine(string.Empty);

                try
                {
                    return DownloadOptimizerDataSet(method);
                }
                finally
                {
                    // возвращаем процесс в режим оптимизатора
                    _context.RestartOsEngine("-optimizer");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, $"EnsureOptimizerDataSet failed: {error.Message}");
                return false;
            }
        }

        private bool IsOptimizerDataSetOnDisk()
        {
            try
            {
                string dataDirectory = Path.Combine(
                    Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");

                string setFolder = Path.Combine(dataDirectory, "Set_" + DataSetName);

                if (!Directory.Exists(setFolder))
                {
                    return false;
                }

                for (int i = 0; i < DataSetSecurities.Length; i++)
                {
                    string securityFile = Path.Combine(setFolder,
                        DataSetSecurities[i], DataSetTimeFrame, DataSetSecurities[i] + ".txt");

                    if (!File.Exists(securityFile)
                        || new FileInfo(securityFile).Length == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool DownloadOptimizerDataSet(string method)
        {
            // открываем Os.Data и ждём, пока он поднимется
            _context.Client.ToolsCall("terminal_open_mode", new { mode = "data" });

            if (!WaitForOsDataMode())
            {
                _context.RecordFail(Module, method, "Os.Data mode did not open in time");
                return false;
            }

            // рецепт один в один из модуля Data (TestDownloadFlow):
            // сначала активация сервера данных, иначе CreateSet упадёт с "Active server not found"
            _context.Client.ToolsCall("server_management_activate", new { type = DataServerType });
            _context.Client.ToolsCall("server_instance_connect", new { type = DataServerType });

            bool securitiesReady = false;
            DateTime waitStart = DateTime.Now;

            while ((DateTime.Now - waitStart).TotalSeconds < 120)
            {
                string response = _context.Client.ToolsCall("server_instance_get_securities", new { type = DataServerType });

                if (IsSuccessResponse(response, out string text) && !string.IsNullOrEmpty(text))
                {
                    using (JsonDocument document = JsonDocument.Parse(text))
                    {
                        if (document.RootElement.TryGetProperty("count", out JsonElement countElement)
                            && countElement.GetInt32() > 0)
                        {
                            securitiesReady = true;
                            break;
                        }
                    }
                }

                Thread.Sleep(2000);
            }

            if (!securitiesReady)
            {
                _context.RecordFail(Module, method, $"server '{DataServerType}' did not provide securities within 120 seconds");
                return false;
            }

            // сносим недокачанный сет от прошлого неудачного прогона
            try { _context.Client.ToolsCall("data_set_off", new { name = DataSetName }); } catch { }
            try { _context.Client.ToolsCall("data_delete_set", new { name = DataSetName }); } catch { }

            object createRequest = new
            {
                name = DataSetName,
                source = DataServerType,
                source_name = DataServerType,
                timeframes = new[] { DataSetTimeFrame },
                date_from = "2024-01-01T00:00:00",
                date_to = "2024-06-30T00:00:00"
            };

            _context.PrintRequest(Module, "data_create_set", createRequest);
            string createResponse = _context.Client.ToolsCall("data_create_set", createRequest);
            _context.PrintResponse(createResponse);

            if (!IsSuccessResponse(createResponse, out _))
            {
                _context.RecordFail(Module, method, "failed to create data set");
                return false;
            }

            object[] securitiesArray = new object[DataSetSecurities.Length];

            for (int i = 0; i < DataSetSecurities.Length; i++)
            {
                securitiesArray[i] = new { name = DataSetSecurities[i] };
            }

            object addRequest = new { name = DataSetName, securities = securitiesArray };
            _context.PrintRequest(Module, "data_set_securities_add", addRequest);
            string addResponse = _context.Client.ToolsCall("data_set_securities_add", addRequest);
            _context.PrintResponse(addResponse);

            if (!IsSuccessResponse(addResponse, out string addText) || string.IsNullOrEmpty(addText))
            {
                _context.RecordFail(Module, method, "failed to add securities to set");
                return false;
            }

            using (JsonDocument addDocument = JsonDocument.Parse(addText))
            {
                if (!addDocument.RootElement.TryGetProperty("added_count", out JsonElement addedCountElement)
                    || addedCountElement.GetInt32() != DataSetSecurities.Length)
                {
                    _context.RecordFail(Module, method, "added_count does not match securities count");
                    return false;
                }
            }

            object onRequest = new { name = DataSetName };
            _context.PrintRequest(Module, "data_set_on", onRequest);
            string onResponse = _context.Client.ToolsCall("data_set_on", onRequest);
            _context.PrintResponse(onResponse);

            if (!IsSuccessResponse(onResponse, out _))
            {
                _context.RecordFail(Module, method, "failed to turn set on");
                return false;
            }

            // ждём полной закачки по всем бумагам
            DateTime deadline = DateTime.Now.AddSeconds(900);

            while (DateTime.Now < deadline)
            {
                if (IsOptimizerDataSetLoaded())
                {
                    _context.RecordPass(Module, method,
                        $"set '{DataSetName}' downloaded: {string.Join(", ", DataSetSecurities)}");
                    return true;
                }

                Thread.Sleep(5000);
            }

            _context.RecordFail(Module, method, "data set did not finish loading within 900 seconds");
            return false;
        }

        private bool WaitForOsDataMode()
        {
            DateTime deadline = DateTime.Now.AddSeconds(60);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("data_get_sets", new { });

                    if (IsSuccessResponse(response, out string text) && !string.IsNullOrEmpty(text))
                    {
                        using (JsonDocument document = JsonDocument.Parse(text))
                        {
                            // пока режим не открыт, вместо массива сетов приходит объект-заглушка
                            if (document.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // Os.Data ещё поднимается
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        private bool IsOptimizerDataSetLoaded()
        {
            for (int i = 0; i < DataSetSecurities.Length; i++)
            {
                string response = _context.Client.ToolsCall("data_get_security_status", new
                {
                    name = DataSetName,
                    security = DataSetSecurities[i],
                    timeframe = DataSetTimeFrame
                });

                if (!IsSuccessResponse(response, out string text) || string.IsNullOrEmpty(text))
                {
                    return false;
                }

                try
                {
                    using (JsonDocument document = JsonDocument.Parse(text))
                    {
                        string status = document.RootElement.TryGetProperty("status", out JsonElement statusElement)
                            ? statusElement.GetString() ?? string.Empty
                            : string.Empty;

                        int objectsCount = document.RootElement.TryGetProperty("objects_count", out JsonElement objectsElement)
                            ? objectsElement.GetInt32()
                            : 0;

                        if (status != "Load" || objectsCount <= 0)
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSuccessResponse(string response, out string text)
        {
            text = string.Empty;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        return false;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        return false;
                    }

                    text = content[0].GetProperty("Text").GetString() ?? string.Empty;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool WaitForStorageLoaded(int expectedSecuritiesCount)
        {
            DateTime deadline = DateTime.Now.AddSeconds(180);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("optimizer_data_get_status", new { });

                    using (JsonDocument document = JsonDocument.Parse(response))
                    {
                        string text = document.RootElement.GetProperty("Content")[0].GetProperty("Text").GetString() ?? string.Empty;

                        using (JsonDocument inner = JsonDocument.Parse(text))
                        {
                            // кол-во бумаг тоже проверяем: до конца перезагрузки
                            // хранилище может отдавать бумаги прошлого сета
                            if (inner.RootElement.TryGetProperty("is_loaded", out JsonElement isLoaded)
                                && isLoaded.GetBoolean()
                                && inner.RootElement.TryGetProperty("securities_count", out JsonElement securitiesCount)
                                && securitiesCount.GetInt32() == expectedSecuritiesCount)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // хранилище ещё не готово
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        private bool WaitForOptimizationEnd()
        {
            DateTime deadline = DateTime.Now.AddMinutes(5);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("optimizer_get_status", new { });

                    using (JsonDocument document = JsonDocument.Parse(response))
                    {
                        string text = document.RootElement.GetProperty("Content")[0].GetProperty("Text").GetString() ?? string.Empty;

                        using (JsonDocument inner = JsonDocument.Parse(text))
                        {
                            if (inner.RootElement.TryGetProperty("is_running", out JsonElement isRunning)
                                && !isRunning.GetBoolean())
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // статус ещё не доступен
                }

                Thread.Sleep(2000);
            }

            return false;
        }

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
                        }
                    }
                }
                catch
                {
                    // ignore read errors
                }
            }
        }

        private void TestBotSetUnknown()
        {
            const string method = "optimizer_bot_set";
            object request = new { strategy_name = "McpNoSuchStrategy" };

            try
            {
                _context.PrintRequest(Module, $"{method}(unknown)", request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || !isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "expected IsError for unknown strategy");
                        return;
                    }
                }

                _context.RecordPass(Module, method, "unknown strategy rejected");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void RestoreSettings()
        {
            // каждый шаг восстанавливаем независимо и проверяем ответ:
            // молчаливый откат настроек пользователя недопустим
            Dictionary<string, object> dataRestore = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(_originalSourceType))
            {
                dataRestore["source_type"] = _originalSourceType;
            }

            if (!string.IsNullOrWhiteSpace(_originalSetName))
            {
                dataRestore["set_name"] = _originalSetName;
            }

            if (!string.IsNullOrWhiteSpace(_originalFolderPath))
            {
                dataRestore["folder_path"] = _originalFolderPath;
            }

            if (!string.IsNullOrWhiteSpace(_originalDateFrom))
            {
                dataRestore["date_from"] = _originalDateFrom;
                dataRestore["date_to"] = _originalDateTo;
            }

            if (dataRestore.Count > 0)
            {
                RestoreCall("data", "optimizer_data_set_config", dataRestore);
            }

            RestoreCall("dividends", "optimizer_dividends_set_config", new
            {
                taxes_is_on = _originalTaxesIsOn,
                margin_regime = _originalMarginRegime
            });

            if (_originalTradeSettings.ValueKind == JsonValueKind.Object)
            {
                JsonElement trade = _originalTradeSettings;

                RestoreCall("trade settings", "optimizer_trade_settings_set", new
                {
                    commission_type = trade.GetProperty("commission_type").GetString(),
                    commission_value = trade.GetProperty("commission_value").GetDecimal(),
                    order_execution_type = trade.GetProperty("order_execution_type").GetString(),
                    slippage_to_simple_order = trade.GetProperty("slippage_to_simple_order").GetInt32(),
                    slippage_to_stop_order = trade.GetProperty("slippage_to_stop_order").GetInt32(),
                    start_deposit = trade.GetProperty("start_deposit").GetDecimal()
                });
            }

            if (_originalPhases.ValueKind == JsonValueKind.Object)
            {
                JsonElement phases = _originalPhases;

                // диапазон фаз — это даты мастера; берём исходные из data config,
                // а не захваченные в TestPhases (там уже стояли тестовые)
                RestoreCall("phases", "optimizer_phases_set", new
                {
                    time_start = string.IsNullOrWhiteSpace(_originalDateFrom)
                        ? phases.GetProperty("time_start").GetString()
                        : _originalDateFrom,
                    time_end = string.IsNullOrWhiteSpace(_originalDateTo)
                        ? phases.GetProperty("time_end").GetString()
                        : _originalDateTo,
                    iteration_count = phases.GetProperty("iteration_count").GetInt32(),
                    percent_on_filtration = phases.GetProperty("percent_on_filtration").GetDecimal(),
                    last_in_sample = phases.GetProperty("last_in_sample").GetBoolean()
                });
            }

            if (_originalFilters.ValueKind == JsonValueKind.Object)
            {
                JsonElement filters = _originalFilters;

                RestoreCall("filters", "optimizer_filters_set", new
                {
                    filter_profit_value = filters.GetProperty("filter_profit_value").GetDecimal(),
                    filter_profit_is_on = filters.GetProperty("filter_profit_is_on").GetBoolean(),
                    filter_max_draw_down_value = filters.GetProperty("filter_max_draw_down_value").GetDecimal(),
                    filter_max_draw_down_is_on = filters.GetProperty("filter_max_draw_down_is_on").GetBoolean(),
                    filter_middle_profit_value = filters.GetProperty("filter_middle_profit_value").GetDecimal(),
                    filter_middle_profit_is_on = filters.GetProperty("filter_middle_profit_is_on").GetBoolean(),
                    filter_profit_factor_value = filters.GetProperty("filter_profit_factor_value").GetDecimal(),
                    filter_profit_factor_is_on = filters.GetProperty("filter_profit_factor_is_on").GetBoolean(),
                    filter_deals_count_value = filters.GetProperty("filter_deals_count_value").GetInt32(),
                    filter_deals_count_is_on = filters.GetProperty("filter_deals_count_is_on").GetBoolean()
                });
            }

            if (_originalPositionSupport.ValueKind == JsonValueKind.Object)
            {
                JsonElement support = _originalPositionSupport;

                RestoreCall("position support", "optimizer_position_support_set", new
                {
                    stop_is_on = support.GetProperty("stop_is_on").GetBoolean(),
                    stop_distance = support.GetProperty("stop_distance").GetDecimal(),
                    stop_slippage = support.GetProperty("stop_slippage").GetDecimal(),
                    profit_is_on = support.GetProperty("profit_is_on").GetBoolean(),
                    profit_distance = support.GetProperty("profit_distance").GetDecimal(),
                    profit_slippage = support.GetProperty("profit_slippage").GetDecimal(),
                    second_to_open_is_on = support.GetProperty("second_to_open_is_on").GetBoolean(),
                    second_to_open = support.GetProperty("second_to_open").GetDouble(),
                    second_to_close_is_on = support.GetProperty("second_to_close_is_on").GetBoolean(),
                    second_to_close = support.GetProperty("second_to_close").GetDouble(),
                    setback_to_open_is_on = support.GetProperty("setback_to_open_is_on").GetBoolean(),
                    setback_to_open_position = support.GetProperty("setback_to_open_position").GetDecimal(),
                    setback_to_close_is_on = support.GetProperty("setback_to_close_is_on").GetBoolean(),
                    setback_to_close_position = support.GetProperty("setback_to_close_position").GetDecimal(),
                    double_exit_is_on = support.GetProperty("double_exit_is_on").GetBoolean(),
                    type_double_exit_order = support.GetProperty("type_double_exit_order").GetString(),
                    double_exit_slippage = support.GetProperty("double_exit_slippage").GetDecimal(),
                    values_type = support.GetProperty("values_type").GetString(),
                    order_type_time = support.GetProperty("order_type_time").GetString(),
                    limits_maker_only = support.GetProperty("limits_maker_only").GetBoolean()
                });
            }

            // параметры восстанавливаем ДО робота: optimizer_params_set применяется
            // к выбранному роботу, а это пока ещё TwoTimeFramesBot
            if (_originalPcLength.ValueKind == JsonValueKind.Object)
            {
                RestoreCall("params", "optimizer_params_set", new
                {
                    parameters = new object[]
                    {
                        new
                        {
                            name = "PC length",
                            value = _originalPcLength.GetProperty("value").GetInt32(),
                            start = _originalPcLength.GetProperty("start").GetInt32(),
                            stop = _originalPcLength.GetProperty("stop").GetInt32(),
                            step = _originalPcLength.GetProperty("step").GetInt32(),
                            on = _originalPcLength.GetProperty("on").GetBoolean()
                        }
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(_originalStrategy))
            {
                RestoreCall("strategy", "optimizer_bot_set", new
                {
                    strategy_name = _originalStrategy,
                    is_script = _originalIsScript
                });
            }

            if (_originalThreads > 0)
            {
                RestoreCall("threads", "optimizer_set_threads", new { threads_count = _originalThreads });
            }
        }

        private void RestoreCall(string name, string method, object request)
        {
            try
            {
                string response = _context.Client.ToolsCall(method, request);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    if (document.RootElement.TryGetProperty("IsError", out JsonElement isError)
                        && isError.GetBoolean())
                    {
                        Console.WriteLine($"  [OPTIMIZER] WARNING: restore '{name}' returned IsError: {response}");
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"  [OPTIMIZER] WARNING: restore '{name}' failed: {error.Message}");
            }
        }

        private bool WaitForOptimizer()
        {
            const string method = "optimizer_data_get_status";
            DateTime deadline = DateTime.Now.AddSeconds(30);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall(method, new { });

                    using (JsonDocument document = JsonDocument.Parse(response))
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
                    // мастер ещё не готов, ждём
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
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
