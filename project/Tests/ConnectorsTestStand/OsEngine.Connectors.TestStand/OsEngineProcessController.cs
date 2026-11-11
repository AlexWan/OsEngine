/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace OsEngine.Connectors.TestStand
{
    /// <summary>
    /// Default implementation of OsEngine process controller.
    /// </summary>
    public class OsEngineProcessController : IDisposable
    {
        private readonly string _osEnginePath;
        private readonly string _apiKey;
        private readonly int _port;
        private readonly bool _streamableHttp;

        // Телеметрия запуска (BUG-0280): строки [Mem] о памяти и [Proc] о гибели процесса в логе стенда.
        private const int SampleIntervalMs = 10000;

        private Timer? _sampler;
        private volatile int _stopReason;
        private long _lastSampleWs;
        private long _lastSamplePeak;
        private int _deathLogged;
        private EventHandler? _exitedHandler;
        private DateTime _launchTime;
        private int _launchPid;

        public Process? CurrentProcess { get; private set; }

        public McpApiClient? Client { get; private set; }

        public OsEngineProcessController(string osEnginePath, int port, string apiKey, bool streamableHttp = true)
        {
            _osEnginePath = osEnginePath ?? throw new ArgumentNullException(nameof(osEnginePath));
            _port = port;
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamableHttp = streamableHttp;
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

            // Телеметрия включается до WaitForReady: тихая смерть на старте не должна теряться.
            StartTelemetry(CurrentProcess);

            Client = new McpApiClient(baseUrl, _apiKey, _streamableHttp);

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
            StopSampler();

            Process? process = CurrentProcess;

            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        Console.WriteLine($"[Proc] pid={_launchPid} STOP requested at={NowStamp()}");
                        process.Kill();
                        // stand_kill — только после подтверждённого Kill(): иначе гонка
                        // «само-смерть между HasExited и Kill» дала бы ложный stand_kill.
                        _stopReason = 1;

                        if (!process.WaitForExit(5000))
                        {
                            Console.WriteLine($"[Proc] pid={_launchPid} STILL RUNNING after 5000ms (kill not confirmed), exit=n/a");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Proc] pid={_launchPid} already exited exit={ExitCodeText(process)}");
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine($"Failed to stop OsEngine: {error.Message}");
                }
                finally
                {
                    // Запись о гибели — на любой выход из Stop(), включая исключение Kill
                    // (смерть в микроокне HasExited→Kill, отказ доступа): событие Exited пишет
                    // её само; коротко ждём и, если не дождались, пишем сами — строка гибели
                    // не должна теряться (инвариант BUG-0280).
                    if (!WaitDeathLogged(500))
                    {
                        LogDeath(process, _launchPid, _launchTime, _stopReason == 1 ? "stand_kill" : "self_exit");
                    }
                }

                UnsubscribeExited(process);
                process.Dispose();
            }

            CurrentProcess = null;
        }

        #region Process telemetry

        /// <summary>
        /// Включает телеметрию запуска: запись гибели процесса по событию Exited и сэмплер памяти.
        /// Замыкание обработчика держит только переменные этого запуска, общие поля — лишь причина остановки.
        /// </summary>
        private void StartTelemetry(Process process)
        {
            StopSampler();

            Process launch = process;
            int pid = launch.Id;
            DateTime startTime = DateTime.Now;

            _lastSampleWs = 0;
            _lastSamplePeak = 0;
            _stopReason = 0;
            _deathLogged = 0;
            _launchTime = startTime;
            Volatile.Write(ref _launchPid, pid);

            _exitedHandler = delegate
            {
                // Событие Exited от опоздавшего старого запуска не должно доставляться в слот
                // нового: не писать EXITED чужого pid, не глушить его сэмплер, не красть слот гибели.
                if (Volatile.Read(ref _launchPid) != pid)
                {
                    return;
                }

                LogDeath(launch, pid, startTime, _stopReason == 1 ? "stand_kill" : "self_exit");
                StopSampler();
                UnsubscribeExited(launch);
            };

            try
            {
                // Сначала подписка, потом EnableRaisingEvents: иначе гибель в зазоре между
                // включением событий и подпиской обработчика осталась бы незамеченной.
                launch.Exited += _exitedHandler;
                launch.EnableRaisingEvents = true;
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Proc] pid={pid} failed to subscribe process exit: {error.Message}");
            }

            _sampler = new Timer(SampleMemory, null, SampleIntervalMs, SampleIntervalMs);
        }

        /// <summary>Сэмпл памяти движка раз в <see cref="SampleIntervalMs"/> мс: строка [Mem], блокировок нет.</summary>
        private void SampleMemory(object? state)
        {
            try
            {
                Process? process = CurrentProcess;

                if (process == null)
                {
                    StopSampler();
                    return;
                }

                // Process кэширует свойства памяти: без Refresh() второй сэмпл повторил бы первый.
                process.Refresh();

                long workingSet = process.WorkingSet64;
                long privateBytes = process.PrivateMemorySize64;
                long peakWorkingSet = process.PeakWorkingSet64;
                _lastSampleWs = workingSet;
                _lastSamplePeak = peakWorkingSet;

                Console.WriteLine(
                    $"[Mem] pid={process.Id} ws={ToMb(workingSet)} private={ToMb(privateBytes)} peakWS={ToMb(peakWorkingSet)} "
                    + $"gcStand={ToMb(GC.GetTotalMemory(false))} gen0/1/2={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
            }
            catch (Exception error)
            {
                // Процесс ушёл или уже утилизирован между тиками: дальше сэмплов не будет.
                StopSampler();
                Console.WriteLine($"[Mem] sampling stopped: {error.GetType().Name}: {error.Message}");
            }
        }

        /// <summary>Единственная запись о гибели запуска. Повторный вызов того же запуска молчит.</summary>
        private void LogDeath(Process process, int pid, DateTime startTime, string reason)
        {
            if (Interlocked.CompareExchange(ref _deathLogged, 1, 0) != 0)
            {
                return;
            }

            string at = NowStamp();
            string exit = ExitCodeText(process);

            try
            {
                double uptime = Math.Max(0, (DateTime.Now - startTime).TotalSeconds);
                Console.WriteLine(
                    $"[Proc] pid={pid} EXITED exit={exit} at={at} "
                    + $"uptime={uptime.ToString("F1", CultureInfo.InvariantCulture)}s reason={reason} "
                    + $"peakWS={PeakText(process)} lastWS={(_lastSampleWs > 0 ? ToMb(_lastSampleWs) : "n/a")}");
            }
            catch (Exception error)
            {
                // Значения потерять можно, саму строку гибели — нет.
                Console.WriteLine($"[Proc] pid={pid} EXITED exit={exit} at={at} uptime=n/a reason={reason} peakWS=n/a lastWS=n/a ({error.GetType().Name}: {error.Message})");
            }
        }

        private void StopSampler()
        {
            Timer? sampler = _sampler;

            if (sampler == null)
            {
                return;
            }

            _sampler = null;

            try
            {
                sampler.Change(Timeout.Infinite, Timeout.Infinite);
                sampler.Dispose();
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Mem] failed to stop sampler: {error.Message}");
            }
        }

        private void UnsubscribeExited(Process process)
        {
            EventHandler? handler = _exitedHandler;

            if (handler == null)
            {
                return;
            }

            _exitedHandler = null;

            try
            {
                process.Exited -= handler;
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Proc] failed to unsubscribe process exit: {error.Message}");
            }
        }

        private bool WaitDeathLogged(int timeoutMs)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);

            while (Volatile.Read(ref _deathLogged) == 0 && DateTime.Now < deadline)
            {
                Thread.Sleep(25);
            }

            return Volatile.Read(ref _deathLogged) != 0;
        }

        private static string ExitCodeText(Process process)
        {
            try
            {
                int exitCode = process.ExitCode;
                return $"0x{exitCode:X8} ({exitCode})";
            }
            catch (Exception error)
            {
                return "n/a:" + error.GetType().Name;
            }
        }

        /// <summary>Пик рабочего набора: у живого процесса — свежий, у мёртвого — последний сэмпл (с '~').</summary>
        private string PeakText(Process process)
        {
            try
            {
                process.Refresh();
                return ToMb(process.PeakWorkingSet64);
            }
            catch (Exception error)
            {
                if (_lastSamplePeak > 0)
                {
                    return ToMb(_lastSamplePeak) + "~";
                }

                return "n/a:" + error.GetType().Name;
            }
        }

        private static string ToMb(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture) + "MB";
        }

        private static string NowStamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        #endregion

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
