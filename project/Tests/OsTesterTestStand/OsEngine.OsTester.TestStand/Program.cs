/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using OsEngine.OsTester.TestStand.Tests;

namespace OsEngine.OsTester.TestStand
{
    /// <summary>
    /// Штатный авто-тест тестера OsEngine (штатный контур Мастера Tester).
    /// Поднимает OsEngine в режиме testerlight и гоняет тесты через MCP:
    /// данные, очистка роботов, BotTabSimple, BotTabScreener, настройки,
    /// типы данных, начисления (дивиденды/маржа/налоги), отчёты.
    /// </summary>
    class Program
    {
        private static readonly List<string> _moduleCatalog = new List<string>();

        private static int _matchedModules = 0;

        private static string _moduleFilter = string.Empty;

        #region Entry point

        static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
                // консоль может не поддерживать UTF-8 — остаёмся на дефолте
            }

            string logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"ostester-test-stand-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            StreamWriter? fileWriter = null;
            TextWriter? originalOut = null;

            try
            {
                originalOut = Console.Out;

                fileWriter = new StreamWriter(logPath, false, Encoding.UTF8) { AutoFlush = true };

                Console.SetOut(new MultiTextWriter(originalOut, fileWriter));

                Console.WriteLine($"Log file: {logPath}");

                int failed = RunTestStand(args);
                return failed > 0 ? 1 : 0;
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
                    try
                    {
                        Console.SetOut(originalOut);
                    }
                    catch
                    {
                        // восстановить поток вывода не удалось — уже завершаемся
                    }
                }

                fileWriter?.Dispose();
            }
        }

        #endregion

        #region Test execution

        private static int RunTestStand(string[] args)
        {
            TestStandOptions options = ParseOptions(args);

            if (!File.Exists(options.OsEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {options.OsEnginePath}");
            }

            using (OsEngineProcessController processController = new OsEngineProcessController(options.OsEnginePath, options.Port, options.ApiKey, options.StreamableHttp))
            {
                processController.Restart(options.OsEngineArgs, TimeSpan.FromSeconds(options.TimeoutSeconds));

                McpApiClient client = processController.Client
                    ?? throw new InvalidOperationException("MCP client is not available after restart");

                Console.WriteLine("Running tests...");
                Console.WriteLine($"Module filter: {options.ModuleFilter}");
                Console.WriteLine();

                TestSecrets secrets = TestSecrets.Load(AppDomain.CurrentDomain.BaseDirectory, allowPrompt: !options.NoWait);

                TestContext context = new TestContext(
                    client,
                    processController,
                    options.OsEnginePath,
                    options.Port,
                    options.ApiKey,
                    options.TimeoutSeconds,
                    secrets);

                context.DataSetName = options.DataSetName;

                context.PrintHeader();

                Console.WriteLine($"Transport: {(options.StreamableHttp ? "v2 (Streamable HTTP)" : "v1")}");
                Console.WriteLine();

                Stopwatch stopwatch = Stopwatch.StartNew();

                RunAllTests(context, options.ModuleFilter);

                stopwatch.Stop();
                context.PrintSummary(stopwatch.Elapsed);

                return context.Failed;
            }
        }

        private static void RunAllTests(TestContext context, string moduleFilter)
        {
            _moduleFilter = moduleFilter ?? string.Empty;
            _matchedModules = 0;

            // 1. Данные — наличие данных для всех тестов.
            RunModule(context, 1, "Data", () => new DataTests(context).RunAll());

            // 2. Очистка — убрать всех роботов со старых прогонов.
            RunModule(context, 2, "Cleanup", () => new CleanupTests(context).RunAll());

            // 3. BotTabSimple — два штатных робота на длинном периоде.
            RunModule(context, 3, "Simple", () => new SimpleTests(context).RunAll());

            // 4. BotTabScreener — два робота-скринера.
            RunModule(context, 4, "Screener", () => new ScreenerTests(context).RunAll());

            // 5. Настройки тестера.
            RunModule(context, 5, "Settings", () => new SettingsTests(context).RunAll());

            // 6. Тип данных TickAllCandleState.
            RunModule(context, 6, "TickAllCandleState", () => new TickAllCandleStateTests(context).RunAll());

            // 7. Тип данных MarketDepthAllCandleState.
            RunModule(context, 7, "MarketDepthAllCandleState", () => new MarketDepthAllCandleStateTests(context).RunAll());

            // 8. Отчёты.
            RunModule(context, 8, "Report", () => new ReportTests(context).RunAll());

            if (_moduleFilter.Length > 0 && _matchedModules == 0)
            {
                Console.WriteLine($"No modules matched filter '{_moduleFilter}'. Available modules:");

                foreach (string entry in _moduleCatalog)
                {
                    Console.WriteLine(entry);
                }
            }
        }

        private static void RunModule(TestContext context, int number, string name, Action run)
        {
            _moduleCatalog.Add($" {number,2}. {name}");

            if (_moduleFilter.Length > 0 && ModuleMatches(_moduleFilter, number, name) == false)
            {
                return;
            }

            _matchedModules++;
            Console.WriteLine($"[Module {number}] {name}");

            try
            {
                run();
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{name}] Module failed: {error.Message}");
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

                if (int.TryParse(token, out int moduleNumber))
                {
                    if (moduleNumber == number)
                    {
                        return true;
                    }
                }
                else if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Command line

        private static TestStandOptions ParseOptions(string[] args)
        {
            TestStandOptions options = new TestStandOptions
            {
                OsEnginePath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..", "..",
                    "OsEngine", "bin", "Debug", "OsEngine.exe")),
                Port = 6500,
                ApiKey = "osengine-mcp-default-key",
                TimeoutSeconds = 60,
                NoWait = false,
                StreamableHttp = true,
                OsEngineArgs = "-testerlight"
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
                else if ((arg == "--module" || arg == "-m") && i + 1 < args.Length)
                {
                    options.ModuleFilter = args[++i];
                }
                else if (arg == "--set" && i + 1 < args.Length)
                {
                    options.DataSetName = args[++i];
                }
                else if (arg == "--transport" && i + 1 < args.Length)
                {
                    string transport = args[++i].ToLowerInvariant();
                    if (transport == "v1" || transport == "1")
                    {
                        options.StreamableHttp = false;
                    }
                }
                else if (!arg.StartsWith("--"))
                {
                    options.OsEnginePath = Path.GetFullPath(arg);
                }
            }

            return options;
        }

        #endregion

        #region Helper classes

        private class MultiTextWriter : TextWriter
        {
            private readonly TextWriter[] _writers;

            public MultiTextWriter(params TextWriter[] writers)
            {
                _writers = writers;
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void WriteLine(string? value)
            {
                foreach (TextWriter writer in _writers)
                {
                    try
                    {
                        writer.WriteLine(value);
                    }
                    catch
                    {
                        // пишем во все потоки по возможности — ошибка одного не важна
                    }
                }
            }

            public override void Write(string? value)
            {
                foreach (TextWriter writer in _writers)
                {
                    try
                    {
                        writer.Write(value);
                    }
                    catch
                    {
                        // пишем во все потоки по возможности — ошибка одного не важна
                    }
                }
            }
        }

        private class TestStandOptions
        {
            public string OsEnginePath = string.Empty;
            public string OsEngineArgs = string.Empty;
            public int Port;
            public string ApiKey = string.Empty;
            public int TimeoutSeconds;
            public bool NoWait;
            public bool StreamableHttp = true;
            public string ModuleFilter = string.Empty;
            public string DataSetName = "TesterSBER";
        }

        #endregion
    }
}
