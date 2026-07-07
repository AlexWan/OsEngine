/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for security wiki reference methods.
    /// Reads pre-built markdown files from WikiConnectionTest and caches them in memory.
    /// </summary>
    public class WikiSecuritiesApi : IMcpToolProvider
    {
        #region Fields

        private static readonly Dictionary<string, string> ConnectorFileNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "moex_iss", "moex_iss_securities.md" },
                { "tinvest", "tinvest_securities.md" },
                { "alor", "alor_securities.md" },
                { "qscalp", "qscalp_securities.md" }
            };

        private static readonly Dictionary<string, string> ConnectorDisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "moex_iss", "MoexDataServer" },
                { "tinvest", "TInvest" },
                { "alor", "Alor" },
                { "qscalp", "QscalpMarketDepth" }
            };

        private static readonly object _cacheLocker = new object();
        private static Dictionary<string, WikiSecurityFile> _cache;
        private static bool _cacheLoaded;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region IMcpToolProvider

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool
                {
                    Name = "wiki_securities_moex_iss",
                    Description = "List securities from MOEX ISS data connector",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter = new
                            {
                                type = "string",
                                description = "Optional substring filter by name, class, full name or id"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload securities files from disk"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_securities_tinvest",
                    Description = "List securities available via TInvest connector",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter = new
                            {
                                type = "string",
                                description = "Optional substring filter by name, class, full name or id"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload securities files from disk"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_securities_alor",
                    Description = "List securities available via Alor connector",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter = new
                            {
                                type = "string",
                                description = "Optional substring filter by name, class, full name or id"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload securities files from disk"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_securities_qscalp",
                    Description = "List securities available via QScalp market depth connector",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter = new
                            {
                                type = "string",
                                description = "Optional substring filter by name, class, full name or id"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload securities files from disk"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_securities_mapping_info",
                    Description = "Search securities across all connector wiki files by name, ticker or description substring",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Name, ticker or substring to search for"
                            },
                            connector = new
                            {
                                type = "string",
                                description = "Optional connector to limit search: moex_iss, tinvest, alor, qscalp or MoexDataServer, TInvest, Alor, QscalpMarketDepth"
                            },
                            limit = new
                            {
                                type = "integer",
                                description = "Maximum number of results (default: 50)"
                            },
                            exact = new
                            {
                                type = "boolean",
                                description = "If true, match only exact name (case-insensitive)"
                            }
                        },
                        required = new[] { "query" }
                    }
                }
            };
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
                    case "wiki_securities_moex_iss":
                        response.Result = GetSecuritiesList(request.Params, "moex_iss");
                        break;

                    case "wiki_securities_tinvest":
                        response.Result = GetSecuritiesList(request.Params, "tinvest");
                        break;

                    case "wiki_securities_alor":
                        response.Result = GetSecuritiesList(request.Params, "alor");
                        break;

                    case "wiki_securities_qscalp":
                        response.Result = GetSecuritiesList(request.Params, "qscalp");
                        break;

                    case "wiki_securities_mapping_info":
                        response.Result = GetMappingInfo(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in wiki securities API"
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

        #endregion

        #region Private methods

        private object GetSecuritiesList(JsonElement parameters, string connectorShortName)
        {
            bool refresh = GetBoolParameter(parameters, "refresh");
            string filter = GetStringParameter(parameters, "filter");

            EnsureCacheLoaded(refresh);

            WikiSecurityFile file;

            lock (_cacheLocker)
            {
                _cache.TryGetValue(connectorShortName, out file);
            }

            if (file == null)
            {
                return new
                {
                    securities = new List<object>(),
                    count = 0,
                    connector = GetDisplayName(connectorShortName),
                    collected_at = (string)null
                };
            }

            List<JsonElement> securities = FilterSecurities(file.Securities, filter);

            return new
            {
                securities,
                count = securities.Count,
                connector = GetDisplayName(connectorShortName),
                collected_at = GetCollectedAt(file.Metadata)
            };
        }

        private object GetMappingInfo(JsonElement parameters)
        {
            string query = GetStringParameter(parameters, "query");
            string connector = GetStringParameter(parameters, "connector");
            int limit = GetIntParameter(parameters, "limit", 50);
            bool exact = GetBoolParameter(parameters, "exact");

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Parameter 'query' is required");
            }

            EnsureCacheLoaded(false);

            List<string> shortNames = string.IsNullOrWhiteSpace(connector)
                ? ConnectorFileNames.Keys.ToList()
                : new List<string> { NormalizeConnector(connector) };

            List<MappingResult> results = new List<MappingResult>();

            foreach (string shortName in shortNames)
            {
                WikiSecurityFile file;

                lock (_cacheLocker)
                {
                    _cache.TryGetValue(shortName, out file);
                }

                if (file == null)
                {
                    continue;
                }

                results.AddRange(SearchConnector(file, shortName, query, exact));
            }

            if (results.Count == 0)
            {
                throw new InvalidOperationException($"Search error. No data found for key '{query}'");
            }

            List<MappingResult> ordered = results
                .OrderBy(r => r.MatchPriority)
                .ThenBy(r => r.Connector)
                .ThenBy(r => GetSecurityName(r.Security))
                .ToList();

            int total = ordered.Count;
            List<MappingResult> limited = ordered.Take(limit).ToList();

            return new
            {
                query,
                total,
                results = limited.Select(r => new
                {
                    connector = r.Connector,
                    connector_short = r.ConnectorShort,
                    is_trading_supported = r.IsTradingSupported,
                    is_data_feed_supported = r.IsDataFeedSupported,
                    security = r.Security
                }).ToList()
            };
        }

        private void EnsureCacheLoaded(bool refresh)
        {
            lock (_cacheLocker)
            {
                if (!refresh && _cacheLoaded)
                {
                    return;
                }

                ClearCache();

                _cache = new Dictionary<string, WikiSecurityFile>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, string> pair in ConnectorFileNames)
                {
                    try
                    {
                        string filePath = GetWikiFilePath(pair.Value);

                        if (!File.Exists(filePath))
                        {
                            SendLog($"wiki_securities: file not found {filePath}", LogMessageType.Error);
                            continue;
                        }

                        WikiSecurityFile wikiFile = LoadWikiFile(filePath);
                        _cache[pair.Key] = wikiFile;
                    }
                    catch (Exception ex)
                    {
                        SendLog($"wiki_securities: failed to load {pair.Value}: {ex}", LogMessageType.Error);
                    }
                }

                _cacheLoaded = true;
            }
        }

        private void ClearCache()
        {
            if (_cache == null)
            {
                return;
            }

            foreach (WikiSecurityFile file in _cache.Values)
            {
                file.Dispose();
            }

            _cache.Clear();
        }

        private WikiSecurityFile LoadWikiFile(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = content.TrimStart('\uFEFF');
            return ParseMarkdown(content);
        }

        private WikiSecurityFile ParseMarkdown(string content)
        {
            string metadataJson = ExtractCodeBlock(content, "json");
            string jsonl = ExtractCodeBlock(content, "jsonl");

            JsonDocument metadataDoc = string.IsNullOrEmpty(metadataJson)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(metadataJson);

            List<string> lines = jsonl
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            JsonDocument securitiesDoc;

            if (lines.Count == 0)
            {
                securitiesDoc = JsonDocument.Parse("[]");
            }
            else
            {
                string arrayJson = "[" + string.Join(",", lines) + "]";
                securitiesDoc = JsonDocument.Parse(arrayJson);
            }

            return new WikiSecurityFile(metadataDoc, securitiesDoc);
        }

        private string ExtractCodeBlock(string content, string language)
        {
            string startMarker = "```" + language;
            int startIndex = content.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);

            if (startIndex < 0)
            {
                return string.Empty;
            }

            int contentStart = startIndex + startMarker.Length;
            int newLineIndex = content.IndexOf('\n', contentStart);

            if (newLineIndex >= 0)
            {
                contentStart = newLineIndex + 1;
            }

            int endIndex = content.IndexOf("```", contentStart, StringComparison.Ordinal);

            if (endIndex < 0)
            {
                return string.Empty;
            }

            return content.Substring(contentStart, endIndex - contentStart).Trim();
        }

        private List<JsonElement> FilterSecurities(JsonElement securitiesArray, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return securitiesArray.EnumerateArray().ToList();
            }

            string lowerFilter = filter.ToLowerInvariant();

            return securitiesArray.EnumerateArray()
                .Where(s => MatchesFilter(s, lowerFilter))
                .ToList();
        }

        private bool MatchesFilter(JsonElement security, string lowerFilter)
        {
            return ContainsProperty(security, "name", lowerFilter)
                || ContainsProperty(security, "nameClass", lowerFilter)
                || ContainsProperty(security, "nameFull", lowerFilter)
                || ContainsProperty(security, "nameId", lowerFilter);
        }

        private bool ContainsProperty(JsonElement security, string propertyName, string lowerFilter)
        {
            if (!security.TryGetProperty(propertyName, out JsonElement property))
            {
                return false;
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string value = property.GetString();
            return value != null && value.ToLowerInvariant().Contains(lowerFilter);
        }

        private List<MappingResult> SearchConnector(WikiSecurityFile file, string shortName, string query, bool exact)
        {
            List<MappingResult> results = new List<MappingResult>();
            string lowerQuery = query.ToLowerInvariant();
            string displayName = GetDisplayName(shortName);
            bool isTradingSupported = GetBoolMetadata(file.Metadata, "isTradingSupported");
            bool isDataFeedSupported = GetBoolMetadata(file.Metadata, "isDataFeedSupported");

            foreach (JsonElement security in file.Securities.EnumerateArray())
            {
                int priority = GetMatchPriority(security, lowerQuery, exact);

                if (priority < 0)
                {
                    continue;
                }

                results.Add(new MappingResult
                {
                    Connector = displayName,
                    ConnectorShort = shortName,
                    Security = security,
                    MatchPriority = priority,
                    IsTradingSupported = isTradingSupported,
                    IsDataFeedSupported = isDataFeedSupported
                });
            }

            return results;
        }

        private int GetMatchPriority(JsonElement security, string lowerQuery, bool exact)
        {
            string name = GetStringProperty(security, "name");
            string nameClass = GetStringProperty(security, "nameClass");
            string nameFull = GetStringProperty(security, "nameFull");
            string nameId = GetStringProperty(security, "nameId");

            if (exact)
            {
                return string.Equals(name, lowerQuery, StringComparison.OrdinalIgnoreCase) ? 0 : -1;
            }

            if (string.Equals(name, lowerQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string lowerName = name.ToLowerInvariant();

            if (lowerName.Contains(lowerQuery))
            {
                return 1;
            }

            if (nameClass.ToLowerInvariant().Contains(lowerQuery)
                || nameFull.ToLowerInvariant().Contains(lowerQuery)
                || nameId.ToLowerInvariant().Contains(lowerQuery))
            {
                return 2;
            }

            return -1;
        }

        private bool GetBoolMetadata(JsonElement metadata, string propertyName)
        {
            if (metadata.TryGetProperty("permissions", out JsonElement permissions)
                && permissions.ValueKind == JsonValueKind.Object
                && permissions.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            return false;
        }

        private string GetSecurityName(JsonElement security)
        {
            return GetStringProperty(security, "name");
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private int GetIntParameter(JsonElement parameters, string name, int defaultValue)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out int value))
            {
                return value;
            }

            return defaultValue;
        }

        private string NormalizeConnector(string connector)
        {
            if (ConnectorFileNames.ContainsKey(connector))
            {
                return ConnectorFileNames.Keys
                    .First(k => string.Equals(k, connector, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(connector, "MoexDataServer", StringComparison.OrdinalIgnoreCase))
            {
                return "moex_iss";
            }

            if (string.Equals(connector, "TInvest", StringComparison.OrdinalIgnoreCase))
            {
                return "tinvest";
            }

            if (string.Equals(connector, "Alor", StringComparison.OrdinalIgnoreCase))
            {
                return "alor";
            }

            if (string.Equals(connector, "QscalpMarketDepth", StringComparison.OrdinalIgnoreCase))
            {
                return "qscalp";
            }

            return connector.ToLowerInvariant();
        }

        private string GetDisplayName(string shortName)
        {
            return ConnectorDisplayNames.TryGetValue(shortName, out string displayName)
                ? displayName
                : shortName;
        }

        private string GetCollectedAt(JsonElement metadata)
        {
            if (metadata.TryGetProperty("collectedAt", out JsonElement collectedAt)
                && collectedAt.ValueKind == JsonValueKind.String)
            {
                return collectedAt.GetString();
            }

            return null;
        }

        private string GetStringParameter(JsonElement parameters, string name)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty(name, out JsonElement element)
                && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            return string.Empty;
        }

        private bool GetBoolParameter(JsonElement parameters, string name)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty(name, out JsonElement element)
                && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            {
                return element.GetBoolean();
            }

            return false;
        }

        private string GetWikiFilePath(string fileName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, "Wiki", fileName);
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion

        #region Nested types

        private class WikiSecurityFile : IDisposable
        {
            public JsonDocument MetadataDocument { get; }

            public JsonElement Metadata => MetadataDocument.RootElement;

            public JsonDocument SecuritiesDocument { get; }

            public JsonElement Securities => SecuritiesDocument.RootElement;

            public WikiSecurityFile(JsonDocument metadataDocument, JsonDocument securitiesDocument)
            {
                MetadataDocument = metadataDocument ?? throw new ArgumentNullException(nameof(metadataDocument));
                SecuritiesDocument = securitiesDocument ?? throw new ArgumentNullException(nameof(securitiesDocument));
            }

            public void Dispose()
            {
                MetadataDocument.Dispose();
                SecuritiesDocument.Dispose();
            }
        }

        private class MappingResult
        {
            public string Connector { get; set; }

            public string ConnectorShort { get; set; }

            public JsonElement Security { get; set; }

            public int MatchPriority { get; set; }

            public bool IsTradingSupported { get; set; }

            public bool IsDataFeedSupported { get; set; }
        }

        #endregion
    }
}
