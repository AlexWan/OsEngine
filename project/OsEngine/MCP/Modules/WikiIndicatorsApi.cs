/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for indicator wiki reference methods.
    /// </summary>
    public class WikiIndicatorsApi : IMcpToolProvider
    {
        #region Fields

        private const string DescriptionFileName = "IndicatorsDescription.json";
        private readonly object _cacheLocker = new object();

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
                    Name = "wiki_indicators_list",
                    Description = "List available indicators with description, parameter count and series count",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            refresh = new
                            {
                                type = "boolean",
                                description = "Force rebuild all descriptions from live instances"
                            },
                            location = new
                            {
                                type = "string",
                                description = "Filter by location: All, Include, Script"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_indicator_info",
                    Description = "Get detailed information about a single indicator including parameters and data series",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            class_name = new
                            {
                                type = "string",
                                description = "Indicator class name or script file name without extension"
                            },
                            is_script = new
                            {
                                type = "boolean",
                                description = "True if the indicator is a script from Custom/Indicators/Scripts"
                            }
                        },
                        required = new[] { "class_name" }
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
                    case "wiki_indicators_list":
                        response.Result = GetIndicatorsList(request.Params);
                        break;

                    case "wiki_indicator_info":
                        response.Result = GetIndicatorInfo(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in wiki indicators API"
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

        private object GetIndicatorsList(JsonElement parameters)
        {
            bool refresh = false;
            string locationFilter = "All";

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("refresh", out JsonElement refreshEl)
                    && refreshEl.ValueKind == JsonValueKind.True)
                {
                    refresh = true;
                }

                if (parameters.TryGetProperty("location", out JsonElement locationEl)
                    && locationEl.ValueKind == JsonValueKind.String)
                {
                    locationFilter = locationEl.GetString();
                }
            }

            List<string> names = IndicatorsFactory.GetIndicatorsNames()
                .Where(n => !n.Equals("EmptyIndicator", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n)
                .ToList();

            Dictionary<string, IndicatorDescriptionCache> cache = refresh
                ? new Dictionary<string, IndicatorDescriptionCache>(StringComparer.OrdinalIgnoreCase)
                : ReadCache();

            List<object> result = new List<object>();
            HashSet<string> processedClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool cacheUpdated = false;

            foreach (string className in names)
            {
                if (!IsKnownIndicator(className, out bool isScript))
                {
                    continue;
                }

                processedClassNames.Add(className);

                string location = isScript ? "Script" : "Include";

                if (locationFilter != "All" && location != locationFilter)
                {
                    continue;
                }

                IndicatorDescriptionCache description = null;
                string error = null;

                if (!refresh && cache.TryGetValue(className, out description) && description != null)
                {
                    // Use cached description.
                }
                else
                {
                    try
                    {
                        description = BuildDescription(className, isScript);
                        if (description != null)
                        {
                            cache[className] = description;
                            cacheUpdated = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        SendLog($"wiki_indicators_list: failed to describe '{className}': {ex}", LogMessageType.Error);
                    }
                }

                result.Add(ToListItem(description, className, location, error));
            }

            // Remove stale entries that are no longer valid indicators (e.g. readme files in Dlls folders).
            List<string> staleKeys = cache.Keys
                .Where(k => !processedClassNames.Contains(k))
                .ToList();

            foreach (string staleKey in staleKeys)
            {
                cache.Remove(staleKey);
                cacheUpdated = true;
            }

            if (cacheUpdated)
            {
                SaveCache(cache.Values.ToList());
            }

            return new { indicators = result };
        }

        private object GetIndicatorInfo(JsonElement parameters)
        {
            string className = null;
            bool? isScript = null;

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("class_name", out JsonElement classNameEl)
                    && classNameEl.ValueKind == JsonValueKind.String)
                {
                    className = classNameEl.GetString();
                }

                if (parameters.TryGetProperty("is_script", out JsonElement isScriptEl)
                    && (isScriptEl.ValueKind == JsonValueKind.True || isScriptEl.ValueKind == JsonValueKind.False))
                {
                    isScript = isScriptEl.GetBoolean();
                }
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("Parameter 'class_name' is required");
            }

            bool resolvedScript;

            if (isScript.HasValue)
            {
                resolvedScript = isScript.Value;

                if (!IsKnownIndicator(className, out bool actualScript) || actualScript != resolvedScript)
                {
                    throw new InvalidOperationException($"Indicator '{className}' not found at requested location");
                }
            }
            else
            {
                if (!IsKnownIndicator(className, out resolvedScript))
                {
                    throw new InvalidOperationException($"Indicator '{className}' not found");
                }
            }

            Aindicator indicator = IndicatorsFactory.CreateIndicatorByName(
                className, "", false, StartProgram.IsOsOptimizer);

            if (indicator == null)
            {
                throw new InvalidOperationException($"Indicator '{className}' could not be instantiated");
            }

            try
            {
                return new
                {
                    class_name = className,
                    display_name = GetDisplayName(indicator, className),
                    location = resolvedScript ? "Script" : "Include",
                    description = indicator.Description,
                    parameters = indicator.Parameters.Select(SerializeParameter).ToList(),
                    series = indicator.DataSeries.Select(SerializeSeries).ToList()
                };
            }
            finally
            {
                indicator.Delete();
            }
        }

        private IndicatorDescriptionCache BuildDescription(string className, bool isScript)
        {
            Aindicator indicator = IndicatorsFactory.CreateIndicatorByName(
                className, "", false, StartProgram.IsOsOptimizer);

            if (indicator == null)
            {
                throw new InvalidOperationException($"Could not instantiate indicator '{className}'");
            }

            try
            {
                return new IndicatorDescriptionCache
                {
                    ClassName = className,
                    DisplayName = GetDisplayName(indicator, className),
                    Location = isScript ? "Script" : "Include",
                    Description = indicator.Description,
                    ParameterCount = indicator.Parameters.Count,
                    SeriesCount = indicator.DataSeries.Count
                };
            }
            finally
            {
                indicator.Delete();
            }
        }

        private static string GetDisplayName(Aindicator indicator, string className)
        {
            Type type = indicator.GetType();
            IndicatorAttribute attribute = type.GetCustomAttribute<IndicatorAttribute>();
            return attribute?.Name ?? className;
        }

        private static bool IsKnownIndicator(string className, out bool isScript)
        {
            if (IndicatorsFactory.Indicators.ContainsKey(className))
            {
                isScript = false;
                return true;
            }

            if (IsScriptFileExists(className))
            {
                isScript = true;
                return true;
            }

            isScript = false;
            return false;
        }

        private static bool IsScriptFileExists(string className)
        {
            const string scriptFolderPath = @"Custom\Indicators\Scripts";

            if (!Directory.Exists(scriptFolderPath))
            {
                return false;
            }

            string csName = className + ".cs";
            string txtName = className + ".txt";

            foreach (string file in Directory.GetFiles(scriptFolderPath, "*.*", SearchOption.AllDirectories))
            {
                // Skip service files inside "Dlls" folders — they are not indicator scripts.
                if (IsInDllsFolder(file))
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);
                if (fileName.Equals(csName, StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals(txtName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInDllsFolder(string filePath)
        {
            string[] parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part => part.Equals("Dlls", StringComparison.OrdinalIgnoreCase));
        }

        private object ToListItem(IndicatorDescriptionCache description, string className, string location, string error)
        {
            if (description == null)
            {
                return new
                {
                    class_name = className,
                    display_name = className,
                    location,
                    description = (string)null,
                    parameters_count = 0,
                    series_count = 0,
                    error = error ?? "unknown error"
                };
            }

            return new
            {
                class_name = description.ClassName,
                display_name = description.DisplayName,
                location = description.Location,
                description = description.Description,
                parameters_count = description.ParameterCount,
                series_count = description.SeriesCount,
                error = (string)null
            };
        }

        private Dictionary<string, IndicatorDescriptionCache> ReadCache()
        {
            Dictionary<string, IndicatorDescriptionCache> cache =
                new Dictionary<string, IndicatorDescriptionCache>(StringComparer.OrdinalIgnoreCase);

            lock (_cacheLocker)
            {
                if (!File.Exists(DescriptionFileName))
                {
                    return cache;
                }

                try
                {
                    string json = File.ReadAllText(DescriptionFileName);
                    List<IndicatorDescriptionCache> descriptions =
                        JsonSerializer.Deserialize<List<IndicatorDescriptionCache>>(json);

                    if (descriptions != null)
                    {
                        foreach (IndicatorDescriptionCache description in descriptions)
                        {
                            if (!string.IsNullOrWhiteSpace(description.ClassName))
                            {
                                cache[description.ClassName] = description;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLog($"wiki_indicators_list: failed to read {DescriptionFileName}: {ex.Message}", LogMessageType.Error);
                }
            }

            return cache;
        }

        private void SaveCache(List<IndicatorDescriptionCache> descriptions)
        {
            lock (_cacheLocker)
            {
                try
                {
                    List<IndicatorDescriptionCache> ordered = descriptions
                        .OrderBy(d => d.ClassName)
                        .ToList();

                    string json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });

                    File.WriteAllText(DescriptionFileName, json);
                }
                catch (Exception ex)
                {
                    SendLog($"wiki_indicators_list: failed to write {DescriptionFileName}: {ex.Message}", LogMessageType.Error);
                }
            }
        }

        private object SerializeParameter(IndicatorParameter parameter)
        {
            string name = parameter.Name;
            string type = parameter.Type.ToString();

            switch (parameter)
            {
                case IndicatorParameterInt p:
                    return new
                    {
                        name,
                        type,
                        value = p.ValueInt,
                        default_value = p.ValueIntDefault
                    };

                case IndicatorParameterDecimal p:
                    return new
                    {
                        name,
                        type,
                        value = p.ValueDecimal,
                        default_value = p.ValueDecimalDefault
                    };

                case IndicatorParameterBool p:
                    return new
                    {
                        name,
                        type,
                        value = p.ValueBool,
                        default_value = p.ValueBoolDefault
                    };

                case IndicatorParameterString p:
                    return new
                    {
                        name,
                        type,
                        value = p.ValueString,
                        default_value = p.ValueStringDefault,
                        values = p.ValuesString
                    };

                default:
                    return new { name, type };
            }
        }

        private object SerializeSeries(IndicatorDataSeries series)
        {
            return new
            {
                name = series.Name,
                type = series.ChartPaintType.ToString(),
                color = ColorTranslator.ToHtml(series.Color),
                is_paint = series.IsPaint,
                can_rebuild = series.CanReBuildHistoricalValues
            };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion

        #region Nested types

        private class IndicatorDescriptionCache
        {
            public string ClassName { get; set; }
            public string DisplayName { get; set; }
            public string Location { get; set; }
            public string Description { get; set; }
            public int ParameterCount { get; set; }
            public int SeriesCount { get; set; }
        }

        #endregion
    }
}
