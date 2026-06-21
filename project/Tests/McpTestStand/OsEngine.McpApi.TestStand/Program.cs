/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace OsEngine.McpApi.TestStand
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.InputEncoding = System.Text.Encoding.UTF8;

                RunTestStand(args);
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine($"Test stand failed: {error}");
                return 1;
            }
        }

        private static void RunTestStand(string[] args)
        {
            TestStandOptions options = ParseOptions(args);

            string osEnginePath = options.OsEnginePath;

            if (!File.Exists(osEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {osEnginePath}");
            }

            Console.WriteLine($"Starting OsEngine: {osEnginePath}");

            Process? osEngineProcess = null;
            McpApiClient? client = null;

            try
            {
                string workingDirectory = Path.GetDirectoryName(osEnginePath) ?? string.Empty;

                osEngineProcess = Process.Start(new ProcessStartInfo(osEnginePath)
                {
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }) ?? throw new InvalidOperationException("Failed to start OsEngine process");

                client = new McpApiClient(options.BaseUrl, options.ApiKey);

                Console.WriteLine("Waiting for MCP API ready...");

                try
                {
                    client.WaitForReady(TimeSpan.FromSeconds(options.TimeoutSeconds));
                }
                catch (TimeoutException error)
                {
                    throw new TimeoutException($"MCP API readiness wait failed: {error.Message}", error);
                }

                Console.WriteLine("MCP API is ready. Running tests...");
                Console.WriteLine();

                var runner = new TestRunner(client);
                List<TestResult> results = runner.RunAll();

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
                try
                {
                    client?.Dispose();
                }
                catch (Exception error)
                {
                    Console.WriteLine($"Failed to dispose client: {error.Message}");
                }

                if (osEngineProcess != null && !osEngineProcess.HasExited)
                {
                    try
                    {
                        osEngineProcess.Kill();
                        osEngineProcess.WaitForExit(5000);
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine($"Failed to stop OsEngine: {error.Message}");
                    }
                }

                osEngineProcess?.Dispose();
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
        }
    }
}
