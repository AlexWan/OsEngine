/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.MCP.Json;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Tab.Internal;
using OsEngine.Robots;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for the optimizer (OptimizerMaster): data source,
    /// robot selection, parameters, phases, execution, report.
    /// Works only when the optimizer mode is open (OptimizerMaster.Master != null).
    /// </summary>
    public class OptimizerApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

        private OptimizerMaster _master;

        private DateTime _lastProgressSent = DateTime.MinValue;

        private bool _stopRequested;

        private bool _lastFinishedPartial;

        private TimeSpan? _timeToEnd;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public OptimizerApi(Action<string, object> publishEvent)
        {
            _publishEvent = publishEvent;
        }

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
                    case "optimizer_data_get_config":
                        response.Result = GetDataConfig();
                        break;

                    case "optimizer_data_set_config":
                        response.Result = SetDataConfig(request.Params);
                        break;

                    case "optimizer_data_get_status":
                        response.Result = GetDataStatus();
                        break;

                    case "optimizer_dividends_get_config":
                        response.Result = GetDividendsConfig();
                        break;

                    case "optimizer_dividends_set_config":
                        response.Result = SetDividendsConfig(request.Params);
                        break;

                    case "optimizer_bot_get":
                        response.Result = GetBot();
                        break;

                    case "optimizer_bot_set":
                        response.Result = SetBot(request.Params);
                        break;

                    case "optimizer_bot_tab_get_config":
                        response.Result = GetBotTabConfig(request.Params);
                        break;

                    case "optimizer_bot_tab_set_config":
                        response.Result = SetBotTabConfig(request.Params);
                        break;

                    case "optimizer_trade_settings_get":
                        response.Result = GetTradeSettings();
                        break;

                    case "optimizer_trade_settings_set":
                        response.Result = SetTradeSettings(request.Params);
                        break;

                    case "optimizer_position_support_get":
                        response.Result = GetPositionSupport();
                        break;

                    case "optimizer_position_support_set":
                        response.Result = SetPositionSupport(request.Params);
                        break;

                    case "optimizer_phases_get":
                        response.Result = GetPhases();
                        break;

                    case "optimizer_phases_set":
                        response.Result = SetPhases(request.Params);
                        break;

                    case "optimizer_filters_get":
                        response.Result = GetFilters();
                        break;

                    case "optimizer_filters_set":
                        response.Result = SetFilters(request.Params);
                        break;

                    case "optimizer_params_get":
                        response.Result = GetParams();
                        break;

                    case "optimizer_params_set":
                        response.Result = SetParams(request.Params);
                        break;

                    case "optimizer_params_reset":
                        response.Result = ResetParams();
                        break;

                    case "optimizer_get_pass_count":
                        response.Result = GetPassCount();
                        break;

                    case "optimizer_get_threads":
                        response.Result = GetThreads();
                        break;

                    case "optimizer_set_threads":
                        response.Result = SetThreads(request.Params);
                        break;

                    case "optimizer_start":
                        response.Result = Start();
                        break;

                    case "optimizer_stop":
                        response.Result = Stop();
                        break;

                    case "optimizer_get_status":
                        response.Result = GetStatus();
                        break;

                    case "optimizer_get_report":
                        response.Result = GetReport(request.Params);
                        break;

                    case "optimizer_save_report":
                        response.Result = SaveReport(request.Params);
                        break;

                    case "optimizer_load_report":
                        response.Result = LoadReport(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in optimizer API"
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
                    Name = "optimizer_data_get_config",
                    Description = "Get optimizer data source configuration",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_data_set_config",
                    Description = "Set optimizer data source configuration. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            source_type = new { type = "string", description = "Set, Folder" },
                            set_name = new { type = "string", description = "OsData set name (for Set)" },
                            folder_path = new { type = "string", description = "Data folder path (for Folder)" },
                            type_tester_data = new { type = "string", description = "Candle, TickAllCandleState, TickOnlyReadyCandle, MarketDepthAllCandleState, MarketDepthOnlyReadyCandle" },
                            date_from = new { type = "string", description = "Optimization range start (ISO)" },
                            date_to = new { type = "string", description = "Optimization range end (ISO)" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_data_get_status",
                    Description = "Get optimizer data storage status: is data loaded, securities count, actual time range, available sets",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_dividends_get_config",
                    Description = "Get dividends, margin and taxes configuration of the optimizer",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_dividends_set_config",
                    Description = "Set dividends, margin and taxes configuration. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            dividends_is_on = new { type = "boolean" },
                            margin_regime = new { type = "string", description = "Off, Summ, Percent" },
                            taxes_is_on = new { type = "boolean" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_bot_get",
                    Description = "Get the robot selected for optimization: strategy name, script flag, is it loaded",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_bot_set",
                    Description = "Select the robot for optimization. The robot is created immediately; check readiness via optimizer_bot_get (is_loaded)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            strategy_name = new { type = "string", description = "Robot class name from wiki_robots_list" },
                            is_script = new { type = "boolean", description = "Script robot (auto-detected if omitted)" }
                        },
                        required = new[] { "strategy_name" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_bot_tab_get_config",
                    Description = "Get tabs of the optimization robot (BotToTest). Simple tabs: security and timeframe. Screener tabs: securities list, timeframe, internal tabs count. Without tab_name returns all tabs",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            tab_name = new { type = "string" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_bot_tab_set_config",
                    Description = "Configure a tab of the optimization robot. Simple tab: security_name + time_frame. Screener tab: securities array + time_frame (all internal tabs are rebuilt at once). Required to pass the readiness checks before optimizer_start. IMPORTANT: security names in tester/optimizer storage are data file names WITH extension — use 'SBER.txt', not 'SBER'",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            tab_name = new { type = "string" },
                            security_name = new { type = "string", description = "Simple tab: security name as in the storage: file name WITH extension, e.g. 'SBER.txt'" },
                            security_class = new { type = "string" },
                            time_frame = new { type = "string", description = "TimeFrame enum value (Min30, Hour1 etc.)" },
                            securities = new { type = "array", description = "Screener tab: securities to trade, e.g. [{\"name\":\"SBER.txt\"},{\"name\":\"GAZP.txt\"}]. Replaces the whole list" }
                        },
                        required = new[] { "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_trade_settings_get",
                    Description = "Get optimizer trade settings: commission, execution type, slippage, start deposit",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_trade_settings_set",
                    Description = "Set optimizer trade settings. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            commission_type = new { type = "string", description = "None, OneLotFix, Percent" },
                            commission_value = new { type = "number" },
                            order_execution_type = new { type = "string", description = "Touch, Intersection, FiftyFifty" },
                            slippage_to_simple_order = new { type = "integer" },
                            slippage_to_stop_order = new { type = "integer" },
                            start_deposit = new { type = "number" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_params_get",
                    Description = "Get optimization parameter space of the selected robot: values, ranges, step, on/off for optimization",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_params_set",
                    Description = "Change optimization parameters by name: value, start/stop/step, on/off. Strict: any error rejects the whole request",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            parameters = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        value = new { type = "number" },
                                        start = new { type = "number" },
                                        stop = new { type = "number" },
                                        step = new { type = "number" },
                                        on = new { type = "boolean" }
                                    }
                                }
                            }
                        },
                        required = new[] { "parameters" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_params_reset",
                    Description = "Reset optimization parameters to the robot standard values",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_get_pass_count",
                    Description = "Get the estimated number of optimization passes with the current settings. Must not be called while the optimization is running",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_get_threads",
                    Description = "Get the number of optimization threads",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_set_threads",
                    Description = "Set the number of optimization threads (1..50)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            threads_count = new { type = "integer", description = "1..50" }
                        },
                        required = new[] { "threads_count" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_start",
                    Description = "Start the optimization. Returns started=false with the list of errors if the readiness checks fail",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_stop",
                    Description = "Stop the running optimization. Partial results are kept (is_partial flag in report and finished event)",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_get_status",
                    Description = "Get optimization status: is_running, prime and per-thread progress, estimated time to end",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_get_report",
                    Description = "Get optimization results by phases: bot params and metrics per run. Parameters are full and can be applied to a real robot via bot_set_params",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sort_type = new { type = "string", description = "SortBotsType enum value (TotalProfit, ProfitFactor, SharpRatio etc.)" },
                            limit = new { type = "integer", description = "Max reports per phase (0 = all)" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_save_report",
                    Description = "Save optimization results to a file",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string" }
                        },
                        required = new[] { "path" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_load_report",
                    Description = "Load optimization results from a file (replaces current results)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string" }
                        },
                        required = new[] { "path" }
                    }
                },
                new McpTool
                {
                    Name = "optimizer_phases_get",
                    Description = "Get optimization phases (walk-forward) settings and the current phase list",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_phases_set",
                    Description = "Set optimization phases settings and rebuild the phase list. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            time_start = new { type = "string", description = "Total range start (ISO)" },
                            time_end = new { type = "string", description = "Total range end (ISO)" },
                            iteration_count = new { type = "integer", description = "Number of InSample+OutOfSample pairs" },
                            percent_on_filtration = new { type = "number", description = "OutOfSample length in % of InSample" },
                            last_in_sample = new { type = "boolean", description = "Last phase is InSample only" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_filters_get",
                    Description = "Get walk-forward filtration settings (filter between InSample and OutOfSample)",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_filters_set",
                    Description = "Set walk-forward filtration settings. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter_profit_value = new { type = "number" },
                            filter_profit_is_on = new { type = "boolean" },
                            filter_max_draw_down_value = new { type = "number" },
                            filter_max_draw_down_is_on = new { type = "boolean" },
                            filter_middle_profit_value = new { type = "number" },
                            filter_middle_profit_is_on = new { type = "boolean" },
                            filter_profit_factor_value = new { type = "number" },
                            filter_profit_factor_is_on = new { type = "boolean" },
                            filter_deals_count_value = new { type = "integer" },
                            filter_deals_count_is_on = new { type = "boolean" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "optimizer_position_support_get",
                    Description = "Get position support settings (BotManualControl) applied to all optimization runs. second_to_open/second_to_close are in seconds",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "optimizer_position_support_set",
                    Description = "Set position support settings for optimization runs. All fields are optional. second_to_open/second_to_close are in seconds",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            stop_is_on = new { type = "boolean" },
                            stop_distance = new { type = "number" },
                            stop_slippage = new { type = "number" },
                            profit_is_on = new { type = "boolean" },
                            profit_distance = new { type = "number" },
                            profit_slippage = new { type = "number" },
                            second_to_open_is_on = new { type = "boolean" },
                            second_to_open = new { type = "number", description = "Seconds" },
                            second_to_close_is_on = new { type = "boolean" },
                            second_to_close = new { type = "number", description = "Seconds" },
                            setback_to_open_is_on = new { type = "boolean" },
                            setback_to_open_position = new { type = "number" },
                            setback_to_close_is_on = new { type = "boolean" },
                            setback_to_close_position = new { type = "number" },
                            double_exit_is_on = new { type = "boolean" },
                            type_double_exit_order = new { type = "string", description = "Limit, Market, Iceberg" },
                            double_exit_slippage = new { type = "number" },
                            values_type = new { type = "string", description = "MinPriceStep, Absolute, Percent" },
                            order_type_time = new { type = "string", description = "Specified, GTC, Day" },
                            limits_maker_only = new { type = "boolean" }
                        },
                        required = new string[0]
                    }
                }
            };
        }

        #endregion

        #region Data configuration

        private object GetDataConfig()
        {
            OptimizerMaster master = GetMasterRequired();
            OptimizerDataStorage storage = master.Storage;

            string setName = null;

            if (storage.ActiveSet != null && storage.ActiveSet.StartsWith(@"Data\Set_"))
            {
                setName = storage.ActiveSet.Substring(@"Data\Set_".Length);
            }

            return new
            {
                source_type = storage.SourceDataType.ToString(),
                set_name = setName,
                folder_path = storage.PathToFolder,
                type_tester_data = storage.TypeTesterData.ToString(),
                date_from = master.TimeStart,
                date_to = master.TimeEnd
            };
        }

        private object SetDataConfig(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();
            OptimizerDataStorage storage = master.Storage;

            if (parameters.TryGetProperty("source_type", out JsonElement sourceTypeElement)
                && sourceTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TesterSourceDataType>(sourceTypeElement.GetString(), true, out TesterSourceDataType sourceType))
            {
                storage.SourceDataType = sourceType;
            }

            if (parameters.TryGetProperty("set_name", out JsonElement setNameElement)
                && setNameElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(setNameElement.GetString()))
            {
                storage.SetNewSet(setNameElement.GetString());
            }

            if (parameters.TryGetProperty("folder_path", out JsonElement folderElement)
                && folderElement.ValueKind == JsonValueKind.String)
            {
                storage.PathToFolder = folderElement.GetString();
            }

            if (parameters.TryGetProperty("type_tester_data", out JsonElement typeElement)
                && typeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TesterDataType>(typeElement.GetString(), true, out TesterDataType typeTesterData))
            {
                storage.TypeTesterData = typeTesterData;
            }

            if (parameters.TryGetProperty("date_from", out JsonElement dateFromElement)
                && dateFromElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateFromElement.GetString(), out DateTime dateFrom))
            {
                master.TimeStart = dateFrom;
            }

            if (parameters.TryGetProperty("date_to", out JsonElement dateToElement)
                && dateToElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateToElement.GetString(), out DateTime dateTo))
            {
                master.TimeEnd = dateTo;
            }

            return GetDataConfig();
        }

        private object GetDataStatus()
        {
            OptimizerMaster master = GetMasterRequired();
            OptimizerDataStorage storage = master.Storage;

            int securitiesCount = 0;
            bool isLoaded = false;

            if (storage.SecuritiesTester != null)
            {
                securitiesCount = storage.SecuritiesTester.Count;
                isLoaded = securitiesCount > 0;
            }

            return new
            {
                is_loaded = isLoaded,
                securities_count = securitiesCount,
                time_min = storage.TimeMin,
                time_max = storage.TimeMax,
                time_start = storage.TimeStart,
                time_end = storage.TimeEnd,
                available_sets = storage.Sets
            };
        }

        #endregion

        #region Dividends, margin and taxes

        private object GetDividendsConfig()
        {
            OptimizerMaster master = GetMasterRequired();
            OptimizerDataStorage storage = master.Storage;

            return new
            {
                dividends_is_on = storage.DividendsIsOn,
                margin_regime = storage.MarginRegime,
                taxes_is_on = storage.TaxesIsOn
            };
        }

        private object SetDividendsConfig(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();
            OptimizerDataStorage storage = master.Storage;

            if (parameters.TryGetProperty("dividends_is_on", out JsonElement dividendsElement)
                && (dividendsElement.ValueKind == JsonValueKind.True || dividendsElement.ValueKind == JsonValueKind.False))
            {
                storage.DividendsIsOn = dividendsElement.GetBoolean();
            }

            if (parameters.TryGetProperty("margin_regime", out JsonElement marginElement)
                && marginElement.ValueKind == JsonValueKind.String)
            {
                string marginRegime = marginElement.GetString();

                if (marginRegime == "Off" || marginRegime == "Summ" || marginRegime == "Percent")
                {
                    storage.MarginRegime = marginRegime;
                }
                else
                {
                    throw new ArgumentException($"Unknown margin_regime '{marginRegime}'. Expected: Off, Summ, Percent");
                }
            }

            if (parameters.TryGetProperty("taxes_is_on", out JsonElement taxesElement)
                && (taxesElement.ValueKind == JsonValueKind.True || taxesElement.ValueKind == JsonValueKind.False))
            {
                storage.TaxesIsOn = taxesElement.GetBoolean();
            }

            return GetDividendsConfig();
        }

        #endregion

        #region Robot selection

        private object GetBot()
        {
            OptimizerMaster master = GetMasterRequired();

            return new
            {
                strategy_name = master.StrategyName,
                is_script = master.IsScript,
                is_loaded = master.BotToTest != null
            };
        }

        private object SetBot(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();

            if (!parameters.TryGetProperty("strategy_name", out JsonElement strategyElement)
                || strategyElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(strategyElement.GetString()))
            {
                throw new ArgumentException("strategy_name is required and must be a non-empty string");
            }

            string strategyName = strategyElement.GetString();

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();
            List<string> scriptNames = BotFactory.GetScriptsNamesStrategy();

            bool isScript;

            if (parameters.TryGetProperty("is_script", out JsonElement isScriptElement)
                && (isScriptElement.ValueKind == JsonValueKind.True || isScriptElement.ValueKind == JsonValueKind.False))
            {
                isScript = isScriptElement.GetBoolean();
            }
            else if (includeNames.Contains(strategyName))
            {
                isScript = false;
            }
            else if (scriptNames.Contains(strategyName))
            {
                isScript = true;
            }
            else
            {
                throw new ArgumentException($"Unknown strategy: {strategyName}");
            }

            master.StrategyName = strategyName;
            master.IsScript = isScript;

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                master.CreateBot();
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(new Action(master.CreateBot));
            }

            return GetBot();
        }

        #endregion

        #region Bot tabs configuration

        private object GetBotTabConfig(JsonElement parameters)
        {
            OptimizerMaster master = GetMasterRequired();

            if (master.BotToTest == null)
            {
                throw new InvalidOperationException("No optimization robot selected (optimizer_bot_set)");
            }

            string tabName = null;

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                && tabNameElement.ValueKind == JsonValueKind.String)
            {
                tabName = tabNameElement.GetString();
            }

            List<object> tabs = new List<object>();
            List<IIBotTab> sources = master.BotToTest.GetTabs();

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].TabType == BotTabType.Simple)
                {
                    BotTabSimple simple = (BotTabSimple)sources[i];

                    if (tabName != null && simple.TabName != tabName)
                    {
                        continue;
                    }

                    tabs.Add(BuildBotTabResponse(simple));
                }
                else if (sources[i].TabType == BotTabType.Screener)
                {
                    BotTabScreener screener = (BotTabScreener)sources[i];

                    if (tabName != null && screener.TabName != tabName)
                    {
                        continue;
                    }

                    tabs.Add(BuildScreenerTabResponse(screener));
                }
            }

            if (tabName != null && tabs.Count == 0)
            {
                throw new ArgumentException($"Tab '{tabName}' not found in robot '{master.StrategyName}'");
            }

            return new { tabs = tabs, count = tabs.Count };
        }

        private object SetBotTabConfig(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(tabNameElement.GetString()))
            {
                throw new ArgumentException("tab_name is required and must be a non-empty string");
            }

            OptimizerMaster master = GetMasterRequired();

            if (master.BotToTest == null)
            {
                throw new InvalidOperationException("No optimization robot selected (optimizer_bot_set)");
            }

            string tabName = tabNameElement.GetString();

            ValidateBotTabSecurities(master, parameters);

            IIBotTab tab = FindAnyBotTab(master, tabName);

            if (tab.TabType == BotTabType.Simple)
            {
                if (MainWindow.GetDispatcher.CheckAccess())
                {
                    ApplyBotTabConfig((BotTabSimple)tab, parameters);
                }
                else
                {
                    MainWindow.GetDispatcher.Invoke(() => ApplyBotTabConfig((BotTabSimple)tab, parameters));
                }

                return BuildBotTabResponse((BotTabSimple)tab);
            }

            if (tab.TabType == BotTabType.Screener)
            {
                if (MainWindow.GetDispatcher.CheckAccess())
                {
                    ApplyScreenerTabConfig((BotTabScreener)tab, parameters);
                }
                else
                {
                    MainWindow.GetDispatcher.Invoke(() => ApplyScreenerTabConfig((BotTabScreener)tab, parameters));
                }

                return BuildScreenerTabResponse((BotTabScreener)tab);
            }

            throw new ArgumentException($"Tab '{tabName}' has unsupported type '{tab.TabType}'. Only Simple and Screener tabs are configurable");
        }

        private void ValidateBotTabSecurities(OptimizerMaster master, JsonElement parameters)
        {
            List<string> namesToCheck = new List<string>();

            if (parameters.TryGetProperty("security_name", out JsonElement securityNameElement)
                && securityNameElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(securityNameElement.GetString()))
            {
                namesToCheck.Add(securityNameElement.GetString());
            }

            if (parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                && securitiesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement secElement in securitiesElement.EnumerateArray())
                {
                    if (secElement.ValueKind == JsonValueKind.Object
                        && secElement.TryGetProperty("name", out JsonElement nameElement)
                        && nameElement.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(nameElement.GetString()))
                    {
                        namesToCheck.Add(nameElement.GetString());
                    }
                }
            }

            if (namesToCheck.Count == 0)
            {
                return;
            }

            List<SecurityTester> securities = master.Storage.SecuritiesTester;

            if (securities == null || securities.Count == 0)
            {
                // хранилище ещё не загружено — проверять нечего
                return;
            }

            List<string> availableNames = new List<string>();

            for (int i = 0; i < securities.Count; i++)
            {
                string name = securities[i].Security.Name;

                if (availableNames.Contains(name) == false)
                {
                    availableNames.Add(name);
                }
            }

            for (int i = 0; i < namesToCheck.Count; i++)
            {
                if (availableNames.Contains(namesToCheck[i]))
                {
                    continue;
                }

                string namesText;

                if (availableNames.Count <= 30)
                {
                    namesText = string.Join(", ", availableNames);
                }
                else
                {
                    namesText = string.Join(", ", availableNames.GetRange(0, 30))
                        + $" ... and {availableNames.Count - 30} more";
                }

                throw new ArgumentException(
                    $"Security '{namesToCheck[i]}' not found in optimizer storage. "
                    + "Storage keeps data file names with extension (use 'SBER.txt', not 'SBER'). "
                    + $"Available: {namesText}");
            }
        }

        private void ApplyBotTabConfig(BotTabSimple tab, JsonElement parameters)
        {
            bool changed = false;

            if (parameters.TryGetProperty("security_name", out JsonElement securityNameElement)
                && securityNameElement.ValueKind == JsonValueKind.String)
            {
                tab.Connector.SecurityName = securityNameElement.GetString();
                changed = true;
            }

            if (parameters.TryGetProperty("security_class", out JsonElement securityClassElement)
                && securityClassElement.ValueKind == JsonValueKind.String)
            {
                tab.Connector.SecurityClass = securityClassElement.GetString();
                changed = true;
            }

            if (parameters.TryGetProperty("time_frame", out JsonElement timeFrameElement)
                && timeFrameElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TimeFrame>(timeFrameElement.GetString(), true, out TimeFrame timeFrame))
            {
                tab.Connector.TimeFrame = timeFrame;
                changed = true;
            }

            if (changed)
            {
                tab.Connector.TimeFrameBuilder.Save();
                tab.Connector.Save();
            }
        }

        private IIBotTab FindAnyBotTab(OptimizerMaster master, string tabName)
        {
            List<IIBotTab> sources = master.BotToTest.GetTabs();

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].TabType == BotTabType.Simple
                    && ((BotTabSimple)sources[i]).TabName == tabName)
                {
                    return sources[i];
                }

                if (sources[i].TabType == BotTabType.Screener
                    && ((BotTabScreener)sources[i]).TabName == tabName)
                {
                    return sources[i];
                }
            }

            throw new ArgumentException($"Tab '{tabName}' not found in robot '{master.StrategyName}'");
        }

        private object BuildBotTabResponse(BotTabSimple tab)
        {
            return new
            {
                tab_name = tab.TabName,
                tab_type = tab.TabType.ToString(),
                security_name = tab.Connector.SecurityName,
                security_class = tab.Connector.SecurityClass,
                time_frame = tab.Connector.TimeFrame.ToString()
            };
        }

        private void ApplyScreenerTabConfig(BotTabScreener screener, JsonElement parameters)
        {
            bool needReload = false;

            if (parameters.TryGetProperty("portfolio_name", out JsonElement portfolioElement)
                && portfolioElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(portfolioElement.GetString()))
            {
                screener.PortfolioName = portfolioElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("time_frame", out JsonElement timeFrameElement)
                && timeFrameElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TimeFrame>(timeFrameElement.GetString(), true, out TimeFrame timeFrame))
            {
                screener.TimeFrame = timeFrame;
                needReload = true;
            }

            if (parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                && securitiesElement.ValueKind == JsonValueKind.Array)
            {
                List<ActivatedSecurity> newSecurities = new List<ActivatedSecurity>();

                foreach (JsonElement secElement in securitiesElement.EnumerateArray())
                {
                    if (secElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    ActivatedSecurity sec = new ActivatedSecurity();

                    if (secElement.TryGetProperty("name", out JsonElement nameElement)
                        && nameElement.ValueKind == JsonValueKind.String)
                    {
                        sec.SecurityName = nameElement.GetString();
                    }

                    if (secElement.TryGetProperty("class_name", out JsonElement classElement)
                        && classElement.ValueKind == JsonValueKind.String)
                    {
                        sec.SecurityClass = classElement.GetString();
                    }
                    else if (secElement.TryGetProperty("class", out JsonElement classAliasElement)
                        && classAliasElement.ValueKind == JsonValueKind.String)
                    {
                        sec.SecurityClass = classAliasElement.GetString();
                    }

                    if (secElement.TryGetProperty("is_on", out JsonElement isOnElement)
                        && (isOnElement.ValueKind == JsonValueKind.True || isOnElement.ValueKind == JsonValueKind.False))
                    {
                        sec.IsOn = isOnElement.GetBoolean();
                    }
                    else
                    {
                        sec.IsOn = true;
                    }

                    newSecurities.Add(sec);
                }

                screener.SecuritiesNames = newSecurities;
                needReload = true;
            }

            // без портфеля скринер не создаёт внутренние вкладки (TabsReadyToLoad).
            // в тестере/оптимизаторе портфель эмулируемый — GodMode
            if (string.IsNullOrEmpty(screener.PortfolioName))
            {
                screener.PortfolioName = "GodMode";
                needReload = true;
            }

            screener.SaveSettings();

            if (needReload)
            {
                // внутренние вкладки пересоздаём синхронно:
                // проверка готовности при старте читает их сразу после ответа API
                screener.NeedToReloadTabs = true;
                screener.TryReLoadTabs();
            }
        }

        private object BuildScreenerTabResponse(BotTabScreener screener)
        {
            List<object> securities = new List<object>();

            if (screener.SecuritiesNames != null)
            {
                for (int i = 0; i < screener.SecuritiesNames.Count; i++)
                {
                    ActivatedSecurity sec = screener.SecuritiesNames[i];

                    securities.Add(new
                    {
                        name = sec.SecurityName,
                        class_name = sec.SecurityClass,
                        is_on = sec.IsOn
                    });
                }
            }

            return new
            {
                tab_name = screener.TabName,
                tab_type = screener.TabType.ToString(),
                time_frame = screener.TimeFrame.ToString(),
                securities = securities,
                securities_count = securities.Count,
                tabs_count = screener.Tabs?.Count ?? 0
            };
        }

        #endregion

        #region Trade settings and position support

        private object GetTradeSettings()
        {
            OptimizerMaster master = GetMasterRequired();

            return new
            {
                commission_type = master.CommissionType.ToString(),
                commission_value = master.CommissionValue,
                order_execution_type = master.OrderExecutionType.ToString(),
                slippage_to_simple_order = master.SlippageToSimpleOrder,
                slippage_to_stop_order = master.SlippageToStopOrder,
                start_deposit = master.StartDeposit
            };
        }

        private object SetTradeSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();

            if (parameters.TryGetProperty("commission_type", out JsonElement commissionTypeElement)
                && commissionTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CommissionType>(commissionTypeElement.GetString(), true, out CommissionType commissionType))
            {
                master.CommissionType = commissionType;
            }

            if (parameters.TryGetProperty("commission_value", out JsonElement commissionValueElement)
                && commissionValueElement.ValueKind == JsonValueKind.Number
                && commissionValueElement.TryGetDecimal(out decimal commissionValue))
            {
                master.CommissionValue = commissionValue;
            }

            if (parameters.TryGetProperty("order_execution_type", out JsonElement executionElement)
                && executionElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<OrderExecutionType>(executionElement.GetString(), true, out OrderExecutionType executionType))
            {
                master.OrderExecutionType = executionType;
            }

            if (parameters.TryGetProperty("slippage_to_simple_order", out JsonElement slippageSimpleElement)
                && slippageSimpleElement.ValueKind == JsonValueKind.Number
                && slippageSimpleElement.TryGetInt32(out int slippageSimple))
            {
                master.SlippageToSimpleOrder = slippageSimple;
            }

            if (parameters.TryGetProperty("slippage_to_stop_order", out JsonElement slippageStopElement)
                && slippageStopElement.ValueKind == JsonValueKind.Number
                && slippageStopElement.TryGetInt32(out int slippageStop))
            {
                master.SlippageToStopOrder = slippageStop;
            }

            if (parameters.TryGetProperty("start_deposit", out JsonElement depositElement)
                && depositElement.ValueKind == JsonValueKind.Number
                && depositElement.TryGetDecimal(out decimal startDeposit))
            {
                master.StartDeposit = startDeposit;
            }

            return GetTradeSettings();
        }

        private object GetPositionSupport()
        {
            OptimizerMaster master = GetMasterRequired();
            BotManualControl support = master.ManualControl;

            return new
            {
                stop_is_on = support.StopIsOn,
                stop_distance = support.StopDistance,
                stop_slippage = support.StopSlippage,
                profit_is_on = support.ProfitIsOn,
                profit_distance = support.ProfitDistance,
                profit_slippage = support.ProfitSlippage,
                second_to_open_is_on = support.SecondToOpenIsOn,
                second_to_open = support.SecondToOpen.TotalSeconds,
                second_to_close_is_on = support.SecondToCloseIsOn,
                second_to_close = support.SecondToClose.TotalSeconds,
                setback_to_open_is_on = support.SetbackToOpenIsOn,
                setback_to_open_position = support.SetbackToOpenPosition,
                setback_to_close_is_on = support.SetbackToCloseIsOn,
                setback_to_close_position = support.SetbackToClosePosition,
                double_exit_is_on = support.DoubleExitIsOn,
                type_double_exit_order = support.TypeDoubleExitOrder.ToString(),
                double_exit_slippage = support.DoubleExitSlippage,
                values_type = support.ValuesType.ToString(),
                order_type_time = support.OrderTypeTime.ToString(),
                limits_maker_only = support.LimitsMakerOnly
            };
        }

        private object SetPositionSupport(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();
            BotManualControl support = master.ManualControl;

            ApplyBoolField(parameters, "stop_is_on", value => support.StopIsOn = value);
            ApplyDecimalField(parameters, "stop_distance", value => support.StopDistance = value);
            ApplyDecimalField(parameters, "stop_slippage", value => support.StopSlippage = value);
            ApplyBoolField(parameters, "profit_is_on", value => support.ProfitIsOn = value);
            ApplyDecimalField(parameters, "profit_distance", value => support.ProfitDistance = value);
            ApplyDecimalField(parameters, "profit_slippage", value => support.ProfitSlippage = value);
            ApplyBoolField(parameters, "second_to_open_is_on", value => support.SecondToOpenIsOn = value);
            ApplyBoolField(parameters, "second_to_close_is_on", value => support.SecondToCloseIsOn = value);
            ApplyBoolField(parameters, "setback_to_open_is_on", value => support.SetbackToOpenIsOn = value);
            ApplyDecimalField(parameters, "setback_to_open_position", value => support.SetbackToOpenPosition = value);
            ApplyBoolField(parameters, "setback_to_close_is_on", value => support.SetbackToCloseIsOn = value);
            ApplyDecimalField(parameters, "setback_to_close_position", value => support.SetbackToClosePosition = value);
            ApplyBoolField(parameters, "double_exit_is_on", value => support.DoubleExitIsOn = value);
            ApplyEnumField<OrderPriceType>(parameters, "type_double_exit_order", value => support.TypeDoubleExitOrder = value);
            ApplyDecimalField(parameters, "double_exit_slippage", value => support.DoubleExitSlippage = value);
            ApplyEnumField<ManualControlValuesType>(parameters, "values_type", value => support.ValuesType = value);
            ApplyEnumField<OrderTypeTime>(parameters, "order_type_time", value => support.OrderTypeTime = value);
            ApplyBoolField(parameters, "limits_maker_only", value => support.LimitsMakerOnly = value);

            if (parameters.TryGetProperty("second_to_open", out JsonElement secondToOpenElement)
                && secondToOpenElement.ValueKind == JsonValueKind.Number
                && secondToOpenElement.TryGetDouble(out double secondToOpen))
            {
                support.SecondToOpen = TimeSpan.FromSeconds(secondToOpen);
            }

            if (parameters.TryGetProperty("second_to_close", out JsonElement secondToCloseElement)
                && secondToCloseElement.ValueKind == JsonValueKind.Number
                && secondToCloseElement.TryGetDouble(out double secondToClose))
            {
                support.SecondToClose = TimeSpan.FromSeconds(secondToClose);
            }

            support.Save();

            return GetPositionSupport();
        }

        private void ApplyBoolField(JsonElement parameters, string name, Action<bool> apply)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            {
                apply(element.GetBoolean());
            }
        }

        private void ApplyDecimalField(JsonElement parameters, string name, Action<decimal> apply)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetDecimal(out decimal value))
            {
                apply(value);
            }
        }

        private void ApplyEnumField<TEnum>(JsonElement parameters, string name, Action<TEnum> apply) where TEnum : struct
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.String
                && Enum.TryParse<TEnum>(element.GetString(), true, out TEnum value))
            {
                apply(value);
            }
        }

        #endregion

        #region Phases and filters

        private object GetPhases()
        {
            OptimizerMaster master = GetMasterRequired();
            List<object> fazes = new List<object>();

            if (master.Fazes != null)
            {
                for (int i = 0; i < master.Fazes.Count; i++)
                {
                    fazes.Add(new
                    {
                        type = master.Fazes[i].TypeFaze.ToString(),
                        time_start = master.Fazes[i].TimeStart,
                        time_end = master.Fazes[i].TimeEnd,
                        days = master.Fazes[i].Days
                    });
                }
            }

            return new
            {
                time_start = master.TimeStart,
                time_end = master.TimeEnd,
                iteration_count = master.IterationCount,
                percent_on_filtration = master.PercentOnFiltration,
                last_in_sample = master.LastInSample,
                fazes = fazes,
                fazes_count = fazes.Count
            };
        }

        private object SetPhases(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();

            if (parameters.TryGetProperty("time_start", out JsonElement timeStartElement)
                && timeStartElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(timeStartElement.GetString(), out DateTime timeStart))
            {
                master.TimeStart = timeStart;
            }

            if (parameters.TryGetProperty("time_end", out JsonElement timeEndElement)
                && timeEndElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(timeEndElement.GetString(), out DateTime timeEnd))
            {
                master.TimeEnd = timeEnd;
            }

            if (parameters.TryGetProperty("iteration_count", out JsonElement iterationElement)
                && iterationElement.ValueKind == JsonValueKind.Number
                && iterationElement.TryGetInt32(out int iterationCount)
                && iterationCount > 0)
            {
                master.IterationCount = iterationCount;
            }

            if (parameters.TryGetProperty("percent_on_filtration", out JsonElement percentElement)
                && percentElement.ValueKind == JsonValueKind.Number
                && percentElement.TryGetDecimal(out decimal percentOnFiltration))
            {
                master.PercentOnFiltration = percentOnFiltration;
            }

            if (parameters.TryGetProperty("last_in_sample", out JsonElement lastElement)
                && (lastElement.ValueKind == JsonValueKind.True || lastElement.ValueKind == JsonValueKind.False))
            {
                master.LastInSample = lastElement.GetBoolean();
            }

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                master.ReloadFazes();
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(new Action(master.ReloadFazes));
            }

            return GetPhases();
        }

        private object GetFilters()
        {
            OptimizerMaster master = GetMasterRequired();

            return new
            {
                filter_profit_value = master.FilterProfitValue,
                filter_profit_is_on = master.FilterProfitIsOn,
                filter_max_draw_down_value = master.FilterMaxDrawDownValue,
                filter_max_draw_down_is_on = master.FilterMaxDrawDownIsOn,
                filter_middle_profit_value = master.FilterMiddleProfitValue,
                filter_middle_profit_is_on = master.FilterMiddleProfitIsOn,
                filter_profit_factor_value = master.FilterProfitFactorValue,
                filter_profit_factor_is_on = master.FilterProfitFactorIsOn,
                filter_deals_count_value = master.FilterDealsCountValue,
                filter_deals_count_is_on = master.FilterDealsCountIsOn
            };
        }

        private object SetFilters(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            OptimizerMaster master = GetMasterRequired();

            ApplyDecimalField(parameters, "filter_profit_value", value => master.FilterProfitValue = value);
            ApplyBoolField(parameters, "filter_profit_is_on", value => master.FilterProfitIsOn = value);
            ApplyDecimalField(parameters, "filter_max_draw_down_value", value => master.FilterMaxDrawDownValue = value);
            ApplyBoolField(parameters, "filter_max_draw_down_is_on", value => master.FilterMaxDrawDownIsOn = value);
            ApplyDecimalField(parameters, "filter_middle_profit_value", value => master.FilterMiddleProfitValue = value);
            ApplyBoolField(parameters, "filter_middle_profit_is_on", value => master.FilterMiddleProfitIsOn = value);
            ApplyDecimalField(parameters, "filter_profit_factor_value", value => master.FilterProfitFactorValue = value);
            ApplyBoolField(parameters, "filter_profit_factor_is_on", value => master.FilterProfitFactorIsOn = value);

            if (parameters.TryGetProperty("filter_deals_count_value", out JsonElement dealsElement)
                && dealsElement.ValueKind == JsonValueKind.Number
                && dealsElement.TryGetInt32(out int dealsCount))
            {
                master.FilterDealsCountValue = dealsCount;
            }

            ApplyBoolField(parameters, "filter_deals_count_is_on", value => master.FilterDealsCountIsOn = value);

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                master.ReloadFazes();
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(new Action(master.ReloadFazes));
            }

            return GetFilters();
        }

        #endregion

        #region Optimization parameters

        private object GetParams()
        {
            OptimizerMaster master = GetMasterRequired();

            List<IIStrategyParameter> parameters = master.Parameters;
            List<object> result = new List<object>();

            if (parameters == null)
            {
                return new { strategy_name = master.StrategyName, parameters = result, count = 0 };
            }

            List<bool> parametersOn = master.ParametersOn;

            for (int i = 0; i < parameters.Count; i++)
            {
                bool isOn = parametersOn != null && i < parametersOn.Count && parametersOn[i];
                result.Add(SerializeOptimizerParam(parameters[i], isOn));
            }

            return new { strategy_name = master.StrategyName, parameters = result, count = result.Count };
        }

        private object SerializeOptimizerParam(IIStrategyParameter parameter, bool isOn)
        {
            string name = parameter.Name;
            string type = parameter.Type.ToString();

            switch (parameter)
            {
                case StrategyParameterInt p:
                    return new
                    {
                        name,
                        type,
                        on = isOn,
                        value = p.ValueInt,
                        default_value = p.ValueIntDefolt,
                        start = p.ValueIntStart,
                        stop = p.ValueIntStop,
                        step = p.ValueIntStep,
                        step_type = p.StepType.ToString()
                    };

                case StrategyParameterDecimal p:
                    return new
                    {
                        name,
                        type,
                        on = isOn,
                        value = p.ValueDecimal,
                        default_value = p.ValueDecimalDefolt,
                        start = p.ValueDecimalStart,
                        stop = p.ValueDecimalStop,
                        step = p.ValueDecimalStep,
                        step_type = p.StepType.ToString()
                    };

                case StrategyParameterDecimalCheckBox p:
                    return new
                    {
                        name,
                        type,
                        on = isOn,
                        value = p.ValueDecimal,
                        default_value = p.ValueDecimalDefolt,
                        start = p.ValueDecimalStart,
                        stop = p.ValueDecimalStop,
                        step = p.ValueDecimalStep,
                        step_type = p.StepType.ToString(),
                        is_checked = p.CheckState == CheckState.Checked
                    };

                case StrategyParameterString p:
                    return new { name, type, on = isOn, value = p.ValueString, values = p.ValuesString };

                case StrategyParameterBool p:
                    return new { name, type, on = isOn, value = p.ValueBool, default_value = p.ValueBoolDefolt };

                case StrategyParameterTimeOfDay p:
                    return new
                    {
                        name,
                        type,
                        on = isOn,
                        hour = p.Value.Hour,
                        minute = p.Value.Minute,
                        second = p.Value.Second,
                        millisecond = p.Value.Millisecond
                    };

                case StrategyParameterCheckBox p:
                    return new { name, type, on = isOn, is_checked = p.CheckState == CheckState.Checked };

                default:
                    return new { name, type, on = isOn };
            }
        }

        private object SetParams(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("parameters", out JsonElement itemsElement)
                || itemsElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("parameters is required and must be an array");
            }

            OptimizerMaster master = GetMasterRequired();
            List<IIStrategyParameter> masterParams = master.Parameters;

            if (masterParams == null)
            {
                throw new InvalidOperationException("No optimization robot selected or the robot has no parameters");
            }

            List<bool> parametersOn = master.ParametersOn;

            foreach (JsonElement item in itemsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out JsonElement nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Each parameter item must have a name");
                }

                string paramName = nameElement.GetString();
                int index = -1;

                for (int i = 0; i < masterParams.Count; i++)
                {
                    if (masterParams[i].Name == paramName)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    throw new ArgumentException($"Parameter '{paramName}' not found in robot '{master.StrategyName}'");
                }

                ApplyParamValue(masterParams[index], item);

                if (item.TryGetProperty("on", out JsonElement onElement)
                    && (onElement.ValueKind == JsonValueKind.True || onElement.ValueKind == JsonValueKind.False))
                {
                    parametersOn[index] = onElement.GetBoolean();
                }
            }

            master.SaveStandardParameters();
            master.NotifyParametersChanged();

            return GetParams();
        }

        private void ApplyParamValue(IIStrategyParameter parameter, JsonElement item)
        {
            if (parameter is StrategyParameterInt intParam)
            {
                if (item.TryGetProperty("value", out JsonElement valueElement)
                    && valueElement.ValueKind == JsonValueKind.Number
                    && valueElement.TryGetInt32(out int newValue))
                {
                    intParam.ValueInt = newValue;
                }

                // диапазоны перебора read-only, меняются только через строку сохранения
                string[] save = intParam.GetStringToSave().Split('#');

                if (item.TryGetProperty("start", out JsonElement startElement)
                    && startElement.ValueKind == JsonValueKind.Number
                    && startElement.TryGetInt32(out int newStart))
                {
                    save[3] = newStart.ToString();
                }

                if (item.TryGetProperty("stop", out JsonElement stopElement)
                    && stopElement.ValueKind == JsonValueKind.Number
                    && stopElement.TryGetInt32(out int newStop))
                {
                    save[4] = newStop.ToString();
                }

                if (item.TryGetProperty("step", out JsonElement stepElement)
                    && stepElement.ValueKind == JsonValueKind.Number
                    && stepElement.TryGetInt32(out int newStep))
                {
                    save[5] = newStep.ToString();
                }

                if (Convert.ToInt32(save[3]) > Convert.ToInt32(save[4]))
                {
                    throw new ArgumentException($"Parameter '{parameter.Name}': start {save[3]} is greater than stop {save[4]}");
                }

                intParam.LoadParamFromString(save);
                return;
            }

            if (parameter is StrategyParameterDecimal decimalParam)
            {
                if (item.TryGetProperty("value", out JsonElement valueElement)
                    && valueElement.ValueKind == JsonValueKind.Number
                    && valueElement.TryGetDecimal(out decimal newValue))
                {
                    decimalParam.ValueDecimal = newValue;
                }

                string[] save = decimalParam.GetStringToSave().Split('#');

                if (item.TryGetProperty("start", out JsonElement startElement)
                    && startElement.ValueKind == JsonValueKind.Number
                    && startElement.TryGetDecimal(out decimal newStart))
                {
                    save[3] = newStart.ToString();
                }

                if (item.TryGetProperty("stop", out JsonElement stopElement)
                    && stopElement.ValueKind == JsonValueKind.Number
                    && stopElement.TryGetDecimal(out decimal newStop))
                {
                    save[4] = newStop.ToString();
                }

                if (item.TryGetProperty("step", out JsonElement stepElement)
                    && stepElement.ValueKind == JsonValueKind.Number
                    && stepElement.TryGetDecimal(out decimal newStep))
                {
                    save[5] = newStep.ToString();
                }

                if (save[3].ToDecimal() > save[4].ToDecimal())
                {
                    throw new ArgumentException($"Parameter '{parameter.Name}': start {save[3]} is greater than stop {save[4]}");
                }

                decimalParam.LoadParamFromString(save);
                return;
            }

            if (parameter is StrategyParameterDecimalCheckBox decimalCheckBoxParam)
            {
                if (item.TryGetProperty("value", out JsonElement valueElement)
                    && valueElement.ValueKind == JsonValueKind.Number
                    && valueElement.TryGetDecimal(out decimal newValue))
                {
                    decimalCheckBoxParam.ValueDecimal = newValue;
                }

                return;
            }

            if (parameter is StrategyParameterBool boolParam)
            {
                if (item.TryGetProperty("value", out JsonElement valueElement)
                    && (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False))
                {
                    boolParam.ValueBool = valueElement.GetBoolean();
                }

                return;
            }

            if (parameter is StrategyParameterString stringParam)
            {
                if (item.TryGetProperty("value", out JsonElement valueElement)
                    && valueElement.ValueKind == JsonValueKind.String)
                {
                    stringParam.ValueString = valueElement.GetString();
                }

                return;
            }

            if (item.TryGetProperty("value", out _))
            {
                throw new ArgumentException($"Parameter '{parameter.Name}' of type '{parameter.Type}' can not be changed via optimizer_params_set");
            }
        }

        private object ResetParams()
        {
            OptimizerMaster master = GetMasterRequired();

            if (string.IsNullOrEmpty(master.StrategyName))
            {
                throw new InvalidOperationException("No optimization robot selected");
            }

            List<IIStrategyParameter> standardParams = master.ParametersStandard;

            if (standardParams == null)
            {
                throw new InvalidOperationException($"Failed to load standard parameters for robot '{master.StrategyName}'");
            }

            master.SaveStandardParameters();
            master.NotifyParametersChanged();

            return GetParams();
        }

        #endregion

        #region Pass count and threads

        private object GetPassCount()
        {
            OptimizerMaster master = GetMasterRequired();

            if (master._optimizerExecutor.IsRunning)
            {
                throw new InvalidOperationException(
                    "optimizer_get_pass_count must not be called while the optimization is running");
            }

            return new { pass_count = master.GetMaxBotsCount() };
        }

        private object GetThreads()
        {
            OptimizerMaster master = GetMasterRequired();

            return new { threads_count = master.ThreadsCount };
        }

        private object SetThreads(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("threads_count", out JsonElement threadsElement)
                || threadsElement.ValueKind != JsonValueKind.Number
                || !threadsElement.TryGetInt32(out int threadsCount)
                || threadsCount < 1
                || threadsCount > 50)
            {
                throw new ArgumentException("threads_count is required and must be in range 1..50");
            }

            OptimizerMaster master = GetMasterRequired();
            master.ThreadsCount = threadsCount;

            return GetThreads();
        }

        #endregion

        #region Execution (start, stop, status) and SSE

        public void AttachToMaster(OptimizerMaster master)
        {
            DetachFromMaster();
            _master = master;

            if (_master != null)
            {
                _master._optimizerExecutor.TestingProgressChangeEvent += Executor_TestingProgressChangeEvent;
                _master._optimizerExecutor.TestReadyEvent += Executor_TestReadyEvent;
                _master.TimeToEndChangeEvent += Master_TimeToEndChangeEvent;
            }
        }

        public void DetachFromMaster()
        {
            if (_master != null)
            {
                _master._optimizerExecutor.TestingProgressChangeEvent -= Executor_TestingProgressChangeEvent;
                _master._optimizerExecutor.TestReadyEvent -= Executor_TestReadyEvent;
                _master.TimeToEndChangeEvent -= Master_TimeToEndChangeEvent;
                _master = null;
            }

            _stopRequested = false;
            _timeToEnd = null;
        }

        private object Start()
        {
            OptimizerMaster master = GetMasterRequired();

            if (master._optimizerExecutor.IsRunning)
            {
                throw new InvalidOperationException("Optimization is already running");
            }

            List<string> errors = master.CheckReadyDataHeadless();

            if (errors.Count > 0)
            {
                return new { started = false, errors = errors };
            }

            bool started;

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                started = master.StartHeadless();
            }
            else
            {
                started = (bool)MainWindow.GetDispatcher.Invoke(new Func<bool>(master.StartHeadless));
            }

            if (!started)
            {
                throw new InvalidOperationException("Optimization was not started (already running or no parameters)");
            }

            _stopRequested = false;

            return new { started = true };
        }

        private object Stop()
        {
            OptimizerMaster master = GetMasterRequired();

            if (!master._optimizerExecutor.IsRunning)
            {
                return new { stopped = false, is_running = false };
            }

            _stopRequested = true;
            master.Stop();

            return new { stopped = true };
        }

        private object GetStatus()
        {
            OptimizerMaster master = GetMasterRequired();
            List<object> threads = new List<object>();

            if (master.ProgressBarStatuses != null)
            {
                for (int i = 0; i < master.ProgressBarStatuses.Count; i++)
                {
                    threads.Add(new
                    {
                        num = master.ProgressBarStatuses[i].Num,
                        current_value = master.ProgressBarStatuses[i].CurrentValue,
                        max_value = master.ProgressBarStatuses[i].MaxValue,
                        is_finalized = master.ProgressBarStatuses[i].IsFinalized
                    });
                }
            }

            return new
            {
                is_running = master._optimizerExecutor.IsRunning,
                prime_progress = new
                {
                    current_value = master.PrimeProgressBarStatus.CurrentValue,
                    max_value = master.PrimeProgressBarStatus.MaxValue,
                    num = master.PrimeProgressBarStatus.Num,
                    is_finalized = master.PrimeProgressBarStatus.IsFinalized
                },
                threads = threads,
                time_to_end = _timeToEnd?.ToString()
            };
        }

        private void Executor_TestingProgressChangeEvent(int currentValue, int maxValue, int numServer)
        {
            try
            {
                // события идут очень часто, шлём наружу не чаще раза в секунду
                if (_lastProgressSent.AddSeconds(1) > DateTime.Now)
                {
                    return;
                }

                _lastProgressSent = DateTime.Now;

                _publishEvent("optimizer.test.progress", new
                {
                    current_value = currentValue,
                    max_value = maxValue,
                    server_num = numServer
                });
            }
            catch (Exception error)
            {
                SendLog(error.ToString(), LogMessageType.Error);
            }
        }

        private void Executor_TestReadyEvent(List<OptimizerFazeReport> reports)
        {
            try
            {
                int reportsCount = 0;

                if (reports != null)
                {
                    for (int i = 0; i < reports.Count; i++)
                    {
                        if (reports[i].Reports != null)
                        {
                            reportsCount += reports[i].Reports.Count;
                        }
                    }
                }

                _publishEvent("optimizer.test.finished", new
                {
                    is_partial = _stopRequested,
                    fazes_count = reports?.Count ?? 0,
                    reports_count = reportsCount
                });

                _lastFinishedPartial = _stopRequested;
                _stopRequested = false;
            }
            catch (Exception error)
            {
                SendLog(error.ToString(), LogMessageType.Error);
            }
        }

        private void Master_TimeToEndChangeEvent(TimeSpan timeToEnd)
        {
            _timeToEnd = timeToEnd;
        }

        #endregion

        #region Report

        private object GetReport(JsonElement parameters)
        {
            OptimizerMaster master = GetMasterRequired();
            List<OptimizerFazeReport> reports = master._optimizerExecutor.ReportsToFazes;

            SortBotsType? sortType = null;

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("sort_type", out JsonElement sortElement)
                && sortElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<SortBotsType>(sortElement.GetString(), true, out SortBotsType parsedSort))
            {
                sortType = parsedSort;
            }

            int limit = 0;

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("limit", out JsonElement limitElement)
                && limitElement.ValueKind == JsonValueKind.Number
                && limitElement.TryGetInt32(out int limitValue)
                && limitValue > 0)
            {
                limit = limitValue;
            }

            List<object> fazes = new List<object>();
            int totalReports = 0;

            if (reports != null)
            {
                for (int i = 0; i < reports.Count; i++)
                {
                    List<OptimizerReport> fazeReports = reports[i].Reports;

                    if (sortType != null)
                    {
                        fazeReports = new List<OptimizerReport>(fazeReports);
                        OptimizerFazeReport.SortResults(fazeReports, sortType.Value);
                    }

                    List<object> reportList = new List<object>();

                    for (int j = 0; j < fazeReports.Count && (limit == 0 || j < limit); j++)
                    {
                        reportList.Add(SerializeOptimizerReport(fazeReports[j]));
                    }

                    totalReports += reportList.Count;

                    fazes.Add(new
                    {
                        type = reports[i].Faze.TypeFaze.ToString(),
                        time_start = reports[i].Faze.TimeStart,
                        time_end = reports[i].Faze.TimeEnd,
                        days = reports[i].Faze.Days,
                        reports = reportList
                    });
                }
            }

            return new
            {
                is_partial = _lastFinishedPartial,
                fazes = fazes,
                fazes_count = fazes.Count,
                reports_count = totalReports
            };
        }

        private object SerializeOptimizerReport(OptimizerReport report)
        {
            List<object> parameters = new List<object>();

            List<IIStrategyParameter> reportParams = report.GetParameters();

            if (reportParams != null)
            {
                for (int i = 0; i < reportParams.Count; i++)
                {
                    parameters.Add(SerializeOptimizerParam(reportParams[i], false));
                }
            }

            return new
            {
                bot_num = report.BotNum,
                bot_name = report.BotName,
                parameters = parameters,
                positions_count = report.PositionsCount,
                total_profit = report.TotalProfit,
                total_profit_percent = report.TotalProfitPercent,
                profit_position_percent = report.ProfitPositionPercent,
                max_draw_down = report.MaxDrawDawn,
                average_profit = report.AverageProfit,
                average_profit_percent_one_contract = report.AverageProfitPercentOneContract,
                profit_factor = report.ProfitFactor,
                pay_off_ratio = report.PayOffRatio,
                recovery = report.Recovery,
                sharp_ratio = report.SharpRatio,
                average_time_in_position = report.AverageTimeInPosition
            };
        }

        private object SaveReport(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("path", out JsonElement pathElement)
                || pathElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(pathElement.GetString()))
            {
                throw new ArgumentException("path is required and must be a non-empty string");
            }

            OptimizerMaster master = GetMasterRequired();
            List<OptimizerFazeReport> reports = master._optimizerExecutor.ReportsToFazes;

            if (reports == null || reports.Count == 0)
            {
                throw new InvalidOperationException("No optimization results to save");
            }

            StringBuilder saveStr = new StringBuilder();
            int reportsCount = 0;

            for (int i = 0; i < reports.Count; i++)
            {
                saveStr.Append(reports[i].GetSaveString() + "\r\n");
                reportsCount += reports[i].Reports.Count;
            }

            string path = pathElement.GetString();

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.Write(saveStr);
            }

            return new
            {
                saved = true,
                path = path,
                fazes_count = reports.Count,
                reports_count = reportsCount
            };
        }

        private object LoadReport(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("path", out JsonElement pathElement)
                || pathElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(pathElement.GetString()))
            {
                throw new ArgumentException("path is required and must be a non-empty string");
            }

            string path = pathElement.GetString();

            if (!File.Exists(path))
            {
                throw new ArgumentException($"Report file not found: {path}");
            }

            OptimizerMaster master = GetMasterRequired();
            List<OptimizerFazeReport> reports = new List<OptimizerFazeReport>();

            using (StreamReader reader = new StreamReader(path))
            {
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();

                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    OptimizerFazeReport fazeReport = new OptimizerFazeReport();
                    fazeReport.LoadFromString(line);
                    reports.Add(fazeReport);
                }
            }

            master._optimizerExecutor.ReportsToFazes = reports;

            return GetReport(parameters);
        }

        #endregion

        #region Private methods

        private OptimizerMaster GetMasterRequired()
        {
            if (OptimizerMaster.Master == null)
            {
                throw new InvalidOperationException(
                    "Optimizer mode is not open. Open it first (terminal_open_mode optimizer or launch with -optimizer)");
            }

            return OptimizerMaster.Master;
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
