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

namespace OsEngine.OsTrader.SystemAnalyze
{
    public class SystemUsageAnalyzeMaster
    {
        #region Service

        public static void Activate()
        {
            if (_worker == null)
            {
                _ramMemoryUsageAnalyze = new RamMemoryUsageAnalyze();
                _ramMemoryUsageAnalyze.RamUsageCollectionChange += _ramMemoryUsageAnalyze_RamUsageCollectionChange;

                _cpuUsageAnalyze = new CpuUsageAnalyze();
                _cpuUsageAnalyze.CpuUsageCollectionChange += _cpuUsageAnalyze_CpuUsageCollectionChange;

                _ecqUsageAnalyze = new EcqUsageAnalyze();
                _ecqUsageAnalyze.EcqUsageCollectionChange += _ecqUsageAnalyze_EcqUsageCollectionChange;

                _worker = new Thread(WorkMethod);
                _worker.Start();
            }
        }

        private static void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        private static RamMemoryUsageAnalyze _ramMemoryUsageAnalyze;

        private static CpuUsageAnalyze _cpuUsageAnalyze;

        private static EcqUsageAnalyze _ecqUsageAnalyze;

        #endregion

        #region Settings

        private static SystemAnalyzeUi _ui;

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
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

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

        public static bool EcqCollectDataIsOn
        {
            get
            {
                return _ecqUsageAnalyze.EcqCollectDataIsOn;
            }
            set
            {
                _ecqUsageAnalyze.EcqCollectDataIsOn = value;
            }
        }

        public static SavePointPeriod RamPeriodSavePoint
        {
            get
            {
                return _ramMemoryUsageAnalyze.RamPeriodSavePoint;
            }
            set
            {
                _ramMemoryUsageAnalyze.RamPeriodSavePoint = value;
            }
        }

        public static SavePointPeriod CpuPeriodSavePoint
        {
            get
            {
                return _cpuUsageAnalyze.CpuPeriodSavePoint;
            }
            set
            {
                _cpuUsageAnalyze.CpuPeriodSavePoint = value;
            }
        }

        public static SavePointPeriod EcqPeriodSavePoint
        {
            get
            {
                return _ecqUsageAnalyze.EcqPeriodSavePoint;
            }
            set
            {
                _ecqUsageAnalyze.EcqPeriodSavePoint = value;
            }
        }

        public static int RamPointsMax
        {
            get
            {
                return _ramMemoryUsageAnalyze.RamPointsMax;
            }
            set
            {
                _ramMemoryUsageAnalyze.RamPointsMax = value;
            }
        }

        public static int CpuPointsMax
        {
            get
            {
                return _cpuUsageAnalyze.CpuPointsMax;
            }
            set
            {
                _cpuUsageAnalyze.CpuPointsMax = value;
            }
        }

        public static int EcqPointsMax
        {
            get
            {
                return _ecqUsageAnalyze.EcqPointsMax;
            }
            set
            {
                _ecqUsageAnalyze.EcqPointsMax = value;
            }
        }

        #endregion

        #region Data

        public static List<SystemUsagePointRam> ValuesRam
        {
            get
            {
                return _ramMemoryUsageAnalyze.Values;
            }
        }

        public static List<SystemUsagePointCpu> ValuesCpu
        {
            get
            {
                return _cpuUsageAnalyze.Values;
            }
        }

        public static List<SystemUsagePointEcq> ValuesEcq
        {
            get
            {
                return _ecqUsageAnalyze.Values;
            }
        }

        #endregion

        #region Work thread

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

                    Thread.Sleep(1000);

