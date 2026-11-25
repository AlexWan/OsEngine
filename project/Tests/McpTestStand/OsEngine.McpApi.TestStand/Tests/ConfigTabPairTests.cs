/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OsEngine.McpApi.TestStand;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Модуль 24 — ConfigTabPair (FEATURE-0141): контракт bot_get/bot_set_config_tab_pair
    /// на живой сессии (-testerlight): tools/list и схема set, ключ pair_num и
    /// create_pair=max+1 (не Count), round-trip всех полей с pair-level флагами,
    /// негативы без изменения состояния, отказ events_is_on=false на Tester,
    /// симметрия текстов ошибок V1/V2 (не default-роут), регресс-пин реестра.
    /// </summary>
    public class ConfigTabPairTests
    {
        private const string Module = "CONFIG_TAB_PAIR";

        private const string Bot = "pair_mcp";
        private const string Tab = "pair_mcptab0";
        private const string FixturePairName = "pair_mcptab07";
        private const int FixturePairNum = 7;

        private readonly TestContext _context;

        public ConfigTabPairTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestFixtureAndBot();
            TestToolsListPresence();
            TestSetSchemaShape();
            TestCreatePairNumMaxPlusOne();
            TestRoundTripAllFields();
            TestPairStructure();
            TestNegativeLegFieldWithoutKey();
            TestNegativePairName();
            TestNegativeBothKeys();
            TestNegativeEnumPlusOne();
            TestNegativeKinds();
            TestNegativeEmptySecurityName();
            TestNegativeDedupSameClass();
            TestNegativeDedupSwap();
            TestNegativeUnknownPairNum();
            TestNegativeCreatePairEventsFalseRejected();
            TestV1V2ErrorTextEqual();
            TestRegistryRegression();
            TestCleanup();
        }

        #region Setup / structure tests

        private void TestFixtureAndBot()
        {
            const string method = "pair_fixture_bootstrap";

            try
            {
                DeleteBotQuietly();
                DeleteFixtureFiles();

                string engine = EngineDirectory();
                Directory.CreateDirectory(engine);

                File.WriteAllLines(Path.Combine(engine, Tab + "StandartPairsSettings.txt"), new[]
                {
                    "0", "7", "0", "7", "50", "1", "50", "No",
                    "Percent", "Currency", "Percent", "Currency",
                    "True", "False",
                    "Off", "Off",
                    "True", "True"
                });

                File.WriteAllLines(Path.Combine(engine, Tab + "PairsNamesToLoad.txt"), new[] { FixturePairName });

                File.WriteAllLines(Path.Combine(engine, FixturePairName + "PairsSettings.txt"), new[]
                {
                    FixturePairNum.ToString(), "0", "7", "0", "7", "50", "1", "50",
                    "Percent", "Currency", "Percent", "Currency",
                    "Off", "Off", "No", "True", "True", "True"
                });

                string created = Call("bot_create", new { strategy_name = "PairCorrelationTrader", name = Bot });
                bool createdOk = !IsError(created);

                string state = GetInner(Get("bot_get_config_tab_pair", new { bot_id = Bot, tab_name = Tab }));
                int count = 0;
                bool hasFixturePair = false;
                bool legsEmpty = false;

                if (createdOk && TryParseInner(state, out JsonElement root))
                {
                    count = GetInt(root, "count");
                    hasFixturePair = HasPairNum(root, FixturePairNum);

                    if (root.TryGetProperty("pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement pair in pairs.EnumerateArray())
                        {
                            legsEmpty = GetPropString(pair, "sec1_security_name").Length == 0;
                        }
                    }
                }

                if (createdOk && count == 1 && hasFixturePair && legsEmpty)
                {
                    _context.RecordPass(Module, method, $"бот {Bot} поднят, пара pair_num={FixturePairNum} из фикстуры, плечи пустые (get не падает при Security==null)");
                }
                else
                {
                    _context.RecordFail(Module, method, $"created={createdOk}, count={count}, fixturePair={hasFixturePair}, legsEmpty={legsEmpty}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestToolsListPresence()
        {
            const string method = "pair_tools_list_presence";

            try
            {
                JsonElement tools = GetToolsArray(_context.Client.ToolsList());

                bool hasGet = HasToolNamed(tools, "bot_get_config_tab_pair");
                bool hasSet = HasToolNamed(tools, "bot_set_config_tab_pair");

                if (hasGet && hasSet)
                {
                    _context.RecordPass(Module, method, "tools/list содержит bot_get_config_tab_pair и bot_set_config_tab_pair");
                }
                else
                {
                    _context.RecordFail(Module, method, $"get={hasGet}, set={hasSet}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSetSchemaShape()
        {
            const string method = "pair_set_schema_shape";

            try
            {
                JsonElement tools = GetToolsArray(_context.Client.ToolsList());
                JsonElement tool = FindToolNamed(tools, "bot_set_config_tab_pair");

                if (tool.ValueKind == JsonValueKind.Undefined)
                {
                    _context.RecordFail(Module, method, "инструмент не найден в tools/list");
                    return;
                }

                JsonElement schema = default;

                if (!TryProp(tool, "inputSchema", out schema) && !TryProp(tool, "InputSchema", out schema))
                {
                    _context.RecordFail(Module, method, "у инструмента нет inputSchema");
                    return;
                }

                bool pairNameRejected = true;

                if (TryProp(schema, "properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
                {
                    pairNameRejected = !TryProp(properties, "pair_name", out _);
                }

                bool requiredOk = false;
                string requiredDetail = string.Empty;

                if (TryProp(schema, "required", out JsonElement required) && required.ValueKind == JsonValueKind.Array)
                {
                    List<string> names = new List<string>();

                    foreach (JsonElement item in required.EnumerateArray())
                    {
                        names.Add(item.GetString() ?? string.Empty);
                    }

                    requiredOk = names.Count == 2 && names[0] == "bot_id" && names[1] == "tab_name";
                    requiredDetail = string.Join(",", names);
                }

                if (pairNameRejected && requiredOk)
                {
                    _context.RecordPass(Module, method, "pair_name отсутствует в properties, required=[bot_id, tab_name]");
                }
                else
                {
                    _context.RecordFail(Module, method, $"pair_name_отсутствует={pairNameRejected}, required=[{requiredDetail}]");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCreatePairNumMaxPlusOne()
        {
            const string method = "pair_create_pair_num_max_plus_one";

            try
            {
                string response = Set(new
                {
                    bot_id = Bot,
                    tab_name = Tab,
                    create_pair = true,
                    sec1_server_type = "Tester",
                    sec1_server_full_name = "Tester",
                    sec1_portfolio_name = "GodMode",
                    sec1_security_name = "SBER",
                    sec1_security_class = "TestClass",
                    sec1_time_frame = "Min30",
                    sec2_server_type = "Tester",
                    sec2_server_full_name = "Tester",
                    sec2_security_name = "LKOH",
                    sec2_security_class = "TestClass",
                    sec2_time_frame = "Min30",
                    sec1_trade_regime = "Market",
                    sec2_trade_regime = "Market",
                    events_is_on = true
                });

                if (!TryParseInner(GetInner(response), out JsonElement root))
                {
                    _context.RecordFail(Module, method, "create_pair не ответил: " + ErrorText(response));
                    return;
                }

                int count = GetInt(root, "count");
                bool hasNum8 = HasPairNum(root, 8);
                string name8 = GetPairFieldString(root, 8, "pair_name");
                List<int> nums = CollectPairNums(root);

                bool countStyleAbsent = !nums.Contains(1) && !nums.Contains(2);

                if (count == 2 && hasNum8 && name8 == Tab + 8 && countStyleAbsent)
                {
                    _context.RecordPass(Module, method, $"create_pair -> pair_num=8 (max+1 от фикстуры 7, не Count), pair_name='{name8}'");
                }
                else
                {
                    _context.RecordFail(Module, method, $"count={count}, num8={hasNum8}, name='{name8}', nums=[{string.Join(",", nums)}]");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestRoundTripAllFields()
        {
            const string method = "pair_round_trip_all_fields";

            try
            {
                string response = Set(new
                {
                    bot_id = Bot,
                    tab_name = Tab,
                    pair_num = FixturePairNum,
                    sec1_server_type = "Tester",
                    sec1_server_full_name = "Tester",
                    sec1_portfolio_name = "GodMode",
                    sec1_security_name = "SBER",
                    sec1_security_class = "TestClass",
                    sec1_time_frame = "Min30",
                    sec1_commission_type = "Percent",
                    sec1_commission_value = 0.1,
                    sec2_server_type = "Tester",
                    sec2_server_full_name = "Tester",
                    sec2_portfolio_name = "GodMode",
                    sec2_security_name = "LKOH",
                    sec2_security_class = "TestClass",
                    sec2_time_frame = "Min30",
                    sec2_commission_type = "OneLotFix",
                    sec2_commission_value = 10,
                    sec1_trade_regime = "Market",
                    sec2_trade_regime = "Limit",
                    sec1_volume_type = "Contract",
                    sec2_volume_type = "Contract",
                    sec1_slippage_type = "Percent",
                    sec2_slippage_type = "Absolute",
                    sec1_slippage = 1,
                    sec2_slippage = 2,
                    sec1_volume = 5,
                    sec2_volume = 6,
                    correlation_look_back = 40,
                    cointegration_look_back = 35,
                    cointegration_deviation = 2,
                    auto_rebuild_correlation = true,
                    auto_rebuild_cointegration = false,
                    events_is_on = true,
                    emulator_is_on = true
                });

                if (!TryParseInner(GetInner(response), out JsonElement root))
                {
                    _context.RecordFail(Module, method, "set не ответил: " + ErrorText(response));
                    return;
                }

                Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "sec1_security_name", "SBER" },
                    { "sec2_security_name", "LKOH" },
                    { "sec1_security_class", "TestClass" },
                    { "sec1_time_frame", "Min30" },
                    { "sec1_server_full_name", "Tester" },
                    { "sec1_portfolio_name", "GodMode" },
                    { "sec1_commission_type", "Percent" },
                    { "sec2_commission_type", "OneLotFix" },
                    { "sec1_trade_regime", "Market" },
                    { "sec2_trade_regime", "Limit" },
                    { "sec1_volume_type", "Contract" },
                    { "sec1_slippage_type", "Percent" },
                    { "sec2_slippage_type", "Absolute" },
                    { "correlation_look_back", "40" },
                    { "cointegration_look_back", "35" }
                };

                List<string> mismatches = new List<string>();

                foreach (KeyValuePair<string, string> field in expected)
                {
                    string actual = GetPairFieldString(root, FixturePairNum, field.Key);

                    if (actual != field.Value)
                    {
                        mismatches.Add($"{field.Key}='{actual}' (ожидалось '{field.Value}')");
                    }
                }

                string events = GetPairFieldString(root, FixturePairNum, "events_is_on");
                string emulator = GetPairFieldString(root, FixturePairNum, "emulator_is_on");

                if (events != "True" || emulator != "True")
                {
                    mismatches.Add($"pair-level флаги: events='{events}', emulator='{emulator}'");
                }

                string num5 = GetPairFieldString(root, FixturePairNum, "sec1_volume");

                if (num5 != "5")
                {
                    mismatches.Add($"sec1_volume='{num5}'");
                }

                // Каскад без ключа (D2): set на стандарт вкладки перезаписывает существующие пары.
                string stdSet = Set(new { bot_id = Bot, tab_name = Tab, correlation_look_back = 33 });
                bool stdApplied = false;
                string stdDetail = "нет ответа";

                if (TryParseInner(GetInner(stdSet), out JsonElement stdRoot))
                {
                    int topLookBack = GetInt(stdRoot, "correlation_look_back");
                    int pairLookBack = GetPairFieldInt(stdRoot, FixturePairNum, "correlation_look_back");
                    stdApplied = topLookBack == 33 && pairLookBack == 33;
                    stdDetail = $"top={topLookBack}, pair={pairLookBack}";
                }

                if (mismatches.Count == 0 && stdApplied)
                {
                    _context.RecordPass(Module, method, "round-trip всех полей + pair-level флага; set без ключа = стандарт с каскадом на пару (look_back 33)");
                }
                else
                {
                    _context.RecordFail(Module, method, string.Join("; ", mismatches) + " | standard: " + stdDetail);
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestPairStructure()
        {
            const string method = "pair_structure";

            try
            {
                string state = GetInner(Get("bot_get_config_tab_pair", new { bot_id = Bot, tab_name = Tab }));

                if (!TryParseInner(state, out JsonElement root))
                {
                    _context.RecordFail(Module, method, "get не разобран");
                    return;
                }

                string type = GetPropString(root, "type");
                string key = GetPairFieldString(root, FixturePairNum, "security_key");
                bool keysPresent = GetPairFieldString(root, FixturePairNum, "pair_name").Length > 0
                    && GetPairNode(root, FixturePairNum).TryGetProperty("pair_num", out _);

                if (type == "Pair" && key == "SBER_|_LKOH" && keysPresent)
                {
                    _context.RecordPass(Module, method, "type=Pair, security_key='SBER_|_LKOH', пара несёт pair_num/pair_name/security_key");
                }
                else
                {
                    _context.RecordFail(Module, method, $"type='{type}', key='{key}', keys={keysPresent}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        #endregion

        #region Negatives

        private void TestNegativeLegFieldWithoutKey()
        {
            TestNegative("pair_negative_leg_without_key",
                new { bot_id = Bot, tab_name = Tab, sec1_server_type = "Tester" },
                "set per pair");
        }

        private void TestNegativePairName()
        {
            TestNegative("pair_negative_pair_name",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, pair_name = "hack" },
                "Unknown property 'pair_name'");
        }

        private void TestNegativeBothKeys()
        {
            TestNegative("pair_negative_both_keys",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, create_pair = true },
                "Specify only pair_num or create_pair");
        }

        private void TestNegativeEnumPlusOne()
        {
            TestNegative("pair_negative_enum_plus_one",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, sec1_trade_regime = "+1" },
                "Allowed values");
        }

        private void TestNegativeKinds()
        {
            TestNegative("pair_negative_kind_bool",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, events_is_on = "yes" },
                "must be a boolean");

            TestNegative("pair_negative_kind_integer",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, correlation_look_back = 1.5 },
                "must be an integer");

            TestNegative("pair_negative_kind_number_string",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, sec1_volume = "7" },
                "must be a number");
        }

        private void TestNegativeEmptySecurityName()
        {
            TestNegative("pair_negative_empty_security_name",
                new { bot_id = Bot, tab_name = Tab, pair_num = FixturePairNum, sec1_security_name = "" },
                "must not be empty");
        }

        private void TestNegativeDedupSameClass()
        {
            TestNegative("pair_negative_dedup_same",
                new
                {
                    bot_id = Bot,
                    tab_name = Tab,
                    create_pair = true,
                    sec1_security_name = "SBER",
                    sec1_security_class = "TestClass",
                    sec2_security_name = "LKOH",
                    sec2_security_class = "TestClass",
                    sec1_time_frame = "Min30",
                    sec2_time_frame = "Min30"
                },
                "already exists on tab");
        }

        private void TestNegativeDedupSwap()
        {
            TestNegative("pair_negative_dedup_swap",
                new
                {
                    bot_id = Bot,
                    tab_name = Tab,
                    create_pair = true,
                    sec1_security_name = "LKOH",
                    sec1_security_class = "TestClass",
                    sec2_security_name = "SBER",
                    sec2_security_class = "TestClass",
                    sec1_time_frame = "Min30",
                    sec2_time_frame = "Min30"
                },
                "already exists on tab");
        }

        private void TestNegativeUnknownPairNum()
        {
            TestNegative("pair_negative_unknown_pair_num",
                new { bot_id = Bot, tab_name = Tab, pair_num = 99, sec1_volume = 3 },
                "not found");
        }

        private void TestNegativeCreatePairEventsFalseRejected()
        {
            TestNegative("pair_negative_create_events_false",
                new
                {
                    bot_id = Bot,
                    tab_name = Tab,
                    create_pair = true,
                    events_is_on = false,
                    sec1_security_name = "GMKN",
                    sec2_security_name = "NVTK"
                },
                "not allowed for Tester");
        }

        private void TestNegative(string method, object args, string expectSubstring)
        {
            try
            {
                string before = GetInner(Get("bot_get_config_tab_pair", new { bot_id = Bot, tab_name = Tab }));

                string response = Set(args);

                bool errored = IsError(response);
                string text = ErrorText(response);
                bool hasMessage = text.IndexOf(expectSubstring, StringComparison.OrdinalIgnoreCase) >= 0;
                string after = GetInner(Get("bot_get_config_tab_pair", new { bot_id = Bot, tab_name = Tab }));

                if (errored && hasMessage && before == after)
                {
                    _context.RecordPass(Module, method, $"отклонено до изменения состояния: {Trim(text, 100)}");
                }
                else
                {
                    _context.RecordFail(Module, method, $"IsError={errored}, сообщение жду '{expectSubstring}', получил '{Trim(text, 120)}', состояние изменилось={before != after}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        #endregion

        #region V1/V2 symmetry and registry

        private void TestV1V2ErrorTextEqual()
        {
            const string method = "pair_v1_v2_error_text_equal";

            try
            {
                object badArgs = new { bot_id = Bot, tab_name = Tab, pair_name = "x" };

                string currentText = ErrorText(Set(badArgs));

                using (McpApiClient other = new McpApiClient(_context.Client.BaseUrl, _context.ApiKey, !_context.Client.StreamableHttp))
                {
                    string otherText = ErrorText(other.ToolsCall("bot_set_config_tab_pair", badArgs));

                    bool bothErrors = currentText.Length > 0 && otherText.Length > 0;
                    bool equal = string.Equals(currentText, otherText, StringComparison.Ordinal);
                    bool viaRobotsApi = currentText.IndexOf("Unknown property", StringComparison.OrdinalIgnoreCase) >= 0
                        && currentText.IndexOf("not found in robots API", StringComparison.OrdinalIgnoreCase) < 0;

                    if (bothErrors && equal && viaRobotsApi)
                    {
                        _context.RecordPass(Module, method, $"тексты ошибок V1/V2 идентичны и идут через RobotsApi (не default-роут): {Trim(currentText, 90)}");
                    }
                    else
                    {
                        _context.RecordFail(Module, method, $"equal={equal}, robots-api-текст={viaRobotsApi}, текущий транспорт: '{Trim(currentText, 80)}', другой: '{Trim(otherText, 80)}'");
                    }
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestRegistryRegression()
        {
            const string method = "pair_registry_regression";

            try
            {
                JsonElement tools = GetToolsArray(_context.Client.ToolsList());

                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                int total = 0;
                bool duplicated = false;

                foreach (JsonElement tool in tools.EnumerateArray())
                {
                    string name = GetPropString(tool, "name").Length > 0 ? GetPropString(tool, "name") : GetPropString(tool, "Name");

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!names.Add(name))
                        {
                            duplicated = true;
                        }

                        total++;
                    }
                }

                string[] mustHave =
                {
                    "bot_get_config_tab_pair", "bot_set_config_tab_pair",
                    "bot_get_config_tab_simple", "bot_set_config_tab_simple",
                    "bot_get_config_tab_screener", "bot_set_config_tab_screener",
                    "bot_create", "bot_get_sources"
                };

                List<string> missing = new List<string>();

                foreach (string name in mustHave)
                {
                    if (!names.Contains(name))
                    {
                        missing.Add(name);
                    }
                }

                if (!duplicated && missing.Count == 0 && total > 0)
                {
                    _context.RecordPass(Module, method, $"реестр: {total} инструментов без дублей, новые пара + simple/screener/create/sources на месте");
                }
                else
                {
                    _context.RecordFail(Module, method, $"дубли={duplicated}, нет {total} из-за: {string.Join(", ", missing)}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCleanup()
        {
            const string method = "pair_cleanup";

            try
            {
                DeleteBotQuietly();
                DeleteFixtureFiles();
                _context.RecordPass(Module, method, $"бот {Bot} и фикстуры убраны");
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        #endregion

        #region Helpers

        // Engine-настройки живёт рядом с запускаемым OsEngine.exe, а не рядом со стендом
        private string EngineDirectory() =>
            Path.Combine(Path.GetDirectoryName(_context.OsEnginePath)!, "Engine");

        private void DeleteFixtureFiles()
        {
            string engine = EngineDirectory();

            if (!Directory.Exists(engine))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(engine, "pair_mcp*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception error)
                {
                    Console.WriteLine($"  [Pair] не удалён файл {Path.GetFileName(file)}: {error.Message}");
                }
            }
        }

        private void DeleteBotQuietly()
        {
            try
            {
                Quiet("bot_delete", new { bot_id = Bot });
            }
            catch (Exception error)
            {
                Console.WriteLine($"  [Pair] bot_delete: {error.Message}");
            }
        }

        private string Call(string method, object arguments)
        {
            _context.PrintRequest(Module, method, arguments);
            string response = _context.Client.ToolsCall(method, arguments);
            _context.PrintResponse(response);
            return response;
        }

        private string Quiet(string method, object arguments)
        {
            return _context.Client.ToolsCall(method, arguments);
        }

        private string Get(string method, object arguments)
        {
            return Call(method, arguments);
        }

        private string Set(object arguments)
        {
            return Call("bot_set_config_tab_pair", arguments);
        }

        private static bool IsError(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    if (doc.RootElement.TryGetProperty("IsError", out JsonElement isError))
                    {
                        return isError.ValueKind == JsonValueKind.True;
                    }

                    return doc.RootElement.TryGetProperty("isError", out JsonElement lower) && lower.ValueKind == JsonValueKind.True;
                }
            }
            catch
            {
                return true;
            }
        }

        private static string GetInner(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    JsonElement root = doc.RootElement;
                    JsonElement content;

                    if (!TryProp(root, "Content", out content) && !TryProp(root, "content", out content))
                    {
                        return string.Empty;
                    }

                    if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
                    {
                        return string.Empty;
                    }

                    JsonElement first = content[0];
                    return TryProp(first, "Text", out JsonElement text) ? text.GetString() ?? string.Empty : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ErrorText(string response)
        {
            string inner = GetInner(response);
            return inner.Length > 0 ? inner : response;
        }

        private static bool TryParseInner(string inner, out JsonElement root)
        {
            root = default;

            if (string.IsNullOrEmpty(inner))
            {
                return false;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(inner))
                {
                    root = doc.RootElement.Clone();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private JsonElement GetToolsArray(string toolsListResponse)
        {
            using (JsonDocument doc = JsonDocument.Parse(toolsListResponse))
            {
                JsonElement tools;

                if (!TryProp(doc.RootElement, "Tools", out tools) && !TryProp(doc.RootElement, "tools", out tools))
                {
                    throw new InvalidOperationException("tools/list без массива Tools");
                }

                return tools.Clone();
            }
        }

        private static bool TryProp(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(name, out value))
                {
                    return true;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string GetPropString(JsonElement element, string name)
        {
            return TryProp(element, name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool HasToolNamed(JsonElement tools, string name)
        {
            foreach (JsonElement tool in tools.EnumerateArray())
            {
                if (GetPropString(tool, "name") == name || GetPropString(tool, "Name") == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonElement FindToolNamed(JsonElement tools, string name)
        {
            foreach (JsonElement tool in tools.EnumerateArray())
            {
                if (GetPropString(tool, "name") == name || GetPropString(tool, "Name") == name)
                {
                    return tool;
                }
            }

            return default;
        }

        private static int GetInt(JsonElement root, string name)
        {
            return TryProp(root, name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
                ? element.GetInt32()
                : 0;
        }

        private static JsonElement GetPairNode(JsonElement root, int pairNum)
        {
            if (TryProp(root, "pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    if (TryProp(pair, "pair_num", out JsonElement num) && num.ValueKind == JsonValueKind.Number && num.GetInt32() == pairNum)
                    {
                        return pair;
                    }
                }
            }

            return default;
        }

        private static bool HasPairNum(JsonElement root, int pairNum)
        {
            return GetPairNode(root, pairNum).ValueKind != JsonValueKind.Undefined;
        }

        private static List<int> CollectPairNums(JsonElement root)
        {
            List<int> nums = new List<int>();

            if (TryProp(root, "pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    if (TryProp(pair, "pair_num", out JsonElement num) && num.ValueKind == JsonValueKind.Number)
                    {
                        nums.Add(num.GetInt32());
                    }
                }
            }

            return nums;
        }

        private static string GetPairFieldString(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind == JsonValueKind.Undefined ? string.Empty : RawFieldString(pair, field);
        }

        private static string RawFieldString(JsonElement pair, string field)
        {
            if (!TryProp(pair, field, out JsonElement value))
            {
                return string.Empty;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                    return value.GetRawText();
                case JsonValueKind.True:
                    return "True";
                case JsonValueKind.False:
                    return "False";
                default:
                    return value.GetRawText();
            }
        }

        private static int GetPairFieldInt(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);
            return pair.ValueKind != JsonValueKind.Undefined && TryProp(pair, field, out JsonElement value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }

        #endregion
    }
}
