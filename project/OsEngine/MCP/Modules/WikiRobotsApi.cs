/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.MCP.Json;
using OsEngine.MCP.Modules;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for robot wiki reference methods.
    /// </summary>
    public class WikiRobotsApi : IMcpToolProvider
    {
        #region Fields

        private const string DescriptionFileName = "BotsDescription.txt";
        private readonly object _descriptionFileLocker = new object();

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
                    Name = "wiki_robots_list",
                    Description = "List available robots with description, sources and indicators",
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
                            },
                            include_engines = new
                            {
                                type = "boolean",
                                description = "Include Engine, ScreenerEngine and ClusterEngine robots"
                            }
                        },
                        required = new string[0]
                    }
                },
                new McpTool
                {
                    Name = "wiki_robot_info",
                    Description = "Get detailed information about a single robot including parameters",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            class_name = new
                            {
                                type = "string",
                                description = "Robot class name or script file name without extension"
                            },
                            is_script = new
                            {
                                type = "boolean",
                                description = "True if the robot is a script from Custom/Robots"
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
            var response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "wiki_robots_list":
                        response.Result = GetRobotsList(request.Params);
                        break;

                    case "wiki_robot_info":
                        response.Result = GetRobotInfo(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in wiki robots API"
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

        private object GetRobotsList(JsonElement parameters)
        {
            bool refresh = false;
            string locationFilter = "All";
            bool includeEngines = true;

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("refresh", out var refreshEl)
                    && refreshEl.ValueKind == JsonValueKind.True)
                {
                    refresh = true;
                }

                if (parameters.TryGetProperty("location", out var locationEl)
                    && locationEl.ValueKind == JsonValueKind.String)
                {
                    locationFilter = locationEl.GetString();
                }

                if (parameters.TryGetProperty("include_engines", out var enginesEl)
                    && enginesEl.ValueKind == JsonValueKind.False)
                {
                    includeEngines = false;
                }
            }

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();
            List<string> scriptNames = BotFactory.GetScriptsNamesStrategy();

            HashSet<string> scriptSet = new HashSet<string>(scriptNames, StringComparer.OrdinalIgnoreCase);
            includeNames = includeNames
                .Where(n => !scriptSet.Contains(n))
                .ToList();

            if (!includeEngines)
            {
                includeNames = includeNames
                    .Where(n => n != "Engine" && n != "ScreenerEngine" && n != "ClusterEngine")
                    .ToList();
            }

            HashSet<string> includeSet = new HashSet<string>(includeNames, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, BotDescription> descriptionsByClass =
                new Dictionary<string, BotDescription>(StringComparer.OrdinalIgnoreCase);

            if (!refresh)
            {
                foreach (BotDescription cached in ReadDescriptionsFromFile())
                {
                    if (!string.IsNullOrWhiteSpace(cached.ClassName))
                    {
                        descriptionsByClass[cached.ClassName] = cached;
                    }
                }
            }

            List<object> result = new List<object>();
            bool cacheUpdated = false;

            foreach (string className in includeNames.Concat(scriptNames).OrderBy(n => n))
            {
                bool isScript = scriptSet.Contains(className);
                BotCreationType location = isScript ? BotCreationType.Script : BotCreationType.Include;

                if (locationFilter != "All" && location.ToString() != locationFilter)
                {
                    continue;
                }

                BotDescription description = null;
                string error = null;

                if (!refresh && descriptionsByClass.TryGetValue(className, out description) && description != null)
                {
                    // Use cached description.
                }
                else
                {
                    try
                    {
                        description = BuildBotDescription(className, isScript);
                        if (description != null)
                        {
                            descriptionsByClass[className] = description;
                            cacheUpdated = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        SendLog($"wiki_robots_list: failed to describe '{className}': {ex}", LogMessageType.Error);
                    }
                }

                result.Add(ToListItem(description, className, location, error));
            }

            if (cacheUpdated)
            {
                SaveDescriptionsToFile(descriptionsByClass.Values.ToList());
            }

            return new { robots = result };
        }

        private object GetRobotInfo(JsonElement parameters)
        {
            string className = null;
            bool? isScript = null;

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("class_name", out var classNameEl)
                    && classNameEl.ValueKind == JsonValueKind.String)
                {
                    className = classNameEl.GetString();
                }

                if (parameters.TryGetProperty("is_script", out var isScriptEl)
                    && (isScriptEl.ValueKind == JsonValueKind.True || isScriptEl.ValueKind == JsonValueKind.False))
                {
                    isScript = isScriptEl.GetBoolean();
                }
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("Parameter 'class_name' is required");
            }

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();
            List<string> scriptNames = BotFactory.GetScriptsNamesStrategy();

            HashSet<string> includeSet = new HashSet<string>(includeNames, StringComparer.OrdinalIgnoreCase);
            HashSet<string> scriptSet = new HashSet<string>(scriptNames, StringComparer.OrdinalIgnoreCase);

            bool resolvedScript;

            if (isScript.HasValue)
            {
                resolvedScript = isScript.Value;

                if (resolvedScript && !scriptSet.Contains(className))
                {
                    throw new InvalidOperationException($"Robot '{className}' not found in scripts");
                }

                if (!resolvedScript && !includeSet.Contains(className))
                {
                    throw new InvalidOperationException($"Robot '{className}' not found");
                }
            }
            else
            {
                if (includeSet.Contains(className))
                {
                    resolvedScript = false;
                }
                else if (scriptSet.Contains(className))
                {
                    resolvedScript = true;
                }
                else
                {
                    throw new InvalidOperationException($"Robot '{className}' not found");
                }
            }

            BotPanel bot = BotFactory.GetStrategyForName(className, "", StartProgram.IsTester, resolvedScript);

            if (bot == null)
            {
                throw new InvalidOperationException($"Robot '{className}' could not be instantiated");
            }

            try
            {
                return new
                {
                    class_name = className,
                    location = resolvedScript
                        ? BotCreationType.Script.ToString()
                        : BotCreationType.Include.ToString(),
                    description = bot.Description,
                    sources = GetSourcesFromBot(bot).Select(ParseSource).ToList(),
                    indicators = GetIndicatorsFromBot(bot),
                    parameters = bot.Parameters.Select(ConvertParameter).ToList()
                };
            }
            finally
            {
                bot.Delete();
            }
        }

        private BotDescription BuildBotDescription(string className, bool isScript)
        {
            BotPanel bot = BotFactory.GetStrategyForName(className, "", StartProgram.IsTester, isScript);
            if (bot == null)
            {
                throw new InvalidOperationException($"Could not instantiate robot '{className}'");
            }

            try
            {
                return new BotDescription
                {
                    ClassName = className,
                    Description = bot.Description,
                    Location = isScript ? BotCreationType.Script : BotCreationType.Include,
                    Sources = GetSourcesFromBot(bot),
                    Indicators = GetIndicatorsFromBot(bot)
                };
            }
            finally
            {
                bot.Delete();
            }
        }

        private List<string> GetSourcesFromBot(BotPanel bot)
        {
            List<string> sourcesList = new List<string>();

            if (bot.TabsSimple != null && bot.TabsSimple.Count > 0)
            {
                sourcesList.Add(BotTabType.Simple + " " + bot.TabsSimple.Count);
            }

            if (bot.TabsIndex != null && bot.TabsIndex.Count > 0)
            {
                sourcesList.Add(BotTabType.Index + " " + bot.TabsIndex.Count);
            }

            if (bot.TabsCluster != null && bot.TabsCluster.Count > 0)
            {
                sourcesList.Add(BotTabType.Cluster + " " + bot.TabsCluster.Count);
            }

            if (bot.TabsPair != null && bot.TabsPair.Count > 0)
            {
                sourcesList.Add(BotTabType.Pair + " " + bot.TabsPair.Count);
            }

            if (bot.TabsScreener != null && bot.TabsScreener.Count > 0)
            {
                sourcesList.Add(BotTabType.Screener + " " + bot.TabsScreener.Count);
            }

            if (bot.TabsPolygon != null && bot.TabsPolygon.Count > 0)
            {
                sourcesList.Add(BotTabType.Polygon + " " + bot.TabsPolygon.Count);
            }

            if (bot.TabsNews != null && bot.TabsNews.Count > 0)
            {
                sourcesList.Add(BotTabType.News + " " + bot.TabsNews.Count);
            }

            return sourcesList;
        }

        private List<string> GetIndicatorsFromBot(BotPanel bot)
        {
            List<string> indicators = new List<string>();

            if (bot.TabsSimple != null && bot.TabsSimple.Count > 0)
            {
                for (int i = 0; i < bot.TabsSimple.Count; i++)
                {
                    BotTabSimple tab = bot.TabsSimple[i];
                    if (tab.Indicators == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < tab.Indicators.Count; j++)
                    {
                        indicators.Add(tab.Indicators[j].GetType().Name);
                    }
                }
            }

            if (bot.TabsIndex != null && bot.TabsIndex.Count > 0)
            {
                for (int i = 0; i < bot.TabsIndex.Count; i++)
                {
                    BotTabIndex tab = bot.TabsIndex[i];
                    if (tab.Indicators == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < tab.Indicators.Count; j++)
                    {
                        indicators.Add(tab.Indicators[j].GetType().Name);
                    }
                }
            }

            if (bot.TabsScreener != null && bot.TabsScreener.Count > 0)
            {
                for (int i = 0; i < bot.TabsScreener.Count; i++)
                {
                    BotTabScreener tab = bot.TabsScreener[i];
                    if (tab._indicators == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < tab._indicators.Count; j++)
                    {
                        indicators.Add(tab._indicators[j].Type);
                    }
                }
            }

            return indicators;
        }

        private object ParseSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return new { type = "Unknown", count = 0 };
            }

            string[] parts = source.Split(new[] { ' ' }, 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out int count))
            {
                return new { type = parts[0], count };
            }

            return new { type = source, count = 1 };
        }

        private object ToListItem(BotDescription description, string className, BotCreationType location, string error)
        {
            if (description == null)
            {
                return new
                {
                    class_name = className,
                    location = location.ToString(),
                    description = (string)null,
                    sources = new List<object>(),
                    indicators = new List<string>(),
                    error = error ?? "unknown error"
                };
            }

            return new
            {
                class_name = description.ClassName,
                location = description.Location.ToString(),
                description = description.Description,
                sources = description.Sources.Select(ParseSource).ToList(),
                indicators = description.Indicators,
                error = (string)null
            };
        }

        private List<BotDescription> ReadDescriptionsFromFile()
        {
            List<BotDescription> descriptions = new List<BotDescription>();

            lock (_descriptionFileLocker)
            {
                if (!File.Exists(DescriptionFileName))
                {
                    return descriptions;
                }

                try
                {
                    string[] lines = File.ReadAllLines(DescriptionFileName);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        BotDescription description = new BotDescription();
                        try
                        {
                            description.LoadFromSaveStr(line);
                            descriptions.Add(description);
                        }
                        catch
                        {
                            // Ignore malformed lines.
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLog($"wiki_robots_list: failed to read {DescriptionFileName}: {ex.Message}", LogMessageType.Error);
                }
            }

            return descriptions;
        }

        private void SaveDescriptionsToFile(List<BotDescription> descriptions)
        {
            lock (_descriptionFileLocker)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(DescriptionFileName, false))
                    {
                        foreach (BotDescription description in descriptions.OrderBy(d => d.ClassName))
                        {
                            writer.WriteLine(description.GetStringToSave());
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLog($"wiki_robots_list: failed to write {DescriptionFileName}: {ex.Message}", LogMessageType.Error);
                }
            }
        }

        private object ConvertParameter(IIStrategyParameter parameter)
        {
            string name = parameter.Name;
            string type = parameter.Type.ToString();
            string tabName = parameter.TabName;

            switch (parameter)
            {
                case StrategyParameterInt p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        value = p.ValueInt,
                        default_value = p.ValueIntDefolt,
                        start = p.ValueIntStart,
                        stop = p.ValueIntStop,
                        step = p.ValueIntStep,
                        step_type = p.StepType.ToString()
                    };

                case StrategyParameterDecimal p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        value = p.ValueDecimal,
                        default_value = p.ValueDecimalDefolt,
                        start = p.ValueDecimalStart,
                        stop = p.ValueDecimalStop,
                        step = p.ValueDecimalStep,
                        step_type = p.StepType.ToString()
                    };

                case StrategyParameterDecimalCheckBox p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        value = p.ValueDecimal,
                        default_value = p.ValueDecimalDefolt,
                        start = p.ValueDecimalStart,
                        stop = p.ValueDecimalStop,
                        step = p.ValueDecimalStep,
                        step_type = p.StepType.ToString(),
                        is_checked = p.CheckState == CheckState.Checked
                    };

                case StrategyParameterString p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        value = p.ValueString,
                        values = p.ValuesString
                    };

                case StrategyParameterBool p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        value = p.ValueBool,
                        default_value = p.ValueBoolDefolt
                    };

                case StrategyParameterTimeOfDay p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        hour = p.Value.Hour,
                        minute = p.Value.Minute,
                        second = p.Value.Second,
                        millisecond = p.Value.Millisecond
                    };

                case StrategyParameterCheckBox p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        is_checked = p.CheckState == CheckState.Checked
                    };

                case StrategyParameterButton p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName
                    };

                case StrategyParameterLabel p:
                    return new
                    {
                        name,
                        type,
                        tab_name = tabName,
                        label = p.Label,
                        value = p.Value
                    };

                default:
                    return new { name, type, tab_name = tabName };
            }
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
