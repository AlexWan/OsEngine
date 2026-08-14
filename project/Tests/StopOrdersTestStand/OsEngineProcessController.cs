/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace StopOrdersTestStand
{
    /// <summary>
    /// Default implementation of OsEngine process controller.
    /// </summary>
    public class OsEngineProcessController : IDisposable
    {
        private readonly string _osEnginePath;
        private readonly string _apiKey;
        private readonly int _port;

        public Process? CurrentProcess { get; private set; }

        public McpApiClient? Client { get; private set; }

        public OsEngineProcessController(string osEnginePath, int port, string apiKey)
        {
            _osEnginePath = osEnginePath ?? throw new ArgumentNullException(nameof(osEnginePath));
            _port = port;
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public void Restart(string arguments, TimeSpan timeout)
        {
            Stop();
            DisposeClient();

            if (!File.Exists(_osEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {_osEnginePath}");
            }

            string workingDirectory = Path.GetDirectoryName(_osEnginePath) ?? string.Empty;
            string baseUrl = $"http://localhost:{_port}";

            EnsureMcpApiEnabled(workingDirectory);

            ProcessStartInfo startInfo = new ProcessStartInfo(_osEnginePath)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            Console.WriteLine($"Restarting OsEngine: {_osEnginePath} {arguments}");

            CurrentProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start OsEngine process");

            Client = new McpApiClient(baseUrl, _apiKey);

            try
            {
                Client.WaitForReady(timeout);
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException($"MCP API readiness wait failed: {error.Message}", error);
            }

            Console.WriteLine("MCP API is ready after restart.");
        }

        /// <summary>
        /// Ensures the MCP API is enabled in OsEngine settings before the process starts.
        /// Settings file: Engine\McpSettings.txt (port, api key, IsEnabled, IsFullLogEnabled, allowed IPs).
        /// </summary>
        private void EnsureMcpApiEnabled(string workingDirectory)
        {
            try
            {
                string settingsPath = Path.Combine(workingDirectory, "Engine", "McpSettings.txt");

                if (File.Exists(settingsPath))
                {
                    string[] lines = File.ReadAllLines(settingsPath);

                    if (lines.Length >= 4
                        && string.Equals(lines[2].Trim(), "True", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (lines.Length >= 4)
                    {
                        lines[2] = "True";
                        File.WriteAllLines(settingsPath, lines);
                        Console.WriteLine("MCP API was disabled in McpSettings.txt. Enabled for the test stand.");
                    }
                }
                else
                {
                    string engineDir = Path.Combine(workingDirectory, "Engine");

                    if (Directory.Exists(engineDir) == false)
                    {
                        return;
                    }

                    File.WriteAllLines(settingsPath, new[]
                    {
                        _port.ToString(),
                        _apiKey,
                        "True",
                        "False",
                        "127.0.0.1|any;::1|any"
                    });

                    Console.WriteLine("McpSettings.txt created with MCP API enabled for the test stand.");
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"Failed to ensure MCP API is enabled: {error.Message}");
            }
        }

        public void Stop()
        {
            if (CurrentProcess != null && !CurrentProcess.HasExited)
            {
                try
                {
                    CurrentProcess.Kill();
                    CurrentProcess.WaitForExit(5000);
                }
                catch (Exception error)
                {
                    Console.WriteLine($"Failed to stop OsEngine: {error.Message}");
                }
            }

            CurrentProcess?.Dispose();
            CurrentProcess = null;
        }

        public void Dispose()
        {
            DisposeClient();
            Stop();
        }

        private void DisposeClient()
        {
            if (Client != null)
            {
                try
                {
                    Client.Dispose();
                }
                catch (Exception error)
                {
                    Console.WriteLine($"Failed to dispose MCP client: {error.Message}");
                }

                Client = null;
            }
        }
    }
}
