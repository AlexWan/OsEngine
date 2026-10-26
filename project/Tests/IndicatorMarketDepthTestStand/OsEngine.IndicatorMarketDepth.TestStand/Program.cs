/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using OsEngine.IndicatorMarketDepth.TestStand.Tests;

namespace OsEngine.IndicatorMarketDepth.TestStand
{
    class Program
    {
        private static readonly List<string> _moduleCatalog = new List<string>();

        private static int _matchedModules = 0;

        private static string _moduleFilter = string.Empty;

        static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
                // ignore
            }

            string logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"indicator-market-depth-test-stand-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            StreamWriter fileWriter = null;
            TextWriter originalOut = null;

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
                        // ignore
                    }
                }

                fileWriter?.Dispose();
            }
        }

        private static int RunTestStand(string[] args)
        {
            StandOptions options = ParseOptions(args);

            if (!File.Exists(options.OsEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {options.OsEnginePath}");
            }

            using (OsEngineProcessController processController = new OsEngineProcessController(options.OsEnginePath, options.Port, options.ApiKey))
            {
                processController.Restart(options.OsEngineArgs, TimeSpan.FromSeconds(options.TimeoutSeconds));

                McpApiClient client = processController.Client
                    ?? throw new InvalidOperationException("MCP client is not available after restart");

                Console.WriteLine("Running tests...");
                Console.WriteLine($"Module filter: {options.ModuleFilter}");
                Console.WriteLine($"Indicators: {string.Join(", ", options.Indicators)}");
                Console.WriteLine();

                TestContext context = new TestContext(client, processController, options.OsEnginePath, options.Port, options.ApiKey, options.TimeoutSeconds)
                {
                    Security = options.Security,
                    DateFrom = options.DateFrom,
                    DateTo = options.DateTo,
                    MinDataDays = options.MinDataDays
                };

                context.PrintHeader();

                Stopwatch stopwatch = Stopwatch.StartNew();

                RunAllTests(context, options);

                stopwatch.Stop();
                context.PrintSummary(stopwatch.Elapsed);

                return context.Failed;
            }
        }

        private static void RunAllTests(TestContext context, StandOptions options)
        {
            _moduleFilter = options.ModuleFilter ?? string.Empty;
            _matchedModules = 0;

            RunModule(context, 1, "Data", () => new DataPreparation(context).PrepareSet());

            int number = 2;

            foreach (string indicator in options.Indicators)
            {
                string indicatorName = indicator;
                RunModule(context, number, indicatorName, () => new IndicatorModule(context, indicatorName).Run());
                number++;
            }

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
                context.RecordFail(name, "Module", error.Message);
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

        private static StandOptions ParseOptions(string[] args)
        {
            StandOptions options = new StandOptions
            {
                OsEnginePath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..", "..",
                    "OsEngine", "bin", "Debug", "OsEngine.exe")),
                Port = 6500,
                ApiKey = "osengine-mcp-default-key",
                TimeoutSeconds = 60,
                NoWait = false,
                OsEngineArgs = "-testerlight",
                Indicators = new List<string> { "OrderBookImbalance", "DepthVolume", "Spread", "Delta", "SpeedOfTape", "Rvol" },
                Security = "SBER"
            };

            options.DateTo = DateTime.Now.Date.AddDays(-2);

            while (options.DateTo.DayOfWeek == DayOfWeek.Saturday || options.DateTo.DayOfWeek == DayOfWeek.Sunday)
            {
                options.DateTo = options.DateTo.AddDays(-1);
            }

            options.DateFrom = options.DateTo.AddDays(-9);
            options.MinDataDays = 3;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "--os-engine-path":
                        options.OsEnginePath = args[++i];
                        break;
                    case "--port":
                        options.Port = int.Parse(args[++i]);
                        break;
                    case "--api-key":
                        options.ApiKey = args[++i];
                        break;
                    case "--timeout":
                        options.TimeoutSeconds = int.Parse(args[++i]);
                        break;
                    case "--no-wait":
                        options.NoWait = true;
                        break;
                    case "--module":
                        options.ModuleFilter = args[++i];
                        break;
                    case "--indicator":
                        options.Indicators = new List<string> { args[++i] };
                        break;
                    case "--security":
                        options.Security = args[++i];
                        break;
                    case "--data-from":
                        options.DateFrom = DateTime.Parse(args[++i]);
                        break;
                    case "--data-to":
                        options.DateTo = DateTime.Parse(args[++i]);
                        break;
                    case "--min-data-days":
                        options.MinDataDays = int.Parse(args[++i]);
                        break;
                }
            }

            return options;
        }
    }

    public class StandOptions
    {
        public string OsEnginePath = string.Empty;
        public int Port;
        public string ApiKey = string.Empty;
        public int TimeoutSeconds;
        public bool NoWait;
        public string OsEngineArgs = string.Empty;
        public List<string> Indicators = new List<string>();
        public string Security = string.Empty;
        public DateTime DateFrom;
        public DateTime DateTo;
        public int MinDataDays;
        public string ModuleFilter = string.Empty;
    }

    public class MultiTextWriter : TextWriter
    {
        private readonly TextWriter _primary;

        private readonly TextWriter _secondary;

        public MultiTextWriter(TextWriter primary, TextWriter secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public override Encoding Encoding => _primary.Encoding;

        public override void Write(char value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }

        public override void WriteLine(string value)
        {
            _primary.WriteLine(value);
            _secondary.WriteLine(value);
        }

        public override void Flush()
        {
            _primary.Flush();
            _secondary.Flush();
        }
    }
}
