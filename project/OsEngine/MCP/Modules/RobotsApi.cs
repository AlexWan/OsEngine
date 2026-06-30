/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using OsEngine.Candles;
using OsEngine.Candles.Factory;
using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using JournalClass = OsEngine.Journal.Journal;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.MCP.Json;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API module for robot management.
    /// Works in any mode that has a robot list: IsTester, IsOsTrader, IsOsOptimizer.
    /// </summary>
    public class RobotsApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public RobotsApi(Action<string, object> publishEvent)
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
                    case "bot_get_list":
                        response.Result = GetBots();
                        break;

                    case "bot_create":
                        response.Result = CreateBot(request.Params);
                        break;

                    case "bot_delete":
                        response.Result = DeleteBot(request.Params);
                        break;

                    case "bot_get_params":
                        response.Result = GetBotParams(request.Params);
                        break;

                    case "bot_set_params":
                        response.Result = SetBotParams(request.Params);
                        break;

                    case "bot_get_sources":
                        response.Result = GetBotSources(request.Params);
                        break;

                    case "bot_get_config_tab_simple":
                        response.Result = GetBotConfigTabSimple(request.Params);
                        break;

                    case "bot_set_config_tab_simple":
                        response.Result = SetBotConfigTabSimple(request.Params);
                        break;

                    case "bot_get_config_tab_screener":
                        response.Result = GetBotConfigTabScreener(request.Params);
                        break;

                    case "bot_set_config_tab_screener":
                        response.Result = SetBotConfigTabScreener(request.Params);
                        break;

                    case "bot_get_config_tab_index":
                        response.Result = GetBotConfigTabIndex(request.Params);
                        break;

                    case "bot_set_config_tab_index":
                        response.Result = SetBotConfigTabIndex(request.Params);
                        break;

                    case "bot_journal_get_settings":
                        response.Result = GetJournalSettings(request.Params);
                        break;

                    case "bot_journal_set_settings":
                        response.Result = SetJournalSettings(request.Params);
                        break;

                    case "bot_journal_get_summary":
                        response.Result = GetJournalSummary(request.Params);
                        break;

                    case "bot_journal_get_equity":
                        response.Result = GetJournalEquity(request.Params);
                        break;

                    case "bot_journal_get_statistics":
                        response.Result = GetJournalStatistics(request.Params);
                        break;

                    case "bot_journal_get_drawdown":
                        response.Result = GetJournalDrawdown(request.Params);
                        break;

                    case "bot_journal_get_volume":
                        response.Result = GetJournalVolume(request.Params);
                        break;

                    case "bot_journal_get_open_positions":
                        response.Result = GetJournalOpenPositions(request.Params);
                        break;

                    case "bot_journal_get_closed_positions":
                        response.Result = GetJournalClosedPositions(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in robots API"
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
                    Name = "bot_get_list",
                    Description = "Get list of robots loaded in the terminal",
                    InputSchema = new { type = "object", properties = new { }, required = new string[0] }
                },
                new McpTool
                {
                    Name = "bot_create",
                    Description = "Create a new robot",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            strategy_name = new { type = "string", description = "Strategy class name from wiki_robots_list" },
                            name = new { type = "string", description = "Optional unique robot name. Generated automatically if not provided" }
                        },
                        required = new[] { "strategy_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_delete",
                    Description = "Delete a robot by name or number",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" }
                        },
                        required = new[] { "bot_id" }
                    }
                },
                new McpTool
                {
                    Name = "bot_get_params",
                    Description = "Get robot strategy parameters",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" }
                        },
                        required = new[] { "bot_id" }
                    }
                },
                new McpTool
                {
                    Name = "bot_set_params",
                    Description = "Set robot strategy parameters",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            parameters = new
                            {
                                type = "object",
                                additionalProperties = true,
                                description = "Dictionary of parameter names to values"
                            }
                        },
                        required = new[] { "bot_id", "parameters" }
                    }
                },
                new McpTool
                {
                    Name = "bot_get_sources",
                    Description = "Get list of robot tabs (sources) with types and names",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" }
                        },
                        required = new[] { "bot_id" }
                    }
                },
                new McpTool
                {
                    Name = "bot_get_config_tab_simple",
                    Description = "Get BotTabSimple connector configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Tab name from bot_get_sources" }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_set_config_tab_simple",
                    Description = "Set BotTabSimple connector configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Tab name from bot_get_sources" },
                            server_type = new { type = "string" },
                            server_full_name = new { type = "string" },
                            portfolio_name = new { type = "string" },
                            emulator_is_on = new { type = "boolean" },
                            commission_type = new { type = "string", description = "None, Percent, OneLotFix" },
                            commission_value = new { type = "number" },
                            security_class = new { type = "string" },
                            security_name = new { type = "string" },
                            events_is_on = new { type = "boolean" },
                            candle_market_data_type = new { type = "string", description = "Tick, MarketDepth" },
                            candle_create_method_type = new { type = "string", description = "Simple, Renko, Volume, Ticks, Delta, HeikenAshi, Revers, Range" },
                            time_frame = new { type = "string", description = "TimeFrame enum value" },
                            save_trades_in_candles = new { type = "boolean" },
                            build_non_trading_candles = new { type = "boolean" }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_get_config_tab_screener",
                    Description = "Get BotTabScreener configuration",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Screener tab name from bot_get_sources" }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_set_config_tab_screener",
                    Description = "Set BotTabScreener configuration (server, portfolio, time frame, securities)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Screener tab name from bot_get_sources" },
                            server_type = new { type = "string" },
                            server_name = new { type = "string" },
                            portfolio_name = new { type = "string" },
                            emulator_is_on = new { type = "boolean" },
                            candle_market_data_type = new { type = "string", description = "Tick, MarketDepth" },
                            candle_create_method_type = new { type = "string", description = "Simple, Renko, Volume, Ticks, Delta, HeikenAshi, Revers, Range" },
                            commission_type = new { type = "string", description = "None, Percent, OneLotFix" },
                            commission_value = new { type = "number" },
                            save_trades_in_candles = new { type = "boolean" },
                            time_frame = new { type = "string", description = "TimeFrame enum value" },
                            securities_class = new { type = "string" },
                            events_is_on = new { type = "boolean" },
                            securities = new
                            {
                                type = "array",
                                description = "List of ActivatedSecurity objects: name, class, is_on",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        class_name = new { type = "string" },
                                        is_on = new { type = "boolean" }
                                    }
                                }
                            }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_get_config_tab_index",
                    Description = "Get BotTabIndex configuration (formula, securities, time frame)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Index tab name from bot_get_sources" }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_set_config_tab_index",
                    Description = "Set BotTabIndex configuration (formula, securities, time frame, calculation depth, auto-formula)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_id = new { type = "string", description = "Robot number or unique name" },
                            tab_name = new { type = "string", description = "Index tab name from bot_get_sources" },
                            server_type = new { type = "string" },
                            server_name = new { type = "string" },
                            portfolio_name = new { type = "string" },
                            emulator_is_on = new { type = "boolean" },
                            candle_market_data_type = new { type = "string", description = "Tick, MarketDepth" },
                            candle_create_method_type = new { type = "string", description = "Simple, Renko, Volume, Ticks, Delta, HeikenAshi, Revers, Range" },
                            commission_type = new { type = "string", description = "None, Percent, OneLotFix" },
                            commission_value = new { type = "number" },
                            save_trades_in_candles = new { type = "boolean" },
                            time_frame = new { type = "string", description = "TimeFrame enum value" },
                            securities_class = new { type = "string" },
                            events_is_on = new { type = "boolean" },
                            user_formula = new { type = "string", description = "Index formula, e.g. A0+A1" },
                            calculation_depth = new { type = "integer", description = "Number of candles used for index calculation" },
                            percent_normalization = new { type = "boolean", description = "Normalize candles in percent before calculation" },
                            auto_formula = new
                            {
                                type = "object",
                                description = "Auto-formula builder settings",
                                properties = new
                                {
                                    regime = new { type = "string", description = "Off, OncePerHour, OncePerDay, OncePerWeek" },
                                    day_of_week = new { type = "string", description = "Monday..Sunday" },
                                    hour = new { type = "integer", description = "Hour of day to rebuild formula" },
                                    sec_count = new { type = "integer", description = "Number of securities to include in auto formula" },
                                    days_look_back = new { type = "integer", description = "Days to look back when selecting securities" },
                                    sort_type = new { type = "string", description = "FirstInArray, VolumeWeighted, MaxVolatilityWeighted, MinVolatilityWeighted" },
                                    mult_type = new { type = "string", description = "PriceWeighted, VolumeWeighted, EqualWeighted, Cointegration" },
                                    write_log_on_rebuild = new { type = "boolean" }
                                }
                            },
                            securities = new
                            {
                                type = "array",
                                description = "List of ActivatedSecurity objects: name, class, is_on",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        class_name = new { type = "string" },
                                        is_on = new { type = "boolean" }
                                    }
                                }
                            }
                        },
                        required = new[] { "bot_id", "tab_name" }
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_settings",
                    Description = "Get journal settings (robot group, multiplier and on/off state)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns settings for all robots" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_set_settings",
                    Description = "Set journal settings (robot group, multiplier and on/off state)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, settings apply to all listed robots" },
                            settings = new
                            {
                                type = "array",
                                description = "Array of {bot_name, group, mult, is_on}",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        bot_name = new { type = "string" },
                                        group = new { type = "string" },
                                        mult = new { type = "number" },
                                        is_on = new { type = "boolean" }
                                    },
                                    required = new[] { "bot_name" }
                                }
                            }
                        },
                        required = new[] { "settings" }
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_summary",
                    Description = "Get journal summary: total profit and period",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns summary for all robots" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_equity",
                    Description = "Get equity curve",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns equity for all robots" },
                            chart_type = new { type = "string", description = "Absolute, Percent1Contract, DepositPercent" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_statistics",
                    Description = "Get journal statistics table",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns statistics for all robots" },
                            side = new { type = "string", description = "All, Long, Short" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_drawdown",
                    Description = "Get drawdown curve",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns drawdown for all robots" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_volume",
                    Description = "Get volume and leverage per security",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns volumes for all robots" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_open_positions",
                    Description = "Get open positions",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns open positions for all robots" },
                            limit = new { type = "integer" },
                            offset = new { type = "integer" }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "bot_journal_get_closed_positions",
                    Description = "Get closed positions",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            bot_name = new { type = "string", description = "Optional unique robot name. If omitted, returns closed positions for all robots" },
                            include_failed = new { type = "boolean" },
                            limit = new { type = "integer" },
                            offset = new { type = "integer" }
                        },
                        required = new string[0]
                    }
                }
            };
        }

        #endregion

        #region Private methods

        private OsTraderMaster GetMaster()
        {
            return OsTraderMaster.Master;
        }

        private OsTraderMaster GetMasterRequired()
        {
            OsTraderMaster master = GetMaster();
            if (master == null)
            {
                throw new InvalidOperationException("Robot master is not available. Open a mode that supports robots first.");
            }
            return master;
        }

        public object GetBots()
        {
            OsTraderMaster master = GetMasterRequired();
            List<object> bots = new List<object>();

            if (master.PanelsArray != null)
            {
                for (int i = 0; i < master.PanelsArray.Count; i++)
                {
                    BotPanel bot = master.PanelsArray[i];
                    bots.Add(new
                    {
                        number = i + 1,
                        class_name = bot.GetNameStrategyType(),
                        name = bot.NameStrategyUniq
                    });
                }
            }

            return new { bots = bots, count = bots.Count };
        }

        public BotPanel CreateBotPanel(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("strategy_name", out JsonElement strategyNameElement)
                || strategyNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("strategy_name is required");
            }

            string strategyName = strategyNameElement.GetString();

            string instanceName = null;
            if (parameters.TryGetProperty("name", out JsonElement nameElement)
                && nameElement.ValueKind == JsonValueKind.String)
            {
                instanceName = nameElement.GetString();
            }

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();
            List<string> scriptNames = BotFactory.GetScriptsNamesStrategy();
            bool isScript = scriptNames.Contains(strategyName);

            if (!includeNames.Contains(strategyName) && !isScript)
            {
                throw new ArgumentException($"Unknown strategy: {strategyName}");
            }

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceName = GenerateUniqueBotName(strategyName, master);
            }
            else
            {
                instanceName = SanitizeBotName(instanceName);
            }

            if (BotNameExists(instanceName, master))
            {
                throw new ArgumentException($"Robot name '{instanceName}' already exists");
            }

            BotPanel bot = BotFactory.GetStrategyForName(strategyName, instanceName, master._startProgram, isScript);
            if (bot == null)
            {
                throw new InvalidOperationException($"Failed to create robot '{strategyName}'");
            }

            try
            {
                if (MainWindow.GetDispatcher.CheckAccess())
                {
                    master.CreateNewBot(bot);
                }
                else
                {
                    MainWindow.GetDispatcher.Invoke(() => master.CreateNewBot(bot));
                }
            }
            catch
            {
                bot.Delete();
                throw;
            }

            return bot;
        }

        private object CreateBot(JsonElement parameters)
        {
            BotPanel bot = CreateBotPanel(parameters);

            OsTraderMaster master = GetMaster();
            int number = 1;
            if (master?.PanelsArray != null)
            {
                for (int i = 0; i < master.PanelsArray.Count; i++)
                {
                    if (master.PanelsArray[i].NameStrategyUniq == bot.NameStrategyUniq)
                    {
                        number = i + 1;
                        break;
                    }
                }
            }

            return new
            {
                number = number,
                class_name = bot.GetNameStrategyType(),
                name = bot.NameStrategyUniq
            };
        }

        public object DeleteBot(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel botToDelete = null;
            int? number = null;

            if (botIdElement.ValueKind == JsonValueKind.Number && botIdElement.TryGetInt32(out int num))
            {
                number = num;
                if (num < 1 || master.PanelsArray == null || num > master.PanelsArray.Count)
                {
                    throw new ArgumentException($"Bot number {num} is out of range");
                }
                botToDelete = master.PanelsArray[num - 1];
            }
            else if (botIdElement.ValueKind == JsonValueKind.String)
            {
                string name = botIdElement.GetString();
                if (master.PanelsArray != null)
                {
                    for (int i = 0; i < master.PanelsArray.Count; i++)
                    {
                        if (master.PanelsArray[i].NameStrategyUniq == name)
                        {
                            botToDelete = master.PanelsArray[i];
                            number = i + 1;
                            break;
                        }
                    }
                }
                if (botToDelete == null)
                {
                    throw new ArgumentException($"Robot with name '{name}' not found");
                }
            }
            else
            {
                throw new ArgumentException("bot_id must be a number or string");
            }

            string deletedName = botToDelete.NameStrategyUniq;
            int deletedNumber = number.Value;
            string botIdString = botIdElement.ToString();

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                master.DeleteRobotByInstance(botToDelete);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => master.DeleteRobotByInstance(botToDelete));
            }

            return new
            {
                deleted = true,
                bot_id = botIdString,
                number = deletedNumber,
                name = deletedName
            };
        }

        private string GenerateUniqueBotName(string baseName, OsTraderMaster master)
        {
            string sanitized = SanitizeBotName(baseName);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Bot";
            }

            if (!BotNameExists(sanitized, master))
            {
                return sanitized;
            }

            int counter = 1;
            while (BotNameExists($"{sanitized}_{counter}", master))
            {
                counter++;
            }

            return $"{sanitized}_{counter}";
        }

        private string SanitizeBotName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name
                .Replace("/", "").Replace("\\", "").Replace("*", "").Replace("-", "")
                .Replace("+", "").Replace(":", "").Replace("@", "").Replace(";", "")
                .Replace("%", "").Replace(">", "").Replace("<", "").Replace("^", "")
                .Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "")
                .Replace("`", "").Replace("(", "").Replace(")", "")
                .Replace("$", "").Replace("#", "").Replace("!", "").Replace("&", "")
                .Replace("?", "").Replace("=", "").Replace(",", "").Replace(".", "")
                .Replace("'", "").Replace("|", "").Replace("~", "").Replace("№", "")
                .Replace("\"", "")
                .Trim();
        }

        private bool BotNameExists(string name, OsTraderMaster master)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            if (master.PanelsArray != null)
            {
                for (int i = 0; i < master.PanelsArray.Count; i++)
                {
                    if (master.PanelsArray[i].NameStrategyUniq == name)
                    {
                        return true;
                    }
                }
            }

            string realFile = @"Engine\SettingsRealKeeper.txt";
            string testerFile = @"Engine\SettingsTesterKeeper.txt";

            return NameExistsInFile(name, realFile) || NameExistsInFile(name, testerFile);
        }

        private bool NameExistsInFile(string name, string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        string[] parts = line.Split('@');
                        if (parts.Length > 0 && parts[0] == name)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // ignore file read errors
            }

            return false;
        }

        private BotPanel FindBot(OsTraderMaster master, JsonElement botIdElement)
        {
            if (botIdElement.ValueKind == JsonValueKind.Number && botIdElement.TryGetInt32(out int num))
            {
                if (num < 1 || master.PanelsArray == null || num > master.PanelsArray.Count)
                {
                    throw new ArgumentException($"Bot number {num} is out of range");
                }
                return master.PanelsArray[num - 1];
            }
            else if (botIdElement.ValueKind == JsonValueKind.String)
            {
                string name = botIdElement.GetString();
                if (master.PanelsArray != null)
                {
                    for (int i = 0; i < master.PanelsArray.Count; i++)
                    {
                        if (master.PanelsArray[i].NameStrategyUniq == name)
                        {
                            return master.PanelsArray[i];
                        }
                    }
                }
                throw new ArgumentException($"Robot with name '{name}' not found");
            }
            else
            {
                throw new ArgumentException("bot_id must be a number or string");
            }
        }

        private object GetBotParams(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);
            List<object> result = new List<object>();

            if (bot.Parameters != null)
            {
                for (int i = 0; i < bot.Parameters.Count; i++)
                {
                    IIStrategyParameter param = bot.Parameters[i];
                    object paramObj = SerializeParameter(param);
                    if (paramObj != null)
                    {
                        result.Add(paramObj);
                    }
                }
            }

            return new { parameters = result, count = result.Count };
        }

        private object SerializeParameter(IIStrategyParameter param)
        {
            switch (param.Type)
            {
                case StrategyParameterType.Int:
                    StrategyParameterInt intParam = (StrategyParameterInt)param;
                    return new
                    {
                        name = intParam.Name,
                        type = "Int",
                        value = intParam.ValueInt,
                        default_value = intParam.ValueIntDefolt,
                        start = intParam.ValueIntStart,
                        stop = intParam.ValueIntStop,
                        step = intParam.ValueIntStep,
                        step_type = intParam.StepType.ToString()
                    };

                case StrategyParameterType.Decimal:
                    StrategyParameterDecimal decimalParam = (StrategyParameterDecimal)param;
                    return new
                    {
                        name = decimalParam.Name,
                        type = "Decimal",
                        value = decimalParam.ValueDecimal,
                        default_value = decimalParam.ValueDecimalDefolt,
                        start = decimalParam.ValueDecimalStart,
                        stop = decimalParam.ValueDecimalStop,
                        step = decimalParam.ValueDecimalStep,
                        step_type = decimalParam.StepType.ToString()
                    };

                case StrategyParameterType.String:
                    StrategyParameterString stringParam = (StrategyParameterString)param;
                    return new
                    {
                        name = stringParam.Name,
                        type = "String",
                        value = stringParam.ValueString,
                        values = stringParam.ValuesString
                    };

                case StrategyParameterType.Bool:
                    StrategyParameterBool boolParam = (StrategyParameterBool)param;
                    return new
                    {
                        name = boolParam.Name,
                        type = "Bool",
                        value = boolParam.ValueBool,
                        default_value = boolParam.ValueBoolDefolt
                    };

                case StrategyParameterType.TimeOfDay:
                    StrategyParameterTimeOfDay timeParam = (StrategyParameterTimeOfDay)param;
                    return new
                    {
                        name = timeParam.Name,
                        type = "TimeOfDay",
                        value = $"{timeParam.Value.Hour:D2}:{timeParam.Value.Minute:D2}:{timeParam.Value.Second:D2}"
                    };

                case StrategyParameterType.CheckBox:
                    StrategyParameterCheckBox checkParam = (StrategyParameterCheckBox)param;
                    return new
                    {
                        name = checkParam.Name,
                        type = "CheckBox",
                        value = checkParam.CheckState.ToString()
                    };

                case StrategyParameterType.DecimalCheckBox:
                    StrategyParameterDecimalCheckBox decimalCheckParam = (StrategyParameterDecimalCheckBox)param;
                    return new
                    {
                        name = decimalCheckParam.Name,
                        type = "DecimalCheckBox",
                        value = decimalCheckParam.ValueDecimal,
                        check_state = decimalCheckParam.CheckState.ToString(),
                        default_value = decimalCheckParam.ValueDecimalDefolt,
                        start = decimalCheckParam.ValueDecimalStart,
                        stop = decimalCheckParam.ValueDecimalStop,
                        step = decimalCheckParam.ValueDecimalStep,
                        step_type = decimalCheckParam.StepType.ToString()
                    };

                case StrategyParameterType.Button:
                case StrategyParameterType.Label:
                    return new
                    {
                        name = param.Name,
                        type = param.Type.ToString()
                    };

                default:
                    return null;
            }
        }

        private object SetBotParams(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            if (!parameters.TryGetProperty("parameters", out JsonElement paramsToSetElement)
                || paramsToSetElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("parameters is required and must be an object");
            }

            BotPanel bot = FindBot(master, botIdElement);
            List<string> updated = new List<string>();
            List<string> notFound = new List<string>();

            foreach (JsonProperty property in paramsToSetElement.EnumerateObject())
            {
                string paramName = property.Name;
                IIStrategyParameter targetParam = bot.Parameters?.Find(p => p.Name == paramName);

                if (targetParam == null)
                {
                    notFound.Add(paramName);
                    continue;
                }

                SetParameterValue(targetParam, property.Value);
                updated.Add(paramName);
            }

            return new
            {
                updated = updated,
                updated_count = updated.Count,
                not_found = notFound,
                not_found_count = notFound.Count
            };
        }

        private void SetParameterValue(IIStrategyParameter parameter, JsonElement valueElement)
        {
            switch (parameter.Type)
            {
                case StrategyParameterType.Int:
                    if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out int intValue))
                    {
                        ((StrategyParameterInt)parameter).ValueInt = intValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires an integer value");
                    }
                    break;

                case StrategyParameterType.Decimal:
                    if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out decimal decimalValue))
                    {
                        ((StrategyParameterDecimal)parameter).ValueDecimal = decimalValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a decimal value");
                    }
                    break;

                case StrategyParameterType.String:
                    if (valueElement.ValueKind == JsonValueKind.String)
                    {
                        ((StrategyParameterString)parameter).ValueString = valueElement.GetString();
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a string value");
                    }
                    break;

                case StrategyParameterType.Bool:
                    if (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False)
                    {
                        ((StrategyParameterBool)parameter).ValueBool = valueElement.GetBoolean();
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a boolean value");
                    }
                    break;

                case StrategyParameterType.TimeOfDay:
                    if (valueElement.ValueKind == JsonValueKind.String
                        && TimeSpan.TryParse(valueElement.GetString(), out TimeSpan timeSpan))
                    {
                        StrategyParameterTimeOfDay timeParam = (StrategyParameterTimeOfDay)parameter;
                        timeParam.Value.Hour = timeSpan.Hours;
                        timeParam.Value.Minute = timeSpan.Minutes;
                        timeParam.Value.Second = timeSpan.Seconds;
                        timeParam.Value.Millisecond = timeSpan.Milliseconds;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a time value in HH:MM:SS format");
                    }
                    break;

                case StrategyParameterType.CheckBox:
                    if (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False)
                    {
                        ((StrategyParameterCheckBox)parameter).CheckState = valueElement.GetBoolean()
                            ? CheckState.Checked
                            : CheckState.Unchecked;
                    }
                    else if (valueElement.ValueKind == JsonValueKind.String && Enum.TryParse<CheckState>(valueElement.GetString(), true, out CheckState checkState))
                    {
                        ((StrategyParameterCheckBox)parameter).CheckState = checkState;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a boolean or CheckState value");
                    }
                    break;

                case StrategyParameterType.DecimalCheckBox:
                    if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out decimal decimalCheckValue))
                    {
                        ((StrategyParameterDecimalCheckBox)parameter).ValueDecimal = decimalCheckValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter '{parameter.Name}' requires a decimal value");
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Parameter type '{parameter.Type}' is not supported for setting");
            }
        }

        private object GetBotSources(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);
            List<object> sources = new List<object>();

            AddSources(sources, bot.TabsSimple, "Simple");
            AddSources(sources, bot.TabsScreener, "Screener");
            AddSources(sources, bot.TabsIndex, "Index");
            AddSources(sources, bot.TabsCluster, "Cluster");
            AddSources(sources, bot.TabsPair, "Pair");
            AddSources(sources, bot.TabsPolygon, "Polygon");
            AddSources(sources, bot.TabsNews, "News");
            AddSources(sources, bot.TabsSyntheticBond, "SyntheticBond");

            return new { sources = sources, count = sources.Count };
        }

        private void AddSources<T>(List<object> sources, List<T> tabs, string type)
        {
            if (tabs == null)
            {
                return;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                T tab = tabs[i];
                string tabName = GetTabName(tab);
                sources.Add(new
                {
                    type = type,
                    name = tabName
                });
            }
        }

        private string GetTabName<T>(T tab)
        {
            if (tab == null)
            {
                return null;
            }

            System.Reflection.PropertyInfo property = typeof(T).GetProperty("TabName");
            if (property != null)
            {
                return property.GetValue(tab) as string;
            }

            return tab.ToString();
        }

        private object GetBotConfigTabSimple(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabSimple tab = FindBotTabSimple(bot, tabName);
            ConnectorCandles connector = tab.Connector;

            bool buildNonTradingCandles = false;
            if (connector.TimeFrameBuilder.CandleSeriesRealization is Simple simpleSeries)
            {
                buildNonTradingCandles = simpleSeries.BuildNonTradingCandles.ValueBool;
            }

            return new
            {
                server_type = connector.ServerType.ToString(),
                server_full_name = connector.ServerFullName,
                portfolio_name = connector.PortfolioName,
                emulator_is_on = connector.EmulatorIsOn,
                commission_type = connector.CommissionType.ToString(),
                commission_value = connector.CommissionValue,
                security_class = connector.SecurityClass,
                security_name = connector.SecurityName,
                events_is_on = connector.EventsIsOn,
                candle_market_data_type = connector.CandleMarketDataType.ToString(),
                candle_create_method_type = connector.CandleCreateMethodType,
                time_frame = connector.TimeFrame.ToString(),
                save_trades_in_candles = connector.SaveTradesInCandles,
                build_non_trading_candles = buildNonTradingCandles
            };
        }

        private object SetBotConfigTabSimple(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabSimple tab = FindBotTabSimple(bot, tabName);

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                ApplyBotConfigTabSimple(tab, parameters);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => ApplyBotConfigTabSimple(tab, parameters));
            }

            return GetBotConfigTabSimple(parameters);
        }

        private void ApplyBotConfigTabSimple(BotTabSimple tab, JsonElement parameters)
        {
            ConnectorCandles connector = tab.Connector;

            if (parameters.TryGetProperty("server_type", out JsonElement serverTypeElement)
                && serverTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<ServerType>(serverTypeElement.GetString(), true, out ServerType serverType))
            {
                connector.ServerType = serverType;
            }

            if (parameters.TryGetProperty("server_full_name", out JsonElement serverFullNameElement)
                && serverFullNameElement.ValueKind == JsonValueKind.String)
            {
                connector.ServerFullName = serverFullNameElement.GetString();
            }

            if (parameters.TryGetProperty("portfolio_name", out JsonElement portfolioNameElement)
                && portfolioNameElement.ValueKind == JsonValueKind.String)
            {
                connector.PortfolioName = portfolioNameElement.GetString();
            }

            if (parameters.TryGetProperty("emulator_is_on", out JsonElement emulatorIsOnElement)
                && (emulatorIsOnElement.ValueKind == JsonValueKind.True || emulatorIsOnElement.ValueKind == JsonValueKind.False))
            {
                connector.EmulatorIsOn = emulatorIsOnElement.GetBoolean();
            }

            if (parameters.TryGetProperty("commission_type", out JsonElement commissionTypeElement)
                && commissionTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CommissionType>(commissionTypeElement.GetString(), true, out CommissionType commissionType))
            {
                connector.CommissionType = commissionType;
            }

            if (parameters.TryGetProperty("commission_value", out JsonElement commissionValueElement)
                && commissionValueElement.ValueKind == JsonValueKind.Number
                && commissionValueElement.TryGetDecimal(out decimal commissionValue))
            {
                connector.CommissionValue = commissionValue;
            }

            if (parameters.TryGetProperty("events_is_on", out JsonElement eventsIsOnElement)
                && (eventsIsOnElement.ValueKind == JsonValueKind.True || eventsIsOnElement.ValueKind == JsonValueKind.False))
            {
                connector.EventsIsOn = eventsIsOnElement.GetBoolean();
            }

            if (parameters.TryGetProperty("candle_market_data_type", out JsonElement candleMarketDataTypeElement)
                && candleMarketDataTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CandleMarketDataType>(candleMarketDataTypeElement.GetString(), true, out CandleMarketDataType candleMarketDataType))
            {
                connector.CandleMarketDataType = candleMarketDataType;
            }

            if (parameters.TryGetProperty("candle_create_method_type", out JsonElement candleCreateMethodTypeElement)
                && candleCreateMethodTypeElement.ValueKind == JsonValueKind.String)
            {
                connector.CandleCreateMethodType = candleCreateMethodTypeElement.GetString();
            }

            if (parameters.TryGetProperty("time_frame", out JsonElement timeFrameElement)
                && timeFrameElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TimeFrame>(timeFrameElement.GetString(), true, out TimeFrame timeFrame))
            {
                connector.TimeFrame = timeFrame;
            }

            if (parameters.TryGetProperty("save_trades_in_candles", out JsonElement saveTradesElement)
                && (saveTradesElement.ValueKind == JsonValueKind.True || saveTradesElement.ValueKind == JsonValueKind.False))
            {
                connector.SaveTradesInCandles = saveTradesElement.GetBoolean();
            }

            if (parameters.TryGetProperty("build_non_trading_candles", out JsonElement buildNonTradingElement)
                && (buildNonTradingElement.ValueKind == JsonValueKind.True || buildNonTradingElement.ValueKind == JsonValueKind.False))
            {
                if (connector.TimeFrameBuilder.CandleSeriesRealization is Simple simpleSeries)
                {
                    simpleSeries.BuildNonTradingCandles.ValueBool = buildNonTradingElement.GetBoolean();
                }
            }

            bool securityChanged = false;

            if (parameters.TryGetProperty("security_class", out JsonElement securityClassElement)
                && securityClassElement.ValueKind == JsonValueKind.String)
            {
                connector.SecurityClass = securityClassElement.GetString();
                securityChanged = true;
            }

            if (parameters.TryGetProperty("security_name", out JsonElement securityNameElement)
                && securityNameElement.ValueKind == JsonValueKind.String)
            {
                connector.SecurityName = securityNameElement.GetString();
                securityChanged = true;
            }

            connector.TimeFrameBuilder.Save();
            connector.Save();

            if (!securityChanged)
            {
                connector.ReconnectHard();
            }
        }

        private BotTabSimple FindBotTabSimple(BotPanel bot, string tabName)
        {
            if (bot.TabsSimple == null)
            {
                throw new InvalidOperationException($"Bot '{bot.NameStrategyUniq}' has no Simple tabs");
            }

            for (int i = 0; i < bot.TabsSimple.Count; i++)
            {
                if (bot.TabsSimple[i].TabName == tabName)
                {
                    return bot.TabsSimple[i];
                }
            }

            throw new ArgumentException($"Simple tab '{tabName}' not found in bot '{bot.NameStrategyUniq}'");
        }

        private object GetBotConfigTabScreener(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabScreener screener = FindBotTabScreener(bot, tabName);

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
                server_type = screener.ServerType.ToString(),
                server_name = screener.ServerName,
                portfolio_name = screener.PortfolioName,
                emulator_is_on = screener.EmulatorIsOn,
                candle_market_data_type = screener.CandleMarketDataType.ToString(),
                candle_create_method_type = screener.CandleCreateMethodType,
                commission_type = screener.CommissionType.ToString(),
                commission_value = screener.CommissionValue,
                save_trades_in_candles = screener.SaveTradesInCandles,
                time_frame = screener.TimeFrame.ToString(),
                securities_class = screener.SecuritiesClass,
                events_is_on = screener.EventsIsOn,
                candle_series_realization = screener.CandleSeriesRealization?.GetType().Name,
                securities = securities,
                tabs_count = screener.Tabs?.Count ?? 0
            };
        }

        private object SetBotConfigTabScreener(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabScreener screener = FindBotTabScreener(bot, tabName);

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                ApplyBotConfigTabScreener(screener, parameters);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => ApplyBotConfigTabScreener(screener, parameters));
            }

            return GetBotConfigTabScreener(parameters);
        }

        private void ApplyBotConfigTabScreener(BotTabScreener screener, JsonElement parameters)
        {
            bool needReload = false;

            if (parameters.TryGetProperty("server_type", out JsonElement serverTypeElement)
                && serverTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<ServerType>(serverTypeElement.GetString(), true, out ServerType serverType))
            {
                screener.ServerType = serverType;
                needReload = true;
            }

            if (parameters.TryGetProperty("server_name", out JsonElement serverNameElement)
                && serverNameElement.ValueKind == JsonValueKind.String)
            {
                screener.ServerName = serverNameElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("portfolio_name", out JsonElement portfolioNameElement)
                && portfolioNameElement.ValueKind == JsonValueKind.String)
            {
                screener.PortfolioName = portfolioNameElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("emulator_is_on", out JsonElement emulatorIsOnElement)
                && (emulatorIsOnElement.ValueKind == JsonValueKind.True || emulatorIsOnElement.ValueKind == JsonValueKind.False))
            {
                screener.EmulatorIsOn = emulatorIsOnElement.GetBoolean();
                needReload = true;
            }

            if (parameters.TryGetProperty("candle_market_data_type", out JsonElement candleMarketDataTypeElement)
                && candleMarketDataTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CandleMarketDataType>(candleMarketDataTypeElement.GetString(), true, out CandleMarketDataType candleMarketDataType))
            {
                screener.CandleMarketDataType = candleMarketDataType;
                needReload = true;
            }

            if (parameters.TryGetProperty("candle_create_method_type", out JsonElement candleCreateMethodTypeElement)
                && candleCreateMethodTypeElement.ValueKind == JsonValueKind.String)
            {
                screener.CandleCreateMethodType = candleCreateMethodTypeElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("commission_type", out JsonElement commissionTypeElement)
                && commissionTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CommissionType>(commissionTypeElement.GetString(), true, out CommissionType commissionType))
            {
                screener.CommissionType = commissionType;
                needReload = true;
            }

            if (parameters.TryGetProperty("commission_value", out JsonElement commissionValueElement)
                && commissionValueElement.ValueKind == JsonValueKind.Number
                && commissionValueElement.TryGetDecimal(out decimal commissionValue))
            {
                screener.CommissionValue = commissionValue;
                needReload = true;
            }

            if (parameters.TryGetProperty("save_trades_in_candles", out JsonElement saveTradesElement)
                && (saveTradesElement.ValueKind == JsonValueKind.True || saveTradesElement.ValueKind == JsonValueKind.False))
            {
                screener.SaveTradesInCandles = saveTradesElement.GetBoolean();
                needReload = true;
            }

            if (parameters.TryGetProperty("events_is_on", out JsonElement eventsIsOnElement)
                && (eventsIsOnElement.ValueKind == JsonValueKind.True || eventsIsOnElement.ValueKind == JsonValueKind.False))
            {
                screener.EventsIsOn = eventsIsOnElement.GetBoolean();
                needReload = true;
            }

            if (parameters.TryGetProperty("securities_class", out JsonElement securitiesClassElement)
                && securitiesClassElement.ValueKind == JsonValueKind.String)
            {
                screener.SecuritiesClass = securitiesClassElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("time_frame", out JsonElement timeFrameElement)
                && timeFrameElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TimeFrame>(timeFrameElement.GetString(), true, out TimeFrame timeFrame))
            {
                screener.TimeFrame = timeFrame;

                if (screener.CandleSeriesRealization != null)
                {
                    for (int i = 0; i < screener.CandleSeriesRealization.Parameters.Count; i++)
                    {
                        ICandleSeriesParameter param = screener.CandleSeriesRealization.Parameters[i];
                        if (param.SysName == "TimeFrame" && param.Type == CandlesParameterType.StringCollection)
                        {
                            ((CandlesParameterString)param).ValueString = timeFrame.ToString();
                        }
                    }
                }

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

            screener.SaveSettings();

            if (needReload)
            {
                screener.NeedToReloadTabs = true;
            }
        }

        private BotTabScreener FindBotTabScreener(BotPanel bot, string tabName)
        {
            List<BotTabScreener> screeners = bot.TabsScreener;

            if (screeners == null || screeners.Count == 0)
            {
                throw new InvalidOperationException($"Bot '{bot.NameStrategyUniq}' has no Screener tabs");
            }

            for (int i = 0; i < screeners.Count; i++)
            {
                if (screeners[i].TabName == tabName)
                {
                    return screeners[i];
                }
            }

            throw new ArgumentException($"Screener tab '{tabName}' not found in bot '{bot.NameStrategyUniq}'");
        }

        #endregion

        #region Index tab configuration

        private object GetBotConfigTabIndex(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabIndex index = FindBotTabIndex(bot, tabName);

            ConnectorCandles first = null;
            if (index.Tabs != null && index.Tabs.Count > 0)
            {
                first = index.Tabs[0];
            }

            List<object> securities = new List<object>();
            if (index.Tabs != null)
            {
                for (int i = 0; i < index.Tabs.Count; i++)
                {
                    ConnectorCandles connector = index.Tabs[i];

                    if (string.IsNullOrEmpty(connector.SecurityName))
                    {
                        continue;
                    }

                    securities.Add(new
                    {
                        name = connector.SecurityName,
                        class_name = connector.SecurityClass,
                        is_on = true
                    });
                }
            }

            bool buildNonTradingCandles = false;
            if (first?.TimeFrameBuilder?.CandleSeriesRealization is Simple simpleSeries)
            {
                buildNonTradingCandles = simpleSeries.BuildNonTradingCandles.ValueBool;
            }

            return new
            {
                tab_name = index.TabName,
                server_type = first?.ServerType.ToString() ?? string.Empty,
                server_name = first?.ServerFullName ?? string.Empty,
                portfolio_name = first?.PortfolioName ?? string.Empty,
                emulator_is_on = first?.EmulatorIsOn ?? false,
                candle_market_data_type = first?.CandleMarketDataType.ToString() ?? string.Empty,
                candle_create_method_type = first?.CandleCreateMethodType ?? string.Empty,
                commission_type = first?.CommissionType.ToString() ?? string.Empty,
                commission_value = first?.CommissionValue ?? 0m,
                save_trades_in_candles = first?.SaveTradesInCandles ?? false,
                time_frame = first?.TimeFrame.ToString() ?? string.Empty,
                securities_class = first?.SecurityClass ?? string.Empty,
                events_is_on = index.EventsIsOn,
                candle_series_realization = first?.TimeFrameBuilder?.CandleSeriesRealization?.GetType().Name,
                build_non_trading_candles = buildNonTradingCandles,
                user_formula = index.UserFormula ?? string.Empty,
                calculation_depth = index.CalculationDepth,
                percent_normalization = index.PercentNormalization,
                auto_formula = GetIndexAutoFormulaConfig(index.AutoFormulaBuilder),
                securities = securities,
                tabs_count = index.Tabs?.Count ?? 0
            };
        }

        private object SetBotConfigTabIndex(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("bot_id", out JsonElement botIdElement))
            {
                throw new ArgumentException("bot_id is required");
            }

            BotPanel bot = FindBot(master, botIdElement);

            if (!parameters.TryGetProperty("tab_name", out JsonElement tabNameElement)
                || tabNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("tab_name is required");
            }

            string tabName = tabNameElement.GetString();
            BotTabIndex index = FindBotTabIndex(bot, tabName);

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                ApplyBotConfigTabIndex(index, bot, parameters);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => ApplyBotConfigTabIndex(index, bot, parameters));
            }

            return GetBotConfigTabIndex(parameters);
        }

        private void ApplyBotConfigTabIndex(BotTabIndex index, BotPanel bot, JsonElement parameters)
        {
            MassSourcesCreator creator = index.Creator;

            if (creator == null)
            {
                creator = new MassSourcesCreator(bot.StartProgram);
            }

            if (index.Tabs != null && index.Tabs.Count > 0)
            {
                ConnectorCandles first = index.Tabs[0];
                creator.ServerType = first.ServerType;
                creator.ServerName = first.ServerFullName;
                creator.TimeFrame = first.TimeFrame;
                creator.EmulatorIsOn = first.EmulatorIsOn;
                creator.SecuritiesClass = first.SecurityClass;
                creator.PortfolioName = first.PortfolioName;
                creator.SaveTradesInCandles = first.SaveTradesInCandles;
                creator.CandleCreateMethodType = first.CandleCreateMethodType;
                creator.CandleMarketDataType = first.CandleMarketDataType;
                creator.CommissionType = first.CommissionType;
                creator.CommissionValue = first.CommissionValue;
                creator.CandleSeriesRealization.SetSaveString(first.TimeFrameBuilder.CandleSeriesRealization.GetSaveString());
            }

            bool needReload = false;

            if (parameters.TryGetProperty("server_type", out JsonElement serverTypeElement)
                && serverTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<ServerType>(serverTypeElement.GetString(), true, out ServerType serverType))
            {
                creator.ServerType = serverType;
                needReload = true;
            }

            if (parameters.TryGetProperty("server_name", out JsonElement serverNameElement)
                && serverNameElement.ValueKind == JsonValueKind.String)
            {
                creator.ServerName = serverNameElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("portfolio_name", out JsonElement portfolioNameElement)
                && portfolioNameElement.ValueKind == JsonValueKind.String)
            {
                creator.PortfolioName = portfolioNameElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("emulator_is_on", out JsonElement emulatorIsOnElement)
                && (emulatorIsOnElement.ValueKind == JsonValueKind.True || emulatorIsOnElement.ValueKind == JsonValueKind.False))
            {
                creator.EmulatorIsOn = emulatorIsOnElement.GetBoolean();
                needReload = true;
            }

            if (parameters.TryGetProperty("candle_market_data_type", out JsonElement candleMarketDataTypeElement)
                && candleMarketDataTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CandleMarketDataType>(candleMarketDataTypeElement.GetString(), true, out CandleMarketDataType candleMarketDataType))
            {
                creator.CandleMarketDataType = candleMarketDataType;
                needReload = true;
            }

            if (parameters.TryGetProperty("candle_create_method_type", out JsonElement candleCreateMethodTypeElement)
                && candleCreateMethodTypeElement.ValueKind == JsonValueKind.String)
            {
                creator.CandleCreateMethodType = candleCreateMethodTypeElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("commission_type", out JsonElement commissionTypeElement)
                && commissionTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CommissionType>(commissionTypeElement.GetString(), true, out CommissionType commissionType))
            {
                creator.CommissionType = commissionType;
                needReload = true;
            }

            if (parameters.TryGetProperty("commission_value", out JsonElement commissionValueElement)
                && commissionValueElement.ValueKind == JsonValueKind.Number
                && commissionValueElement.TryGetDecimal(out decimal commissionValue))
            {
                creator.CommissionValue = commissionValue;
                needReload = true;
            }

            if (parameters.TryGetProperty("save_trades_in_candles", out JsonElement saveTradesElement)
                && (saveTradesElement.ValueKind == JsonValueKind.True || saveTradesElement.ValueKind == JsonValueKind.False))
            {
                creator.SaveTradesInCandles = saveTradesElement.GetBoolean();
                needReload = true;
            }

            if (parameters.TryGetProperty("securities_class", out JsonElement securitiesClassElement)
                && securitiesClassElement.ValueKind == JsonValueKind.String)
            {
                creator.SecuritiesClass = securitiesClassElement.GetString();
                needReload = true;
            }

            if (parameters.TryGetProperty("time_frame", out JsonElement timeFrameElement)
                && timeFrameElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<TimeFrame>(timeFrameElement.GetString(), true, out TimeFrame timeFrame))
            {
                creator.TimeFrame = timeFrame;
                needReload = true;
            }

            List<ActivatedSecurity> currentSecurities = new List<ActivatedSecurity>();
            if (index.Tabs != null)
            {
                for (int i = 0; i < index.Tabs.Count; i++)
                {
                    ConnectorCandles connector = index.Tabs[i];

                    if (string.IsNullOrEmpty(connector.SecurityName))
                    {
                        continue;
                    }

                    currentSecurities.Add(new ActivatedSecurity
                    {
                        SecurityName = connector.SecurityName,
                        SecurityClass = connector.SecurityClass,
                        IsOn = true
                    });
                }
            }

            List<ActivatedSecurity> newSecurities = currentSecurities;

            if (parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                && securitiesElement.ValueKind == JsonValueKind.Array)
            {
                newSecurities = new List<ActivatedSecurity>();

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

                needReload = true;
            }

            if (creator.CandleSeriesRealization != null)
            {
                for (int i = 0; i < creator.CandleSeriesRealization.Parameters.Count; i++)
                {
                    ICandleSeriesParameter param = creator.CandleSeriesRealization.Parameters[i];
                    if (param.SysName == "TimeFrame" && param.Type == CandlesParameterType.StringCollection)
                    {
                        ((CandlesParameterString)param).ValueString = creator.TimeFrame.ToString();
                    }
                }
            }

            index.Creator = creator;

            if (needReload)
            {
                index.SetNewSecuritiesList(newSecurities);
            }

            if (parameters.TryGetProperty("user_formula", out JsonElement userFormulaElement)
                && userFormulaElement.ValueKind == JsonValueKind.String)
            {
                index.UserFormula = userFormulaElement.GetString();
            }

            if (parameters.TryGetProperty("calculation_depth", out JsonElement calculationDepthElement)
                && calculationDepthElement.ValueKind == JsonValueKind.Number
                && calculationDepthElement.TryGetInt32(out int calculationDepth))
            {
                index.CalculationDepth = calculationDepth;

                if (IndexHasCandles(index))
                {
                    index.RebuildHard();
                }
            }

            if (parameters.TryGetProperty("percent_normalization", out JsonElement percentNormalizationElement)
                && (percentNormalizationElement.ValueKind == JsonValueKind.True || percentNormalizationElement.ValueKind == JsonValueKind.False))
            {
                index.PercentNormalization = percentNormalizationElement.GetBoolean();

                if (IndexHasCandles(index))
                {
                    index.RebuildHard();
                }
            }

            if (parameters.TryGetProperty("events_is_on", out JsonElement eventsIsOnElement)
                && (eventsIsOnElement.ValueKind == JsonValueKind.True || eventsIsOnElement.ValueKind == JsonValueKind.False))
            {
                index.EventsIsOn = eventsIsOnElement.GetBoolean();
            }

            if (parameters.TryGetProperty("auto_formula", out JsonElement autoFormulaElement)
                && autoFormulaElement.ValueKind == JsonValueKind.Object)
            {
                ApplyIndexAutoFormulaConfig(index.AutoFormulaBuilder, autoFormulaElement);
            }
        }

        private object GetIndexAutoFormulaConfig(IndexFormulaBuilder builder)
        {
            if (builder == null)
            {
                return new
                {
                    regime = "Off",
                    day_of_week = "Monday",
                    hour = 10,
                    sec_count = 5,
                    days_look_back = 20,
                    sort_type = "FirstInArray",
                    mult_type = "PriceWeighted",
                    write_log_on_rebuild = true
                };
            }

            return new
            {
                regime = builder.Regime.ToString(),
                day_of_week = builder.DayOfWeekToRebuildIndex.ToString(),
                hour = builder.HourInDayToRebuildIndex,
                sec_count = builder.IndexSecCount,
                days_look_back = builder.DaysLookBackInBuilding,
                sort_type = builder.IndexSortType.ToString(),
                mult_type = builder.IndexMultType.ToString(),
                write_log_on_rebuild = builder.WriteLogMessageOnRebuild
            };
        }

        private void ApplyIndexAutoFormulaConfig(IndexFormulaBuilder builder, JsonElement config)
        {
            if (builder == null || config.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (config.TryGetProperty("regime", out JsonElement regimeElement)
                && regimeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<IndexAutoFormulaBuilderRegime>(regimeElement.GetString(), true, out IndexAutoFormulaBuilderRegime regime))
            {
                builder.Regime = regime;
            }

            if (config.TryGetProperty("day_of_week", out JsonElement dayOfWeekElement)
                && dayOfWeekElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<DayOfWeek>(dayOfWeekElement.GetString(), true, out DayOfWeek dayOfWeek))
            {
                builder.DayOfWeekToRebuildIndex = dayOfWeek;
            }

            if (config.TryGetProperty("hour", out JsonElement hourElement)
                && hourElement.ValueKind == JsonValueKind.Number
                && hourElement.TryGetInt32(out int hour))
            {
                builder.HourInDayToRebuildIndex = hour;
            }

            if (config.TryGetProperty("sec_count", out JsonElement secCountElement)
                && secCountElement.ValueKind == JsonValueKind.Number
                && secCountElement.TryGetInt32(out int secCount))
            {
                builder.IndexSecCount = secCount;
            }

            if (config.TryGetProperty("days_look_back", out JsonElement daysLookBackElement)
                && daysLookBackElement.ValueKind == JsonValueKind.Number
                && daysLookBackElement.TryGetInt32(out int daysLookBack))
            {
                builder.DaysLookBackInBuilding = daysLookBack;
            }

            if (config.TryGetProperty("sort_type", out JsonElement sortTypeElement)
                && sortTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<SecuritySortType>(sortTypeElement.GetString(), true, out SecuritySortType sortType))
            {
                builder.IndexSortType = sortType;
            }

            if (config.TryGetProperty("mult_type", out JsonElement multTypeElement)
                && multTypeElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<IndexMultType>(multTypeElement.GetString(), true, out IndexMultType multType))
            {
                builder.IndexMultType = multType;
            }

            if (config.TryGetProperty("write_log_on_rebuild", out JsonElement writeLogElement)
                && (writeLogElement.ValueKind == JsonValueKind.True || writeLogElement.ValueKind == JsonValueKind.False))
            {
                builder.WriteLogMessageOnRebuild = writeLogElement.GetBoolean();
            }
        }

        private bool IndexHasCandles(BotTabIndex index)
        {
            if (index == null ||
                index.Tabs == null ||
                index.Tabs.Count <= 1)
            {
                return false;
            }

            for (int i = 0; i < index.Tabs.Count; i++)
            {
                if (index.Tabs[i] == null)
                {
                    return false;
                }

                List<Candle> candles = index.Tabs[i].Candles(true);

                if (candles == null || candles.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private BotTabIndex FindBotTabIndex(BotPanel bot, string tabName)
        {
            List<BotTabIndex> indices = bot.TabsIndex;

            if (indices == null || indices.Count == 0)
            {
                throw new InvalidOperationException($"Bot '{bot.NameStrategyUniq}' has no Index tabs");
            }

            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i].TabName == tabName)
                {
                    return indices[i];
                }
            }

            throw new ArgumentException($"Index tab '{tabName}' not found in bot '{bot.NameStrategyUniq}'");
        }

        #endregion

        #region Journal settings

        private object GetJournalSettings(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            string botName = GetOptionalBotName(parameters);

            if (botName != null && !BotWithNameExists(master, botName))
            {
                throw new ArgumentException($"Robot '{botName}' not found");
            }

            List<JournalBotSetting> settings = LoadJournalSettings(master, botName);

            return new { robots = settings, count = settings.Count };
        }

        private object SetJournalSettings(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            string botName = GetOptionalBotName(parameters);

            if (botName != null && !BotWithNameExists(master, botName))
            {
                throw new ArgumentException($"Robot '{botName}' not found");
            }

            if (!parameters.TryGetProperty("settings", out JsonElement settingsElement)
                || settingsElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("settings array is required");
            }

            List<JournalBotSetting> current = LoadJournalSettings(master, null);
            List<string> updated = new List<string>();

            foreach (JsonElement item in settingsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty("bot_name", out JsonElement nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string name = nameElement.GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (botName != null && name != botName)
                {
                    continue;
                }

                if (!BotWithNameExists(master, name))
                {
                    continue;
                }

                JournalBotSetting setting = current.Find(s => s.BotName == name);

                if (setting == null)
                {
                    setting = new JournalBotSetting
                    {
                        BotName = name,
                        Group = string.Empty,
                        Mult = 100m,
                        IsOn = true
                    };
                    current.Add(setting);
                }

                if (item.TryGetProperty("group", out JsonElement groupElement)
                    && groupElement.ValueKind == JsonValueKind.String)
                {
                    setting.Group = groupElement.GetString() ?? string.Empty;
                }

                if (item.TryGetProperty("mult", out JsonElement multElement)
                    && multElement.ValueKind == JsonValueKind.Number
                    && multElement.TryGetDecimal(out decimal mult))
                {
                    setting.Mult = mult;
                }

                if (item.TryGetProperty("is_on", out JsonElement isOnElement)
                    && (isOnElement.ValueKind == JsonValueKind.True || isOnElement.ValueKind == JsonValueKind.False))
                {
                    setting.IsOn = isOnElement.GetBoolean();
                }

                updated.Add(name);
            }

            SaveJournalSettings(master._startProgram, current);

            return new { updated = updated, updated_count = updated.Count };
        }

        private string GetOptionalBotName(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameters.TryGetProperty("bot_name", out JsonElement botNameElement)
                && botNameElement.ValueKind == JsonValueKind.String)
            {
                string name = botNameElement.GetString() ?? string.Empty;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }

            return null;
        }

        private bool BotWithNameExists(OsTraderMaster master, string name)
        {
            if (master.PanelsArray == null)
            {
                return false;
            }

            for (int i = 0; i < master.PanelsArray.Count; i++)
            {
                if (master.PanelsArray[i].NameStrategyUniq == name)
                {
                    return true;
                }
            }

            return false;
        }

        private List<JournalBotSetting> LoadJournalSettings(OsTraderMaster master, string targetBotName)
        {
            string path = GetJournalSettingsPath(master._startProgram);
            List<JournalBotSetting> settings = new List<JournalBotSetting>();
            HashSet<string> processed = new HashSet<string>();

            if (master.PanelsArray != null)
            {
                for (int i = 0; i < master.PanelsArray.Count; i++)
                {
                    string name = master.PanelsArray[i].NameStrategyUniq;

                    if (targetBotName != null && name != targetBotName)
                    {
                        continue;
                    }

                    settings.Add(new JournalBotSetting
                    {
                        BotName = name,
                        Group = string.Empty,
                        Mult = 100m,
                        IsOn = true
                    });

                    processed.Add(name);
                }
            }

            if (File.Exists(path))
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        string[] parts = line.Split('&');

                        if (parts.Length < 4)
                        {
                            continue;
                        }

                        string botName = parts[0];

                        if (targetBotName != null && botName != targetBotName)
                        {
                            continue;
                        }

                        if (!processed.Contains(botName))
                        {
                            continue;
                        }

                        JournalBotSetting setting = settings.Find(s => s.BotName == botName);

                        if (setting == null)
                        {
                            continue;
                        }

                        setting.Group = parts[1];

                        if (decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal mult))
                        {
                            setting.Mult = mult;
                        }

                        if (bool.TryParse(parts[3], out bool isOn))
                        {
                            setting.IsOn = isOn;
                        }
                    }
                }
            }

            return settings;
        }

        private void SaveJournalSettings(StartProgram startProgram, List<JournalBotSetting> settings)
        {
            string path = GetJournalSettingsPath(startProgram);
            string directory = Path.GetDirectoryName(path) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    JournalBotSetting setting = settings[i];
                    writer.WriteLine($"{setting.BotName}&{setting.Group}&{setting.Mult.ToString(CultureInfo.InvariantCulture)}&{setting.IsOn}");
                }
            }
        }

        private string GetJournalSettingsPath(StartProgram startProgram)
        {
            return Path.Combine("Engine", $"{startProgram}JournalSettings.txt");
        }

        private class JournalBotSetting
        {
            [System.Text.Json.Serialization.JsonPropertyName("bot_name")]
            public string BotName { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("group")]
            public string Group { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("mult")]
            public decimal Mult { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("is_on")]
            public bool IsOn { get; set; }
        }

        #endregion

        #region Journal data

        private object GetJournalSummary(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            List<Position> positions = GetPositions(master, botName, PositionSet.ClosedAndOpen);
            List<Position> deals = positions.FindAll(p => p.State != PositionStateType.OpeningFail);

            decimal totalProfitAbs = 0;
            decimal totalProfitPercent = 0;
            DateTime periodStart = DateTime.MinValue;
            DateTime periodEnd = DateTime.MinValue;

            if (deals.Count > 0)
            {
                Position[] dealsArray = deals.ToArray();
                totalProfitAbs = PositionStatisticGenerator.GetAllProfitInAbsolute(dealsArray, false);
                totalProfitPercent = PositionStatisticGenerator.GetAllProfitPercent(dealsArray, false);

                periodStart = deals.Min(p => p.TimeOpen);
                periodEnd = deals.Max(p => p.TimeClose);
            }

            return new
            {
                total_profit_abs = totalProfitAbs,
                total_profit_percent = totalProfitPercent,
                period_start = periodStart == DateTime.MinValue ? null : periodStart.ToString("O"),
                period_end = periodEnd == DateTime.MinValue ? null : periodEnd.ToString("O")
            };
        }

        private object GetJournalEquity(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            string chartType = GetOptionalString(parameters, "chart_type") ?? "DepositPercent";

            List<Position> positions = GetPositions(master, botName, PositionSet.ClosedAndOpen);
            List<Position> deals = positions.FindAll(p => p.State != PositionStateType.OpeningFail);
            deals = deals.OrderBy(p => p.TimeOpen).ToList();

            List<object> points = new List<object>();
            decimal cumulative = 0;

            for (int i = 0; i < deals.Count; i++)
            {
                Position pos = deals[i];
                decimal value = GetPositionProfitForChartType(pos, chartType);
                cumulative += value * (pos.MultToJournal / 100);

                DateTime time = pos.State == PositionStateType.Done ? pos.TimeClose : pos.TimeOpen;
                if (time == DateTime.MinValue)
                {
                    time = pos.TimeOpen;
                }

                points.Add(new { time = time.ToString("O"), value = cumulative });
            }

            return new { points = points, count = points.Count };
        }

        private object GetJournalStatistics(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            string side = GetOptionalString(parameters, "side") ?? "All";

            List<Position> positions = GetPositions(master, botName, PositionSet.ClosedAndOpen);
            List<Position> deals = positions.FindAll(p => p.State != PositionStateType.OpeningFail);
            deals = FilterBySide(deals, side);

            if (deals.Count == 0)
            {
                return new
                {
                    net_profit = 0m,
                    net_profit_percent = 0m,
                    deals_count = 0,
                    average_holding_time = "",
                    sharpe = 0m,
                    profit_factor = 0m,
                    recovery = 0m,
                    profitable_deals = 0,
                    losing_deals = 0,
                    max_drawdown_percent = 0m,
                    commission = 0m
                };
            }

            Position[] dealsArray = deals.ToArray();

            return new
            {
                net_profit = PositionStatisticGenerator.GetAllProfitInAbsolute(dealsArray, false),
                net_profit_percent = PositionStatisticGenerator.GetAllProfitPercent(dealsArray, false),
                deals_count = PositionStatisticGenerator.GetAllDealsCount(dealsArray),
                average_holding_time = PositionStatisticGenerator.GetAverageTimeOnPoses(dealsArray),
                sharpe = PositionStatisticGenerator.GetSharpRatio(dealsArray, 7),
                profit_factor = PositionStatisticGenerator.GetProfitFactor(dealsArray),
                recovery = PositionStatisticGenerator.GetRecovery(dealsArray),
                profitable_deals = PositionStatisticGenerator.GetProfitDeal(dealsArray),
                losing_deals = deals.Count - PositionStatisticGenerator.GetProfitDeal(dealsArray),
                max_drawdown_percent = PositionStatisticGenerator.GetMaxDownPercent(dealsArray),
                commission = PositionStatisticGenerator.GetCommissionAmount(dealsArray)
            };
        }

        private object GetJournalDrawdown(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            List<Position> positions = GetPositions(master, botName, PositionSet.ClosedAndOpen);
            List<Position> deals = positions.FindAll(p => p.State != PositionStateType.OpeningFail);
            deals = deals.OrderBy(p => p.TimeOpen).ToList();

            List<object> points = new List<object>();
            decimal cumulative = 0;
            decimal maxEquity = 0;

            for (int i = 0; i < deals.Count; i++)
            {
                Position pos = deals[i];
                cumulative += pos.ProfitPortfolioAbs * (pos.MultToJournal / 100);

                if (cumulative > maxEquity)
                {
                    maxEquity = cumulative;
                }

                decimal absolute = cumulative - maxEquity;
                decimal percent = maxEquity != 0 ? Math.Round((absolute / maxEquity) * 100, 6) : 0;

                DateTime time = pos.State == PositionStateType.Done ? pos.TimeClose : pos.TimeOpen;
                if (time == DateTime.MinValue)
                {
                    time = pos.TimeOpen;
                }

                points.Add(new
                {
                    time = time.ToString("O"),
                    absolute = absolute,
                    percent = percent
                });
            }

            return new { points = points, count = points.Count };
        }

        private object GetJournalVolume(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            List<Position> positions = GetPositions(master, botName, PositionSet.ClosedAndOpen);
            List<Position> deals = positions.FindAll(p => p.State != PositionStateType.OpeningFail);

            List<object> points = new List<object>();

            for (int i = 0; i < deals.Count; i++)
            {
                Position pos = deals[i];
                decimal volume = pos.MaxVolume;

                if (volume == 0)
                {
                    volume = pos.OpenVolume;
                }

                if (volume == 0)
                {
                    continue;
                }

                decimal volumeInMoney = volume * pos.EntryPrice;
                decimal leverage = pos.PortfolioValueOnOpenPosition != 0
                    ? Math.Round(volumeInMoney / pos.PortfolioValueOnOpenPosition, 2)
                    : 0;

                points.Add(new
                {
                    security_name = pos.SecurityName ?? string.Empty,
                    time = pos.TimeCreate.ToString("O"),
                    volume = volume,
                    leverage = leverage
                });
            }

            return new { points = points, count = points.Count };
        }

        private object GetJournalOpenPositions(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            int limit = GetOptionalInt(parameters, "limit") ?? int.MaxValue;
            int offset = GetOptionalInt(parameters, "offset") ?? 0;

            List<Position> positions = GetPositions(master, botName, PositionSet.Open);
            positions = positions.OrderByDescending(p => p.TimeOpen).ToList();
            positions = ApplyPagination(positions, limit, offset);

            return new
            {
                positions = positions.Select(p => PositionToDto(p)).ToList(),
                count = positions.Count
            };
        }

        private object GetJournalClosedPositions(JsonElement parameters)
        {
            OsTraderMaster master = GetMasterRequired();
            string botName = GetOptionalBotName(parameters);
            ValidateBotNameIfSpecified(master, botName);

            bool includeFailed = GetOptionalBool(parameters, "include_failed") ?? false;
            int limit = GetOptionalInt(parameters, "limit") ?? int.MaxValue;
            int offset = GetOptionalInt(parameters, "offset") ?? 0;

            List<Position> positions = GetPositions(master, botName, PositionSet.Closed);

            if (!includeFailed)
            {
                positions = positions.FindAll(p => p.State == PositionStateType.Done);
            }

            positions = positions.OrderByDescending(p => p.TimeClose).ToList();
            positions = ApplyPagination(positions, limit, offset);

            return new
            {
                positions = positions.Select(p => PositionToDto(p)).ToList(),
                count = positions.Count
            };
        }

        #endregion

        #region Journal helpers

        private enum PositionSet
        {
            ClosedAndOpen,
            Open,
            Closed
        }

        private List<Position> GetPositions(OsTraderMaster master, string botName, PositionSet set)
        {
            List<Position> result = new List<Position>();

            if (master.PanelsArray == null)
            {
                return result;
            }

            for (int i = 0; i < master.PanelsArray.Count; i++)
            {
                BotPanel bot = master.PanelsArray[i];

                if (botName != null && bot.NameStrategyUniq != botName)
                {
                    continue;
                }

                List<JournalClass> journals = bot.GetJournals();

                if (journals == null)
                {
                    continue;
                }

                for (int j = 0; j < journals.Count; j++)
                {
                    JournalClass journal = journals[j];

                    if (journal == null)
                    {
                        continue;
                    }

                    switch (set)
                    {
                        case PositionSet.Open:
                            result.AddRange(journal.OpenPositions ?? new List<Position>());
                            break;
                        case PositionSet.Closed:
                            result.AddRange(journal.CloseAllPositions ?? new List<Position>());
                            break;
                        default:
                            result.AddRange(journal.AllPosition ?? new List<Position>());
                            break;
                    }
                }
            }

            return result;
        }

        private List<Position> FilterBySide(List<Position> positions, string side)
        {
            if (string.IsNullOrWhiteSpace(side)
                || side.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return positions;
            }

            if (side.Equals("Long", StringComparison.OrdinalIgnoreCase))
            {
                return positions.FindAll(p => p.Direction == Side.Buy);
            }

            if (side.Equals("Short", StringComparison.OrdinalIgnoreCase))
            {
                return positions.FindAll(p => p.Direction == Side.Sell);
            }

            throw new ArgumentException($"Unknown side '{side}'. Use All, Long or Short.");
        }

        private decimal GetPositionProfitForChartType(Position pos, string chartType)
        {
            if (chartType.Equals("Percent1Contract", StringComparison.OrdinalIgnoreCase))
            {
                return pos.ProfitOperationPercent;
            }

            if (chartType.Equals("DepositPercent", StringComparison.OrdinalIgnoreCase))
            {
                return pos.ProfitPortfolioPercent;
            }

            return pos.ProfitPortfolioAbs;
        }

        private List<Position> ApplyPagination(List<Position> positions, int limit, int offset)
        {
            if (offset < 0)
            {
                offset = 0;
            }

            if (offset >= positions.Count)
            {
                return new List<Position>();
            }

            if (limit < 0)
            {
                limit = int.MaxValue;
            }

            int take = Math.Min(limit, positions.Count - offset);
            return positions.GetRange(offset, take);
        }

        private object PositionToDto(Position pos)
        {
            string direction = pos.Direction == Side.Buy ? "Long" : (pos.Direction == Side.Sell ? "Short" : "None");

            return new
            {
                number = pos.Number,
                bot_name = pos.NameBot ?? string.Empty,
                security_name = pos.SecurityName ?? string.Empty,
                direction = direction,
                state = pos.State.ToString(),
                open_time = pos.TimeOpen == DateTime.MinValue ? null : pos.TimeOpen.ToString("O"),
                close_time = pos.TimeClose == DateTime.MinValue ? null : pos.TimeClose.ToString("O"),
                entry_price = pos.EntryPrice,
                close_price = pos.ClosePrice,
                volume = pos.MaxVolume,
                open_volume = pos.OpenVolume,
                profit_abs = pos.ProfitPortfolioAbs,
                profit_percent = pos.ProfitPortfolioPercent,
                commission = pos.CommissionTotal()
            };
        }

        private void ValidateBotNameIfSpecified(OsTraderMaster master, string botName)
        {
            if (botName != null && !BotWithNameExists(master, botName))
            {
                throw new ArgumentException($"Robot '{botName}' not found");
            }
        }

        private string GetOptionalString(JsonElement parameters, string propertyName)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameters.TryGetProperty(propertyName, out JsonElement element)
                && element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString() ?? string.Empty;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            return null;
        }

        private int? GetOptionalInt(JsonElement parameters, string propertyName)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameters.TryGetProperty(propertyName, out JsonElement element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out int value))
            {
                return value;
            }

            return null;
        }

        private bool? GetOptionalBool(JsonElement parameters, string propertyName)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameters.TryGetProperty(propertyName, out JsonElement element)
                && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            {
                return element.GetBoolean();
            }

            return null;
        }

        #endregion

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }
    }
}
