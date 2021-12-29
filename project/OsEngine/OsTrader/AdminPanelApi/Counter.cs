using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;

namespace OsEngine.OsTrader.AdminPanelApi
{
    public class Counter
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public float Cpu;
        public float Ram;

        public void Start()
        {
            var process = Process.GetCurrentProcess();

            _cpuCounter = ProcessCpuCounter.GetPerfCounterForProcessId(process.Id);
            _ramCounter = ProcessCpuCounter.GetPerfCounterForProcessId(process.Id, "Working Set - Private");

            System.Timers.Timer t = new System.Timers.Timer(1000);
            t.Elapsed += new ElapsedEventHandler(TimerElapsed);
            t.Start();
        }

        public void TimerElapsed(object source, ElapsedEventArgs e)
        {
            Cpu = _cpuCounter.NextValue() / Environment.ProcessorCount;
            Ram = _ramCounter.NextValue() / 1048576;
        }
    }

    public class ProcessCpuCounter
    {
        public static PerformanceCounter GetPerfCounterForProcessId(int processId, string processCounterName = "% Processor Time")
        {
            string instance = GetInstanceNameForProcessId(processId);
            if (string.IsNullOrEmpty(instance))
                return null;

            return new PerformanceCounter("Process", processCounterName, instance);
        }

        public static string GetInstanceNameForProcessId(int processId)
        {
            var process = Process.GetProcessById(processId);
            string processName = Path.GetFileNameWithoutExtension(process.ProcessName);

            PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");
            string[] instances = cat.GetInstanceNames()
                .Where(inst => inst.StartsWith(processName))
                .ToArray();

            foreach (string instance in instances)
            {
                using (PerformanceCounter cnt = new PerformanceCounter("Process",
                    "ID Process", instance, true))
                {
                    int val = (int)cnt.RawValue;
                    if (val == processId)
                    {
                        return instance;
                    }
                }
            }
            return null;
        }
    }
}