                    _ramMemoryUsageAnalyze.CalculateData();
                    _cpuUsageAnalyze.CalculateData();
                    _ecqUsageAnalyze.CalculateData();
                }
                catch(Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Events

        private static void _ramMemoryUsageAnalyze_RamUsageCollectionChange(List<SystemUsagePointRam> values)
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

        private static void _cpuUsageAnalyze_CpuUsageCollectionChange(List<SystemUsagePointCpu> values)
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

        private static void _ecqUsageAnalyze_EcqUsageCollectionChange(List<SystemUsagePointEcq> values)
        {
            try
            {
                if (EcqUsageCollectionChange != null)
                {
                    EcqUsageCollectionChange(values);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public static event Action<List<SystemUsagePointRam>> RamUsageCollectionChange;

        public static event Action<List<SystemUsagePointCpu>> CpuUsageCollectionChange;

        public static event Action<List<SystemUsagePointEcq>> EcqUsageCollectionChange;

        #endregion

    }

    public class RamMemoryUsageAnalyze
    {
        public List<SystemUsagePointRam> Values = new List<SystemUsagePointRam>();

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
                    Enum.TryParse(reader.ReadLine(), out _ramPeriodSavePoint);
                    _ramPointsMax = Convert.ToInt32(reader.ReadLine());
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
                    writer.WriteLine(_ramPeriodSavePoint);
                    writer.WriteLine(_ramPointsMax);
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

        public SavePointPeriod RamPeriodSavePoint
        {
            get
            {
                return _ramPeriodSavePoint;
            }
            set
            {
                if (_ramPeriodSavePoint == value)
                {
                    return;
                }

                _ramPeriodSavePoint = value;
                Save();
            }
        }
        private SavePointPeriod _ramPeriodSavePoint;

        public int RamPointsMax
        {
            get
            {
                return _ramPointsMax;
            }
            set
            {
                if (_ramPointsMax == value)
                {
                    return;
                }

                _ramPointsMax = value;
                Save();
            }
        }
        private int _ramPointsMax = 100;

        private DateTime _nextCalculateTime;

        public void CalculateData()
        {
            if(_ramCollectDataIsOn == false)
            {
                return;
            }

            if(_nextCalculateTime != DateTime.MinValue
                && _nextCalculateTime > DateTime.Now)
            {
                return;
            }

            if(_ramPeriodSavePoint == SavePointPeriod.OneSecond)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(1);
            }
            else if (_ramPeriodSavePoint == SavePointPeriod.TenSeconds)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(10);
            }
            else //if (_ramPeriodSavePoint == SavePointPeriod.Minute)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(60);
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

            decimal osEngineOccupiedPercent = Math.Round(Convert.ToDecimal(Convert.ToDecimal(myMegaBytes) / (maxMegabytes / 100)), 2);
            decimal totalOccupiedPercent = Math.Round(Convert.ToDecimal((Convert.ToDecimal(maxMegabytes) - freeMegabytes) / (maxMegabytes / 100)), 2);

            SystemUsagePointRam newPoint = new SystemUsagePointRam();
            newPoint.Time = DateTime.Now;
            newPoint.ProgramUsedPercent = osEngineOccupiedPercent;
            newPoint.SystemUsedPercent = totalOccupiedPercent;

            SaveNewPoint(newPoint);
        }

        private void SaveNewPoint(SystemUsagePointRam point)
        {
            Values.Add(point);

            if(Values.Count > _ramPointsMax)
            {
                Values.RemoveAt(0);
            }

            if (RamUsageCollectionChange != null)
            {
                RamUsageCollectionChange(Values);
            }
        }

        public event Action<List<SystemUsagePointRam>> RamUsageCollectionChange;
    }

    public class CpuUsageAnalyze
    {
        public List<SystemUsagePointCpu> Values = new List<SystemUsagePointCpu>();

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
                    Enum.TryParse(reader.ReadLine(), out _cpuPeriodSavePoint);
                    _cpuPointsMax = Convert.ToInt32(reader.ReadLine());
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
                    writer.WriteLine(_cpuPeriodSavePoint);
                    writer.WriteLine(_cpuPointsMax);
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

        public SavePointPeriod CpuPeriodSavePoint
        {
            get
            {
                return _cpuPeriodSavePoint;
            }
            set
            {
                if (_cpuPeriodSavePoint == value)
                {
                    return;
                }

                _cpuPeriodSavePoint = value;
                Save();
            }
        }
        private SavePointPeriod _cpuPeriodSavePoint;

        public int CpuPointsMax
        {
            get
            {
                return _cpuPointsMax;
            }
            set
            {
                if (_cpuPointsMax == value)
                {
                    return;
                }

                _cpuPointsMax = value;
                Save();
            }
        }
        private int _cpuPointsMax = 100;

        private DateTime _nextCalculateTime;

        public void CalculateData()
        {
            if (_cpuCollectDataIsOn == false)
            {
                return;
            }

            if (_nextCalculateTime != DateTime.MinValue
                && _nextCalculateTime > DateTime.Now)
            {
                return;
            }

            if (_cpuPeriodSavePoint == SavePointPeriod.OneSecond)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(1);
            }
            else if (_cpuPeriodSavePoint == SavePointPeriod.TenSeconds)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(10);
            }
            else //if (_cpuPeriodSavePoint == SavePointPeriod.Minute)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(60);
            }

