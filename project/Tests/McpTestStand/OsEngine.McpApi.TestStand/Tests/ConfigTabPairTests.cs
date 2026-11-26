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
    /// симметрия текстов ошибок V1/V2 (не default-роут), регресс-пин реестра,
    /// live-фаза: скачка данных через OsData (BinanceData, без ключей) -> прогон testerlight
    /// -> непустые sources и ненулевой correlation_last живой пары (гейт владельца 25.11).
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
            TestLiveDataDownload();
            TestLivePairAndRun();
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

                // после рестарта Robot master поднимается асинхронно: ждём, пока bot_* перестанет
                // отвечать «Robot master is not available» (тот же паттерн, что WaitForTesterServer)
                string created = Call("bot_create", new { strategy_name = "PairCorrelationTrader", name = Bot });
                DateTime masterDeadline = DateTime.Now.AddSeconds(30);

                while (IsError(created) && created.Contains("Robot master is not available") && DateTime.Now < masterDeadline)
                {
                    System.Threading.Thread.Sleep(1000);
                    created = Call("bot_create", new { strategy_name = "PairCorrelationTrader", name = Bot });
                }

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
                    sec1_server_type = "Tester",
                    sec1_server_full_name = "Tester",
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
                    sec1_server_type = "Tester",
                    sec1_server_full_name = "Tester",
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

        #region Live data (OsData -> tester)

        // Гейт владельца 25.11: модуль сам качает данные через OsData и прогоняет тестер.
        // BinanceData (data.binance.vision) — безключевой архивный источник: MOEX ISS и
        // invest-public-api с машины стенда блокируются SEC_E_UNTRUSTED_ROOT (нет Russian Trusted Root CA).
        private const string LiveSet = "Set_M24PairData";
        private const string LiveServerType = "BinanceData";
        private const string LiveSec1 = "BTCUSDT";
        private const string LiveSec2 = "ETHUSDT";
        private const string LiveTimeFrame = "Min1";
        private const string LiveDateFrom = "2026-10-01";
        private const string LiveDateTo = "2026-10-05";
        private const string LiveBot = "pair_live_mcp";

        /// <summary>
        /// Шаг 1 live-фазы: терминал без light-флага, terminal_open_mode(data), набор OsData
        /// Min1 на короткое окно через BinanceData, ожидание фактической загрузки свечей.
        /// </summary>
        private void TestLiveDataDownload()
        {
            const string method = "pair_live_osdata_download";

            try
            {
                // data-tools живут в полном терминале в режиме data — рестарт без light-флага.
                _context.RestartOsEngine(string.Empty);

                Call("terminal_open_mode", new { mode = "data" });

                if (!WaitOsDataMode(60))
                {
                    _context.RecordFail(Module, method, "OsData mode is not open after terminal_open_mode(data)");
                    return;
                }

                // data-tools доступны только в режиме data — чистка осадка прошлых прогонов здесь.
                Quiet("data_set_off", new { name = LiveSet });
                Quiet("data_delete_set", new { name = LiveSet });

                string activate = Call("server_management_activate", new { type = LiveServerType });
                string sourceName = FirstInstanceName(activate);

                if (sourceName.Length == 0)
                {
                    sourceName = LiveServerType;
                }

                Quiet("server_instance_connect", new { type = LiveServerType });
                // Securities of the source appear only after connect + subscribe (DataTests pattern).
                if (!WaitServerSecurities(180))
                {
                    _context.RecordFail(Module, method, "source "+LiveServerType+" connected, but its securities did not appear within 180 seconds");
                    return;
                }

                string created = Call("data_create_set", new
                {
                    name = LiveSet,
                    source = LiveServerType,
                    source_name = sourceName,
                    timeframes = new[] { LiveTimeFrame },
                    date_from = LiveDateFrom,
                    date_to = LiveDateTo
                });

                if (IsError(created))
                {
                    _context.RecordFail(Module, method, "data_create_set: " + Trim(ErrorText(created), 160));
                    return;
                }

                string added = Call("data_set_securities_add", new
                {
                    name = LiveSet,
                    securities = new object[] { new { name = LiveSec1 }, new { name = LiveSec2 } }
                });

                if (IsError(added))
                {
                    _context.RecordFail(Module, method, "data_set_securities_add: " + Trim(ErrorText(added), 160));
                    return;
                }

                Call("data_set_on", new { name = LiveSet });

                bool ok1 = WaitForSecurityLoaded(LiveSec1, 900, out string detail1, out int count1);
                bool ok2 = WaitForSecurityLoaded(LiveSec2, 900, out string detail2, out int count2);

                Quiet("data_set_off", new { name = LiveSet });

                if (ok1 && ok2 && count1 > 0 && count2 > 0)
                {
                    _context.RecordPass(Module, method, $"{LiveSet} через {LiveServerType} ({LiveDateFrom}..{LiveDateTo} {LiveTimeFrame}): {LiveSec1}={count1}, {LiveSec2}={count2} свечей");
                }
                else
                {
                    _context.RecordFail(Module, method, detail1 + " | " + detail2);
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        /// <summary>
        /// Шаг 2 live-фазы: testerlight на скачанных данных — пара BTCUSDT/ETHUSDT через
        /// bot_set_config_tab_pair, A-5 sources до старта, прогон с fast-forward,
        /// ненулевой correlation_last = подписка потока дошла до вложенных табов пары.
        /// </summary>
        private void TestLivePairAndRun()
        {
            const string method = "pair_live_pair_run";

            try
            {
                // Бот-операции — в testerlight: в пустом режиме Robot master не поднимается
                // («Open a mode that supports robots»). Рестарт после запекания фикстур — ниже.
                _context.RestartOsEngine("-testerlight");

                // Живая фаза — отдельный чистый робот: у фикстурного pair_mcp легаси-пары SBER/LKOH,
                // которых нет в наборе, и тестер блокирует прогон на битом роботе.
                DeleteBotQuietly(LiveBot);

                // Robot master поднимается асинхронно после рестарта — тот же retry, что в фикстуре.
                string createLive = Call("bot_create", new { strategy_name = "PairCorrelationTrader", name = LiveBot });
                DateTime liveMasterDeadline = DateTime.Now.AddSeconds(40);

                while (IsError(createLive) && ErrorText(createLive).Contains("Robot master is not available")
                    && DateTime.Now < liveMasterDeadline)
                {
                    System.Threading.Thread.Sleep(2000);
                    createLive = Call("bot_create", new { strategy_name = "PairCorrelationTrader", name = LiveBot });
                }

                if (IsError(createLive))
                {
                    _context.RecordFail(Module, method, "bot_create " + LiveBot + ": " + Trim(ErrorText(createLive), 160));
                    return;
                }

                string liveTab = WaitLivePairTab(60);

                if (liveTab == null)
                {
                    _context.RecordFail(Module, method, "у робота " + LiveBot + " не появилась Pair-вкладка за 60 секунд");
                    return;
                }

                // После рестарта нумерация пар может смещаться — живой пару ищем по security_key.
                string key = LiveSec1 + "_|_" + LiveSec2;
                int livePairNum = -1;

                if (TryParseInner(GetInner(Quiet("bot_get_config_tab_pair", new { bot_id = LiveBot, tab_name = liveTab })), out JsonElement current))
                {
                    livePairNum = FindPairNumBySecurityKey(current, key);
                }

                if (livePairNum < 0)
                {
                    string live = Set(new
                    {
                        bot_id = LiveBot,
                        tab_name = liveTab,
                        create_pair = true,
                        sec1_server_type = "Tester",
                        sec1_server_full_name = "Tester",
                        sec1_portfolio_name = "GodMode",
                        sec1_security_name = LiveSec1,
                        sec1_security_class = "TestClass",
                        sec1_time_frame = LiveTimeFrame,
                        sec2_server_type = "Tester",
                        sec2_server_full_name = "Tester",
                        sec2_portfolio_name = "GodMode",
                        sec2_security_name = LiveSec2,
                        sec2_security_class = "TestClass",
                        sec2_time_frame = LiveTimeFrame,
                        sec1_trade_regime = "Market",
                        sec2_trade_regime = "Market",
                        events_is_on = true,
                        emulator_is_on = true
                    });

                    if (!TryParseInner(GetInner(live), out JsonElement createdRoot))
                    {
                        _context.RecordFail(Module, method, "set live-пары: " + Trim(ErrorText(live), 160));
                        return;
                    }

                    livePairNum = FindPairNumBySecurityKey(createdRoot, key);
                }

                if (livePairNum < 0)
                {
                    _context.RecordFail(Module, method, $"живая пара {key} не найдена во вкладке");
                    return;
                }

                // Робот обязан быть в regime=On, иначе коннекторы легасей не подписываются и
                // тестер завершается с «ни один бот не подписан» (Message38).
                string regime = Call("bot_set_params", new
                {
                    bot_id = LiveBot,
                    parameters = new Dictionary<string, object> { { "Regime", "On" } }
                });

                if (IsError(regime))
                {
                    _context.RecordFail(Module, method, "bot_set_params Regime: " + Trim(ErrorText(regime), 160));
                    return;
                }

                // Рецепт G-A/G-B-стендов: ConnectorCandles подписывается только когда сконструирован
                // с непустым SecurityName — так грузятся коннекторы на старте движка с диска.
                // Runtime-конфигурация пары живёт в памяти и умирает с kill рестарта, тестер
                // прерывается Message38. Запекаем пару и плечи в фикстуры и перезапускаем движок.
                string livePairName = null;

                if (TryParseInner(GetInner(Get("bot_get_config_tab_pair", new { bot_id = LiveBot, tab_name = liveTab })), out JsonElement beforeRestart))
                {
                    livePairName = GetPairNameByNum(beforeRestart, livePairNum);
                }

                if (livePairName == null)
                {
                    _context.RecordFail(Module, method, $"у пары num={livePairNum} нет pair_name, фикстуры не запечь");
                    return;
                }

                WriteLivePairFixtures(liveTab, livePairName, livePairNum);

                _context.RestartOsEngine("-testerlight");

                // После рестарта MCP-сокет поднимается раньше TesterServer — ждём его,
                // иначе первый же set падает с «Tester server is not available».
                DateTime testerDeadline = DateTime.Now.AddSeconds(120);
                bool testerReady = false;

                while (DateTime.Now < testerDeadline && !testerReady)
                {
                    string probe = Quiet("tester_get_status", new { });
                    testerReady = !IsError(probe);

                    if (!testerReady)
                    {
                        System.Threading.Thread.Sleep(2000);
                    }
                }

                if (!testerReady)
                {
                    _context.RecordFail(Module, method, "Tester server не поднялся за 120 секунд после рестарта");
                    return;
                }

                // Регим возвращаем после рестарта: состояние InEnabled в файл не пишется,
                // без On робот не стартует на TestingStartEvent и не считает корреляцию.
                Quiet("bot_set_params", new
                {
                    bot_id = LiveBot,
                    parameters = new Dictionary<string, object> { { "Regime", "On" } }
                });

                // Нейтральные настройки тестера, как в OsTester-стенде, — прогон предсказуем.
                Quiet("tester_portfolio_set_config", new { start_portfolio = 1000000m, portfolio_calculation_enabled = false });
                Quiet("tester_execution_set_config", new { slippage_to_simple_order = 0, slippage_to_stop_order = 0, order_execution_type = "Touch", non_trade_periods = new object[0] });

                // SetNewSet prepends the "Set_" folder prefix, so the tester wants the bare name.
                string testerSetName = LiveSet.StartsWith("Set_") ? LiveSet.Substring(4) : LiveSet;

                // Даты обязательны: без периода тестер не крутит данные (TimeNow > TimeEnd сразу).
                string setData = Call("tester_data_set_config", new
                {
                    set_name = testerSetName,
                    type_tester_data = "Candle",
                    date_from = LiveDateFrom,
                    date_to = LiveDateTo
                });

                if (IsError(setData))
                {
                    _context.RecordFail(Module, method, "tester_data_set_config: " + Trim(ErrorText(setData), 160));
                    return;
                }

                if (!WaitTesterSecurities(2, 180))
                {
                    _context.RecordFail(Module, method, "тестер не отдал бумаги набора в течение 180 секунд");
                    return;
                }

                // Даём движку восстановить робота с вкладки-файлов и коннекторам — подписаться на Tester.
                System.Threading.Thread.Sleep(15000);

                if (TryParseInner(GetInner(Quiet("bot_get_config_tab_pair", new { bot_id = LiveBot, tab_name = liveTab })), out JsonElement restored))
                {
                    livePairNum = FindPairNumBySecurityKey(restored, key);
                }

                if (livePairNum < 0)
                {
                    _context.RecordFail(Module, method, "живая пара " + key + " не видна во вкладке после рестарта (фикстуры не загружены)");
                    return;
                }

                bool legsVisible = SourcesHavePair(Quiet("bot_get_sources", new { bot_id = LiveBot }), liveTab, LiveSec1, LiveSec2);

                bool finished = StartTestAndWait(1200);

                double correlation = 0;

                if (finished && TryParseInner(GetInner(Get("bot_get_config_tab_pair", new { bot_id = LiveBot, tab_name = liveTab })), out JsonElement after))
                {
                    correlation = GetPairDouble(after, livePairNum, "correlation_last");
                }

                int trades = ExtractTrades(Quiet("tester_get_report", new { }));

                if (finished && legsVisible && correlation != 0)
                {
                    _context.RecordPass(Module, method, $"прогон testerlight на OsData-данных {LiveSec1}/{LiveSec2} {LiveTimeFrame}: A-5 Pair-source зарегистрирован, correlation_last={correlation:F4} (CorrelationChangeEvent атрибутирован паре), trades={trades}");
                }
                else
                {
                    _context.RecordFail(Module, method, $"finished={finished}, sources pair_source={legsVisible}, correlation_last={correlation}, trades={trades}");
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool WaitOsDataMode(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = Quiet("data_get_sets", new { });

                // Режим закрыт -> inner {code, message}; открыт -> массив сетов.
                if (TryParseInner(GetInner(response), out JsonElement root) && root.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        /// <summary>Polls server_instance_get_securities until the live source reports securities.</summary>
        private bool WaitServerSecurities(int timeoutSeconds)
        {
            DateTime start = DateTime.Now;

            while ((DateTime.Now - start).TotalSeconds < timeoutSeconds)
            {
                string response = Quiet("server_instance_get_securities", new { type = LiveServerType });

                if (!IsError(response))
                {
                    JsonElement root;

                    if (TryParseInner(GetInner(response), out root)
                        && root.TryGetProperty("count", out JsonElement count)
                        && count.GetInt32() > 0)
                    {
                        return true;
                    }
                }

                System.Threading.Thread.Sleep(3000);
            }

            return false;
        }

        private static string FirstInstanceName(string activateResponse)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(GetInner(activateResponse)))
                {
                    JsonElement root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        return GetPropString(root[0], "name");
                    }

                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        return GetPropString(root, "source_name").Length > 0 ? GetPropString(root, "source_name") : GetPropString(root, "name");
                    }
                }
            }
            catch
            {
                // activation may answer non-JSON when already active — caller falls back to the type name
            }

            return string.Empty;
        }

        private bool WaitForSecurityLoaded(string security, int timeoutSeconds, out string detail, out int count)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            detail = security + ": нет статуса";
            count = 0;

            while (DateTime.Now < deadline)
            {
                string response = Quiet("data_get_security_status", new { name = LiveSet, security = security, timeframe = LiveTimeFrame });

                if (TryParseInner(GetInner(response), out JsonElement status))
                {
                    count = TryProp(status, "objects_count", out JsonElement objects) && objects.ValueKind == JsonValueKind.Number
                        ? objects.GetInt32()
                        : 0;

                    string state = GetPropString(status, "status");
                    double percent = TryProp(status, "percent_load", out JsonElement percentElement) && percentElement.ValueKind == JsonValueKind.Number
                        ? percentElement.GetDouble()
                        : 0;

                    detail = $"{security}: status={state}, objects={count}, percent={percent:F1}";

                    if (state == "Error")
                    {
                        return false;
                    }

                    if (count > 0 && (state == "None" || percent >= 100))
                    {
                        return true;
                    }
                }
                else
                {
                    detail = security + ": " + Trim(ErrorText(response), 120);
                }

                System.Threading.Thread.Sleep(2000);
            }

            return false;
        }

        private bool WaitTesterSecurities(int minCount, int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = Quiet("tester_get_securities", new { });

                if (TryParseInner(GetInner(response), out JsonElement root)
                    && root.TryGetProperty("count", out JsonElement countElement)
                    && countElement.ValueKind == JsonValueKind.Number
                    && countElement.GetInt32() >= minCount)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>First Pair-type tab name of the live robot, or null on timeout.</summary>
        private string WaitLivePairTab(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                if (TryParseInner(GetInner(Quiet("bot_get_sources", new { bot_id = LiveBot })), out JsonElement root)
                    && root.TryGetProperty("sources", out JsonElement sources)
                    && sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement source in sources.EnumerateArray())
                    {
                        if (GetPropString(source, "type") == "Pair")
                        {
                            string name = GetPropString(source, "name");

                            if (name.Length > 0)
                            {
                                return name;
                            }
                        }
                    }
                }

                System.Threading.Thread.Sleep(1500);
            }

            return null;
        }

        private bool WaitBotTab(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string state = Quiet("bot_get_config_tab_pair", new { bot_id = Bot, tab_name = Tab });

                if (!IsError(state))
                {
                    return true;
                }

                Quiet("bot_create", new { strategy_name = "PairCorrelationTrader", name = Bot });
                System.Threading.Thread.Sleep(1500);
            }

            return false;
        }

        /// <summary>
        /// Запуск теста с подтверждением fast-forward (tester_start не гарантирует FF сам).
        /// </summary>
        private bool StartTestAndWait(int timeoutSeconds)
        {
            DateTime startDeadline = DateTime.Now.AddSeconds(120);
            bool playing = false;

            while (DateTime.Now < startDeadline && !playing)
            {
                string response = Quiet("tester_start", new { fast_forward = true });

                if (TryParseInner(GetInner(response), out JsonElement status))
                {
                    playing = GetPropString(status, "regime") == "Play";
                }

                if (!playing)
                {
                    System.Threading.Thread.Sleep(2000);
                }
            }

            if (!playing)
            {
                return false;
            }

            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = Quiet("tester_get_status", new { });

                if (TryParseInner(GetInner(response), out JsonElement status))
                {
                    string regime = GetPropString(status, "regime");
                    bool fastForward = TryProp(status, "fast_forward", out JsonElement ff) && ff.ValueKind == JsonValueKind.True;
                    double progress = TryProp(status, "progress_percent", out JsonElement progressElement) && progressElement.ValueKind == JsonValueKind.Number
                        ? progressElement.GetDouble()
                        : 0;

                    if (regime == "NotActive" || progress >= 100)
                    {
                        return true;
                    }

                    if (regime == "Play" && !fastForward)
                    {
                        Quiet("tester_fast_forward", new { });
                    }
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Запекает live-пару в фикстуры по рецепту G-A/G-B: на старте движка вкладка грузит
        /// пару, у плечевых коннекторов SecurityName непустой уже в конструкторе — они
        /// подписываются сами (нет Message38 на tester_start). Формат — рабочий файл G-B
        /// и фикстура этого модуля; пары-легенды SBER/LKOH фикстурного робота не затрагиваются.
        /// </summary>
        private void WriteLivePairFixtures(string liveTab, string pairName, int pairNum)
        {
            string engine = EngineDirectory();
            Directory.CreateDirectory(engine);

            File.WriteAllLines(Path.Combine(engine, liveTab + "StandartPairsSettings.txt"), new[]
            {
                "0", "7", "0", "7", "50", "1", "50", "No",
                "Percent", "Currency", "Percent", "Currency",
                "True", "False",
                "Market", "Market",
                "True", "True"
            });

            File.WriteAllLines(Path.Combine(engine, liveTab + "PairsNamesToLoad.txt"), new[] { pairName });

            File.WriteAllLines(Path.Combine(engine, pairName + "PairsSettings.txt"), new[]
            {
                pairNum.ToString(), "0", "7", "0", "7", "50", "1", "50",
                "Percent", "Currency", "Percent", "Currency",
                "Market", "Market", "No", "True", "True", "True"
            });

            // Leg tab = pair name + "1"/"2" (BotTabPair: Tab1 = new BotTabSimple(name + 1)).
            WriteLiveLegFixture(engine, pairName + "1", LiveSec1);
            WriteLiveLegFixture(engine, pairName + "2", LiveSec2);
        }

        private static void WriteLiveLegFixture(string engine, string legTab, string security)
        {
            File.WriteAllLines(Path.Combine(engine, legTab + "ConnectorPrime.txt"), new[]
            {
                "GodMode", "False", security, "Tester", "TestClass", "True", "Tester"
            });

            File.WriteAllLines(Path.Combine(engine, legTab + "TimeFrameBuilder.txt"), new[]
            {
                LiveTimeFrame, "False", "Tick", "Simple",
                "TimeFrame#" + LiveTimeFrame + "$NonTradingCandles#False", "False", "0,2"
            });
        }

        /// <summary>Достаёт pair_name из ответа bot_get_config_tab_pair по pair_num.</summary>
        private static string GetPairNameByNum(JsonElement root, int pairNum)
        {
            if (TryProp(root, "pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    if (TryProp(pair, "pair_num", out JsonElement num) && num.ValueKind == JsonValueKind.Number
                        && num.GetInt32() == pairNum)
                    {
                        return RawFieldString(pair, "pair_name");
                    }
                }
            }

            return null;
        }

        private static int FindPairNumBySecurityKey(JsonElement root, string securityKey)
        {
            if (TryProp(root, "pairs", out JsonElement pairs) && pairs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pair in pairs.EnumerateArray())
                {
                    if (RawFieldString(pair, "security_key") == securityKey
                        && TryProp(pair, "pair_num", out JsonElement num)
                        && num.ValueKind == JsonValueKind.Number)
                    {
                        return num.GetInt32();
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// A-5 in the actual bot_get_sources shape: RobotsApi.AddSources&lt;T&gt; (master) emits only
        /// {type, name} for every tab type — the "securities" field never existed in the response
        /// (fact reported to the orchestrator, 26.11.2026). So the check asserts a registered
        /// source of type=Pair with the live tab name. If the backend ever starts emitting
        /// "securities", both legs are verified there as before (forward-compatible).
        /// </summary>
        private static bool SourcesHavePair(string sourcesResponse, string tabName, string sec1, string sec2)
        {
            if (!TryParseInner(GetInner(sourcesResponse), out JsonElement root))
            {
                return false;
            }

            if (!root.TryGetProperty("sources", out JsonElement sources) || sources.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement source in sources.EnumerateArray())
            {
                if (GetPropString(source, "type") != "Pair")
                {
                    continue;
                }

                if (source.TryGetProperty("securities", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in list.EnumerateArray())
                    {
                        string text = item.GetRawText();

                        if (text.Contains(sec1) && text.Contains(sec2))
                        {
                            return true;
                        }
                    }

                    continue;
                }

                if (GetPropString(source, "name") == tabName)
                {
                    return true;
                }
            }

            return false;
        }

        private static double GetPairDouble(JsonElement root, int pairNum, string field)
        {
            JsonElement pair = GetPairNode(root, pairNum);

            return pair.ValueKind != JsonValueKind.Undefined
                && TryProp(pair, field, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : 0;
        }

        private static int ExtractTrades(string reportResponse)
        {
            if (!TryParseInner(GetInner(reportResponse), out JsonElement root))
            {
                return -1;
            }

            if (root.TryGetProperty("statistics_full", out JsonElement stats)
                && stats.ValueKind == JsonValueKind.Object
                && stats.TryGetProperty("trades", out JsonElement trades)
                && trades.ValueKind == JsonValueKind.Number)
            {
                return trades.GetInt32();
            }

            return 0;
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
            DeleteBotQuietly(Bot);
            DeleteBotQuietly(LiveBot);
        }

        /// <summary>
        /// bot_delete одним выстрелом теряет гонку с поднимающимся Robot master: робот
        /// переживает рестарт движка (файл в Engine\Robots) и крадёт create «already exists».
        /// Поэтому удаляем до фактического исчезновения из bot_get_list.
        /// </summary>
        private void DeleteBotQuietly(string botName)
        {
            try
            {
                DateTime deadline = DateTime.Now.AddSeconds(40);

                while (DateTime.Now < deadline)
                {
                    string response = Quiet("bot_delete", new { bot_id = botName });

                    if (!IsError(response) || response.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    System.Threading.Thread.Sleep(2000);
                }

                Console.WriteLine($"  [Pair] bot_delete {botName}: не удалось удалить за 40 с");
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
