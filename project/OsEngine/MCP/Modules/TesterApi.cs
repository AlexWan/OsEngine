/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using OsEngine.Candles;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API module for Tester configuration and execution control.
    /// </summary>
    public class TesterApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;
        private TesterServer _subscribedServer;
        private TesterRegime _lastPublishedRegime;
        private DateTime _lastProgressEventTime = DateTime.MinValue;
        private readonly object _subscriptionLocker = new object();

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public TesterApi(Action<string, object> publishEvent)
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
                    case "tester_data_get_config":
                        response.Result = GetDataConfig();
                        break;

                    case "tester_data_get_available_sets":
                        response.Result = GetAvailableSets();
                        break;

                    case "tester_get_securities":
                        response.Result = GetTesterSecurities(request.Params);
                        break;

                    case "tester_data_set_config":
                        response.Result = SetDataConfig(request.Params);
                        break;

                    case "tester_execution_get_config":
                        response.Result = GetExecutionConfig();
                        break;

                    case "tester_execution_set_config":
                        response.Result = SetExecutionConfig(request.Params);
                        break;

                    case "tester_portfolio_get_config":
                        response.Result = GetPortfolioConfig();
                        break;

                    case "tester_portfolio_set_config":
                        response.Result = SetPortfolioConfig(request.Params);
                        break;

                    case "tester_start":
                        response.Result = StartTesting(request.Params);
                        break;

                    case "tester_pause":
                        response.Result = PauseTesting();
                        break;

                    case "tester_fast_forward":
                        response.Result = FastForwardTesting();
                        break;

                    case "tester_step_forward":
                        response.Result = StepForwardTesting();
                        break;

                    case "tester_stop":
                        response.Result = StopTesting();
                        break;

                    case "tester_get_status":
                        response.Result = GetTesterStatus();
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in tester API"
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
            List<McpTool> tools = new List<McpTool>
            {
                new McpTool
                {
                    Name = "tester_data_get_config",
                    Description = "Get tester data configuration",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_data_get_available_sets",
                    Description = "Get list of available OsData sets for tester",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_get_securities",
                    Description = "Get securities loaded in the tester server",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_data_set_config",
                    Description = "Set tester data configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            source_type = new { type = "string", description = "Set or Folder" },
                            set_name = new { type = "string" },
                            folder_path = new { type = "string" },
                            type_tester_data = new { type = "string", description = "Candle, TickAllCandleState, TickOnlyReadyCandle, MarketDepthAllCandleState, MarketDepthOnlyReadyCandle" },
                            date_from = new { type = "string", format = "date-time" },
                            date_to = new { type = "string", format = "date-time" },
                            delete_trades_from_memory = new { type = "boolean" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "tester_execution_get_config",
                    Description = "Get tester order execution configuration",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_execution_set_config",
                    Description = "Set tester order execution configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            slippage_to_simple_order = new { type = "integer" },
                            slippage_to_stop_order = new { type = "integer" },
                            order_execution_type = new { type = "string", description = "Touch, Intersection, FiftyFifty" },
                            non_trade_periods = new { type = "array" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "tester_portfolio_get_config",
                    Description = "Get tester portfolio configuration",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_portfolio_set_config",
                    Description = "Set tester portfolio configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            start_portfolio = new { type = "number" },
                            portfolio_calculation_enabled = new { type = "boolean" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "tester_start",
                    Description = "Start tester",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fast_forward = new { type = "boolean", description = "Enable fast forward immediately after start" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "tester_pause",
                    Description = "Pause tester",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_fast_forward",
                    Description = "Toggle tester fast forward mode",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_step_forward",
                    Description = "Execute one step forward (PlusOne)",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_stop",
                    Description = "Stop tester",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "tester_get_status",
                    Description = "Get tester status: regime, current time, start/end time, fast forward",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                }
            };

            return tools;
        }

        #endregion

        #region Private methods

        private TesterServer GetTesterServer()
        {
            List<IServer> servers = ServerMaster.GetServers();
            if (servers == null)
            {
                return null;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i] is TesterServer testerServer)
                {
                    return testerServer;
                }
            }

            return null;
        }

        private TesterServer GetTesterServerRequired()
        {
            TesterServer server = GetTesterServer();
            if (server == null)
            {
                throw new InvalidOperationException("Tester server is not available. Open tester mode first.");
            }
            return server;
        }

        #region Data configuration

        private object GetAvailableSets()
        {
            TesterServer server = GetTesterServerRequired();

            List<string> sets = server.Sets;

            if (sets == null)
            {
                return new List<string>();
            }

            return new List<string>(sets);
        }

        private object GetTesterSecurities(JsonElement parameters)
        {
            TesterServer server = GetTesterServerRequired();

            List<object> securities = new List<object>();

            if (server.Securities != null)
            {
                for (int i = 0; i < server.Securities.Count; i++)
                {
                    Security security = server.Securities[i];
                    if (security == null)
                    {
                        continue;
                    }

                    securities.Add(new
                    {
                        name = security.Name,
                        class_name = security.NameClass
                    });
                }
            }

            return new { securities = securities, count = securities.Count };
        }

        private object GetDataConfig()
        {
            TesterServer server = GetTesterServerRequired();

            string sourceType = server.SourceDataType.ToString();
            string setName = server.SourceDataType == TesterSourceDataType.Set
                ? GetSetDisplayName(server.ActiveSet)
                : null;
            string folderPath = server.SourceDataType == TesterSourceDataType.Folder
                ? server.PathToFolder
                : null;

            return new
            {
                source_type = sourceType,
                set_name = setName,
                folder_path = folderPath,
                type_tester_data = server.TypeTesterData.ToString(),
                date_from = server.TimeStart.ToString("O"),
                date_to = server.TimeEnd.ToString("O"),
                delete_trades_from_memory = server.RemoveTradesFromMemory
            };
        }

        private object SetDataConfig(JsonElement parameters)
        {
            TesterServer server = GetTesterServerRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("source_type", out JsonElement sourceTypeElement)
                && sourceTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TesterSourceDataType>(sourceTypeElement.GetString(), true, out TesterSourceDataType sourceType))
            {
                server.SourceDataType = sourceType;
            }

            if (parameters.TryGetProperty("set_name", out JsonElement setNameElement)
                && setNameElement.ValueKind == JsonValueKind.String)
            {
                string setName = setNameElement.GetString();
                if (!string.IsNullOrWhiteSpace(setName))
                {
                    server.SetNewSet(setName);
                }
            }

            if (parameters.TryGetProperty("folder_path", out JsonElement folderPathElement)
                && folderPathElement.ValueKind == JsonValueKind.String)
            {
                string folderPath = folderPathElement.GetString();
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    server.SetFolderPath(folderPath);
                }
            }

            if (parameters.TryGetProperty("type_tester_data", out JsonElement typeTesterDataElement)
                && typeTesterDataElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TesterDataType>(typeTesterDataElement.GetString(), true, out TesterDataType typeTesterData))
            {
                server.TypeTesterData = typeTesterData;
            }

            bool datesChanged = false;

            if (parameters.TryGetProperty("date_from", out JsonElement dateFromElement)
                && dateFromElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateFromElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateFrom))
            {
                server.TimeStart = dateFrom;
                datesChanged = true;
            }

            if (parameters.TryGetProperty("date_to", out JsonElement dateToElement)
                && dateToElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateToElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateTo))
            {
                server.TimeEnd = dateTo;
                datesChanged = true;
            }

            if (datesChanged)
            {
                server.SaveSecurityTestSettings();
            }

            if (parameters.TryGetProperty("delete_trades_from_memory", out JsonElement deleteTradesElement)
                && (deleteTradesElement.ValueKind == JsonValueKind.True || deleteTradesElement.ValueKind == JsonValueKind.False))
            {
                server.RemoveTradesFromMemory = deleteTradesElement.GetBoolean();
            }

            return GetDataConfig();
        }

        private string GetSetDisplayName(string activeSet)
        {
            if (string.IsNullOrWhiteSpace(activeSet))
            {
                return null;
            }

            const string prefix = @"Set_";
            int index = activeSet.LastIndexOf(prefix);
            if (index >= 0)
            {
                return activeSet.Substring(index + prefix.Length);
            }

            return activeSet;
        }

        #endregion

        #region Execution configuration

        private object GetExecutionConfig()
        {
            TesterServer server = GetTesterServerRequired();

            List<object> periods = new List<object>();
            if (server.NonTradePeriods != null)
            {
                for (int i = 0; i < server.NonTradePeriods.Count; i++)
                {
                    NonTradePeriod period = server.NonTradePeriods[i];
                    periods.Add(new
                    {
                        date_start = period.DateStart.ToString("O"),
                        date_end = period.DateEnd.ToString("O"),
                        is_on = period.IsOn
                    });
                }
            }

            return new
            {
                slippage_to_simple_order = server.SlippageToSimpleOrder,
                slippage_to_stop_order = server.SlippageToStopOrder,
                order_execution_type = server.OrderExecutionType.ToString(),
                non_trade_periods = periods
            };
        }

        private object SetExecutionConfig(JsonElement parameters)
        {
            TesterServer server = GetTesterServerRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("slippage_to_simple_order", out JsonElement slippageSimpleElement)
                && slippageSimpleElement.ValueKind == JsonValueKind.Number
                && slippageSimpleElement.TryGetInt32(out int slippageSimple))
            {
                server.SlippageToSimpleOrder = slippageSimple;
            }

            if (parameters.TryGetProperty("slippage_to_stop_order", out JsonElement slippageStopElement)
                && slippageStopElement.ValueKind == JsonValueKind.Number
                && slippageStopElement.TryGetInt32(out int slippageStop))
            {
                server.SlippageToStopOrder = slippageStop;
            }

            if (parameters.TryGetProperty("order_execution_type", out JsonElement orderExecutionTypeElement)
                && orderExecutionTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<OrderExecutionType>(orderExecutionTypeElement.GetString(), true, out OrderExecutionType orderExecutionType))
            {
                server.OrderExecutionType = orderExecutionType;
            }

            if (parameters.TryGetProperty("non_trade_periods", out JsonElement nonTradePeriodsElement)
                && nonTradePeriodsElement.ValueKind == JsonValueKind.Array)
            {
                List<NonTradePeriod> periods = new List<NonTradePeriod>();

                foreach (JsonElement periodElement in nonTradePeriodsElement.EnumerateArray())
                {
                    if (periodElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    NonTradePeriod period = new NonTradePeriod();

                    if (periodElement.TryGetProperty("date_start", out JsonElement dateStartElement)
                        && dateStartElement.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(dateStartElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateStart))
                    {
                        period.DateStart = dateStart;
                    }

                    if (periodElement.TryGetProperty("date_end", out JsonElement dateEndElement)
                        && dateEndElement.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(dateEndElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateEnd))
                    {
                        period.DateEnd = dateEnd;
                    }

                    if (periodElement.TryGetProperty("is_on", out JsonElement isOnElement)
                        && (isOnElement.ValueKind == JsonValueKind.True || isOnElement.ValueKind == JsonValueKind.False))
                    {
                        period.IsOn = isOnElement.GetBoolean();
                    }

                    periods.Add(period);
                }

                server.NonTradePeriods = periods;
                server.SaveNonTradePeriods();
            }

            return GetExecutionConfig();
        }

        #endregion

        #region Run control and status

        private object StartTesting(JsonElement parameters)
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            bool enableFastForward = false;
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("fast_forward", out JsonElement fastForwardElement)
                && (fastForwardElement.ValueKind == JsonValueKind.True || fastForwardElement.ValueKind == JsonValueKind.False))
            {
                enableFastForward = fastForwardElement.GetBoolean();
            }

            server.TestingStart();

            if (enableFastForward && !server.TestingFastIsActivate)
            {
                server.TestingFastOnOff();
            }

            return GetTesterStatus();
        }

        private object PauseTesting()
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            if (server.TesterRegime == TesterRegime.Play)
            {
                server.TesterRegime = TesterRegime.Pause;
            }

            return GetTesterStatus();
        }

        private object FastForwardTesting()
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            server.TestingFastOnOff();

            return GetTesterStatus();
        }

        private object StepForwardTesting()
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            server.TestingPlusOne();

            return GetTesterStatus();
        }

        private object StopTesting()
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            server.TestingFastIsActivate = false;
            server.TesterRegime = TesterRegime.NotActive;

            return GetTesterStatus();
        }

        private object GetTesterStatus()
        {
            TesterServer server = GetTesterServerRequired();
            EnsureSubscribed(server);

            double progressPercent = 0;
            if (server.TimeEnd > server.TimeStart)
            {
                long totalTicks = server.TimeEnd.Ticks - server.TimeStart.Ticks;
                long currentTicks = server.TimeNow.Ticks - server.TimeStart.Ticks;
                progressPercent = Math.Round((currentTicks * 100.0) / totalTicks, 2);
            }

            return new
            {
                regime = server.TesterRegime.ToString(),
                time_now = server.TimeNow.ToString("O"),
                time_start = server.TimeStart.ToString("O"),
                time_end = server.TimeEnd.ToString("O"),
                fast_forward = server.TestingFastIsActivate,
                is_already_started = server.IsAlreadyStarted,
                progress_percent = progressPercent
            };
        }

        private void EnsureSubscribed(TesterServer server)
        {
            if (server == null)
            {
                return;
            }

            lock (_subscriptionLocker)
            {
                if (_subscribedServer == server)
                {
                    return;
                }

                if (_subscribedServer != null)
                {
                    _subscribedServer.TestingStartEvent -= Server_TestingStartEvent;
                    _subscribedServer.TestingEndEvent -= Server_TestingEndEvent;
                    _subscribedServer.TestRegimeChangeEvent -= Server_TestRegimeChangeEvent;
                    _subscribedServer.NewCandleIncomeEvent -= Server_NewCandleIncomeEvent;
                }

                _subscribedServer = server;
                _lastPublishedRegime = server.TesterRegime;

                _subscribedServer.TestingStartEvent += Server_TestingStartEvent;
                _subscribedServer.TestingEndEvent += Server_TestingEndEvent;
                _subscribedServer.TestRegimeChangeEvent += Server_TestRegimeChangeEvent;
                _subscribedServer.NewCandleIncomeEvent += Server_NewCandleIncomeEvent;
            }
        }

        private void Server_TestingStartEvent()
        {
            _lastPublishedRegime = _subscribedServer?.TesterRegime ?? TesterRegime.NotActive;
            PublishEvent("tester.test.started", new { time = _subscribedServer?.TimeNow });
        }

        private void Server_TestingEndEvent()
        {
            _lastPublishedRegime = _subscribedServer?.TesterRegime ?? TesterRegime.NotActive;
            PublishEvent("tester.test.finished", new { time = _subscribedServer?.TimeNow });
        }

        private void Server_TestRegimeChangeEvent(TesterRegime regime)
        {
            if (_lastPublishedRegime == TesterRegime.Play && regime == TesterRegime.Pause)
            {
                PublishEvent("tester.test.paused", new { regime = regime.ToString(), time = _subscribedServer?.TimeNow });
            }
            else if (_lastPublishedRegime == TesterRegime.Pause && regime == TesterRegime.Play)
            {
                PublishEvent("tester.test.resumed", new { regime = regime.ToString(), time = _subscribedServer?.TimeNow });
            }

            _lastPublishedRegime = regime;
        }

        private void Server_NewCandleIncomeEvent(CandleSeries series)
        {
            TesterServer server = _subscribedServer;
            if (server == null)
            {
                return;
            }

            if (server.TesterRegime != TesterRegime.Play && server.TesterRegime != TesterRegime.PlusOne)
            {
                return;
            }

            DateTime now = DateTime.Now;
            if ((now - _lastProgressEventTime).TotalSeconds < 1)
            {
                return;
            }

            _lastProgressEventTime = now;

            double progressPercent = 0;
            if (server.TimeEnd > server.TimeStart)
            {
                long totalTicks = server.TimeEnd.Ticks - server.TimeStart.Ticks;
                long currentTicks = server.TimeNow.Ticks - server.TimeStart.Ticks;
                progressPercent = Math.Round((currentTicks * 100.0) / totalTicks, 2);
            }

            PublishEvent("tester.test.progress", new
            {
                time = server.TimeNow,
                progress_percent = progressPercent
            });
        }

        private void PublishEvent(string eventName, object payload)
        {
            try
            {
                _publishEvent?.Invoke(eventName, payload);
            }
            catch
            {
                // ignore event publishing errors
            }
        }

        #endregion

        #region Portfolio configuration

        private object GetPortfolioConfig()
        {
            TesterServer server = GetTesterServerRequired();

            return new
            {
                start_portfolio = server.StartPortfolio,
                portfolio_calculation_enabled = server.ProfitMarketIsOn
            };
        }

        private object SetPortfolioConfig(JsonElement parameters)
        {
            TesterServer server = GetTesterServerRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            bool portfolioChanged = false;

            if (parameters.TryGetProperty("start_portfolio", out JsonElement startPortfolioElement)
                && startPortfolioElement.ValueKind == JsonValueKind.Number
                && startPortfolioElement.TryGetDecimal(out decimal startPortfolio))
            {
                server.StartPortfolio = startPortfolio;
                portfolioChanged = true;
            }

            if (parameters.TryGetProperty("portfolio_calculation_enabled", out JsonElement portfolioCalcElement)
                && (portfolioCalcElement.ValueKind == JsonValueKind.True || portfolioCalcElement.ValueKind == JsonValueKind.False))
            {
                server.ProfitMarketIsOn = portfolioCalcElement.GetBoolean();
            }

            if (portfolioChanged)
            {
                server.Save();
            }

            return GetPortfolioConfig();
        }

        #endregion

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
