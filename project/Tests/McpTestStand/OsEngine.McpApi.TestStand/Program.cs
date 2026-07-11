/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using OsEngine.McpApi.TestStand.Tests;

namespace OsEngine.McpApi.TestStand
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_SHOW = 5;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_ERROR_HANDLE = -12;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        static int Main(string[] args)
        {
            string logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"mcp-test-stand-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            StreamWriter? fileWriter = null;
            TextWriter? originalOut = null;
            TextWriter? originalError = null;
            SafeFileHandle? hConOut = null;

            try
            {
                Stream originalStdoutStream = Console.OpenStandardOutput();
                Stream originalStderrStream = Console.OpenStandardError();
                originalOut = new StreamWriter(originalStdoutStream, Encoding.UTF8) { AutoFlush = true };
                originalError = new StreamWriter(originalStderrStream, Encoding.UTF8) { AutoFlush = true };

                CleanupOldLogFiles(logPath);

                fileWriter = new StreamWriter(logPath, false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                FreeConsole();
                AllocConsole();

                hConOut = CreateFile(
                    "CONOUT$",
                    GENERIC_WRITE,
                    FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hConOut != null && !hConOut.IsInvalid)
                {
                    SetStdHandle(STD_OUTPUT_HANDLE, hConOut.DangerousGetHandle());
                    SetStdHandle(STD_ERROR_HANDLE, hConOut.DangerousGetHandle());
                }

                IntPtr hWnd = GetConsoleWindow();
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_SHOW);
                    SetForegroundWindow(hWnd);
                }

                Console.Title = "OsEngine MCP API Test Stand";
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;

                TextWriter consoleOut = hConOut != null && !hConOut.IsInvalid
                    ? new StreamWriter(new FileStream(hConOut, FileAccess.Write), Encoding.UTF8) { AutoFlush = true }
                    : new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };

                TextWriter consoleError = hConOut != null && !hConOut.IsInvalid
                    ? new StreamWriter(new FileStream(hConOut, FileAccess.Write), Encoding.UTF8) { AutoFlush = true }
                    : new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true };

                var multiWriter = new MultiTextWriter(originalOut, consoleOut, fileWriter);
                var multiError = new MultiTextWriter(originalError, consoleError, fileWriter);

                Console.SetOut(multiWriter);
                Console.SetError(multiError);

                Console.WriteLine($"Log file: {logPath}");

                RunTestStand(args);
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine($"Test stand failed: {error}");
                return 1;
            }
            finally
            {
                if (originalOut != null)
                {
                    try { Console.SetOut(originalOut); } catch { }
                }
                if (originalError != null)
                {
                    try { Console.SetError(originalError); } catch { }
                }
                fileWriter?.Dispose();
                hConOut?.Dispose();
            }
        }

        private static void CleanupOldLogFiles(string currentLogPath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string oldLog in Directory.GetFiles(baseDir, "mcp-test-stand-*.log"))
                {
                    if (string.Equals(oldLog, currentLogPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(oldLog);
                    }
                    catch
                    {
                        // ignore files that are locked or otherwise undeletable
                    }
                }
            }
            catch
            {
                // ignore cleanup errors so the test stand can still run
            }
        }

        private class MultiTextWriter : TextWriter
        {
            private readonly TextWriter[] _writers;

            public MultiTextWriter(params TextWriter[] writers)
            {
                _writers = writers ?? Array.Empty<TextWriter>();
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                foreach (TextWriter writer in _writers)
                {
                    try { writer.Write(value); } catch { }
                }
            }

            public override void Write(string? value)
            {
                foreach (TextWriter writer in _writers)
                {
                    try { writer.Write(value); } catch { }
                }
            }

            public override void WriteLine(string? value)
            {
                foreach (TextWriter writer in _writers)
                {
                    try { writer.WriteLine(value); } catch { }
                }
            }

            public override void Flush()
            {
                foreach (TextWriter writer in _writers)
                {
                    try { writer.Flush(); } catch { }
                }
            }
        }

        private static void RunTestStand(string[] args)
        {
            TestStandOptions options = ParseOptions(args);

            if (!File.Exists(options.OsEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {options.OsEnginePath}");
            }

            using (var processController = new OsEngineProcessController(options.OsEnginePath, options.Port, options.ApiKey))
            {
                try
                {
                    // Remove stale auto-created data folders before starting OsEngine so
                    // previous failed runs cannot pollute the test sets.
                    CleanupAutoDataSets(options.OsEnginePath);

                    processController.Restart(options.OsEngineArgs, TimeSpan.FromSeconds(options.TimeoutSeconds));

                    McpApiClient client = processController.Client
                        ?? throw new InvalidOperationException("MCP client is not available after process restart");

                    Console.WriteLine("Running tests...");
                    Console.WriteLine();

                    string testStandDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    TestSecrets secrets = TestSecrets.Load(testStandDirectory);

                    var context = new TestContext(
                        client,
                        processController,
                        options.OsEnginePath,
                        options.Port,
                        options.ApiKey,
                        options.TimeoutSeconds,
                        secrets);

                    context.PrintHeader();

                    List<TestResult> results = RunAllTests(context);

                    CleanupAutoDataSets(options.OsEnginePath);

                    int failed = 0;

                    foreach (TestResult result in results)
                    {
                        if (!result.Success)
                        {
                            failed++;
                        }
                    }

                    if (!options.NoWait)
                    {
                        WaitIfRunByUser();
                    }

                    if (failed > 0)
                    {
                        throw new InvalidOperationException($"{failed} test(s) failed");
                    }
                }
                finally
                {
                    processController.Stop();
                    CleanupAutoDataSets(options.OsEnginePath);
                }
            }
        }

        private static void CleanupAutoDataSets(string osEnginePath)
        {
            try
            {
                string dataDirectory = Path.Combine(Path.GetDirectoryName(osEnginePath) ?? string.Empty, "Data");

                if (!Directory.Exists(dataDirectory))
                {
                    return;
                }

                string[] patterns = new[] { "Set_Mcp*", "Set_MoexIssTop*" };

                foreach (string pattern in patterns)
                {
                    string[] folders = Directory.GetDirectories(dataDirectory, pattern);

                    foreach (string folder in folders)
                    {
                        try
                        {
                            Directory.Delete(folder, true);
                            Console.WriteLine($"Cleaned up data set folder: {folder}");
                        }
                        catch (Exception cleanupError)
                        {
                            Console.WriteLine($"Failed to cleanup data set folder {folder}: {cleanupError.Message}");
                        }
                    }
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }

        private static List<TestResult> RunAllTests(TestContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Модуль: Protocol
                // MCP API: initialize, notifications/initialized, tools/list.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Protocol", string.Empty, () => new ProtocolTests(context).RunAll());

                // Модуль: Logs
                // MCP API: log_get_emergency_log, log_get_mcp_log.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Logs", string.Empty, () => new LogsTests(context).RunAll());

                // Модуль: Settings
                // MCP API: prime_settings_get, prime_settings_set.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Settings", string.Empty, () => new SettingsTests(context).RunAll());

                // Модуль: Config
                // MCP API: mcp_settings_get, mcp_settings_set.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Config", string.Empty, () => new ConfigTests(context).RunAll());

                // Модуль: ServerManagement
                // MCP API: server_management_get_list, server_management_activate,
                //          server_management_get_trade_connectors, server_management_get_data_connectors,
                //          server_management_get_connector_permissions.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да (внутри активирует коннектор TInvest).
                RunModule(context, "ServerManagement", string.Empty, () => new ServerManagementTests(context).RunAll());

                // Модуль: ServerInstance
                // MCP API: server_management_activate, server_instance_get_params,
                //          server_instance_set_params, server_instance_create,
                //          server_instance_delete, server_instance_connect,
                //          server_instance_disconnect, server_instance_get_status,
                //          server_instance_get_securities, server_instance_get_portfolios,
                //          server_instance_get_log.
                // События: server_instance.status_changed, server_instance.security.updated,
                //          server_instance.portfolio.updated, server_instance.log.
                // Запускает OsEngine перед собой: да, в режиме BotStationLight (-robotslight).
                // Останавливает OsEngine после себя: да.
                RunModule(context, "ServerInstance", "-robotslight", () => new ServerInstanceTests(context).RunAll());

                // Модуль: SSE
                // MCP API: GET /api/v1/events.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "SSE", string.Empty, () => new SseTests(context).RunAll());

                // Модуль: Errors
                // MCP API: POST /api/v1/mcp без ключа, прямой terminal_get_status,
                //          tools/call unknown_tool, tools/call без name.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Errors", string.Empty, () => new ErrorTests(context).RunAll());

                // Модуль: WikiRobots
                // MCP API: wiki_robots_list, wiki_robot_info.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "WikiRobots", string.Empty, () => new WikiRobotsTests(context).RunAll());

                // Модуль: WikiIndicators
                // MCP API: wiki_indicators_list, wiki_indicator_info.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "WikiIndicators", string.Empty, () => new WikiIndicatorsTests(context).RunAll());

                // Модуль: WikiSecurities
                // MCP API: wiki_securities_moex_iss, wiki_securities_tinvest,
                //          wiki_securities_alor, wiki_securities_qscalp,
                //          wiki_securities_mapping_info.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "WikiSecurities", string.Empty, () => new WikiSecuritiesTests(context).RunAll());

                // Модуль: WikiDividends
                // MCP API: wiki_dividends_get_history, wiki_dividends_get_future,
                //          wiki_dividends_get_past, wiki_dividends_search_by_date.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "WikiDividends", string.Empty, () => new WikiDividendsTests(context).RunAll());

                // Модуль: Data
                // MCP API: data_get_sets.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Открывает режим OsData через terminal_open_mode.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Data", string.Empty, () => new DataTests(context).RunAll());

                // Модуль: Tester
                // MCP API: tester_data_get_config, tester_data_set_config,
                //          tester_execution_get_config, tester_execution_set_config,
                //          tester_portfolio_get_config, tester_portfolio_set_config.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Открывает режим Tester через terminal_open_mode.
                // Останавливает OsEngine после себя: да.
                RunModule(context, "Tester", string.Empty, () => new TesterTests(context).RunAll());

                // Модуль: Terminal
                // MCP API: ping, terminal_get_status, terminal_launch, terminal_stop, terminal_kill.
                // Запускает OsEngine перед собой: да, без аргументов.
                // Останавливает OsEngine после себя: да (terminal_stop / terminal_kill внутри модуля).
                // Terminal tests are always last because launch/stop/kill
                // terminate or restart the OsEngine process.
                RunModule(context, "Terminal", string.Empty, () => new TerminalTests(context).RunAll());

                stopwatch.Stop();
                context.PrintSummary(stopwatch.Elapsed);
                return context.Results;
            }
            catch (Exception error)
            {
                stopwatch.Stop();
                context.PrintSummary(stopwatch.Elapsed);
                return new List<TestResult>
                {
                    TestResult.Failed("RunAll", error.Message)
                };
            }
        }

        private static void RunModule(TestContext context, string name, string mode, Action run)
        {
            try
            {
                context.RestartOsEngine(mode);
                run();
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{name}] Module failed: {error.Message}");
            }
            finally
            {
                context.StopOsEngine();
            }
        }

        private static TestStandOptions ParseOptions(string[] args)
        {
            var options = new TestStandOptions
            {
                OsEnginePath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..", "..",
                    "OsEngine", "bin", "Debug", "OsEngine.exe")),
                Port = 6500,
                ApiKey = "osengine-mcp-default-key",
                TimeoutSeconds = 60,
                NoWait = false,
                OsEngineArgs = string.Empty
            };

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg == "--port" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int port))
                    {
                        options.Port = port;
                    }
                }
                else if (arg == "--api-key" && i + 1 < args.Length)
                {
                    options.ApiKey = args[++i];
                }
                else if (arg == "--timeout" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int timeout))
                    {
                        options.TimeoutSeconds = timeout;
                    }
                }
                else if (arg == "--no-wait")
                {
                    options.NoWait = true;
                }
                else if (arg == "--mode" && i + 1 < args.Length)
                {
                    string mode = args[++i];
                    options.OsEngineArgs = ConvertModeToArgs(mode);
                }
                else if (!arg.StartsWith("--"))
                {
                    options.OsEnginePath = Path.GetFullPath(arg);
                }
            }

            options.BaseUrl = $"http://localhost:{options.Port}";
            return options;
        }

        private static string ConvertModeToArgs(string mode)
        {
            switch (mode?.ToLowerInvariant())
            {
                case "tester":
                    return "-tester";
                case "robots":
                case "trader":
                case "botstation":
                    return "-robots";
                case "robotslight":
                case "botstationlight":
                    return "-robotslight";
                case "data":
                    return "-data";
                case "optimizer":
                    return "-optimizer";
                case "converter":
                    return "-converter";
                case "":
                case null:
                    return string.Empty;
                default:
                    throw new ArgumentException($"Unknown OsEngine mode: {mode}");
            }
        }

        private static void WaitIfRunByUser()
        {
            try
            {
                if (Console.IsInputRedirected || Console.IsOutputRedirected)
                {
                    return;
                }

                uint[] processes = new uint[1];
                uint count = GetConsoleProcessList(processes, 1);

                // If only our process is attached to the console, the user likely
                // launched the executable directly from Explorer. Wait for a key press
                // so the window does not close immediately.
                if (count <= 1)
                {
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                }
            }
            catch
            {
                // ignore
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        private class TestStandOptions
        {
            public string OsEnginePath = string.Empty;
            public string OsEngineArgs = string.Empty;
            public int Port;
            public string ApiKey = string.Empty;
            public string BaseUrl = string.Empty;
            public int TimeoutSeconds;
            public bool NoWait;
        }
    }
}
