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
using StopOrdersTestStand.Tests;

namespace StopOrdersTestStand
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
                $"stop-orders-test-stand-{DateTime.Now:yyyyMMdd-HHmmss}.log");

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

                Console.Title = "OsEngine Stop Orders Test Stand";
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;

                TextWriter consoleOut = hConOut != null && !hConOut.IsInvalid
                    ? new StreamWriter(new FileStream(hConOut, FileAccess.Write), Encoding.UTF8) { AutoFlush = true }
                    : new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };

                TextWriter consoleError = hConOut != null && !hConOut.IsInvalid
                    ? new StreamWriter(new FileStream(hConOut, FileAccess.Write), Encoding.UTF8) { AutoFlush = true }
                    : new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true };

                MultiTextWriter multiWriter = new MultiTextWriter(originalOut, consoleOut, fileWriter);
                MultiTextWriter multiError = new MultiTextWriter(originalError, consoleError, fileWriter);

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
                foreach (string oldLog in Directory.GetFiles(baseDir, "stop-orders-test-stand-*.log"))
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
            TestStandConfig config = TestStandConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
            TestStandOptions options = ParseOptions(args, config);

            if (!File.Exists(options.OsEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {options.OsEnginePath}");
            }

            using (OsEngineProcessController processController = new OsEngineProcessController(options.OsEnginePath, options.Port, options.ApiKey))
            {
                try
                {
                    processController.Restart(string.Empty, TimeSpan.FromSeconds(options.TimeoutSeconds));

                    McpApiClient client = processController.Client
                        ?? throw new InvalidOperationException("MCP client is not available after process restart");

                    Console.WriteLine("Running tests...");

                    if (options.ModuleFilter.Length > 0)
                    {
                        Console.WriteLine($"Module filter: {options.ModuleFilter}");
                    }

                    Console.WriteLine();

                    string testStandDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    TestSecrets secrets = TestSecrets.Load(testStandDirectory);

                    TestContext context = new TestContext(
                        client,
                        processController,
                        options.OsEnginePath,
                        options.Port,
                        options.ApiKey,
                        options.TimeoutSeconds,
                        secrets,
                        config,
                        options.LiveTrade);

                    context.PrintHeader();

                    List<TestResult> results = RunAllTests(context, options.ModuleFilter, options.TestFilter);

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
                }
            }
        }

        private static string _moduleFilter = string.Empty;
        private static string _testFilter = "all";
        private static int _matchedModules = 0;
        private static List<string> _moduleCatalog = new List<string>();

        private static List<TestResult> RunAllTests(TestContext context, string moduleFilter, string testFilter)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            _moduleFilter = moduleFilter ?? string.Empty;
            _testFilter = string.IsNullOrWhiteSpace(testFilter) ? "all" : testFilter;
            _matchedModules = 0;
            _moduleCatalog.Clear();

            try
            {
                // Модуль: SaveCompatibility
                // Обратная совместимость сохранений позиций (Engine\<tab>DealController.txt):
                // эталонный файл старого формата (без поля PriceCondition) подкладывается до создания
                // робота, затем через MCP API проверяется, что позиции загрузились без потерь.
                // Запускает OsEngine перед собой: да, в режиме BotStationLight (-robotslight).
                // Останавливает OsEngine после себя: да.
                RunModule(context, 1, "SaveCompatibility", "-robotslight", () => new Module1_SaveCompatibilityTests(context).RunAll());

                // Модуль: ServerTests
                // Прогон тестов робота WServerTester на реальном подключении Т-Инвест:
                // O13/O14 - серверные стоп-ордера на сыром IServer,
                // B1..B12 - методы BotTabSimple из региона Server stop orders.
                // Выбор тестов: --test O13,O14,B1 или --test all (по умолчанию все по порядку).
                // Требуется токен из tinvest-token.txt и торговая сессия (нужны живые тики).
                // Запускает OsEngine перед собой: да, в режиме BotStationLight (-robotslight).
                // Останавливает OsEngine после себя: да.
                RunModule(context, 2, "ServerTests", "-robotslight", () => new Module2_WServerTesterTests(context, _testFilter).RunAll());

                if (_moduleFilter.Length > 0 && _matchedModules == 0)
                {
                    Console.WriteLine($"No modules matched filter '{_moduleFilter}'. Available modules:");

                    foreach (string entry in _moduleCatalog)
                    {
                        Console.WriteLine(entry);
                    }

                    stopwatch.Stop();
                    context.PrintSummary(stopwatch.Elapsed);
                    return new List<TestResult>
                    {
                        TestResult.Failed("ModuleFilter", $"No modules matched filter '{_moduleFilter}'")
                    };
                }

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

        private static void RunModule(TestContext context, int number, string name, string mode, Action run)
        {
            _moduleCatalog.Add($" {number,2}. {name}");

            // фильтр проверяем до перезапуска OsEngine: пропуск модуля не должен стоить времени
            if (_moduleFilter.Length > 0 && !ModuleMatches(_moduleFilter, number, name))
            {
                return;
            }

            _matchedModules++;
            Console.WriteLine($"[Module {number}] {name}");

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

        private static bool ModuleMatches(string filter, int number, string name)
        {
            string[] tokens = filter.Split(',');

            foreach (string rawToken in tokens)
            {
                string token = rawToken.Trim();

                if (token.Length == 0)
                {
                    continue;
                }

                // токен целиком из цифр — это номер модуля, иначе — подстрока имени
                if (int.TryParse(token, out int moduleNumber))
                {
                    if (moduleNumber == number)
                    {
                        return true;
                    }
                }
                else if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // Приоритет: аргументы командной строки > test-stand-config.json > встроенные дефолты
        private static TestStandOptions ParseOptions(string[] args, TestStandConfig config)
        {
            TestStandOptions options = new TestStandOptions
            {
                OsEnginePath = string.IsNullOrWhiteSpace(config.OsEnginePath)
                    ? Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "..",
                        "OsEngine", "bin", "Debug", "OsEngine.exe"))
                    : Path.GetFullPath(config.OsEnginePath),
                Port = config.Port,
                ApiKey = config.ApiKey,
                TimeoutSeconds = config.TimeoutSeconds,
                NoWait = false
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
                else if (arg == "--live-trade")
                {
                    options.LiveTrade = true;
                }
                else if (arg == "--test" && i + 1 < args.Length)
                {
                    options.TestFilter = args[++i];
                }
                else if ((arg == "--module" || arg == "-m") && i + 1 < args.Length)
                {
                    options.ModuleFilter = args[++i];
                }
                else if (!arg.StartsWith("--"))
                {
                    options.OsEnginePath = Path.GetFullPath(arg);
                }
            }

            options.BaseUrl = $"http://localhost:{options.Port}";
            return options;
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
            public int Port;
            public string ApiKey = string.Empty;
            public string BaseUrl = string.Empty;
            public int TimeoutSeconds;
            public bool NoWait;
            public bool LiveTrade;
            public string ModuleFilter = string.Empty;
            public string TestFilter = "all";
        }
    }
}
