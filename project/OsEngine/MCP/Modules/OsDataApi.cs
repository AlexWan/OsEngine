/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.MCP.Json;
using OsEngine.OsData;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API module for OsData market data management.
    /// Publishes load-completed events via SSE and will expose data_* tools.
    /// </summary>
    public class OsDataApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;
        private readonly Func<OsDataMaster> _getOsDataMaster;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public OsDataApi(Action<string, object> publishEvent, Func<OsDataMaster> getOsDataMaster)
        {
            _publishEvent = publishEvent;
            _getOsDataMaster = getOsDataMaster;
        }

        #endregion

        #region Public methods

        public McpJsonRpcResponse Handle(McpJsonRpcRequest request)
        {
            var response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "data_get_sets":
                        response.Result = GetSets();
                        break;

                    case "data_create_set":
                        response.Result = CreateSet(request.Params);
                        break;

                    case "data_delete_set":
                        response.Result = DeleteSet(request.Params);
                        break;

                    case "data_set_settings_get":
                        response.Result = GetSetSettings(request.Params);
                        break;

                    case "data_set_settings_set":
                        response.Result = SetSetSettings(request.Params);
                        break;

                    case "data_set_securities_get":
                        response.Result = GetSetSecurities(request.Params);
                        break;

                    case "data_set_securities_add":
                        response.Result = AddSetSecurities(request.Params);
                        break;

                    case "data_set_securities_remove":
                        response.Result = RemoveSetSecurities(request.Params);
                        break;

                    case "data_set_on":
                        response.Result = SetSetRegime(request.Params, DataSetState.On);
                        break;

                    case "data_set_off":
                        response.Result = SetSetRegime(request.Params, DataSetState.Off);
                        break;

                    case "data_get_set_status":
                        response.Result = GetSetStatus(request.Params);
                        break;

                    case "data_get_security_status":
                        response.Result = GetSecurityStatus(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' is not implemented in OsData module"
                        };
                        break;
                }
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                response.Error = new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"OsData method '{request.Method}' failed: {ex.Message}"
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
                    Name = "data_get_sets",
                    Description = "Get list of existing OsData sets",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "data_create_set",
                    Description = "Create a new OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name. 'Set_' prefix is added automatically if missing" },
                            source = new { type = "string", description = "Server type name, e.g. Finam, MoexDataServer, Binance" },
                            source_name = new { type = "string", description = "Active server instance name prefix" },
                            timeframes = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Timeframes to load, e.g. Min5, Hour1, Day"
                            },
                            date_from = new { type = "string", description = "Load start date in ISO 8601 format" },
                            date_to = new { type = "string", description = "Load end date in ISO 8601 format" }
                        },
                        required = new[] { "name", "source", "source_name", "timeframes", "date_from", "date_to" }
                    }
                },
                new McpTool
                {
                    Name = "data_delete_set",
                    Description = "Delete an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name to delete" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_settings_get",
                    Description = "Get settings of an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_settings_set",
                    Description = "Update settings of an OsData set (partial update)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" },
                            settings = new
                            {
                                type = "object",
                                properties = new
                                {
                                    regime = new { type = "string", description = "On or Off" },
                                    timeframes = new
                                    {
                                        type = "array",
                                        items = new { type = "string" },
                                        description = "Timeframes to load, e.g. Min5, Hour1, Day"
                                    },
                                    date_from = new { type = "string", description = "Load start date in ISO 8601 format" },
                                    date_to = new { type = "string", description = "Load end date in ISO 8601 format" },
                                    market_depth_depth = new { type = "integer", description = "Market depth depth" }
                                }
                            }
                        },
                        required = new[] { "name", "settings" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_securities_get",
                    Description = "Get securities of an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_securities_add",
                    Description = "Add securities to an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" },
                            securities = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string", description = "Security ticker" },
                                        id = new { type = "string", description = "Security ID (optional, uses name if not set)" },
                                        @class = new { type = "string", description = "Security class (optional)" },
                                        exchange = new { type = "string", description = "Exchange (optional)" }
                                    },
                                    required = new[] { "name" }
                                }
                            }
                        },
                        required = new[] { "name", "securities" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_securities_remove",
                    Description = "Remove securities from an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" },
                            securities = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "List of security names to remove"
                            }
                        },
                        required = new[] { "name", "securities" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_on",
                    Description = "Turn an OsData set on (start loading)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_set_off",
                    Description = "Turn an OsData set off (stop loading)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_get_set_status",
                    Description = "Get aggregated status and percent load of an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" }
                        },
                        required = new[] { "name" }
                    }
                },
                new McpTool
                {
                    Name = "data_get_security_status",
                    Description = "Get status of a specific security/timeframe in an OsData set",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Set name" },
                            security = new { type = "string", description = "Security name" },
                            timeframe = new { type = "string", description = "Timeframe, e.g. Min1" }
                        },
                        required = new[] { "name", "security", "timeframe" }
                    }
                }
            };
        }

        #region Private methods

        private object GetSets()
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            var result = new List<object>();

            try
            {
                if (master.Sets != null)
                {
                    for (int i = 0; i < master.Sets.Count; i++)
                    {
                        OsDataSet set = master.Sets[i];

                        if (set == null ||
                            string.IsNullOrEmpty(set.SetName))
                        {
                            continue;
                        }

                        List<string> securities = new List<string>();

                        if (set.SecuritiesLoad != null)
                        {
                            for (int j = 0; j < set.SecuritiesLoad.Count; j++)
                            {
                                SecurityToLoad security = set.SecuritiesLoad[j];

                                if (security != null &&
                                    !string.IsNullOrEmpty(security.SecName))
                                {
                                    securities.Add(security.SecName);
                                }
                            }
                        }

                        result.Add(new
                        {
                            name = set.SetName,
                            regime = set.BaseSettings.Regime.ToString(),
                            source = set.BaseSettings.Source.ToString(),
                            source_name = set.BaseSettings.SourceName,
                            percent_load = set.PercentLoad(),
                            securities_count = securities.Count,
                            securities = securities
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to get OsData sets: {ex.Message}"
                };
            }

            return result;
        }

        private object CreateSet(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                string sourceName = ParseRequiredString(parameters, "source");
                string serverInstanceName = ParseRequiredString(parameters, "source_name");
                List<TimeFrame> timeframes = ParseTimeFrameList(parameters, "timeframes");
                DateTime dateFrom = ParseDateTime(parameters, "date_from");
                DateTime dateTo = ParseDateTime(parameters, "date_to");

                if (!Enum.TryParse<ServerType>(sourceName, true, out ServerType source))
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Unknown server type '{sourceName}'"
                    };
                }

                OsDataSet set = master.CreateSet(name, source, serverInstanceName, timeframes, dateFrom, dateTo);

                return new
                {
                    name = set.SetName,
                    regime = set.BaseSettings.Regime.ToString(),
                    source = set.BaseSettings.Source.ToString(),
                    source_name = set.BaseSettings.SourceName,
                    timeframes = timeframes.ConvertAll(tf => tf.ToString()),
                    date_from = set.BaseSettings.TimeStart,
                    date_to = set.BaseSettings.TimeEnd
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to create OsData set: {ex.Message}"
                };
            }
        }

        private object DeleteSet(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                bool deleted = master.DeleteSet(name);

                if (!deleted)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                string setName = name.StartsWith("Set_", StringComparison.InvariantCultureIgnoreCase)
                    ? name
                    : "Set_" + name;

                return new
                {
                    name = setName,
                    deleted = true
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to delete OsData set: {ex.Message}"
                };
            }
        }

        private object GetSetSettings(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                List<TimeFrame> timeframes = OsDataMaster.GetActiveTimeFrames(set.BaseSettings);

                return new
                {
                    name = set.SetName,
                    regime = set.BaseSettings.Regime.ToString(),
                    source = set.BaseSettings.Source.ToString(),
                    source_name = set.BaseSettings.SourceName,
                    timeframes = timeframes.ConvertAll(tf => tf.ToString()),
                    date_from = set.BaseSettings.TimeStart,
                    date_to = set.BaseSettings.TimeEnd,
                    market_depth_depth = set.BaseSettings.MarketDepthDepth
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to get OsData set settings: {ex.Message}"
                };
            }
        }

        private object SetSetSettings(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");

                if (parameters.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("settings", out JsonElement settingsElement)
                    || settingsElement.ValueKind != JsonValueKind.Object)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = "Parameter 'settings' is required and must be an object"
                    };
                }

                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                IServerPermission permission = ServerMaster.GetServerPermission(set.BaseSettings.Source);

                bool updated = master.UpdateSetSettings(name, settings =>
                {
                    if (settingsElement.TryGetProperty("regime", out JsonElement regimeElement)
                        && regimeElement.ValueKind == JsonValueKind.String)
                    {
                        string regimeStr = regimeElement.GetString();

                        if (!Enum.TryParse<DataSetState>(regimeStr, true, out DataSetState regime))
                        {
                            throw new ArgumentException($"Unknown regime '{regimeStr}'");
                        }

                        settings.Regime = regime;
                    }

                    if (settingsElement.TryGetProperty("timeframes", out JsonElement timeframesElement))
                    {
                        List<TimeFrame> timeframes = ParseTimeFrameList(timeframesElement);

                        for (int i = 0; i < timeframes.Count; i++)
                        {
                            if (permission != null &&
                                !OsDataMaster.IsTimeFrameSupportedByServer(timeframes[i], permission))
                            {
                                throw new ArgumentException($"Timeframe '{timeframes[i]}' is not supported by server '{settings.Source}'");
                            }
                        }

                        ResetTimeFrameFlags(settings);

                        for (int i = 0; i < timeframes.Count; i++)
                        {
                            OsDataMaster.SetTimeFrameFlag(settings, timeframes[i], true);
                        }
                    }

                    if (settingsElement.TryGetProperty("date_from", out JsonElement dateFromElement)
                        && dateFromElement.ValueKind == JsonValueKind.String)
                    {
                        settings.TimeStart = ParseDateTime(dateFromElement);
                    }

                    if (settingsElement.TryGetProperty("date_to", out JsonElement dateToElement)
                        && dateToElement.ValueKind == JsonValueKind.String)
                    {
                        settings.TimeEnd = ParseDateTime(dateToElement);
                    }

                    if (settingsElement.TryGetProperty("market_depth_depth", out JsonElement depthElement)
                        && depthElement.ValueKind == JsonValueKind.Number)
                    {
                        settings.MarketDepthDepth = depthElement.GetInt32();
                    }

                    if (settings.TimeStart > settings.TimeEnd)
                    {
                        throw new ArgumentException("Date from must be less than or equal to date to");
                    }
                });

                if (!updated)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                return new
                {
                    name = set.SetName,
                    updated = true
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to update OsData set settings: {ex.Message}"
                };
            }
        }

        private object GetSetSecurities(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                var result = new List<object>();

                if (set.SecuritiesLoad != null)
                {
                    for (int i = 0; i < set.SecuritiesLoad.Count; i++)
                    {
                        SecurityToLoad security = set.SecuritiesLoad[i];

                        if (security == null)
                        {
                            continue;
                        }

                        result.Add(new
                        {
                            name = security.SecName,
                            id = security.SecId,
                            @class = security.SecClass,
                            exchange = security.SecExchange,
                            price_step = security.PriceStep,
                            volume_step = security.VolumeStep
                        });
                    }
                }

                return result;
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to get set securities: {ex.Message}"
                };
            }
        }

        private object AddSetSecurities(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                List<SecurityInput> inputs = ParseSecurityInputList(parameters);

                if (inputs.Count == 0)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = "Parameter 'securities' must contain at least one security"
                    };
                }

                IServer server = set.GetSourceServer();

                if (server == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32603,
                        Message = $"Source server for set '{set.SetName}' not found"
                    };
                }

                if (server.Securities == null || server.Securities.Count == 0)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32603,
                        Message = $"Source server '{server.ServerType}' has no available securities"
                    };
                }

                var securitiesToAdd = new List<Security>();

                for (int i = 0; i < inputs.Count; i++)
                {
                    SecurityInput input = inputs[i];
                    Security match = FindSecurityMatch(server.Securities, input);

                    if (match == null)
                    {
                        return new McpJsonRpcError
                        {
                            Code = -32602,
                            Message = $"Security '{input.Name}' not found on server '{server.ServerType}'"
                        };
                    }

                    securitiesToAdd.Add(match);
                }

                int addedCount = set.AddSecurities(securitiesToAdd);

                return new
                {
                    name = set.SetName,
                    added_count = addedCount
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to add set securities: {ex.Message}"
                };
            }
        }

        private object RemoveSetSecurities(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                if (parameters.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                    || securitiesElement.ValueKind != JsonValueKind.Array)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = "Parameter 'securities' is required and must be an array of strings"
                    };
                }

                var names = new List<string>();

                foreach (JsonElement item in securitiesElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        throw new ArgumentException("Securities to remove must contain only strings");
                    }

                    names.Add(item.GetString());
                }

                int removedCount = set.RemoveSecurities(names);

                return new
                {
                    name = set.SetName,
                    removed_count = removedCount
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to remove set securities: {ex.Message}"
                };
            }
        }

        private List<SecurityInput> ParseSecurityInputList(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                || securitiesElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Parameter 'securities' is required and must be an array of objects");
            }

            var result = new List<SecurityInput>();

            foreach (JsonElement item in securitiesElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Securities must contain only objects");
                }

                if (!item.TryGetProperty("name", out JsonElement nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Each security must have a 'name' string");
                }

                var input = new SecurityInput
                {
                    Name = nameElement.GetString()
                };

                if (item.TryGetProperty("id", out JsonElement idElement)
                    && idElement.ValueKind == JsonValueKind.String)
                {
                    input.Id = idElement.GetString();
                }

                if (item.TryGetProperty("class", out JsonElement classElement)
                    && classElement.ValueKind == JsonValueKind.String)
                {
                    input.Class = classElement.GetString();
                }

                if (item.TryGetProperty("exchange", out JsonElement exchangeElement)
                    && exchangeElement.ValueKind == JsonValueKind.String)
                {
                    input.Exchange = exchangeElement.GetString();
                }

                result.Add(input);
            }

            return result;
        }

        private Security FindSecurityMatch(List<Security> securities, SecurityInput input)
        {
            for (int i = 0; i < securities.Count; i++)
            {
                Security security = securities[i];

                if (security == null)
                {
                    continue;
                }

                if (!string.Equals(security.Name, input.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(input.Class)
                    && !string.Equals(security.NameClass, input.Class, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(input.Exchange)
                    && !string.Equals(security.Exchange, input.Exchange, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                return security;
            }

            return null;
        }

        private string ParseRequiredString(JsonElement parameters, string propertyName)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty(propertyName, out JsonElement element)
                || element.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"Parameter '{propertyName}' is required and must be a string");
            }

            string value = element.GetString();

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Parameter '{propertyName}' cannot be empty");
            }

            return value;
        }

        private List<TimeFrame> ParseTimeFrameList(JsonElement parameters, string propertyName)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty(propertyName, out JsonElement element)
                || element.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException($"Parameter '{propertyName}' is required and must be an array of strings");
            }

            return ParseTimeFrameList(element);
        }

        private List<TimeFrame> ParseTimeFrameList(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Timeframes must be an array of strings");
            }

            List<TimeFrame> result = new List<TimeFrame>();

            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Timeframes must contain only strings");
                }

                string timeFrameName = item.GetString();

                if (!Enum.TryParse<TimeFrame>(timeFrameName, true, out TimeFrame timeFrame))
                {
                    throw new ArgumentException($"Unknown timeframe '{timeFrameName}'");
                }

                result.Add(timeFrame);
            }

            return result;
        }

        private DateTime ParseDateTime(JsonElement parameters, string propertyName)
        {
            string value = ParseRequiredString(parameters, propertyName);

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out DateTime result))
            {
                throw new ArgumentException($"Parameter '{propertyName}' must be a valid date/time string");
            }

            return result;
        }

        private DateTime ParseDateTime(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Date value must be a string");
            }

            string value = element.GetString();

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out DateTime result))
            {
                throw new ArgumentException("Date value must be a valid date/time string");
            }

            return result;
        }

        private object SetSetRegime(JsonElement parameters, DataSetState regime)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                bool updated = master.UpdateSetSettings(name, settings =>
                {
                    settings.Regime = regime;
                });

                if (!updated)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                return new
                {
                    name = set.SetName,
                    regime = regime.ToString()
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to set set regime: {ex.Message}"
                };
            }
        }

        private object GetSetStatus(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                return new
                {
                    name = set.SetName,
                    regime = set.BaseSettings.Regime.ToString(),
                    status = set.GetAggregateStatus().ToString(),
                    percent_load = set.PercentLoad()
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to get set status: {ex.Message}"
                };
            }
        }

        private object GetSecurityStatus(JsonElement parameters)
        {
            OsDataMaster master = GetOsDataMaster();

            if (master == null)
            {
                return new McpJsonRpcError
                {
                    Code = -32001,
                    Message = "OsData mode is not open"
                };
            }

            try
            {
                string name = ParseRequiredString(parameters, "name");
                string securityName = ParseRequiredString(parameters, "security");
                string timeFrameName = ParseRequiredString(parameters, "timeframe");

                if (!Enum.TryParse<TimeFrame>(timeFrameName, true, out TimeFrame timeFrame))
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Unknown timeframe '{timeFrameName}'"
                    };
                }

                OsDataSet set = master.GetSet(name);

                if (set == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{name}' not found"
                    };
                }

                if (set.SecuritiesLoad == null || set.SecuritiesLoad.Count == 0)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Set '{set.SetName}' has no securities"
                    };
                }

                SecurityToLoad security = null;

                for (int i = 0; i < set.SecuritiesLoad.Count; i++)
                {
                    if (set.SecuritiesLoad[i] != null &&
                        set.SecuritiesLoad[i].SecName == securityName)
                    {
                        security = set.SecuritiesLoad[i];
                        break;
                    }
                }

                if (security == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Security '{securityName}' not found in set '{set.SetName}'"
                    };
                }

                if (security.SecLoaders == null || security.SecLoaders.Count == 0)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Security '{securityName}' has no loaders in set '{set.SetName}'"
                    };
                }

                SecurityTfLoader loader = null;

                for (int i = 0; i < security.SecLoaders.Count; i++)
                {
                    if (security.SecLoaders[i] != null &&
                        security.SecLoaders[i].TimeFrame == timeFrame)
                    {
                        loader = security.SecLoaders[i];
                        break;
                    }
                }

                if (loader == null)
                {
                    return new McpJsonRpcError
                    {
                        Code = -32602,
                        Message = $"Timeframe '{timeFrameName}' not found for security '{securityName}' in set '{set.SetName}'"
                    };
                }

                return new
                {
                    name = set.SetName,
                    security = securityName,
                    timeframe = timeFrame.ToString(),
                    time_start = loader.TimeStart,
                    time_end = loader.TimeEnd,
                    objects_count = loader.Objects(),
                    percent_load = loader.PercentLoad(),
                    status = loader.Status.ToString()
                };
            }
            catch (ArgumentException ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);

                return new McpJsonRpcError
                {
                    Code = -32603,
                    Message = $"Failed to get security status: {ex.Message}"
                };
            }
        }

        private class SecurityInput
        {
            public string Name;
            public string Id;
            public string Class;
            public string Exchange;
        }

        private void ResetTimeFrameFlags(SettingsToLoadSecurity settings)
        {
            settings.Tf1SecondIsOn = false;
            settings.Tf2SecondIsOn = false;
            settings.Tf5SecondIsOn = false;
            settings.Tf10SecondIsOn = false;
            settings.Tf15SecondIsOn = false;
            settings.Tf20SecondIsOn = false;
            settings.Tf30SecondIsOn = false;
            settings.Tf1MinuteIsOn = false;
            settings.Tf2MinuteIsOn = false;
            settings.Tf5MinuteIsOn = false;
            settings.Tf10MinuteIsOn = false;
            settings.Tf15MinuteIsOn = false;
            settings.Tf30MinuteIsOn = false;
            settings.Tf1HourIsOn = false;
            settings.Tf2HourIsOn = false;
            settings.Tf4HourIsOn = false;
            settings.TfDayIsOn = false;
            settings.TfTickIsOn = false;
            settings.TfMarketDepthIsOn = false;
        }

        private OsDataMaster GetOsDataMaster()
        {
            try
            {
                return _getOsDataMaster?.Invoke();
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Attaches event handlers to the current OsData master.
        /// Call when OsData mode is opened.
        /// </summary>
        public void AttachToMaster()
        {
            try
            {
                DetachFromMaster();

                OsDataMaster master = _getOsDataMaster?.Invoke();

                if (master == null)
                {
                    return;
                }

                master.SetLoadCompletedEvent += Master_SetLoadCompletedEvent;
                master.SecurityLoadCompletedEvent += Master_SecurityLoadCompletedEvent;
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Detaches event handlers from the previous OsData master.
        /// Call when OsData mode is closed.
        /// </summary>
        public void DetachFromMaster()
        {
            try
            {
                OsDataMaster master = _getOsDataMaster?.Invoke();

                if (master == null)
                {
                    return;
                }

                master.SetLoadCompletedEvent -= Master_SetLoadCompletedEvent;
                master.SecurityLoadCompletedEvent -= Master_SecurityLoadCompletedEvent;
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Event handlers

        private void Master_SetLoadCompletedEvent(string setName, decimal percentLoad)
        {
            try
            {
                _publishEvent?.Invoke("data_set_load_completed_event", new
                {
                    name = setName,
                    percent_load = percentLoad
                });
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Master_SecurityLoadCompletedEvent(string setName, string securityName, TimeFrame timeFrame, decimal percentLoad)
        {
            try
            {
                _publishEvent?.Invoke("data_set_security_load_completed_event", new
                {
                    name = setName,
                    security = securityName,
                    timeframe = timeFrame.ToString(),
                    percent_load = percentLoad
                });
            }
            catch (Exception ex)
            {
                SendLog(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logging

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
