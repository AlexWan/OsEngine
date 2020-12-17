using AdminSlave.Model;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace AdminSlave
{
    public class ProcessManager
    {
        public float Cpu;
        public float RamFree;
        public ulong RamAll;
        
        public void CheckStateEngines(List<OsEngine> engines)
        {
            var allNeedProcesses = Process.GetProcessesByName("OsEngine");

            foreach (var osEngine in engines)
            {
                var needProc = allNeedProcesses.FirstOrDefault(p => p.MainModule?.FileName == osEngine.Path);
                if (needProc == null)
                {
                    if (osEngine.State != State.Off)
                    {
                        osEngine.State = State.Off;
                    }
                }
                else if (needProc.Responding)
                {
                    if (osEngine.State != State.Active)
                    {
                        osEngine.State = State.Active;
                    }
                }
                else
                {
                    if (osEngine.State != State.NotAsk)
                    {
                        osEngine.State = State.NotAsk;
                    }
                }
            }
        }

        public int GetProcessIdByPath(string path)
        {
            var allNeedProcesses = Process.GetProcessesByName("OsEngine");
            var needProc = allNeedProcesses.FirstOrDefault(p => p.MainModule?.FileName == path);

            if (needProc != null)
            {
                return needProc.Id;
            }
            return 0;
        }

        public int GetProcessById(int id)
        {
            var allNeedProcesses = Process.GetProcessesByName("OsEngine");
            var needProc = allNeedProcesses.FirstOrDefault(p => p.Id == id);

            if (needProc != null)
            {
                return needProc.Id;
            }
            return 0;
        }

        public void Start()
        {
            RamAll = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1048576;

            _cpuCounter = new PerformanceCounter();
            _cpuCounter.CategoryName = "Processor";
            _cpuCounter.CounterName = "% Processor Time";
            _cpuCounter.InstanceName = "_Total";
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            System.Timers.Timer t = new System.Timers.Timer(1000);
            t.Elapsed += new ElapsedEventHandler(TimerElapsed);
            t.Start();
        }

        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public void TimerElapsed(object source, ElapsedEventArgs e)
        {
            Cpu = _cpuCounter.NextValue();
            RamFree = _ramCounter.NextValue();
        }
    }
}
