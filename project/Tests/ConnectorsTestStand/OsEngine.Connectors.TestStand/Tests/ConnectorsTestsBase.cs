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

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Base runner for WServerTester tests: drives the robot through
    /// the MCP API on a real reference connector connection.
    /// Run data (server type, security, class, volume, timeouts) comes from
    /// test-stand-config.json next to the executable (CLI overrides it).
    ///
    /// The stand checks the WServerTester robot itself ("module is not broken"):
    /// the API works, buttons click, test threads start and produce well-formed
    /// reports (REPORT ... STATUS: OK|FAIL). The report CONTENT is the
    /// responsibility of the test itself - catching errors is its job, so
    /// STATUS: FAIL with caught errors = a successful run of the test.
    /// PASS = test started and a well-formed report arrived.
    /// FAIL = the button was not clicked, the test did not start, or
    /// no/garbage report in time.
    ///
    /// Flow: single server instance with the token -> Connect -> wait securities
    /// -> create WServerTester -> for each requested test set its parameters and
    /// click its start button via bot_click_param_button -> poll the robot log
    /// file (Engine\Log) for the test report.
    ///
    /// Test selection: --test V1,O3,B1 or --test all (default: all module tests).
    /// Tests marked RequiresLiveTrade place real orders on the account
    /// (minimum volume, tests close everything themselves) and run only
    /// with --live-trade. They require trading hours (live ticks).
    /// Without the token file the module is SKIPPED.
    /// </summary>
    public abstract class ConnectorsTestsBase
    {
        private const string TesterStrategyName = "WServerTester";

        /// <summary>
        /// Stand module name (Securities, MarketDepth, Trades, Data, Connection,
        /// Portfolio, Orders, BotTabOrders). Used in results and log.
        /// </summary>
        protected abstract string ModuleName { get; }

        /// <summary>
        /// Tests belonging to this module (subset of KnownTests.All).
        /// </summary>
        protected abstract ServerTestInfo[] ModuleTests { get; }

        /// <summary>
        /// True when the module tests place real orders (require --live-trade).
        /// </summary>
        protected abstract bool ModuleRequiresLiveTrade { get; }

        private string ServerTypeName => _context.Config.ServerTests.ServerType;

        private string TesterBotName => _context.Config.ServerTests.TesterBotName;

        private string SecurityName => _context.Config.ServerTests.SecurityName;

        private string SecurityClass => _context.Config.ServerTests.SecurityClass;

        private TimeSpan ConnectTimeout => TimeSpan.FromSeconds(_context.Config.ServerTests.ConnectTimeoutSeconds);

        private TimeSpan SecuritiesTimeout => TimeSpan.FromSeconds(_context.Config.ServerTests.SecuritiesTimeoutSeconds);

        private TimeSpan TestTimeout => TimeSpan.FromMinutes(_context.Config.ServerTests.TestTimeoutMinutes);

        private TimeSpan TestStartTimeout => TimeSpan.FromMinutes(_context.Config.ServerTests.TestStartTimeoutMinutes);

        private readonly TestContext _context;
        private readonly string _testFilter;

        private string _createdTesterBotName = string.Empty;
        private string _serverName = string.Empty;
        private int _serverNumber = -1;
        private bool _serverCreatedByUs;
        private bool _serverConnected;

        protected ConnectorsTestsBase(TestContext context, string testFilter)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _testFilter = string.IsNullOrWhiteSpace(testFilter) ? "all" : testFilter;
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(ModuleName);

            try
            {
                if (!_context.Secrets.HasToken)
                {
                    _context.RecordPass(ModuleName, "skipped",
                        $"SKIPPED: {_context.Config.ServerTests.TokenFileName} not found next to the executable");
                    return;
                }

                if (ModuleRequiresLiveTrade && !_context.LiveTrade)
                {
                    _context.RecordPass(ModuleName, "skipped",
                        "SKIPPED: module places real orders and requires --live-trade");
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
                _context.RecordFail(ModuleName, "RunAll", error.Message);
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
                return new List<ServerTestInfo>(ModuleTests);
            }

            List<ServerTestInfo> result = new List<ServerTestInfo>();

            string[] requested = _testFilter.Split(',');

            for (int i = 0; i < requested.Length; i++)
            {
                string id = requested[i].Trim();
                ServerTestInfo found = null;

                for (int j = 0; j < ModuleTests.Length; j++)
                {
                    if (string.Equals(ModuleTests[j].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        found = ModuleTests[j];
                        break;
                    }
                }

                if (found == null)
                {
                    _context.RecordFail(ModuleName, "test_selection",
                        $"unknown test '{id}'. Known in module {ModuleName}: {string.Join(", ", ModuleTests.Select(t => t.Id))}");
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

            _context.RecordFail(ModuleName, "wait_robot_master", "robot master is not available 90 seconds after engine start");
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
                    _context.RecordFail(ModuleName, method, "servers list did not stabilize in 90 seconds");
                    return false;
                }

                List<JsonElement> ownServers = allServers
                    .Where(s => s.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == ServerTypeName)
                    .ToList();

                if (allServers.Count > 1
                    && ownServers.Count != allServers.Count)
                {
                    _context.RecordFail(ModuleName, method,
                        "WServerTester requires exactly one server in the system. " +
                        $"Found {allServers.Count}. Remove extra servers from the connection window");
                    return false;
                }

                if (ownServers.Count == 0)
                {
                    // нет ни одного инстанса - активируем коннектор (создаёт базовый инстанс)
                    _context.Client.ToolsCall("server_management_activate", new { type = ServerTypeName });

                    Thread.Sleep(3000);

                    string afterActivateResponse = _context.Client.ToolsCall("server_management_get_list", new { });

                    if (TryParseConfigSilent(afterActivateResponse, out JsonElement afterActivate)
                        && afterActivate.ValueKind == JsonValueKind.Array)
                    {
                        ownServers = afterActivate.EnumerateArray()
                            .Where(s => s.TryGetProperty("type", out JsonElement type)
                                && type.GetString() == ServerTypeName)
                            .Select(e => e.Clone())
                            .ToList();
                    }
                }

                if (ownServers.Count == 0)
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
                    ownServers.Sort((a, b) => a.GetProperty("number").GetInt32()
                        .CompareTo(b.GetProperty("number").GetInt32()));

                    _serverNumber = ownServers[0].GetProperty("number").GetInt32();
                    _serverName = ownServers[0].GetProperty("name").GetString() ?? string.Empty;

                    for (int i = 1; i < ownServers.Count; i++)
                    {
                        int extraNumber = ownServers[i].GetProperty("number").GetInt32();

                        _context.Client.ToolsCall("server_instance_disconnect",
                            new { type = ServerTypeName, number = extraNumber });

                        Thread.Sleep(3000);

                        _context.Client.ToolsCall("server_instance_delete",
                            new { type = ServerTypeName, number = extraNumber });
                    }

                    // проверяем, что лишние инстансы действительно удалены (с повторами)
                    DateTime deadline = DateTime.Now.AddSeconds(60);
                    int leftCount = -1;

                    while (DateTime.Now < deadline)
                    {
                        string recheckResponse = _context.Client.ToolsCall("server_management_get_list", new { });

                        if (TryParseConfigSilent(recheckResponse, out JsonElement recheckConfig)
                            && recheckConfig.ValueKind == JsonValueKind.Array)
                        {
                            leftCount = recheckConfig.EnumerateArray()
                                .Count(s => s.TryGetProperty("type", out JsonElement type)
                                    && type.GetString() == ServerTypeName);

                            if (leftCount == 1)
                            {
                                break;
                            }
                        }

                        Thread.Sleep(2000);
                    }

                    if (leftCount != 1)
                    {
                        _context.RecordFail(ModuleName, method,
                            $"failed to remove extra {ServerTypeName} instances. Left: {leftCount}");
                        return false;
                    }
                }

                _context.RecordPass(ModuleName, method,
                    $"single {ServerTypeName} instance: {_serverName} (#{_serverNumber})");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
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

                    Console.WriteLine($"[{ModuleName}] servers list raw: {TrimForLog(response)}");

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
                    _context.RecordPass(ModuleName, method,
                        $"no Password (token) parameter found on the {ServerTypeName} instance. Skipped");
                    return true;
                }

                object setRequest = new
                {
                    type = ServerTypeName,
                    number = _serverNumber,
                    parameters = new[]
                    {
                        new { name = tokenParamName, value = _context.Secrets.Token }
                    }
                };

                string setResponse = _context.Client.ToolsCall(method, setRequest);

                if (!TryParseConfig(setResponse, method, out JsonElement setConfig)
                    || !setConfig.TryGetProperty("success", out JsonElement success)
                    || success.ValueKind != JsonValueKind.True)
                {
                    _context.RecordFail(ModuleName, method, "set_params did not return success");
                    return false;
                }

                _context.RecordPass(ModuleName, method, $"token set into parameter '{tokenParamName}'");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
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
                        _context.RecordPass(ModuleName, method, $"{_serverName} connected");
                        return true;
                    }

                    Thread.Sleep(1000);
                }

                _context.RecordFail(ModuleName, method, "status Connect not reached in time");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
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
                        _context.RecordPass(ModuleName, method, $"securities received: {count.GetInt32()}");
                        return true;
                    }

                    Thread.Sleep(2000);
                }

                _context.RecordFail(ModuleName, method, $"no securities received from {ServerTypeName}");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
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
                Console.WriteLine($"[{ModuleName}] Failed to get portfolios: {error.Message}");
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

                _context.PrintRequest(ModuleName, "bot_create", request);
                string response = _context.Client.ToolsCall("bot_create", request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                _createdTesterBotName = config.GetProperty("name").GetString() ?? string.Empty;

                if (_createdTesterBotName != TesterBotName)
                {
                    _context.RecordFail(ModuleName, method, $"created robot name mismatch: {_createdTesterBotName}");
                    return false;
                }

                _context.RecordPass(ModuleName, method, $"robot '{TesterBotName}' created");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
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

                _context.PrintRequest(ModuleName, "bot_click_param_button", clickRequest);
                string clickResponse = _context.Client.ToolsCall("bot_click_param_button", clickRequest);
                _context.PrintResponse(clickResponse);

                if (!TryParseConfig(clickResponse, "bot_click_param_button", out JsonElement clickConfig)
                    || !clickConfig.TryGetProperty("clicked", out JsonElement clicked)
                    || clicked.GetBoolean() == false)
                {
                    _context.RecordFail(ModuleName, method, $"button '{test.ButtonName}' was not clicked");
                    return;
                }

                if (!WaitTestStartMarker(test, testStart))
                {
                    _context.RecordFail(ModuleName, method,
                        $"{test.Id}: test did not start - no 'Tests started {test.ReportMarker.Replace("REPORT ", string.Empty)}' marker " +
                        $"in the robot log within {TestStartTimeout.TotalMinutes} min");
                    return;
                }

                _context.RecordPass(ModuleName, method + "_started", $"{test.Id} started on {_serverName}");

                string report = WaitTestReport(test, testStart);

                if (report == null)
                {
                    _context.RecordFail(ModuleName, method,
                        $"no report for {test.Id} in {TestTimeout.TotalMinutes} minutes");
                    return;
                }

                EvaluateReport(test, method, report);
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
            }
        }

        private bool SetTesterParams(ServerTestInfo test)
        {
            const string method = "tester_set_params";

            try
            {
                if (test.Params.Length == 0)
                {
                    return true;
                }

                string portfolio = GetFirstPortfolioNumber();

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                TestStandConfig.ServerTestsConfig config = _context.Config.ServerTests;

                for (int i = 0; i < test.Params.Length; i++)
                {
                    TestParam param = test.Params[i];

                    switch (param.Source)
                    {
                        case ParamValueSource.Security:
                            parameters[param.Name] = config.SecurityName;
                            break;

                        case ParamValueSource.SecurityClass:
                            parameters[param.Name] = config.SecurityClass;
                            break;

                        case ParamValueSource.SecuritiesList:
                            parameters[param.Name] = config.SecuritiesList;
                            break;

                        case ParamValueSource.Separator:
                            parameters[param.Name] = config.SecuritiesSeparator;
                            break;

                        case ParamValueSource.Minutes:
                            parameters[param.Name] = config.MinutesToTest;
                            break;

                        case ParamValueSource.StartDate:
                            // пустая дата - оставляем дефолт робота (текущая дата)
                            if (!string.IsNullOrWhiteSpace(config.StartDate))
                            {
                                parameters[param.Name] = config.StartDate;
                            }
                            break;

                        case ParamValueSource.Portfolio:
                            if (string.IsNullOrWhiteSpace(portfolio))
                            {
                                _context.RecordFail(ModuleName, method,
                                    $"no portfolio on the {ServerTypeName} instance");
                                return false;
                            }
                            parameters[param.Name] = portfolio;
                            break;

                        case ParamValueSource.Volume:
                            parameters[param.Name] = config.Volume;
                            break;

                        case ParamValueSource.Asset:
                            parameters[param.Name] = config.AssetInPortfolio;
                            break;

                        case ParamValueSource.CountOrders:
                            parameters[param.Name] = config.CountOrders;
                            break;

                        case ParamValueSource.SecuritiesCount:
                            parameters[param.Name] = config.SecuritiesCount;
                            break;

                        case ParamValueSource.TimeFrame:
                            parameters[param.Name] = config.TimeFrame;
                            break;
                    }
                }

                if (parameters.Count == 0)
                {
                    return true;
                }

                object request = new { bot_id = _createdTesterBotName, parameters = parameters };

                _context.PrintRequest(ModuleName, method, request);
                string response = _context.Client.ToolsCall("bot_set_params", request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement responseConfig))
                {
                    return false;
                }

                _context.RecordPass(ModuleName, method,
                    $"{test.Id} params set: {parameters.Count} parameter(s)");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(ModuleName, method, error.Message);
                return false;
            }
        }

        /// <summary>
        /// Evaluate a test report. The stand only checks that the test ran and
        /// produced a well-formed report (REPORT + STATUS: OK|FAIL). The errors
        /// the test caught are its own business - they are logged as info.
        /// </summary>
        private void EvaluateReport(ServerTestInfo test, string method, string report)
        {
            bool statusOk = report.Contains("STATUS: OK");
            bool statusFail = report.Contains("STATUS: FAIL");

            if (!statusOk && !statusFail)
            {
                _context.RecordFail(ModuleName, method,
                    $"{test.Id}: malformed report - no STATUS: OK|FAIL. Report: {CompactReport(report)}");
                return;
            }

            List<string> actualErrors = ExtractReportErrors(report);

            string message = $"{test.Id}: test ran and reported on {ServerTypeName}. " +
                $"Status: {(statusOk ? "OK" : "FAIL")}";

            if (actualErrors.Count > 0)
            {
                string errorsLine = string.Join("; ", actualErrors);

                if (errorsLine.Length > 300)
                {
                    errorsLine = errorsLine.Substring(0, 300);
                }

                message += $". Errors caught by the test: {actualErrors.Count} ({errorsLine})";
            }

            _context.RecordPass(ModuleName, method, message);
        }

        /// <summary>
        /// Extract error lines from a test report (lines after "Errors:" up to
        /// "SERVICE INFO" or the end of the report).
        /// </summary>
        private List<string> ExtractReportErrors(string report)
        {
            List<string> errors = new List<string>();

            string[] lines = report.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inErrors = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.StartsWith("Errors:", StringComparison.OrdinalIgnoreCase))
                {
                    inErrors = true;
                    continue;
                }

                if (line.StartsWith("SERVICE INFO", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (inErrors && line.Length > 0)
                {
                    errors.Add(line);
                }
            }

            return errors;
        }

        /// <summary>
        /// Wait for the "Tests started &lt;TestClass&gt;" marker in the robot log.
        /// The robot writes this marker when a test thread actually starts
        /// (AServerTester). No marker after the button click = the test did not
        /// start (wrong parameters, precondition errors like "No Servers Found").
        /// </summary>
        private bool WaitTestStartMarker(ServerTestInfo test, DateTime testStart)
        {
            string engineDir = Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty;
            string logDir = Path.Combine(engineDir, "Engine", "Log");
            string marker = "Tests started " + test.ReportMarker.Replace("REPORT ", string.Empty);

            DateTime deadline = DateTime.Now.Add(TestStartTimeout);

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

                            if (content.IndexOf(marker, StringComparison.Ordinal) >= 0)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // log file is being written. Retry
                }

                Thread.Sleep(1000);
            }

            return false;
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
                Console.WriteLine($"[{ModuleName}] Failed to delete tester robot: {error.Message}");
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
                Console.WriteLine($"[{ModuleName}] Failed to disconnect {ServerTypeName}: {error.Message}");
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
                Console.WriteLine($"[{ModuleName}] Failed to delete {ServerTypeName} instance: {error.Message}");
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
                    _context.RecordFail(ModuleName, method, "IsError is true");
                    return false;
                }

                if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                {
                    _context.RecordFail(ModuleName, method, "Content is empty");
                    return false;
                }

                string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    _context.RecordFail(ModuleName, method, "Content text is empty");
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
