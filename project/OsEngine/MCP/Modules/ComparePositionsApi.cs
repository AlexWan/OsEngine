/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for the compare positions module (robots vs exchange positions)
    /// and position synchronization.
    /// </summary>
    public class ComparePositionsApi : IMcpToolProvider
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
                    case "compare_positions_get":
                        response.Result = GetComparePositions(request.Params);
                        break;

                    case "compare_positions_get_settings":
                        response.Result = GetSettings(request.Params);
                        break;

                    case "compare_positions_set_settings":
                        response.Result = SetSettings(request.Params);
                        break;

                    case "compare_positions_set_ignored":
                        response.Result = SetIgnored(request.Params);
                        break;

                    case "compare_positions_sync_all":
                        response.Result = SyncAll(request.Params);
                        break;

                    case "compare_positions_sync_this":
                        response.Result = SyncThis(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in compare positions API"
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
                    Name = "compare_positions_get",
                    Description = "Get fresh compare positions data (robots vs exchange) for all portfolios of a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string", description = "Server type (TInvest, Binance etc.)" },
                            number = new { type = "integer", description = "Server instance number (default 0)" }
                        },
                        required = new[] { "server_type" }
                    }
                },
                new McpTool
                {
                    Name = "compare_positions_get_settings",
                    Description = "Get compare positions module settings of a server instance",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string" },
                            number = new { type = "integer", description = "Server instance number (default 0)" }
                        },
                        required = new[] { "server_type" }
                    }
                },
                new McpTool
                {
                    Name = "compare_positions_set_settings",
                    Description = "Set compare positions module settings. All fields are optional",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string" },
                            number = new { type = "integer" },
                            verification_period = new { type = "string", description = "Min1, Min5, Min10, Min30" },
                            time_delay_seconds = new { type = "integer" },
                            portfolios_to_watch = new { type = "array", items = new { type = "string" }, description = "Portfolios watched for mismatches (replaces the whole list)" }
                        },
                        required = new[] { "server_type" }
                    }
                },
                new McpTool
                {
                    Name = "compare_positions_set_ignored",
                    Description = "Set the list of securities ignored by the compare positions module (replaces the whole list)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string" },
                            number = new { type = "integer" },
                            securities = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "server_type", "securities" }
                    }
                },
                new McpTool
                {
                    Name = "compare_positions_sync_all",
                    Description = "Synchronize a whole portfolio with robots accounting: sends market orders for every mismatched security (close excess / open missing)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string" },
                            number = new { type = "integer" },
                            portfolio_name = new { type = "string" }
                        },
                        required = new[] { "server_type", "portfolio_name" }
                    }
                },
                new McpTool
                {
                    Name = "compare_positions_sync_this",
                    Description = "Synchronize one security in a portfolio with robots accounting: sends a market order for the difference (close excess / open missing)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            server_type = new { type = "string" },
                            number = new { type = "integer" },
                            portfolio_name = new { type = "string" },
                            security_name = new { type = "string" }
                        },
                        required = new[] { "server_type", "portfolio_name", "security_name" }
                    }
                }
            };
        }

        #endregion

        #region Private methods

        private object GetComparePositions(JsonElement parameters)
        {
            ComparePositionsModule module = FindModule(parameters);

            List<ComparePositionsPortfolio> portfolios = module.UpdateCompareData();
            List<object> result = new List<object>();

            if (portfolios == null)
            {
                return new { portfolios = result, count = 0 };
            }

            for (int i = 0; i < portfolios.Count; i++)
            {
                ComparePositionsPortfolio portfolio = portfolios[i];
                List<object> securities = new List<object>();

                for (int j = 0; j < portfolio.CompareSecurities.Count; j++)
                {
                    ComparePositionsSecurity security = portfolio.CompareSecurities[j];

                    securities.Add(new
                    {
                        security = security.Security,
                        status = security.Status.ToString(),
                        robots_long = security.RobotsLong,
                        robots_short = security.RobotsShort,
                        robots_common = security.RobotsCommon,
                        portfolio_long = security.PortfolioLong,
                        portfolio_short = security.PortfolioShort,
                        portfolio_common = security.PortfolioCommon,
                        is_ignored = security.IsIgnored
                    });
                }

                result.Add(new
                {
                    portfolio_name = portfolio.PortfolioName,
                    is_watched = module.PortfoliosToWatch.Contains(portfolio.PortfolioName),
                    securities = securities,
                    securities_count = securities.Count
                });
            }

            return new { portfolios = result, count = result.Count };
        }

        private object GetSettings(JsonElement parameters)
        {
            ComparePositionsModule module = FindModule(parameters);
            return BuildSettingsResponse(module);
        }

        private object SetSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            ComparePositionsModule module = FindModule(parameters);

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                ApplySettings(module, parameters);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => ApplySettings(module, parameters));
            }

            return BuildSettingsResponse(module);
        }

        private void ApplySettings(ComparePositionsModule module, JsonElement parameters)
        {
            if (parameters.TryGetProperty("verification_period", out JsonElement periodElement)
                && periodElement.ValueKind == JsonValueKind.String
                && Enum.TryParse<ComparePositionsVerificationPeriod>(periodElement.GetString(), true, out ComparePositionsVerificationPeriod period))
            {
                module.VerificationPeriod = period;
            }

            if (parameters.TryGetProperty("time_delay_seconds", out JsonElement delayElement)
                && delayElement.ValueKind == JsonValueKind.Number
                && delayElement.TryGetInt32(out int delay)
                && delay > 0)
            {
                module.TimeDelaySeconds = delay;
            }

            if (parameters.TryGetProperty("portfolios_to_watch", out JsonElement watchElement)
                && watchElement.ValueKind == JsonValueKind.Array)
            {
                module.PortfoliosToWatch.Clear();

                foreach (JsonElement item in watchElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        module.PortfoliosToWatch.Add(item.GetString());
                    }
                }
            }

            module.Save();
        }

        private object SetIgnored(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            ComparePositionsModule module = FindModule(parameters);

            if (!parameters.TryGetProperty("securities", out JsonElement securitiesElement)
                || securitiesElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("securities is required and must be an array of strings");
            }

            if (MainWindow.GetDispatcher.CheckAccess())
            {
                ApplyIgnored(module, securitiesElement);
            }
            else
            {
                MainWindow.GetDispatcher.Invoke(() => ApplyIgnored(module, securitiesElement));
            }

            return BuildSettingsResponse(module);
        }

        private void ApplyIgnored(ComparePositionsModule module, JsonElement securitiesElement)
        {
            module.IgnoredSecurities.Clear();

            foreach (JsonElement item in securitiesElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    ComparePositionsSecurity security = new ComparePositionsSecurity();
                    security.Security = item.GetString();
                    security.IsIgnored = true;
                    module.IgnoredSecurities.Add(security);
                }
            }

            module.SaveIgnoredSecurities();
        }

        private object SyncAll(JsonElement parameters)
        {
            ComparePositionsModule module = FindModule(parameters);
            string portfolioName = GetRequiredString(parameters, "portfolio_name");

            CheckServerConnected(module);
            CheckPortfolioExists(module, portfolioName);

            List<ComparePositionsSecurity> outOfSync = module.GetOutOfSyncSecurities(portfolioName);
            List<object> results = new List<object>();
            int sentCount = 0;

            for (int i = 0; i < outOfSync.Count; i++)
            {
                bool sent = module.SynchronizeSecurity(portfolioName, outOfSync[i].Security);

                if (sent)
                {
                    sentCount++;
                }

                results.Add(new { security = outOfSync[i].Security, sent = sent });
            }

            return new { sent_count = sentCount, results = results };
        }

        private object SyncThis(JsonElement parameters)
        {
            ComparePositionsModule module = FindModule(parameters);
            string portfolioName = GetRequiredString(parameters, "portfolio_name");
            string securityName = GetRequiredString(parameters, "security_name");

            CheckServerConnected(module);
            CheckPortfolioExists(module, portfolioName);

            bool sent = module.SynchronizeSecurity(portfolioName, securityName);

            return new { security = securityName, sent = sent };
        }

        private object BuildSettingsResponse(ComparePositionsModule module)
        {
            List<string> ignored = new List<string>();

            for (int i = 0; i < module.IgnoredSecurities.Count; i++)
            {
                ignored.Add(module.IgnoredSecurities[i].Security);
            }

            return new
            {
                verification_period = module.VerificationPeriod.ToString(),
                time_delay_seconds = module.TimeDelaySeconds,
                portfolios_to_watch = module.PortfoliosToWatch,
                ignored_securities = ignored
            };
        }

        private void CheckServerConnected(ComparePositionsModule module)
        {
            if (module.Server.ServerStatus != ServerConnectStatus.Connect)
            {
                throw new InvalidOperationException(
                    $"Server {module.Server.ServerNameUnique} is not connected. Synchronization requires an active connection");
            }
        }

        private void CheckPortfolioExists(ComparePositionsModule module, string portfolioName)
        {
            Portfolio portfolio = module.Server.GetPortfolioForName(portfolioName);

            if (portfolio == null)
            {
                throw new ArgumentException(
                    $"Portfolio '{portfolioName}' not found on server {module.Server.ServerNameUnique}");
            }
        }

        private ComparePositionsModule FindModule(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (!parameters.TryGetProperty("server_type", out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String
                || !Enum.TryParse<ServerType>(typeElement.GetString(), true, out ServerType serverType))
            {
                throw new ArgumentException("server_type is required and must be a valid server type");
            }

            int serverNumber = 0;

            if (parameters.TryGetProperty("number", out JsonElement numberElement)
                && numberElement.ValueKind == JsonValueKind.Number)
            {
                numberElement.TryGetInt32(out serverNumber);
            }

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers != null)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerType == serverType
                        && servers[i].ServerNum == serverNumber)
                    {
                        if (servers[i].ComparePositionsModule == null)
                        {
                            throw new InvalidOperationException(
                                $"Compare positions module is not available on server {serverType}#{serverNumber}");
                        }

                        return servers[i].ComparePositionsModule;
                    }
                }
            }

            throw new ArgumentException($"Server {serverType}#{serverNumber} not found");
        }

        private string GetRequiredString(JsonElement parameters, string name)
        {
            if (!parameters.TryGetProperty(name, out JsonElement element)
                || element.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"{name} is required");
            }

            return element.GetString();
        }

        #endregion
    }
}
