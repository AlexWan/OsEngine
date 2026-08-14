/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace StopOrdersTestStand.Tests
{
    /// <summary>
    /// Module 2. Server tests runner: drives the WServerTester robot through
    /// the MCP API on a real T-Invest connection.
    ///
    /// Flow: single TInvest instance with the token -> Connect -> create
    /// WServerTester -> for each requested test set its O* parameters and click
    /// its "Start test orders N" button via bot_click_param_button -> poll the
    /// robot log file (Engine\Log) for the test report (REPORT ... STATUS: PASS/FAIL).
    ///
    /// Test selection: --test O13,O14,O15 or --test all (default: all registered).
    /// Registered: O13/O14/O15 (server stop orders on raw IServer).
    /// BotTab tests (B1..B12) are not included because the current WServerTester
    /// does not expose them.
    /// Real orders are placed on the account (minimum volume, tests close
    /// everything themselves). Requires trading hours - tests need live ticks.
    /// Without tinvest-token.txt the module is SKIPPED.
    /// </summary>
    public class Module2_WServerTesterTests
    {
        private const string Module = "SERVERTESTS";

        private const string ServerTypeName = "TInvest";
        private const string TesterStrategyName = "WServerTester";
        private const string TesterBotName = "StopServerTesterBot";

        private const string SecurityName = "SBER";
        private const string SecurityClass = "Stock rub";

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan SecuritiesTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(25);

        private class ServerTestInfo
        {
            public string Id;
            public string ButtonName;
            public string ReportMarker;
            public string ParamsGroup;

            public ServerTestInfo(string id, string buttonName, string reportMarker, string paramsGroup)
            {
                Id = id;
                ButtonName = buttonName;
                ReportMarker = reportMarker;
                ParamsGroup = paramsGroup;
            }
        }

        private static readonly ServerTestInfo[] KnownTests =
        {
            new ServerTestInfo("O13", "Start test orders 13", "REPORT Orders_13_StopOrders", "orders test 13"),
            new ServerTestInfo("O14", "Start test orders 14", "REPORT Orders_14_StopLimitPlaceCancel", "orders test 14"),
            new ServerTestInfo("O15", "Start test orders 15", "REPORT Orders_15_StopLimitRequestOnReconnect", "orders test 15"),
            new ServerTestInfo("O16", "Start test orders 16", "REPORT Orders_16_StopTriggerOnReconnect", "orders test 16"),
        };

        private readonly TestContext _context;
        private readonly string _testFilter;

        private string _createdTesterBotName = string.Empty;
        private string _serverName = string.Empty;
        private int _serverNumber = -1;
        private bool _serverCreatedByUs;
        private bool _serverConnected;

        public Module2_WServerTesterTests(TestContext context, string testFilter)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _testFilter = string.IsNullOrWhiteSpace(testFilter) ? "all" : testFilter;
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            try
            {
                if (!_context.Secrets.HasTInvestToken)
                {
                    _context.RecordPass(Module, "skipped", "SKIPPED: tinvest-token.txt not found next to the executable");
                    return;
                }

                List<ServerTestInfo> testsToRun = ResolveTestsToRun();

                if (testsToRun == null)
                {
                    return;
                }

                if (!WaitRobotMaster())
                {
                    return;
                }

                if (!EnsureSingleServerInstance())
                {
                    return;
                }

                if (!SetTokenParam())
                {
                    return;
                }

                if (!ConnectAndWait())
                {
                    return;
                }

                if (!WaitSecurities())
                {
                    return;
                }

                // после Connect нельзя сразу слать ордера (AServer отклоняет их Fail'ом
                // первые WaitTimeSecondsAfterFirstStartToSendOrders секунд) - ждём прогрузку данных
                Thread.Sleep(15000);

                if (!CreateTesterBot())
                {
                    return;
                }

                for (int i = 0; i < testsToRun.Count; i++)
                {
                    RunServerTest(testsToRun[i]);
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "RunAll", error.Message);
            }
            finally
            {
                Cleanup();
            }
        }

        #region Test selection

        private List<ServerTestInfo> ResolveTestsToRun()
        {
            if (string.Equals(_testFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return new List<ServerTestInfo>(KnownTests);
            }

            List<ServerTestInfo> result = new List<ServerTestInfo>();

            string[] requested = _testFilter.Split(',');

            for (int i = 0; i < requested.Length; i++)
            {
                string id = requested[i].Trim();
                ServerTestInfo found = null;

                for (int j = 0; j < KnownTests.Length; j++)
                {
                    if (string.Equals(KnownTests[j].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        found = KnownTests[j];
                        break;
                    }
                }

                if (found == null)
                {
                    _context.RecordFail(Module, "test_selection",
                        $"unknown test '{id}'. Known: {string.Join(", ", KnownTests.Select(t => t.Id))}");
                    return null;
                }

                result.Add(found);
            }

            return result;
        }

        #endregion

        #region Connection steps

        private bool WaitRobotMaster()
        {
            DateTime deadline = DateTime.Now.AddSeconds(90);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("bot_get_list", new { });

                    using (JsonDocument document = JsonDocument.Parse(response))
                    {
                        if (document.RootElement.TryGetProperty("IsError", out JsonElement isError)
                            && isError.GetBoolean() == false)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // engine is still starting. Wait
                }

                Thread.Sleep(1000);
            }

            _context.RecordFail(Module, "wait_robot_master", "robot master is not available 90 seconds after engine start");
            return false;
        }

        private bool EnsureSingleServerInstance()
        {
            const string method = "server_single_instance";

            try
            {
                // инстансы серверов подгружаются асинхронно после старта движка.
                // Ждём стабилизации списка (два одинаковых чтения подряд)
                List<JsonElement> allServers = WaitServersListStable();

                if (allServers == null)
                {
                    _context.RecordFail(Module, method, "servers list did not stabilize in 90 seconds");
                    return false;
                }

                List<JsonElement> tinvestServers = allServers
                    .Where(s => s.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == ServerTypeName)
                    .ToList();

                if (allServers.Count > 1
                    && tinvestServers.Count != allServers.Count)
                {
                    _context.RecordFail(Module, method,
                        "WServerTester requires exactly one server in the system. " +
                        $"Found {allServers.Count}. Remove extra servers from the connection window");
                    return false;
                }

                if (tinvestServers.Count == 0)
                {
                    // нет ни одного инстанса TInvest - активируем коннектор (создаёт базовый инстанс)
                    _context.Client.ToolsCall("server_management_activate", new { type = ServerTypeName });

                    Thread.Sleep(3000);

                    string afterActivateResponse = _context.Client.ToolsCall("server_management_get_list", new { });

                    if (TryParseConfigSilent(afterActivateResponse, out JsonElement afterActivate)
                        && afterActivate.ValueKind == JsonValueKind.Array)
                    {
                        tinvestServers = afterActivate.EnumerateArray()
                            .Where(s => s.TryGetProperty("type", out JsonElement type)
                                && type.GetString() == ServerTypeName)
                            .Select(e => e.Clone())
                            .ToList();
                    }
                }

                if (tinvestServers.Count == 0)
                {
                    // активация не помогла - создаём инстанс явно
                    object createRequest = new { type = ServerTypeName };
                    string createResponse = _context.Client.ToolsCall("server_instance_create", createRequest);

                    if (!TryParseConfig(createResponse, "server_instance_create", out JsonElement created))
                    {
                        return false;
                    }

                    _serverNumber = created.GetProperty("number").GetInt32();
                    _serverName = created.GetProperty("name").GetString() ?? string.Empty;
                    _serverCreatedByUs = true;
                }
                else
                {
                    // берём инстанс с наименьшим номером, лишние отключаем и удаляем
                    tinvestServers.Sort((a, b) => a.GetProperty("number").GetInt32()
                        .CompareTo(b.GetProperty("number").GetInt32()));

                    _serverNumber = tinvestServers[0].GetProperty("number").GetInt32();
                    _serverName = tinvestServers[0].GetProperty("name").GetString() ?? string.Empty;

                    for (int i = 1; i < tinvestServers.Count; i++)
                    {
                        int extraNumber = tinvestServers[i].GetProperty("number").GetInt32();

                        _context.Client.ToolsCall("server_instance_disconnect",
                            new { type = ServerTypeName, number = extraNumber });

                        Thread.Sleep(3000);

                        _context.Client.ToolsCall("server_instance_delete",
                            new { type = ServerTypeName, number = extraNumber });
                    }

                    // проверяем, что лишние инстансы действительно удалены (с повторами)
                    DateTime deadline = DateTime.Now.AddSeconds(60);
                    int tinvestLeft = -1;

                    while (DateTime.Now < deadline)
                    {
                        string recheckResponse = _context.Client.ToolsCall("server_management_get_list", new { });

                        if (TryParseConfigSilent(recheckResponse, out JsonElement recheckConfig)
                            && recheckConfig.ValueKind == JsonValueKind.Array)
                        {
                            tinvestLeft = recheckConfig.EnumerateArray()
                                .Count(s => s.TryGetProperty("type", out JsonElement type)
                                    && type.GetString() == ServerTypeName);

                            if (tinvestLeft == 1)
                            {
                                break;
                            }
                        }

                        Thread.Sleep(2000);
                    }

                    if (tinvestLeft != 1)
                    {
                        _context.RecordFail(Module, method,
                            $"failed to remove extra TInvest instances. Left: {tinvestLeft}");
                        return false;
                    }
                }

                _context.RecordPass(Module, method,
                    $"single TInvest instance: {_serverName} (#{_serverNumber})");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private List<JsonElement> WaitServersListStable()
        {
            DateTime deadline = DateTime.Now.AddSeconds(90);
            int lastCount = -1;
            int stableReads = 0;

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("server_management_get_list", new { });

                    Console.WriteLine($"[{Module}] servers list raw: {TrimForLog(response)}");

                    if (TryParseConfigSilent(response, out JsonElement config)
                        && config.ValueKind == JsonValueKind.Array)
                    {
                        int count = config.GetArrayLength();

                        if (count == lastCount)
                        {
                            stableReads++;

                            if (stableReads >= 2)
                            {
                                return config.EnumerateArray().Select(e => e.Clone()).ToList();
                            }
                        }
                        else
                        {
                            stableReads = 0;
                            lastCount = count;
                        }
                    }
                }
                catch
                {
                    // engine is still starting. Wait
                }

                Thread.Sleep(3000);
            }

            return null;
        }

        private string TrimForLog(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<empty>";
            }

            string singleLine = text.Replace("\r", " ").Replace("\n", " ");

            if (singleLine.Length > 400)
            {
                singleLine = singleLine.Substring(0, 400);
            }

            return singleLine;
        }

        private bool SetTokenParam()
        {
            const string method = "server_instance_set_params";

            try
            {
                object getRequest = new { type = ServerTypeName, number = _serverNumber };
                string getResponse = _context.Client.ToolsCall("server_instance_get_params", getRequest);

                if (!TryParseConfig(getResponse, "server_instance_get_params", out JsonElement paramsConfig))
                {
                    return false;
                }

                string tokenParamName = string.Empty;

                if (paramsConfig.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement parameter in paramsConfig.EnumerateArray())
                    {
                        if (parameter.TryGetProperty("type", out JsonElement type)
                            && type.GetString() == "Password"
                            && parameter.TryGetProperty("name", out JsonElement name))
                        {
                            tokenParamName = name.GetString() ?? string.Empty;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(tokenParamName))
                {
                    _context.RecordFail(Module, method, "no Password (token) parameter found on the TInvest instance");
                    return false;
                }

                object setRequest = new
                {
                    type = ServerTypeName,
                    number = _serverNumber,
                    parameters = new[]
                    {
                        new { name = tokenParamName, value = _context.Secrets.TInvestToken }
                    }
                };

                string setResponse = _context.Client.ToolsCall(method, setRequest);

                if (!TryParseConfig(setResponse, method, out JsonElement setConfig)
                    || !setConfig.TryGetProperty("success", out JsonElement success)
                    || success.ValueKind != JsonValueKind.True)
                {
                    _context.RecordFail(Module, method, "set_params did not return success");
                    return false;
                }

                _context.RecordPass(Module, method, $"token set into parameter '{tokenParamName}'");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool ConnectAndWait()
        {
            const string method = "server_connect_status";

            try
            {
                _context.Client.ToolsCall("server_instance_connect",
                    new { type = ServerTypeName, number = _serverNumber });

                DateTime deadline = DateTime.Now.Add(ConnectTimeout);

                while (DateTime.Now < deadline)
                {
                    string status = GetServerStatus();

                    if (status == "Connect")
                    {
                        _serverConnected = true;
                        _context.RecordPass(Module, method, $"{_serverName} connected");
                        return true;
                    }

                    Thread.Sleep(1000);
                }

                _context.RecordFail(Module, method, "status Connect not reached in time");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool WaitSecurities()
        {
            const string method = "server_data";

            try
            {
                DateTime deadline = DateTime.Now.Add(SecuritiesTimeout);

                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall("server_instance_get_securities",
                        new { type = ServerTypeName, number = _serverNumber });

                    if (TryParseConfigSilent(response, out JsonElement config)
                        && config.TryGetProperty("count", out JsonElement count)
                        && count.GetInt32() > 0)
                    {
                        _context.RecordPass(Module, method, $"securities received: {count.GetInt32()}");
                        return true;
                    }

                    Thread.Sleep(2000);
                }

                _context.RecordFail(Module, method, "no securities received from TInvest");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private string GetServerStatus()
        {
            try
            {
                string response = _context.Client.ToolsCall("server_instance_get_status",
                    new { type = ServerTypeName, number = _serverNumber });

                if (TryParseConfigSilent(response, out JsonElement config)
                    && config.TryGetProperty("status", out JsonElement status))
                {
                    return status.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // ignore and retry
            }

            return string.Empty;
        }

        private string GetFirstPortfolioNumber()
        {
            try
            {
                string response = _context.Client.ToolsCall("server_instance_get_portfolios",
                    new { type = ServerTypeName, number = _serverNumber });

                if (TryParseConfigSilent(response, out JsonElement config)
                    && config.TryGetProperty("portfolios", out JsonElement portfolios))
                {
                    foreach (JsonElement portfolio in portfolios.EnumerateArray())
                    {
                        if (portfolio.TryGetProperty("number", out JsonElement number))
                        {
                            return number.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to get portfolios: {error.Message}");
            }

            return string.Empty;
        }

        #endregion

        #region Tester bot

        private bool CreateTesterBot()
        {
            const string method = "tester_bot_create";

            try
            {
                // на всякий случай удаляем одноимённого робота с прошлого прогона
                try
                {
                    _context.Client.ToolsCall("bot_delete", new { bot_id = TesterBotName });
                }
                catch
                {
                    // ignore
                }

                object request = new { strategy_name = TesterStrategyName, name = TesterBotName };

                _context.PrintRequest(Module, "bot_create", request);
                string response = _context.Client.ToolsCall("bot_create", request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                _createdTesterBotName = config.GetProperty("name").GetString() ?? string.Empty;

                if (_createdTesterBotName != TesterBotName)
                {
                    _context.RecordFail(Module, method, $"created robot name mismatch: {_createdTesterBotName}");
                    return false;
                }

                _context.RecordPass(Module, method, $"robot '{TesterBotName}' created");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void RunServerTest(ServerTestInfo test)
        {
            string method = "run_" + test.Id;

            try
            {
                if (!SetTesterParams(test))
                {
                    return;
                }

                DateTime testStart = DateTime.Now;

                object clickRequest = new { bot_id = _createdTesterBotName, param_name = test.ButtonName };

                _context.PrintRequest(Module, "bot_click_param_button", clickRequest);
                string clickResponse = _context.Client.ToolsCall("bot_click_param_button", clickRequest);
                _context.PrintResponse(clickResponse);

                if (!TryParseConfig(clickResponse, "bot_click_param_button", out JsonElement clickConfig)
                    || !clickConfig.TryGetProperty("clicked", out JsonElement clicked)
                    || clicked.GetBoolean() == false)
                {
                    _context.RecordFail(Module, method, $"button '{test.ButtonName}' was not clicked");
                    return;
                }

                string report = WaitTestReport(test, testStart);

                if (report == null)
                {
                    _context.RecordFail(Module, method,
                        $"no report for {test.Id} in {TestTimeout.TotalMinutes} minutes");
                    return;
                }

                if (report.Contains("STATUS: PASS")
                    || report.Contains("STATUS: OK"))
                {
                    _context.RecordPass(Module, method, $"{test.Id} passed on real TInvest");
                }
                else
                {
                    _context.RecordFail(Module, method, $"{test.Id} FAILED. Report: {CompactReport(report)}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool SetTesterParams(ServerTestInfo test)
        {
            const string method = "tester_set_params";

            try
            {
                string portfolio = GetFirstPortfolioNumber();

                if (string.IsNullOrWhiteSpace(portfolio))
                {
                    _context.RecordFail(Module, method, "no portfolio on the TInvest instance");
                    return false;
                }

                // BotTab tests are not registered in this build, so the volume is always 1 lot.

                decimal volume = 1m;

                Dictionary<string, object> parameters = new Dictionary<string, object>()
                {
                    { "Portfolio. " + test.ParamsGroup, portfolio },
                    { "Sec name. " + test.ParamsGroup, SecurityName },
                    { "Sec class. " + test.ParamsGroup, SecurityClass },
                    { "Volume. " + test.ParamsGroup, volume },
                };

                if (test.Id == "O14")
                {
                    parameters.Add("Count orders test 14", 20);
                }

                object request = new { bot_id = _createdTesterBotName, parameters = parameters };

                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall("bot_set_params", request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                _context.RecordPass(Module, method,
                    $"{test.Id} params set: {SecurityName} / {SecurityClass} / {portfolio}");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private string WaitTestReport(ServerTestInfo test, DateTime testStart)
        {
            string engineDir = Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty;
            string logDir = Path.Combine(engineDir, "Engine", "Log");

            DateTime deadline = DateTime.Now.Add(TestTimeout);

            while (DateTime.Now < deadline)
            {
                try
                {
                    if (Directory.Exists(logDir))
                    {
                        string[] files = Directory.GetFiles(logDir, "*" + TesterBotName + "*Log_*.txt");

                        for (int i = 0; i < files.Length; i++)
                        {
                            if (File.GetLastWriteTime(files[i]) < testStart.AddMinutes(-1))
                            {
                                continue;
                            }

                            string content = ReadFileShared(files[i]);

                            int markerIndex = content.IndexOf(test.ReportMarker, StringComparison.Ordinal);

                            if (markerIndex < 0)
                            {
                                continue;
                            }

                            int statusIndex = content.IndexOf("STATUS:", markerIndex, StringComparison.Ordinal);

                            if (statusIndex < 0)
                            {
                                continue;
                            }

                            int reportEnd = content.IndexOf("SERVICE INFO", statusIndex, StringComparison.Ordinal);

                            if (reportEnd < 0)
                            {
                                reportEnd = Math.Min(statusIndex + 3000, content.Length);
                            }

                            return content.Substring(markerIndex, reportEnd - markerIndex);
                        }
                    }
                }
                catch
                {
                    // log file is being written. Retry
                }

                Thread.Sleep(5000);
            }

            return null;
        }

        private string ReadFileShared(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private string CompactReport(string report)
        {
            string singleLine = report.Replace("\r", " ").Replace("\n", " ");

            while (singleLine.Contains("  "))
            {
                singleLine = singleLine.Replace("  ", " ");
            }

            if (singleLine.Length > 500)
            {
                singleLine = singleLine.Substring(0, 500);
            }

            return singleLine;
        }

        #endregion

        private void Cleanup()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_createdTesterBotName))
                {
                    _context.Client.ToolsCall("bot_delete", new { bot_id = _createdTesterBotName });
                    _createdTesterBotName = string.Empty;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to delete tester robot: {error.Message}");
            }

            try
            {
                if (_serverConnected && _serverNumber >= 0)
                {
                    _context.Client.ToolsCall("server_instance_disconnect",
                        new { type = ServerTypeName, number = _serverNumber });
                    _serverConnected = false;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to disconnect TInvest: {error.Message}");
            }

            try
            {
                if (_serverCreatedByUs && _serverNumber >= 1)
                {
                    _context.Client.ToolsCall("server_instance_delete",
                        new { type = ServerTypeName, number = _serverNumber });
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to delete TInvest instance: {error.Message}");
            }
        }

        #region Response parsing

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            using (JsonDocument document = JsonDocument.Parse(response))
            {
                JsonElement result = document.RootElement;

                if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                {
                    _context.RecordFail(Module, method, "IsError is true");
                    return false;
                }

                if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "Content is empty");
                    return false;
                }

                string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    _context.RecordFail(Module, method, "Content text is empty");
                    return false;
                }

                using (JsonDocument innerDocument = JsonDocument.Parse(text))
                {
                    config = innerDocument.RootElement.Clone();
                    return true;
                }
            }
        }

        private bool TryParseConfigSilent(string response, out JsonElement config)
        {
            config = default;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        return false;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        return false;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return false;
                    }

                    using (JsonDocument innerDocument = JsonDocument.Parse(text))
                    {
                        config = innerDocument.RootElement.Clone();
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
