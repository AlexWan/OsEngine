/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OsEngine.McpApi.TestStand
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

            var startInfo = new ProcessStartInfo(_osEnginePath)
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