            if(_cpuCounterTotal == null)
            {
                _cpuCounterTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounterOsEngine = new PerformanceCounter("Process", "% Processor Time", "OsEngine");
            }
           
            SystemUsagePointCpu newPoint = new SystemUsagePointCpu();
            newPoint.Time = DateTime.Now;
            newPoint.TotalOccupiedPercent = Math.Round(Convert.ToDecimal(_cpuCounterTotal.NextValue()),3);
            newPoint.ProgramOccupiedPercent = Math.Round(Convert.ToDecimal(_cpuCounterOsEngine.NextValue() / Environment.ProcessorCount), 3);

            SaveNewPoint(newPoint);
        }

        private PerformanceCounter _cpuCounterTotal;

        private PerformanceCounter _cpuCounterOsEngine;

        private void SaveNewPoint(SystemUsagePointCpu point)
        {
            Values.Add(point);

            if (Values.Count > _cpuPointsMax)
            {
                Values.RemoveAt(0);
            }

            if (CpuUsageCollectionChange != null)
            {
                CpuUsageCollectionChange(Values);
            }
        }

        public event Action<List<SystemUsagePointCpu>> CpuUsageCollectionChange;
    }

    public class EcqUsageAnalyze
    {
        public List<SystemUsagePointEcq> Values = new List<SystemUsagePointEcq>();

        public EcqUsageAnalyze()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(@"Engine\SystemStress\EcqMemorySettings.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\SystemStress\EcqMemorySettings.txt"))
                {
                    _ecqCollectDataIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _ecqPeriodSavePoint);
                    _ecqPointsMax = Convert.ToInt32(reader.ReadLine());
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

                using (StreamWriter writer = new StreamWriter(@"Engine\SystemStress\EcqMemorySettings.txt", false))
                {
                    writer.WriteLine(_ecqCollectDataIsOn);
                    writer.WriteLine(_ecqPeriodSavePoint);
                    writer.WriteLine(_ecqPointsMax);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public bool EcqCollectDataIsOn
        {
            get
            {
                return _ecqCollectDataIsOn;
            }
            set
            {
                if (_ecqCollectDataIsOn == value)
                {
                    return;
                }

                _ecqCollectDataIsOn = value;
                Save();
            }
        }
        private bool _ecqCollectDataIsOn;

        public SavePointPeriod EcqPeriodSavePoint
        {
            get
            {
                return _ecqPeriodSavePoint;
            }
            set
            {
                if (_ecqPeriodSavePoint == value)
                {
                    return;
                }

                _ecqPeriodSavePoint = value;
                Save();
            }
        }
        private SavePointPeriod _ecqPeriodSavePoint;

        public int EcqPointsMax
        {
            get
            {
                return _ecqPointsMax;
            }
            set
            {
                if (_ecqPointsMax == value)
                {
                    return;
                }

                _ecqPointsMax = value;
                Save();
            }
        }
        private int _ecqPointsMax = 100;

        public void CalculateData()
        {
            if (_ecqCollectDataIsOn == false)
            {
                return;
            }

        }

        private void SaveNewPoint(SystemUsagePointEcq point)
        {
            Values.Add(point);

            if (Values.Count > 10000)
            {
                Values.RemoveAt(0);
            }

            if (EcqUsageCollectionChange != null)
            {
                EcqUsageCollectionChange(Values);
            }
        }

        public event Action<List<SystemUsagePointEcq>> EcqUsageCollectionChange;
    }


    public class SystemUsagePointRam
    {
        public DateTime Time;

        public decimal ProgramUsedPercent;

        public decimal SystemUsedPercent;
    }

    public class SystemUsagePointCpu
    {
        public DateTime Time;

        public decimal ProgramOccupiedPercent;

        public decimal TotalOccupiedPercent;
    }

    public class SystemUsagePointEcq
    {
        public DateTime Time;

        public decimal MarketDepthClearing;

        public decimal BidAskClearing;
    }

    public enum SavePointPeriod
    {
        OneSecond,
        TenSeconds,
        Minute
    }
}
