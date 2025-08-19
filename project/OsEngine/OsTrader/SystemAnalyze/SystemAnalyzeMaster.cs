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

                _moqUsageAnalyze = new MoqUsageAnalyze();
                _moqUsageAnalyze.MoqUsageCollectionChange += _moqUsageAnalyze_MoqUsageCollectionChange;

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

        private static MoqUsageAnalyze _moqUsageAnalyze;

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

        public static bool MoqCollectDataIsOn
        {
            get
            {
                return _moqUsageAnalyze.MoqCollectDataIsOn;
            }
            set
            {
                _moqUsageAnalyze.MoqCollectDataIsOn = value;
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

        public static SavePointPeriod MoqPeriodSavePoint
        {
            get
            {
                return _moqUsageAnalyze.MoqPeriodSavePoint;
            }
            set
            {
                _moqUsageAnalyze.MoqPeriodSavePoint = value;
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

        public static int MoqPointsMax
        {
            get
            {
                return _moqUsageAnalyze.MoqPointsMax;
            }
            set
            {
                _moqUsageAnalyze.MoqPointsMax = value;
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

        public static List<SystemUsagePointMoq> ValuesMoq
        {
            get
            {
                return _moqUsageAnalyze.Values;
            }
        }

        public static SystemUsagePointRam LastValueRam
        {
            get
            {
                List < SystemUsagePointRam > values = _ramMemoryUsageAnalyze.Values;

                if(values != null 
                    && values.Count > 0)
                {
                    return values[^1];
                }
                else
                {
                    return null;
                }
            }
        }

        public static SystemUsagePointCpu LastValueCpu
        {
            get
            {
                List<SystemUsagePointCpu> values = _cpuUsageAnalyze.Values;

                if (values != null
                    && values.Count > 0)
                {
                    return values[^1];
                }
                else
                {
                    return null;
                }
            }
        }

        public static SystemUsagePointEcq LastValueEcq
        {
            get
            {
                List<SystemUsagePointEcq> values = _ecqUsageAnalyze.Values;

                if (values != null
                    && values.Count > 0)
                {
                    return values[^1];
                }
                else
                {
                    return null;
                }
            }
        }

        public static SystemUsagePointMoq LastValueMoq
        {
            get
            {
                List<SystemUsagePointMoq> values = _moqUsageAnalyze.Values;

                if (values != null
                    && values.Count > 0)
                {
                    return values[^1];
                }
                else
                {
                    return null;
                }
            }
        }

        public static int MarketDepthClearingCount
        {
            get
            {
                return _ecqUsageAnalyze.MarketDepthClearingCount;
            }
            set
            {
                _ecqUsageAnalyze.MarketDepthClearingCount = value;
            }
        }

        public static int BidAskClearingCount
        {
            get
            {
                return _ecqUsageAnalyze.BidAskClearingCount;
            }
            set
            {
                _ecqUsageAnalyze.BidAskClearingCount = value;
            }
        }

        public static int OrdersInQueue
        {
            get
            {
                return _moqUsageAnalyze.MaxOrdersInQueue;
            }
            set
            {
                _moqUsageAnalyze.MaxOrdersInQueue = value;
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
                    _moqUsageAnalyze.CalculateData();
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

        private static void _moqUsageAnalyze_MoqUsageCollectionChange(List<SystemUsagePointMoq> values)
        {
            if (MoqUsageCollectionChange != null)
            {
                MoqUsageCollectionChange(values);
            }
        }

        public static event Action<List<SystemUsagePointRam>> RamUsageCollectionChange;

        public static event Action<List<SystemUsagePointCpu>> CpuUsageCollectionChange;

        public static event Action<List<SystemUsagePointEcq>> EcqUsageCollectionChange;

        public static event Action<List<SystemUsagePointMoq>> MoqUsageCollectionChange;

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
                _nextCalculateTime = DateTime.MinValue;
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
                _nextCalculateTime = DateTime.MinValue;
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
                try
                {
                    _cpuCounterTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuCounterOsEngine = new PerformanceCounter("Process", "% Processor Time", "OsEngine");
                }
                catch
                {
                    _cpuCollectDataIsOn = false;
                    ServerMaster.SendNewLogMessage("Can run processor data collection on this PC.", Logging.LogMessageType.Error);
                    return;
                }
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
                _nextCalculateTime = DateTime.MinValue;
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

        public int MarketDepthClearingCount
        {
            get
            {
                return _marketDepthClearingCount;
            }
            set
            {
                _marketDepthClearingCount = value;
            }
        }
        private int _marketDepthClearingCount;

        public int BidAskClearingCount
        {
            get
            {
                return _bidAskClearingCount;
            }
            set
            {
                _bidAskClearingCount = value;
            }
        }
        private int _bidAskClearingCount;

        private DateTime _nextCalculateTime;

        public void CalculateData()
        {
            if (_ecqCollectDataIsOn == false)
            {
                return;
            }

            if (_nextCalculateTime != DateTime.MinValue
                && _nextCalculateTime > DateTime.Now)
            {
                return;
            }

            if (_ecqPeriodSavePoint == SavePointPeriod.OneSecond)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(1);
            }
            else if (_ecqPeriodSavePoint == SavePointPeriod.TenSeconds)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(10);
            }
            else //if (_cpuPeriodSavePoint == SavePointPeriod.Minute)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(60);
            }

            SystemUsagePointEcq newPoint = new SystemUsagePointEcq();
            newPoint.Time = DateTime.Now;
            newPoint.MarketDepthClearingCount = _marketDepthClearingCount;
            newPoint.BidAskClearingCount = _bidAskClearingCount;

            _marketDepthClearingCount = 0;
            _bidAskClearingCount = 0;

            SaveNewPoint(newPoint);
        }

        private void SaveNewPoint(SystemUsagePointEcq point)
        {
            Values.Add(point);

            if (Values.Count > _ecqPointsMax)
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

    public class MoqUsageAnalyze
    {
        public List<SystemUsagePointMoq> Values = new List<SystemUsagePointMoq>();

        public MoqUsageAnalyze()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(@"Engine\SystemStress\MoqMemorySettings.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\SystemStress\MoqMemorySettings.txt"))
                {
                    _moqCollectDataIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _moqPeriodSavePoint);
                    _moqPointsMax = Convert.ToInt32(reader.ReadLine());
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

                using (StreamWriter writer = new StreamWriter(@"Engine\SystemStress\MoqMemorySettings.txt", false))
                {
                    writer.WriteLine(_moqCollectDataIsOn);
                    writer.WriteLine(_moqPeriodSavePoint);
                    writer.WriteLine(_moqPointsMax);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public bool MoqCollectDataIsOn
        {
            get
            {
                return _moqCollectDataIsOn;
            }
            set
            {
                if (_moqCollectDataIsOn == value)
                {
                    return;
                }

                _moqCollectDataIsOn = value;
                Save();
            }
        }
        private bool _moqCollectDataIsOn;

        public SavePointPeriod MoqPeriodSavePoint
        {
            get
            {
                return _moqPeriodSavePoint;
            }
            set
            {
                if (_moqPeriodSavePoint == value)
                {
                    return;
                }

                _moqPeriodSavePoint = value;
                Save();
                _nextCalculateTime = DateTime.MinValue;
            }
        }
        private SavePointPeriod _moqPeriodSavePoint;

        public int MoqPointsMax
        {
            get
            {
                return _moqPointsMax;
            }
            set
            {
                if (_moqPointsMax == value)
                {
                    return;
                }

                _moqPointsMax = value;
                Save();
            }
        }
        private int _moqPointsMax = 100;

        public int MaxOrdersInQueue
        {
            get
            {
                return _maxOrdersInQueue;
            }
            set
            {
                if(value > _maxOrdersInQueue)
                {
                    _maxOrdersInQueue = value;
                }
            }
        }
        private int _maxOrdersInQueue;

        private DateTime _nextCalculateTime;

        public void CalculateData()
        {
            if (_moqCollectDataIsOn == false)
            {
                return;
            }

            if (_nextCalculateTime != DateTime.MinValue
                && _nextCalculateTime > DateTime.Now)
            {
                return;
            }

            if (_moqPeriodSavePoint == SavePointPeriod.OneSecond)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(1);
            }
            else if (_moqPeriodSavePoint == SavePointPeriod.TenSeconds)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(10);
            }
            else //if (_cpuPeriodSavePoint == SavePointPeriod.Minute)
            {
                _nextCalculateTime = DateTime.Now.AddSeconds(60);
            }

            SystemUsagePointMoq newPoint = new SystemUsagePointMoq();
            newPoint.Time = DateTime.Now;
            newPoint.MaxOrdersInQueue = _maxOrdersInQueue;

            _maxOrdersInQueue = 0;

            SaveNewPoint(newPoint);
        }

        private void SaveNewPoint(SystemUsagePointMoq point)
        {
            Values.Add(point);

            if (Values.Count > _moqPointsMax)
            {
                Values.RemoveAt(0);
            }

            if (MoqUsageCollectionChange != null)
            {
                MoqUsageCollectionChange (Values);
            }
        }

        public event Action<List<SystemUsagePointMoq>> MoqUsageCollectionChange;
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

        public decimal MarketDepthClearingCount;

        public decimal BidAskClearingCount;
    }

    public class SystemUsagePointMoq
    {
        public DateTime Time;

        public decimal MaxOrdersInQueue;

    }

    public enum SavePointPeriod
    {
        OneSecond,
        TenSeconds,
        Minute
    }
}
