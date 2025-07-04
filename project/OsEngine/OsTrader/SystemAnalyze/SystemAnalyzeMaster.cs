/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using OsEngine.Language;
using OsEngine.Charts.CandleChart.Indicators;

namespace OsEngine.OsTrader.SystemAnalyze
{
    public class SystemUsageAnalyzeMaster
    {
        public static void Activate()
        {
            if (_worker == null)
            {
                _ramMemoryUsageAnalyze = new RamMemoryUsageAnalyze();
                _ramMemoryUsageAnalyze.RamUsageCollectionChange += _ramMemoryUsageAnalyze_RamUsageCollectionChange;

                _cpuUsageAnalyze = new CpuUsageAnalyze();
                _cpuUsageAnalyze.CpuUsageCollectionChange += _cpuUsageAnalyze_CpuUsageCollectionChange;

                _worker = new Thread(WorkMethod);
                _worker.Start();
            }
        }

        public static void ShowDialog()
        {
            try
            {
                if (_ui == null)
                {
                    _ui = new SystemAnalyzeUi();
                    _ui.Closed += _ui_Closed;
                    _ui.Show();
                }
                else
                {
                    if (_ui.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _ui.WindowState = System.Windows.WindowState.Normal;
                    }

                    _ui.Activate();
                }
            }
            catch(Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private static void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        private static SystemAnalyzeUi _ui;

        public static bool RamCollectDataIsOn
        {
            get
            {
                return _ramMemoryUsageAnalyze.RamCollectDataIsOn;
            }
            set
            {
                _ramMemoryUsageAnalyze.RamCollectDataIsOn = value;
            }
        }

        public static bool CpuCollectDataIsOn
        {
            get
            {
                return _cpuUsageAnalyze.CpuCollectDataIsOn;
            }
            set
            {
                _cpuUsageAnalyze.CpuCollectDataIsOn = value;
            }
        }

        public static List<SystemUsagePoint> ValuesRam
        {
            get
            {
                return _ramMemoryUsageAnalyze.Values;
            }
        }

        public static List<SystemUsagePoint> ValuesCpu
        {
            get
            {
                return _cpuUsageAnalyze.Values;
            }
        }

        private static RamMemoryUsageAnalyze _ramMemoryUsageAnalyze;

        private static CpuUsageAnalyze _cpuUsageAnalyze;

        private static Thread _worker;

        private static void WorkMethod()
        {
            while(true)
            {
                try
                {
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    Thread.Sleep(5000);

                    _ramMemoryUsageAnalyze.CalculateData();
                    _cpuUsageAnalyze.CalculateData();
                }
                catch(Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
                }
            }
        }

        private static void _ramMemoryUsageAnalyze_RamUsageCollectionChange(List<SystemUsagePoint> values)
        {
            try
            {
                if(RamUsageCollectionChange != null)
                {
                    RamUsageCollectionChange(values);
                }
            }
            catch(Exception ex) 
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private static void _cpuUsageAnalyze_CpuUsageCollectionChange(List<SystemUsagePoint> values)
        {
            try
            {
                if (CpuUsageCollectionChange != null)
                {
                    CpuUsageCollectionChange(values);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public static event Action<List<SystemUsagePoint>> RamUsageCollectionChange;

        public static event Action<List<SystemUsagePoint>> CpuUsageCollectionChange;

    }

    public class RamMemoryUsageAnalyze
    {
        public List<SystemUsagePoint> Values = new List<SystemUsagePoint>();

        public RamMemoryUsageAnalyze()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(@"Engine\SystemStress\RamMemorySettings.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\SystemStress\RamMemorySettings.txt"))
                {
                    _ramCollectDataIsOn = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Save()
        {
            try
            {
                if(Directory.Exists("Engine\\SystemStress") == false)
                {
                    Directory.CreateDirectory("Engine\\SystemStress");
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\SystemStress\RamMemorySettings.txt", false))
                {
                    writer.WriteLine(_ramCollectDataIsOn);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public bool RamCollectDataIsOn
        {
            get
            {
                return _ramCollectDataIsOn;
            }
            set
            {
                if(_ramCollectDataIsOn == value)
                {
                    return;
                }

                _ramCollectDataIsOn = value;
                Save();
            }
        }
        private bool _ramCollectDataIsOn;

        public void CalculateData()
        {
            if(_ramCollectDataIsOn == false)
            {
                return;
            }

            // 1 текущий размер программы в оперативной памяти
            Process proc = Process.GetCurrentProcess();
            long memoryMyProcess = proc.PrivateMemorySize64;
            int myMegaBytes = Convert.ToInt32(memoryMyProcess / 1024);

            // 2 общий размер оперативной памяти

            var info = new Microsoft.VisualBasic.Devices.ComputerInfo();
            ulong maxRam = info.TotalPhysicalMemory;
            int maxMegabytes = Convert.ToInt32(maxRam / 1024);

            // 3 свободный размер оперативной памяти

            ulong freeRam = info.AvailablePhysicalMemory;
            int freeMegabytes = Convert.ToInt32(freeRam / 1024);

            SystemUsagePoint newPoint = new SystemUsagePoint();
            newPoint.Time = DateTime.Now;
            newPoint.ProgramUsed = myMegaBytes;
            newPoint.SystemTotal = maxMegabytes;
            newPoint.SystemFree = freeMegabytes;

            SaveNewPoint(newPoint);
        }

        private void SaveNewPoint(SystemUsagePoint point)
        {
            Values.Add(point);

            if(Values.Count > 10000)
            {
                Values.RemoveAt(0);
            }

            if (RamUsageCollectionChange != null)
            {
                RamUsageCollectionChange(Values);
            }
        }

        public event Action<List<SystemUsagePoint>> RamUsageCollectionChange;
    }

    public class CpuUsageAnalyze
    {
        public List<SystemUsagePoint> Values = new List<SystemUsagePoint>();

        public CpuUsageAnalyze()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(@"Engine\SystemStress\CpuMemorySettings.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\SystemStress\CpuMemorySettings.txt"))
                {
                    _cpuCollectDataIsOn = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Save()
        {
            try
            {
                if (Directory.Exists("Engine\\SystemStress") == false)
                {
                    Directory.CreateDirectory("Engine\\SystemStress");
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\SystemStress\CpuMemorySettings.txt", false))
                {
                    writer.WriteLine(_cpuCollectDataIsOn);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public bool CpuCollectDataIsOn
        {
            get
            {
                return _cpuCollectDataIsOn;
            }
            set
            {
                if (_cpuCollectDataIsOn == value)
                {
                    return;
                }

                _cpuCollectDataIsOn = value;
                Save();
            }
        }
        private bool _cpuCollectDataIsOn;

        public void CalculateData()
        {
            if (_cpuCollectDataIsOn == false)
            {
                return;
            }

        }

        private void SaveNewPoint(SystemUsagePoint point)
        {
            Values.Add(point);

            if (Values.Count > 10000)
            {
                Values.RemoveAt(0);
            }

            if (CpuUsageCollectionChange != null)
            {
                CpuUsageCollectionChange(Values);
            }
        }

        public event Action<List<SystemUsagePoint>> CpuUsageCollectionChange;
    }

    public class SystemUsagePoint
    {
        public string ToolTip
        {
            get
            {
                if(_toolTip != null)
                {
                    return _toolTip;
                }

                string result = Time.ToString(OsLocalization.CurCulture) + "\n";

                result += "Total: " + SystemTotal + "\n";
                result += "Free: " + SystemFree + "\n";
                result += "My: " + ProgramUsed ;

                _toolTip = result;

                return result;
            }
        }
        private string _toolTip;

        public DateTime Time;

        public decimal ProgramUsed;

        public decimal SystemTotal;

        public decimal SystemFree;
    }
}
