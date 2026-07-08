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
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for dividend wiki reference methods.
    /// Reads pre-built markdown files from Wiki/Dividends and caches them in memory.
    /// </summary>
    public class WikiDividendsApi : IMcpToolProvider
    {
        #region Fields

        private static readonly object _cacheLocker = new object();
        private static Dictionary<string, DividendSecurityFile> _cache;
        private static bool _cacheLoaded;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Cache management

        /// <summary>
        /// Clears the in-memory cache of dividend files.
        /// Next call will reload files from disk.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLocker)
            {
                _cache = null;
                _cacheLoaded = false;
            }
        }

        #endregion

        #region IMcpToolProvider

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool
                {
                    Name = "wiki_dividends_get_history",
                    Description = "Get dividend payments with registry close date on or before the reference date",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            ticker = new
                            {
                                type = "string",
                                description = "Stock ticker, e.g. SBER"
                            },
                            date = new
                            {
                                type = "string",
                                description = "Reference date in dd.MM.yyyy format (default: today)"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload dividend files from disk"
                            }
                        },
                        required = new[] { "ticker" }
                    }
                },
                new McpTool
                {
                    Name = "wiki_dividends_get_future",
                    Description = "Get the nearest future dividend registry close date on or after the reference date",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            ticker = new
                            {
                                type = "string",
                                description = "Stock ticker, e.g. SBER"
                            },
                            date = new
                            {
                                type = "string",
                                description = "Reference date in dd.MM.yyyy format (default: today)"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload dividend files from disk"
                            }
                        },
                        required = new[] { "ticker" }
                    }
                },
                new McpTool
                {
                    Name = "wiki_dividends_get_past",
                    Description = "Get the nearest past dividend registry close date on or before the reference date",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            ticker = new
                            {
                                type = "string",
                                description = "Stock ticker, e.g. SBER"
                            },
                            date = new
                            {
                                type = "string",
                                description = "Reference date in dd.MM.yyyy format (default: today)"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload dividend files from disk"
                            }
                        },
                        required = new[] { "ticker" }
                    }
                },
                new McpTool
                {
                    Name = "wiki_dividends_search_by_date",
                    Description = "Search historical and future dividends for a Russian stock by registry close date",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            ticker = new
                            {
                                type = "string",
                                description = "Stock ticker, e.g. SBER"
                            },
                            date = new
                            {
                                type = "string",
                                description = "Registry close date in dd.MM.yyyy format"
                            },
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force reload dividend files from disk"
                            }
                        },
                        required = new[] { "ticker", "date" }
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
                    case "wiki_dividends_get_history":
                        response.Result = GetHistory(request.Params);
                        break;

                    case "wiki_dividends_get_future":
                        response.Result = GetFuture(request.Params);
                        break;

                    case "wiki_dividends_get_past":
                        response.Result = GetPast(request.Params);
                        break;

                    case "wiki_dividends_search_by_date":
                        response.Result = SearchByDate(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in wiki dividends API"
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

        private object GetHistory(JsonElement parameters)
        {
            string ticker = GetStringParameter(parameters, "ticker");
            string dateString = GetStringParameter(parameters, "date");
            bool refresh = GetBoolParameter(parameters, "refresh");

            DateTime date = ParseDate(dateString);
            if (date == DateTime.MinValue)
            {
                date = DateTime.Today;
            }

            DividendSecurityFile file = LoadFile(ticker, refresh);

            if (file == null)
            {
                return CreateEmptyHistoryResponse(ticker, date);
            }

            List<DividendRecord> historical = GetAllRecords(file)
                .Where(r => r.RegistryCloseDate.Date <= date.Date)
                .OrderBy(r => r.RegistryCloseDate)
                .ToList();

            return new
            {
                ticker = file.Security,
                date = date.ToString("dd.MM.yyyy"),
                source = file.Source,
                last_updated = file.LastUpdated,
                historical = historical.Select(ToApiRecord).ToList(),
                count = historical.Count
            };
        }

        private object GetFuture(JsonElement parameters)
        {
            string ticker = GetStringParameter(parameters, "ticker");
            string dateString = GetStringParameter(parameters, "date");
            bool refresh = GetBoolParameter(parameters, "refresh");

            DateTime date = ParseDate(dateString);
            if (date == DateTime.MinValue)
            {
                date = DateTime.Today;
            }

            DividendSecurityFile file = LoadFile(ticker, refresh);

            if (file == null)
            {
                return CreateEmptyFutureResponse(ticker, date);
            }

            DividendRecord future = GetAllRecords(file)
                .Where(r => r.RegistryCloseDate.Date >= date.Date)
                .OrderBy(r => r.RegistryCloseDate)
                .FirstOrDefault();

            return new
            {
                ticker = file.Security,
                date = date.ToString("dd.MM.yyyy"),
                source = file.Source,
                last_updated = file.LastUpdated,
                future = future != null ? ToApiRecord(future) : null
            };
        }

        private List<DividendRecord> GetAllRecords(DividendSecurityFile file)
        {
            List<DividendRecord> records = new List<DividendRecord>(file.Historical);
            records.AddRange(file.Future);
            return records;
        }

        private object CreateEmptyHistoryResponse(string ticker, DateTime date)
        {
            return new
            {
                ticker = ticker,
                date = date.ToString("dd.MM.yyyy"),
                source = string.Empty,
                last_updated = string.Empty,
                historical = new List<object>(),
                count = 0
            };
        }

        private object CreateEmptyFutureResponse(string ticker, DateTime date)
        {
            return new
            {
                ticker = ticker,
                date = date.ToString("dd.MM.yyyy"),
                source = string.Empty,
                last_updated = string.Empty,
                future = (object)null
            };
        }

        private object CreateEmptyPastResponse(string ticker, DateTime date)
        {
            return new
            {
                ticker = ticker,
                date = date.ToString("dd.MM.yyyy"),
                source = string.Empty,
                last_updated = string.Empty,
                past = (object)null
            };
        }

        private object CreateEmptySearchResponse(string ticker, DateTime date)
        {
            return new
            {
                ticker = ticker,
                date = date.ToString("dd.MM.yyyy"),
                matches = new List<object>(),
                count = 0
            };
        }

        private object GetPast(JsonElement parameters)
        {
            string ticker = GetStringParameter(parameters, "ticker");
            string dateString = GetStringParameter(parameters, "date");
            bool refresh = GetBoolParameter(parameters, "refresh");

            DateTime date = ParseDate(dateString);
            if (date == DateTime.MinValue)
            {
                date = DateTime.Today;
            }

            DividendSecurityFile file = LoadFile(ticker, refresh);

            if (file == null)
            {
                return CreateEmptyPastResponse(ticker, date);
            }

            DividendRecord past = GetAllRecords(file)
                .Where(r => r.RegistryCloseDate.Date <= date.Date)
                .OrderByDescending(r => r.RegistryCloseDate)
                .FirstOrDefault();

            return new
            {
                ticker = file.Security,
                date = date.ToString("dd.MM.yyyy"),
                source = file.Source,
                last_updated = file.LastUpdated,
                past = past != null ? ToApiRecord(past) : null
            };
        }

        private object SearchByDate(JsonElement parameters)
        {
            string ticker = GetStringParameter(parameters, "ticker");
            string dateString = GetStringParameter(parameters, "date");
            bool refresh = GetBoolParameter(parameters, "refresh");

            if (string.IsNullOrWhiteSpace(dateString))
            {
                throw new ArgumentException("Parameter 'date' is required");
            }

            DateTime searchDate = ParseDate(dateString);
            if (searchDate == DateTime.MinValue)
            {
                throw new ArgumentException($"Invalid date format '{dateString}'. Expected dd.MM.yyyy");
            }

            DividendSecurityFile file = LoadFile(ticker, refresh);

            if (file == null)
            {
                return CreateEmptySearchResponse(ticker, searchDate);
            }

            List<DividendRecord> matches = GetAllRecords(file)
                .Where(r => r.RegistryCloseDate.Date == searchDate.Date)
                .ToList();

            return new
            {
                ticker = file.Security,
                date = searchDate.ToString("dd.MM.yyyy"),
                matches = matches.Select(ToApiRecord).ToList(),
                count = matches.Count
            };
        }

        private DividendSecurityFile LoadFile(string ticker, bool refresh)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Parameter 'ticker' is required");
            }

            EnsureCacheLoaded(refresh);

            string normalizedTicker = ticker.Trim().ToUpperInvariant();

            lock (_cacheLocker)
            {
                if (_cache.TryGetValue(normalizedTicker, out DividendSecurityFile cachedFile))
                {
                    return cachedFile;
                }
            }

            string filePath = GetDividendFilePath(normalizedTicker);

            if (!File.Exists(filePath))
            {
                return null;
            }

            DividendSecurityFile file = ParseMarkdownFile(filePath);

            lock (_cacheLocker)
            {
                _cache[normalizedTicker] = file;
            }

            return file;
        }

        private void EnsureCacheLoaded(bool refresh)
        {
            lock (_cacheLocker)
            {
                if (!refresh && _cacheLoaded)
                {
                    return;
                }

                _cache = new Dictionary<string, DividendSecurityFile>(StringComparer.OrdinalIgnoreCase);
                _cacheLoaded = true;

                string folderPath = GetDividendsFolderPath();

                if (!Directory.Exists(folderPath))
                {
                    SendLog($"wiki_dividends: folder not found {folderPath}", LogMessageType.Error);
                    return;
                }

                string[] files = Directory.GetFiles(folderPath, "*.md");

                foreach (string file in files)
                {
                    try
                    {
                        DividendSecurityFile dividendFile = ParseMarkdownFile(file);

                        if (string.IsNullOrWhiteSpace(dividendFile.Security))
                        {
                            continue;
                        }

                        _cache[dividendFile.Security] = dividendFile;
                    }
                    catch (Exception ex)
                    {
                        SendLog($"wiki_dividends: failed to load {file}: {ex.Message}", LogMessageType.Error);
                    }
                }
            }
        }

        private DividendSecurityFile ParseMarkdownFile(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = content.TrimStart('\uFEFF');

            DividendSecurityFile file = new DividendSecurityFile
            {
                Security = ExtractMetadataValue(content, "Security"),
                LastUpdated = ExtractMetadataValue(content, "LastUpdated"),
                Source = ExtractMetadataValue(content, "Source")
            };

            string historicalSection = ExtractSection(content, "Historical Dividends");
            string futureSection = ExtractSection(content, "Future Registry Close Dates");

            file.Historical = ParseTable(historicalSection);
            file.Future = ParseTable(futureSection);

            return file;
        }

        private string ExtractMetadataValue(string content, string fieldName)
        {
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (!trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = trimmed
                    .Trim('|')
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length >= 2
                    && string.Equals(parts[0], fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
            }

            return string.Empty;
        }

        private string ExtractSection(string content, string sectionTitle)
        {
            int titleIndex = content.IndexOf($"## {sectionTitle}", StringComparison.OrdinalIgnoreCase);

            if (titleIndex < 0)
            {
                return string.Empty;
            }

            int startIndex = content.IndexOf('\n', titleIndex);

            if (startIndex < 0)
            {
                return string.Empty;
            }

            startIndex++;

            int nextTitleIndex = content.IndexOf("## ", startIndex, StringComparison.OrdinalIgnoreCase);

            if (nextTitleIndex < 0)
            {
                return content.Substring(startIndex).Trim();
            }

            return content.Substring(startIndex, nextTitleIndex - startIndex).Trim();
        }

        private List<DividendRecord> ParseTable(string section)
        {
            List<DividendRecord> records = new List<DividendRecord>();

            if (string.IsNullOrWhiteSpace(section))
            {
                return records;
            }

            string[] lines = section
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            int dataStartIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("|", StringComparison.Ordinal)
                    && lines[i].Contains("RegistryCloseDate", StringComparison.OrdinalIgnoreCase))
                {
                    dataStartIndex = i + 2;
                    break;
                }
            }

            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                string line = lines[i];

                if (!line.StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] cells = line
                    .Trim('|')
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();

                if (cells.Length < 4)
                {
                    continue;
                }

                DividendRecord record = new DividendRecord
                {
                    Year = ParseInt(cells[0]),
                    RegistryCloseDate = ParseDate(cells[1]),
                    DividendAmount = ParseDecimal(cells[2]),
                    DividendYield = ParseDecimal(cells[3])
                };

                if (record.RegistryCloseDate == DateTime.MinValue)
                {
                    continue;
                }

                records.Add(record);
            }

            return records;
        }

        private object ToApiRecord(DividendRecord record)
        {
            return new
            {
                year = record.Year,
                registry_close_date = record.RegistryCloseDate.ToString("dd.MM.yyyy"),
                dividend_amount = record.DividendAmount,
                dividend_yield = record.DividendYield
            };
        }

        private DateTime ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            value = value.Trim();

            if (DateTime.TryParseExact(value, "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result))
            {
                return result;
            }

            return DateTime.MinValue;
        }

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            value = value.Trim();

            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            return 0;
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            value = value.Trim().Replace("%", "").Replace("₽", "").Replace("$", "");
            value = value.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
            value = value.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            return 0;
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

        private string GetDividendFilePath(string ticker)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, "Wiki", "Dividends", $"{ticker}.md");
        }

        private string GetDividendsFolderPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, "Wiki", "Dividends");
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion

        #region Nested types

        private class DividendSecurityFile
        {
            public string Security { get; set; }

            public string LastUpdated { get; set; }

            public string Source { get; set; }

            public List<DividendRecord> Historical { get; set; }

            public List<DividendRecord> Future { get; set; }

            public DividendSecurityFile()
            {
                Security = string.Empty;
                LastUpdated = string.Empty;
                Source = string.Empty;
                Historical = new List<DividendRecord>();
                Future = new List<DividendRecord>();
            }
        }

        private class DividendRecord
        {
            public int Year { get; set; }

            public DateTime RegistryCloseDate { get; set; }

            public decimal DividendAmount { get; set; }

            public decimal DividendYield { get; set; }
        }

        #endregion
    }
}
