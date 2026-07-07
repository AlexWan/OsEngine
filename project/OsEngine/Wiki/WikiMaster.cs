/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.MCP.Json;
using OsEngine.MCP.Modules;

namespace OsEngine.Wiki
{
    /// <summary>
    /// Static access to Wiki reference data (securities and dividends) for trading robots.
    /// Wraps existing MCP API providers without duplicating their logic.
    /// All methods are safe: on error they return empty results instead of throwing.
    /// </summary>
    public static class WikiMaster
    {
        #region Fields

        private static readonly WikiDividendsApi DividendsApi = new WikiDividendsApi();
        private static readonly WikiSecuritiesApi SecuritiesApi = new WikiSecuritiesApi();

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Dividends

        /// <summary>
        /// Get dividend payments with registry close date on or before the reference date.
        /// Returns empty result on error.
        /// </summary>
        public static WikiDividendHistory GetDividendsHistory(string ticker, DateTime? date = null)
        {
            ticker = NormalizeTicker(ticker);

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "ticker", ticker },
                    { "refresh", false }
                };

                if (date.HasValue)
                {
                    parameters["date"] = date.Value.ToString("dd.MM.yyyy");
                }

                return CallDividendsApi<WikiDividendHistory>("wiki_dividends_get_history", parameters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetDividendsHistory({ticker}) error: {error}", LogMessageType.Error);
                return new WikiDividendHistory
                {
                    ticker = ticker ?? string.Empty,
                    date = date.HasValue ? date.Value.ToString("dd.MM.yyyy") : DateTime.Today.ToString("dd.MM.yyyy"),
                    source = string.Empty,
                    last_updated = string.Empty,
                    historical = new List<WikiDividendRecord>(),
                    count = 0
                };
            }
        }

        /// <summary>
        /// Get the nearest future dividend registry close date on or after the reference date.
        /// Returns empty result on error.
        /// </summary>
        public static WikiDividendFuture GetDividendsFuture(string ticker, DateTime? date = null)
        {
            ticker = NormalizeTicker(ticker);

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "ticker", ticker },
                    { "refresh", false }
                };

                if (date.HasValue)
                {
                    parameters["date"] = date.Value.ToString("dd.MM.yyyy");
                }

                return CallDividendsApi<WikiDividendFuture>("wiki_dividends_get_future", parameters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetDividendsFuture({ticker}) error: {error}", LogMessageType.Error);
                return new WikiDividendFuture
                {
                    ticker = ticker ?? string.Empty,
                    date = date.HasValue ? date.Value.ToString("dd.MM.yyyy") : DateTime.Today.ToString("dd.MM.yyyy"),
                    source = string.Empty,
                    last_updated = string.Empty,
                    future = null
                };
            }
        }

        /// <summary>
        /// Get the nearest past dividend registry close date on or before the reference date.
        /// Returns empty result on error.
        /// </summary>
        public static WikiDividendPast GetDividendsPast(string ticker, DateTime? date = null)
        {
            ticker = NormalizeTicker(ticker);

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "ticker", ticker },
                    { "refresh", false }
                };

                if (date.HasValue)
                {
                    parameters["date"] = date.Value.ToString("dd.MM.yyyy");
                }

                return CallDividendsApi<WikiDividendPast>("wiki_dividends_get_past", parameters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetDividendsPast({ticker}) error: {error}", LogMessageType.Error);
                return new WikiDividendPast
                {
                    ticker = ticker ?? string.Empty,
                    date = date.HasValue ? date.Value.ToString("dd.MM.yyyy") : DateTime.Today.ToString("dd.MM.yyyy"),
                    source = string.Empty,
                    last_updated = string.Empty,
                    past = null
                };
            }
        }

        /// <summary>
        /// Search historical and future dividends by exact registry close date.
        /// Returns empty result on error.
        /// </summary>
        public static WikiDividendSearch SearchDividendsByDate(string ticker, DateTime date)
        {
            ticker = NormalizeTicker(ticker);

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "ticker", ticker },
                    { "date", date.ToString("dd.MM.yyyy") },
                    { "refresh", false }
                };

                return CallDividendsApi<WikiDividendSearch>("wiki_dividends_search_by_date", parameters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.SearchDividendsByDate({ticker}, {date:dd.MM.yyyy}) error: {error}", LogMessageType.Error);
                return new WikiDividendSearch
                {
                    ticker = ticker ?? string.Empty,
                    date = date.ToString("dd.MM.yyyy"),
                    matches = new List<WikiDividendRecord>(),
                    count = 0
                };
            }
        }

        #endregion

        #region Securities

        /// <summary>
        /// Get securities from MOEX ISS connector wiki.
        /// Returns empty result on error.
        /// </summary>
        public static WikiSecurityListResult GetSecuritiesMoexIss(string filter = null)
        {
            try
            {
                return CallSecuritiesApi("wiki_securities_moex_iss", filter, false);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetSecuritiesMoexIss({filter}) error: {error}", LogMessageType.Error);
                return new WikiSecurityListResult
                {
                    securities = new List<JsonElement>(),
                    count = 0,
                    connector = "MoexDataServer",
                    collected_at = null
                };
            }
        }

        /// <summary>
        /// Get securities from TInvest connector wiki.
        /// Returns empty result on error.
        /// </summary>
        public static WikiSecurityListResult GetSecuritiesTInvest(string filter = null)
        {
            try
            {
                return CallSecuritiesApi("wiki_securities_tinvest", filter, false);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetSecuritiesTInvest({filter}) error: {error}", LogMessageType.Error);
                return new WikiSecurityListResult
                {
                    securities = new List<JsonElement>(),
                    count = 0,
                    connector = "TInvest",
                    collected_at = null
                };
            }
        }

        /// <summary>
        /// Get securities from Alor connector wiki.
        /// Returns empty result on error.
        /// </summary>
        public static WikiSecurityListResult GetSecuritiesAlor(string filter = null)
        {
            try
            {
                return CallSecuritiesApi("wiki_securities_alor", filter, false);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetSecuritiesAlor({filter}) error: {error}", LogMessageType.Error);
                return new WikiSecurityListResult
                {
                    securities = new List<JsonElement>(),
                    count = 0,
                    connector = "Alor",
                    collected_at = null
                };
            }
        }

        /// <summary>
        /// Get securities from QScalp connector wiki.
        /// Returns empty result on error.
        /// </summary>
        public static WikiSecurityListResult GetSecuritiesQScalp(string filter = null)
        {
            try
            {
                return CallSecuritiesApi("wiki_securities_qscalp", filter, false);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetSecuritiesQScalp({filter}) error: {error}", LogMessageType.Error);
                return new WikiSecurityListResult
                {
                    securities = new List<JsonElement>(),
                    count = 0,
                    connector = "QscalpMarketDepth",
                    collected_at = null
                };
            }
        }

        /// <summary>
        /// Search securities across all connector wiki files by name, ticker or description.
        /// Returns empty result on error.
        /// </summary>
        public static WikiSecurityMappingResult GetSecuritiesMapping(string query, string connector = null, int? limit = null, bool exact = false)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "query", query },
                    { "exact", exact },
                    { "refresh", false }
                };

                if (!string.IsNullOrWhiteSpace(connector))
                {
                    parameters["connector"] = connector;
                }

                if (limit.HasValue)
                {
                    parameters["limit"] = limit.Value;
                }

                return CallSecuritiesApi<WikiSecurityMappingResult>("wiki_securities_mapping_info", parameters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage($"WikiMaster.GetSecuritiesMapping({query}) error: {error}", LogMessageType.Error);
                return new WikiSecurityMappingResult
                {
                    query = query ?? string.Empty,
                    total = 0,
                    results = new List<WikiSecurityMappingItem>()
                };
            }
        }

        #endregion

        #region Private methods

        private static T CallDividendsApi<T>(string method, Dictionary<string, object> parameters)
        {
            McpJsonRpcResponse response = ExecuteApiCall(DividendsApi, method, parameters);
            return DeserializeResult<T>(response);
        }

        private static WikiSecurityListResult CallSecuritiesApi(string method, string filter, bool refresh)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "refresh", refresh }
            };

            if (!string.IsNullOrWhiteSpace(filter))
            {
                parameters["filter"] = filter;
            }

            McpJsonRpcResponse response = ExecuteApiCall(SecuritiesApi, method, parameters);
            return DeserializeResult<WikiSecurityListResult>(response);
        }

        private static T CallSecuritiesApi<T>(string method, Dictionary<string, object> parameters)
        {
            McpJsonRpcResponse response = ExecuteApiCall(SecuritiesApi, method, parameters);
            return DeserializeResult<T>(response);
        }

        private static McpJsonRpcResponse ExecuteApiCall(WikiDividendsApi api, string method, Dictionary<string, object> parameters)
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(parameters));

            McpJsonRpcRequest request = new McpJsonRpcRequest
            {
                JsonRpc = "2.0",
                Method = method,
                Params = document.RootElement,
                Id = Guid.NewGuid().ToString()
            };

            McpJsonRpcResponse response = api.Handle(request);

            if (response.Error != null)
            {
                throw new InvalidOperationException($"WikiMaster call '{method}' failed: {response.Error.Message}");
            }

            return response;
        }

        private static McpJsonRpcResponse ExecuteApiCall(WikiSecuritiesApi api, string method, Dictionary<string, object> parameters)
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(parameters));

            McpJsonRpcRequest request = new McpJsonRpcRequest
            {
                JsonRpc = "2.0",
                Method = method,
                Params = document.RootElement,
                Id = Guid.NewGuid().ToString()
            };

            McpJsonRpcResponse response = api.Handle(request);

            if (response.Error != null)
            {
                throw new InvalidOperationException($"WikiMaster call '{method}' failed: {response.Error.Message}");
            }

            return response;
        }

        private static string NormalizeTicker(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                return string.Empty;
            }

            string normalized = ticker;

            if (normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized;
        }

        private static T DeserializeResult<T>(McpJsonRpcResponse response)
        {
            if (response.Result == null)
            {
                return default;
            }

            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }

        #endregion
    }

    #region Dividend models

    public class WikiDividendHistory
    {
        public string ticker { get; set; }
        public string date { get; set; }
        public string source { get; set; }
        public string last_updated { get; set; }
        public List<WikiDividendRecord> historical { get; set; }
        public int count { get; set; }
    }

    public class WikiDividendFuture
    {
        public string ticker { get; set; }
        public string date { get; set; }
        public string source { get; set; }
        public string last_updated { get; set; }
        public WikiDividendRecord future { get; set; }
    }

    public class WikiDividendPast
    {
        public string ticker { get; set; }
        public string date { get; set; }
        public string source { get; set; }
        public string last_updated { get; set; }
        public WikiDividendRecord past { get; set; }
    }

    public class WikiDividendSearch
    {
        public string ticker { get; set; }
        public string date { get; set; }
        public List<WikiDividendRecord> matches { get; set; }
        public int count { get; set; }
    }

    public class WikiDividendRecord
    {
        public int year { get; set; }
        public string registry_close_date { get; set; }
        public decimal dividend_amount { get; set; }
        public decimal dividend_yield { get; set; }
    }

    #endregion

    #region Security models

    public class WikiSecurityListResult
    {
        public List<JsonElement> securities { get; set; }
        public int count { get; set; }
        public string connector { get; set; }
        public string collected_at { get; set; }
    }

    public class WikiSecurityMappingResult
    {
        public string query { get; set; }
        public int total { get; set; }
        public List<WikiSecurityMappingItem> results { get; set; }
    }

    public class WikiSecurityMappingItem
    {
        public string connector { get; set; }
        public string connector_short { get; set; }
        public bool is_trading_supported { get; set; }
        public bool is_data_feed_supported { get; set; }
        public JsonElement security { get; set; }
    }

    #endregion
}
