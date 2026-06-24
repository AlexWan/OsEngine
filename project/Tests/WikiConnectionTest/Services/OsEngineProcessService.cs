/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Diagnostics;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Manages the OsEngine process lifecycle.
    /// </summary>
    public class OsEngineProcessService : IDisposable
    {
        private readonly string _osEnginePath;
        private Process? _process;
        private bool _startedByUs;

        public bool IsRunning => _process != null && !_process.HasExited;

        public OsEngineProcessService(string osEnginePath)
        {
            _osEnginePath = osEnginePath ?? throw new ArgumentNullException(nameof(osEnginePath));
        }

        public void EnsureRunning()
        {
            if (IsRunning)
            {
                return;
            }

            Process[] existing = Process.GetProcessesByName("OsEngine");

            if (existing.Length > 0)
            {
                Console.WriteLine("[Process] OsEngine is already running. Attaching to existing process.");
                _process = existing[0];
                _startedByUs = false;
                return;
            }

            if (!File.Exists(_osEnginePath))
            {
                throw new FileNotFoundException($"OsEngine.exe not found: {_osEnginePath}");
            }

            string workingDirectory = Path.GetDirectoryName(_osEnginePath) ?? string.Empty;

            var startInfo = new ProcessStartInfo(_osEnginePath)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Console.WriteLine($"[Process] Starting OsEngine: {_osEnginePath}");

            _process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start OsEngine process");

            _startedByUs = true;
        }

        public void Stop()
        {
            if (_process == null || _process.HasExited)
            {
                return;
            }

            if (!_startedByUs)
            {
                Console.WriteLine("[Process] OsEngine was running before we started. Leaving it running.");
                return;
            }

            Console.WriteLine("[Process] Stopping OsEngine...");

            try
            {
                _process.Kill();
                _process.WaitForExit(5000);
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Process] Failed to stop OsEngine: {error.Message}");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
