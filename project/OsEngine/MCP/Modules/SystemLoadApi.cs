/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.MCP.Json;
using OsEngine.OsTrader.SystemAnalyze;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for real-time system load data (RAM, CPU, event clearing queue, orders queue).
    /// Data source: SystemUsageAnalyzeMaster.
    /// </summary>
    public class SystemLoadApi : IMcpToolProvider
    {
        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

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
                    case "system_load_get_current":
                        response.Result = GetCurrent();
                        break;

                    case "system_load_get_history":
                        response.Result = GetHistory(request.Params);
                        break;

                    case "system_load_get_settings":
                        response.Result = GetSettings();
                        break;

                    case "system_load_set_settings":
                        response.Result = SetSettings(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in system load API"
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
                    Name = "system_load_get_current",
                    Description = "Get last collected system load points (RAM, CPU, event clearing queue, orders queue). Types with disabled collection return null values; enable collection via system_load_set_settings",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "system_load_get_history",
                    Description = "Get history of system load points by type",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new { type = "string", description = "Ram, Cpu, Ecq, Moq" },
                            limit = new { type = "integer", description = "Max points to return (1..1000, default 100, last points)" }
                        },
                        required = new[] { "type" }
                    }
                },
                new McpTool
                {
                    Name = "system_load_get_settings",
                    Description = "Get system load collection settings for all types (Ram, Cpu, Ecq, Moq)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "system_load_set_settings",
                    Description = "Set system load collection settings. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            ram_collect_data_is_on = new { type = "boolean" },
                            ram_period = new { type = "string", description = "OneSecond, TenSeconds, Minute" },
                            ram_points_max = new { type = "integer" },
                            cpu_collect_data_is_on = new { type = "boolean" },
                            cpu_period = new { type = "string", description = "OneSecond, TenSeconds, Minute" },
                            cpu_points_max = new { type = "integer" },
                            ecq_collect_data_is_on = new { type = "boolean" },
                            ecq_period = new { type = "string", description = "OneSecond, TenSeconds, Minute" },
                            ecq_points_max = new { type = "integer" },
                            moq_collect_data_is_on = new { type = "boolean" },
                            moq_period = new { type = "string", description = "OneSecond, TenSeconds, Minute" },
                            moq_points_max = new { type = "integer" }
                        },
                        required = new string[0]
                    }
                }
            };
        }

        #endregion

        #region Private methods

        private object GetCurrent()
        {
            SystemUsagePointRam ram = null;
            SystemUsagePointCpu cpu = null;
            SystemUsagePointEcq ecq = null;
            SystemUsagePointMoq moq = null;

            try
            {
                // коллекции могут меняться рабочим потоком во время чтения
                ram = SystemUsageAnalyzeMaster.LastValueRam;
                cpu = SystemUsageAnalyzeMaster.LastValueCpu;
                ecq = SystemUsageAnalyzeMaster.LastValueEcq;
                moq = SystemUsageAnalyzeMaster.LastValueMoq;
            }
            catch
            {
                // возвращаем то, что успели прочитать
            }

            string hint = null;

            if (ram == null || cpu == null || ecq == null || moq == null)
            {
                hint = "Collection is off for some types or no points collected yet. Enable collection via system_load_set_settings";
            }

            return new
            {
                ram_collect_data_is_on = SystemUsageAnalyzeMaster.RamCollectDataIsOn,
                cpu_collect_data_is_on = SystemUsageAnalyzeMaster.CpuCollectDataIsOn,
                ecq_collect_data_is_on = SystemUsageAnalyzeMaster.EcqCollectDataIsOn,
                moq_collect_data_is_on = SystemUsageAnalyzeMaster.MoqCollectDataIsOn,
                ram_time = ram?.Time,
                ram_program_percent = ram?.ProgramUsedPercent,
                ram_system_percent = ram?.SystemUsedPercent,
                cpu_time = cpu?.Time,
                cpu_program_percent = cpu?.ProgramOccupiedPercent,
                cpu_system_percent = cpu?.TotalOccupiedPercent,
                ecq_time = ecq?.Time,
                market_depth_clearing_count = ecq?.MarketDepthClearingCount,
                bid_ask_clearing_count = ecq?.BidAskClearingCount,
                moq_time = moq?.Time,
                orders_in_queue = moq?.MaxOrdersInQueue,
                hint = hint
            };
        }

        private object GetHistory(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("type", out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("type is required (Ram, Cpu, Ecq, Moq)");
            }

            string type = typeElement.GetString();
            int limit = ParseLimit(parameters);

            if (string.Equals(type, "Ram", StringComparison.OrdinalIgnoreCase))
            {
                SystemUsagePointRam[] snapshot = SnapshotArray(SystemUsageAnalyzeMaster.ValuesRam);
                List<object> points = new List<object>();

                for (int i = Math.Max(0, snapshot.Length - limit); i < snapshot.Length; i++)
                {
                    points.Add(new
                    {
                        time = snapshot[i].Time,
                        program_percent = snapshot[i].ProgramUsedPercent,
                        system_percent = snapshot[i].SystemUsedPercent
                    });
                }

                return new { type = "Ram", points = points, count = points.Count };
            }

            if (string.Equals(type, "Cpu", StringComparison.OrdinalIgnoreCase))
            {
                SystemUsagePointCpu[] snapshot = SnapshotArray(SystemUsageAnalyzeMaster.ValuesCpu);
                List<object> points = new List<object>();

                for (int i = Math.Max(0, snapshot.Length - limit); i < snapshot.Length; i++)
                {
                    points.Add(new
                    {
                        time = snapshot[i].Time,
                        program_percent = snapshot[i].ProgramOccupiedPercent,
                        system_percent = snapshot[i].TotalOccupiedPercent
                    });
                }

                return new { type = "Cpu", points = points, count = points.Count };
            }

            if (string.Equals(type, "Ecq", StringComparison.OrdinalIgnoreCase))
            {
                SystemUsagePointEcq[] snapshot = SnapshotArray(SystemUsageAnalyzeMaster.ValuesEcq);
                List<object> points = new List<object>();

                for (int i = Math.Max(0, snapshot.Length - limit); i < snapshot.Length; i++)
                {
                    points.Add(new
                    {
                        time = snapshot[i].Time,
                        market_depth_clearing_count = snapshot[i].MarketDepthClearingCount,
                        bid_ask_clearing_count = snapshot[i].BidAskClearingCount
                    });
                }

                return new { type = "Ecq", points = points, count = points.Count };
            }

            if (string.Equals(type, "Moq", StringComparison.OrdinalIgnoreCase))
            {
                SystemUsagePointMoq[] snapshot = SnapshotArray(SystemUsageAnalyzeMaster.ValuesMoq);
                List<object> points = new List<object>();

                for (int i = Math.Max(0, snapshot.Length - limit); i < snapshot.Length; i++)
                {
                    points.Add(new
                    {
                        time = snapshot[i].Time,
                        orders_in_queue = snapshot[i].MaxOrdersInQueue
                    });
                }

                return new { type = "Moq", points = points, count = points.Count };
            }

            throw new ArgumentException($"Unknown type '{type}'. Expected: Ram, Cpu, Ecq, Moq");
        }

        private T[] SnapshotArray<T>(List<T> values)
        {
            try
            {
                if (values == null)
                {
                    return new T[0];
                }

                // коллекция может меняться рабочим потоком во время копирования
                return values.ToArray();
            }
            catch
            {
                return new T[0];
            }
        }

        private object GetSettings()
        {
            return new
            {
                ram = new
                {
                    collect_data_is_on = SystemUsageAnalyzeMaster.RamCollectDataIsOn,
                    period = SystemUsageAnalyzeMaster.RamPeriodSavePoint.ToString(),
                    points_max = SystemUsageAnalyzeMaster.RamPointsMax
                },
                cpu = new
                {
                    collect_data_is_on = SystemUsageAnalyzeMaster.CpuCollectDataIsOn,
                    period = SystemUsageAnalyzeMaster.CpuPeriodSavePoint.ToString(),
                    points_max = SystemUsageAnalyzeMaster.CpuPointsMax
                },
                ecq = new
                {
                    collect_data_is_on = SystemUsageAnalyzeMaster.EcqCollectDataIsOn,
                    period = SystemUsageAnalyzeMaster.EcqPeriodSavePoint.ToString(),
                    points_max = SystemUsageAnalyzeMaster.EcqPointsMax
                },
                moq = new
                {
                    collect_data_is_on = SystemUsageAnalyzeMaster.MoqCollectDataIsOn,
                    period = SystemUsageAnalyzeMaster.MoqPeriodSavePoint.ToString(),
                    points_max = SystemUsageAnalyzeMaster.MoqPointsMax
                }
            };
        }

        private object SetSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            ApplyBool(parameters, "ram_collect_data_is_on", value => SystemUsageAnalyzeMaster.RamCollectDataIsOn = value);
            ApplyPeriod(parameters, "ram_period", value => SystemUsageAnalyzeMaster.RamPeriodSavePoint = value);
            ApplyInt(parameters, "ram_points_max", value => SystemUsageAnalyzeMaster.RamPointsMax = value);

            ApplyBool(parameters, "cpu_collect_data_is_on", value => SystemUsageAnalyzeMaster.CpuCollectDataIsOn = value);
            ApplyPeriod(parameters, "cpu_period", value => SystemUsageAnalyzeMaster.CpuPeriodSavePoint = value);
            ApplyInt(parameters, "cpu_points_max", value => SystemUsageAnalyzeMaster.CpuPointsMax = value);

            ApplyBool(parameters, "ecq_collect_data_is_on", value => SystemUsageAnalyzeMaster.EcqCollectDataIsOn = value);
            ApplyPeriod(parameters, "ecq_period", value => SystemUsageAnalyzeMaster.EcqPeriodSavePoint = value);
            ApplyInt(parameters, "ecq_points_max", value => SystemUsageAnalyzeMaster.EcqPointsMax = value);

            ApplyBool(parameters, "moq_collect_data_is_on", value => SystemUsageAnalyzeMaster.MoqCollectDataIsOn = value);
            ApplyPeriod(parameters, "moq_period", value => SystemUsageAnalyzeMaster.MoqPeriodSavePoint = value);
            ApplyInt(parameters, "moq_points_max", value => SystemUsageAnalyzeMaster.MoqPointsMax = value);

            return GetSettings();
        }

        private void ApplyBool(JsonElement parameters, string name, Action<bool> apply)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            {
                apply(element.GetBoolean());
            }
        }

        private void ApplyPeriod(JsonElement parameters, string name, Action<SavePointPeriod> apply)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.String
                && Enum.TryParse<SavePointPeriod>(element.GetString(), true, out SavePointPeriod period))
            {
                apply(period);
            }
        }

        private void ApplyInt(JsonElement parameters, string name, Action<int> apply)
        {
            if (parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out int value)
                && value > 0)
            {
                apply(value);
            }
        }

        private int ParseLimit(JsonElement parameters)
        {
            if (parameters.TryGetProperty("limit", out JsonElement limitElement)
                && limitElement.ValueKind == JsonValueKind.Number
                && limitElement.TryGetInt32(out int limit))
            {
                return Math.Max(1, Math.Min(limit, 1000));
            }

            return 100;
        }

        #endregion
    }
}
