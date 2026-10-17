/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OsEngine.MCP.Json;
using OsEngine.OsCapitalAllocation;
using OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity;
using OsEngine.OsTrader;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API для режима риск-паритета (HRP). Работает с `CapitalAllocationMaster`,
    /// который открывается через `terminal_open_mode { mode: "allocation" }`.
    /// </summary>
    public class CapitalAllocationApi : IMcpToolProvider
    {
        private CapitalAllocationMaster _master;

        public void AttachToMaster(CapitalAllocationMaster master)
        {
            _master = master;
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "allocation_get_status", Description = "Get capital allocation mode status: is open, current algorithm, period, bots count", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_bots_set", Description = "Set bot position files (replaces the current list)", InputSchema = new { type = "object", properties = new { files = new { type = "array", items = new { type = "string" } } }, required = new[] { "files" } } },
                new McpTool { Name = "allocation_bots_add", Description = "Add bot position files to the current list (append)", InputSchema = new { type = "object", properties = new { files = new { type = "array", items = new { type = "string" } } }, required = new[] { "files" } } },
                new McpTool { Name = "allocation_bots_get", Description = "Get the current bot list", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_bots_clear", Description = "Clear the bot list", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_settings_get", Description = "Get common settings: period and date range", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_settings_set", Description = "Set common settings: period (week/month), date_from, date_to", InputSchema = new { type = "object", properties = new { period = new { type = "string" }, date_from = new { type = "string" }, date_to = new { type = "string" } }, required = new string[0] } },
                new McpTool { Name = "allocation_clustering_settings_get", Description = "Get CorrelationClustering settings: min_correlation", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_clustering_settings_set", Description = "Set CorrelationClustering settings: min_correlation (0..1)", InputSchema = new { type = "object", properties = new { min_correlation = new { type = "number" } }, required = new[] { "min_correlation" } } },
                new McpTool { Name = "allocation_clustering_calculate", Description = "Run CorrelationClustering clustering / allocation", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_clustering_result_get", Description = "Get CorrelationClustering result: clusters and per-bot weights", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_hrp_settings_get", Description = "Get HRP settings: linkage, covariance, correlation", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_hrp_settings_set", Description = "Set HRP settings: linkage (single/complete/average/ward), covariance (sample/shrinkage), correlation (pearson/spearman)", InputSchema = new { type = "object", properties = new { linkage = new { type = "string" }, covariance = new { type = "string" }, correlation = new { type = "string" } }, required = new string[0] } },
                new McpTool { Name = "allocation_hrp_calculate", Description = "Run HRP (hierarchical capital allocation)", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_hrp_result_get", Description = "Get HRP result: weights, dendrogram merges and leaf order", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "allocation_result_save", Description = "Save the current allocation result (clusters and per-bot weights) to a file in the 100%..600% format", InputSchema = new { type = "object", properties = new { file_path = new { type = "string" } }, required = new string[0] } },
                new McpTool { Name = "allocation_load_apply", Description = "Load an allocation file and apply volumes to the running bots (volume = shoulder value / positions count)", InputSchema = new { type = "object", properties = new { file_path = new { type = "string" }, shoulder = new { type = "string" } }, required = new[] { "file_path", "shoulder" } } }
            };
        }

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
                    case "allocation_get_status":
                        response.Result = GetStatus();
                        break;

                    case "allocation_bots_set":
                        response.Result = SetBots(request.Params);
                        break;

                    case "allocation_bots_add":
                        response.Result = AddBots(request.Params);
                        break;

                    case "allocation_bots_get":
                        response.Result = GetBots();
                        break;

                    case "allocation_bots_clear":
                        response.Result = ClearBots();
                        break;

                    case "allocation_settings_get":
                        response.Result = GetSettings();
                        break;

                    case "allocation_settings_set":
                        response.Result = SetSettings(request.Params);
                        break;

                    case "allocation_clustering_settings_get":
                        response.Result = GetCorrelationClusteringSettings();
                        break;

                    case "allocation_clustering_settings_set":
                        response.Result = SetCorrelationClusteringSettings(request.Params);
                        break;

                    case "allocation_clustering_calculate":
                        response.Result = CalculateCorrelationClustering();
                        break;

                    case "allocation_clustering_result_get":
                        response.Result = GetCorrelationClusteringResult();
                        break;

                    case "allocation_hrp_settings_get":
                        response.Result = GetHierarchicalRiskParitySettings();
                        break;

                    case "allocation_hrp_settings_set":
                        response.Result = SetHierarchicalRiskParitySettings(request.Params);
                        break;

                    case "allocation_hrp_calculate":
                        response.Result = CalculateHierarchicalRiskParity();
                        break;

                    case "allocation_hrp_result_get":
                        response.Result = GetHierarchicalRiskParityResult();
                        break;

                    case "allocation_result_save":
                        response.Result = SaveResult(request.Params);
                        break;

                    case "allocation_load_apply":
                        response.Result = LoadApplyAllocation(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in capital allocation API"
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

        #region Common

        private CapitalAllocationMaster GetMaster()
        {
            if (_master == null)
            {
                throw new InvalidOperationException("Capital allocation mode is not open. Use terminal_open_mode with mode 'allocation'");
            }

            return _master;
        }

        private object GetStatus()
        {
            CapitalAllocationMaster master = GetMaster();

            return new
            {
                is_open = true,
                bots_count = master.Bots.Count,
                period = master.PeriodType.ToString().ToLower(),
                date_from = master.StartTime,
                date_to = master.EndTime,
                algorithm = master.Algorithm.ToString().ToLower()
            };
        }

        private object SetBots(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            List<string> paths = ParsePaths(parameters);

            master.ClearBots();
            master.AddBotsFromPaths(paths);

            return new { bots_count = master.Bots.Count };
        }

        private object AddBots(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            List<string> paths = ParsePaths(parameters);

            master.AddBotsFromPaths(paths);

            return new { bots_count = master.Bots.Count };
        }

        private static List<string> ParsePaths(JsonElement parameters)
        {
            List<string> paths = new List<string>();

            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("files", out JsonElement filesElement)
                && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement fileElement in filesElement.EnumerateArray())
                {
                    if (fileElement.ValueKind == JsonValueKind.String)
                    {
                        paths.Add(fileElement.GetString());
                    }
                }
            }

            return paths;
        }

        private object GetBots()
        {
            CapitalAllocationMaster master = GetMaster();

            List<object> bots = new List<object>();

            for (int i = 0; i < master.Bots.Count; i++)
            {
                bots.Add(new { bot_name = master.Bots[i].BotName, path = master.Bots[i].Path });
            }

            return new { bots = bots, bots_count = bots.Count };
        }

        private object ClearBots()
        {
            CapitalAllocationMaster master = GetMaster();

            master.ClearBots();

            return new { bots_count = 0 };
        }

        private object GetSettings()
        {
            CapitalAllocationMaster master = GetMaster();

            return new
            {
                period = master.PeriodType.ToString().ToLower(),
                date_from = master.StartTime,
                date_to = master.EndTime
            };
        }

        private object SetSettings(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("period", out JsonElement periodElement)
                && periodElement.ValueKind == JsonValueKind.String)
            {
                string period = periodElement.GetString().ToLower();

                if (period == "month")
                {
                    master.PeriodType = ReturnPeriodType.Month;
                }
                else if (period == "week")
                {
                    master.PeriodType = ReturnPeriodType.Week;
                }
            }

            if (parameters.TryGetProperty("date_from", out JsonElement dateFromElement)
                && dateFromElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateFromElement.GetString(), out DateTime dateFrom))
            {
                master.StartTime = dateFrom;
            }

            if (parameters.TryGetProperty("date_to", out JsonElement dateToElement)
                && dateToElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateToElement.GetString(), out DateTime dateTo))
            {
                master.EndTime = dateTo;
            }

            return GetSettings();
        }

        #endregion

        #region CorrelationClustering

        private object GetCorrelationClusteringSettings()
        {
            CapitalAllocationMaster master = GetMaster();

            return new { min_correlation = master.MinCorrelation };
        }

        private object SetCorrelationClusteringSettings(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("min_correlation", out JsonElement minCorrelationElement)
                && minCorrelationElement.ValueKind == JsonValueKind.Number
                && minCorrelationElement.TryGetDouble(out double minCorrelation))
            {
                if (minCorrelation < 0)
                {
                    minCorrelation = 0;
                }

                if (minCorrelation > 1)
                {
                    minCorrelation = 1;
                }

                master.MinCorrelation = minCorrelation;
            }

            return GetCorrelationClusteringSettings();
        }

        private object CalculateCorrelationClustering()
        {
            CapitalAllocationMaster master = GetMaster();

            if (master.Bots.Count == 0)
            {
                throw new InvalidOperationException("No bot files loaded. Use allocation_bots_set first");
            }

            master.CalculateCorrelationClustering();

            return new
            {
                calculated = master.Matrix != null,
                bots_count = master.Bots.Count,
                periods_count = master.Matrix != null ? master.Matrix.PeriodsCount : 0
            };
        }

        private object GetCorrelationClusteringResult()
        {
            CapitalAllocationMaster master = GetMaster();

            if (master.Clusters == null || master.Weights == null)
            {
                throw new InvalidOperationException("CorrelationClustering result is empty. Run allocation_clustering_calculate first");
            }

            List<List<int>> clusterGroups = new List<List<int>>();

            for (int i = 0; i < master.Clusters.Count; i++)
            {
                clusterGroups.Add(master.Clusters[i].BotIndices);
            }

            List<object> clusters = BuildClustersResponse(master, clusterGroups);

            return new
            {
                clusters = clusters,
                clusters_count = clusters.Count,
                weights = BuildWeightsResponse(master)
            };
        }

        #endregion

        #region HierarchicalRiskParity

        private object GetHierarchicalRiskParitySettings()
        {
            CapitalAllocationMaster master = GetMaster();

            return new
            {
                linkage = master.LinkageMethod.ToString().ToLower(),
                covariance = master.CovarianceMethod.ToString().ToLower(),
                correlation = master.CorrelationType.ToString().ToLower()
            };
        }

        private object SetHierarchicalRiskParitySettings(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("linkage", out JsonElement linkageElement)
                && linkageElement.ValueKind == JsonValueKind.String
                && Enum.TryParse(linkageElement.GetString(), true, out LinkageMethod linkage))
            {
                master.LinkageMethod = linkage;
            }

            if (parameters.TryGetProperty("covariance", out JsonElement covarianceElement)
                && covarianceElement.ValueKind == JsonValueKind.String
                && Enum.TryParse(covarianceElement.GetString(), true, out CovarianceMethod covariance))
            {
                master.CovarianceMethod = covariance;
            }

            if (parameters.TryGetProperty("correlation", out JsonElement correlationElement)
                && correlationElement.ValueKind == JsonValueKind.String
                && Enum.TryParse(correlationElement.GetString(), true, out CorrelationType correlation))
            {
                master.CorrelationType = correlation;
            }

            return GetHierarchicalRiskParitySettings();
        }

        private object CalculateHierarchicalRiskParity()
        {
            CapitalAllocationMaster master = GetMaster();

            if (master.Bots.Count == 0)
            {
                throw new InvalidOperationException("No bot files loaded. Use allocation_bots_set first");
            }

            master.CalculateHierarchicalRiskParity();

            return new
            {
                calculated = master.Matrix != null,
                bots_count = master.Bots.Count,
                periods_count = master.Matrix != null ? master.Matrix.PeriodsCount : 0
            };
        }

        private object GetHierarchicalRiskParityResult()
        {
            CapitalAllocationMaster master = GetMaster();

            if (master.Hrp == null || master.Weights == null)
            {
                throw new InvalidOperationException("HRP result is empty. Run allocation_hrp_calculate first");
            }

            List<List<int>> clusterGroups = AllocationFileService.ComputeHrpClusters(
                master.Hrp, master.Matrix.BotsCount, master.MinCorrelation);

            List<object> clusters = clusterGroups != null
                ? BuildClustersResponse(master, clusterGroups)
                : new List<object>();

            List<object> merges = new List<object>();

            for (int i = 0; i < master.Hrp.Merges.Count; i++)
            {
                DendrogramNode node = master.Hrp.Merges[i];

                merges.Add(new { left = node.Left, right = node.Right, distance = node.Distance, size = node.Size });
            }

            List<string> leafOrder = new List<string>();

            for (int i = 0; i < master.Hrp.SortedIndices.Count; i++)
            {
                leafOrder.Add(master.Matrix.BotNames[master.Hrp.SortedIndices[i]]);
            }

            return new
            {
                clusters = clusters,
                clusters_count = clusters.Count,
                weights = BuildWeightsResponse(master),
                allocation_merges = merges,
                allocation_leaf_order = leafOrder
            };
        }

        #endregion

        #region Load allocation

        private object SaveResult(JsonElement parameters)
        {
            CapitalAllocationMaster master = GetMaster();

            if (master.Matrix == null || master.Weights == null)
            {
                throw new InvalidOperationException("No result to save. Run allocation_clustering_calculate or allocation_hrp_calculate first");
            }

            List<List<int>> clusters = GetCurrentClusters(master);

            if (clusters == null || clusters.Count == 0)
            {
                throw new InvalidOperationException("No clusters to save");
            }

            string filePath = ReadFilePath(parameters);

            filePath = ResolveFilePath(filePath);

            AllocationFileService.Save(filePath, clusters, master.Matrix.BotNames, master.Weights);

            return new
            {
                saved = true,
                file_path = filePath,
                clusters_count = clusters.Count,
                bots_count = master.Matrix.BotsCount
            };
        }

        private object LoadApplyAllocation(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            string filePath = ResolveFilePath(ReadFilePath(parameters));

            string shoulder = "2";

            if (parameters.TryGetProperty("shoulder", out JsonElement shoulderElement)
                && shoulderElement.ValueKind == JsonValueKind.String)
            {
                shoulder = shoulderElement.GetString();
            }

            if (File.Exists(filePath) == false)
            {
                throw new InvalidOperationException("Allocation file not found: " + filePath);
            }

            if (OsTraderMaster.Master == null)
            {
                throw new InvalidOperationException("Trader or Tester is not running");
            }

            List<ClusterInFile> clusters = AllocationFileService.Load(filePath);

            AllocationApplyResult result = AllocationFileService.Compute(clusters, shoulder, OsTraderMaster.Master.PanelsArray);

            AllocationFileService.Apply(result);

            List<object> applied = new List<object>();

            for (int i = 0; i < result.Applied.Count; i++)
            {
                applied.Add(new
                {
                    bot_name = result.Applied[i].Bot.NameStrategyUniq,
                    volume = result.Applied[i].Volume
                });
            }

            List<object> skipped = new List<object>();

            for (int i = 0; i < result.Skipped.Count; i++)
            {
                skipped.Add(new
                {
                    bot_name = result.Skipped[i].BotName,
                    reason = result.Skipped[i].Reason
                });
            }

            return new
            {
                applied = applied,
                skipped = skipped,
                applied_count = applied.Count,
                skipped_count = skipped.Count
            };
        }

        private static List<List<int>> GetCurrentClusters(CapitalAllocationMaster master)
        {
            if (master.Algorithm == CapitalAllocationAlgorithmType.CorrelationClustering && master.Clusters != null)
            {
                List<List<int>> result = new List<List<int>>();

                for (int i = 0; i < master.Clusters.Count; i++)
                {
                    result.Add(master.Clusters[i].BotIndices);
                }

                return result;
            }

            if (master.Algorithm == CapitalAllocationAlgorithmType.HierarchicalRiskParity && master.Hrp != null && master.Matrix != null)
            {
                return AllocationFileService.ComputeHrpClusters(master.Hrp, master.Matrix.BotsCount, master.MinCorrelation);
            }

            return null;
        }

        private static string ReadFilePath(JsonElement parameters)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("file_path", out JsonElement pathElement)
                && pathElement.ValueKind == JsonValueKind.String)
            {
                return pathElement.GetString();
            }

            return null;
        }

        private static string ResolveFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine", "AllocationResult.txt");
            }

            string directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            return filePath;
        }

        #endregion

        #region Helpers

        private static List<object> BuildClustersResponse(CapitalAllocationMaster master, List<List<int>> clusters)
        {
            List<object> result = new List<object>();

            for (int c = 0; c < clusters.Count; c++)
            {
                List<int> cluster = clusters[c];
                List<object> bots = new List<object>();

                for (int j = 0; j < cluster.Count; j++)
                {
                    int botIndex = cluster[j];
                    decimal weight = master.Weights[botIndex];

                    bots.Add(new
                    {
                        num_in_cluster = j + 1,
                        bot_name = master.Matrix.BotNames[botIndex],
                        shoulders = new decimal[]
                        {
                            Math.Round(weight * 100, 2),
                            Math.Round(weight * 200, 2),
                            Math.Round(weight * 300, 2),
                            Math.Round(weight * 400, 2),
                            Math.Round(weight * 500, 2),
                            Math.Round(weight * 600, 2)
                        }
                    });
                }

                result.Add(new { cluster_number = c + 1, bots = bots });
            }

            return result;
        }

        private static List<object> BuildWeightsResponse(CapitalAllocationMaster master)
        {
            List<int> order = new List<int>();

            for (int i = 0; i < master.Matrix.BotsCount; i++)
            {
                order.Add(i);
            }

            order.Sort(delegate (int a, int b) { return master.Weights[b].CompareTo(master.Weights[a]); });

            List<object> weights = new List<object>();

            for (int i = 0; i < order.Count; i++)
            {
                int botIndex = order[i];

                weights.Add(new
                {
                    bot_name = master.Matrix.BotNames[botIndex],
                    weight_percent = Math.Round(master.Weights[botIndex] * 100, 4)
                });
            }

            return weights;
        }

        #endregion
    }
}
