/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using OsEngine.OsTrader.Panels.Tab.Internal;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;

namespace OsEngine.OsOptimizer
{
    public class OptimizerMaster
    {
        #region Service

        public OptimizerMaster()
        {
            _log = new Log("OptimizerLog", StartProgram.IsTester);
            _log.Listen(this);

            _threadsCount = 1;
            _startDeposit = 100000;

            Storage = new OptimizerDataStorage("Prime", true);
            Storage.SecuritiesChangeEvent += _storage_SecuritiesChangeEvent;
            Storage.TimeChangeEvent += _storage_TimeChangeEvent;

            _filterProfitValue = 10;
            _filterProfitIsOn = false;
            _filterMaxDrawDownValue = -10;
            _filterMaxDrawDownIsOn = false;
            _filterMiddleProfitValue = 0.001m;
            _filterMiddleProfitIsOn = false;
            _filterProfitFactorValue = 1;
            _filterProfitFactorIsOn = false;

            _percentOnFiltration = 30;

            Load();
            LoadClearingInfo();
            LoadNonTradePeriods();

            ManualControl = new BotManualControl("OptimizerManualControl", null, StartProgram.IsOsTrader);

            CreateBot();

            _optimizerExecutor = new OptimizerExecutor(this);
            _optimizerExecutor.LogMessageEvent += SendLogMessage;
            _optimizerExecutor.TestingProgressChangeEvent += _optimizerExecutor_TestingProgressChangeEvent;
            _optimizerExecutor.PrimeProgressChangeEvent += _optimizerExecutor_PrimeProgressChangeEvent;
            _optimizerExecutor.TestReadyEvent += _optimizerExecutor_TestReadyEvent;
            _optimizerExecutor.NeedToMoveUiToEvent += _optimizerExecutor_NeedToMoveUiToEvent;
            _optimizerExecutor.TimeToEndChangeEvent += _optimizerExecutor_TimeToEndChangeEvent;
            ProgressBarStatuses = new List<ProgressBarStatus>();
            PrimeProgressBarStatus = new ProgressBarStatus();
        }

        public int GetMaxBotsCount()
        {
            if (_parameters == null ||
                _parametersOn == null)
            {
                return 0;
            }

            int value = _optimizerExecutor.BotCountOneFaze(_parameters, _parametersOn) * IterationCount * 2;

            if (LastInSample)
            {
                value = value - _optimizerExecutor.BotCountOneFaze(_parameters, _parametersOn);
            }

            return value;
        }

        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\OptimizerSettings.txt", false)
                    )
                {
                    writer.WriteLine(ThreadsCount);
                    writer.WriteLine(StrategyName);
                    writer.WriteLine(_startDeposit);

                    writer.WriteLine(_filterProfitValue);
                    writer.WriteLine(_filterProfitIsOn);
                    writer.WriteLine(_filterMaxDrawDownValue);
                    writer.WriteLine(_filterMaxDrawDownIsOn);
                    writer.WriteLine(_filterMiddleProfitValue);
                    writer.WriteLine(_filterMiddleProfitIsOn);
                    writer.WriteLine(_filterProfitFactorValue);
                    writer.WriteLine(_filterProfitFactorIsOn);

                    writer.WriteLine(_timeStart.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(_timeEnd.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(_percentOnFiltration);

                    writer.WriteLine(_filterDealsCountValue);
                    writer.WriteLine(_filterDealsCountIsOn);
                    writer.WriteLine(_isScript);
                    writer.WriteLine(_iterationCount);
                    writer.WriteLine(_commissionType);
                    writer.WriteLine(_commissionValue);
                    writer.WriteLine(_lastInSample);
                    writer.WriteLine(_orderExecutionType);
                    writer.WriteLine(_slippageToSimpleOrder);
                    writer.WriteLine(_slippageToStopOrder);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Load()
        {
            if (!File.Exists(@"Engine\OptimizerSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\OptimizerSettings.txt"))
                {
                    _threadsCount = Convert.ToInt32(reader.ReadLine());
                    _strategyName = reader.ReadLine();
                    _startDeposit = reader.ReadLine().ToDecimal();
                    _filterProfitValue = reader.ReadLine().ToDecimal();
                    _filterProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMaxDrawDownValue = reader.ReadLine().ToDecimal();
                    _filterMaxDrawDownIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMiddleProfitValue = reader.ReadLine().ToDecimal();
                    _filterMiddleProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterProfitFactorValue = reader.ReadLine().ToDecimal();
                    _filterProfitFactorIsOn = Convert.ToBoolean(reader.ReadLine());

                    _timeStart = Convert.ToDateTime(reader.ReadLine(), CultureInfo.InvariantCulture);
                    _timeEnd = Convert.ToDateTime(reader.ReadLine(), CultureInfo.InvariantCulture);
                    _percentOnFiltration = reader.ReadLine().ToDecimal();

                    _filterDealsCountValue = Convert.ToInt32(reader.ReadLine());
                    _filterDealsCountIsOn = Convert.ToBoolean(reader.ReadLine());
                    _isScript = Convert.ToBoolean(reader.ReadLine());
                    _iterationCount = Convert.ToInt32(reader.ReadLine());
                    _commissionType = (CommissionType)Enum.Parse(typeof(CommissionType),
                        reader.ReadLine() ?? CommissionType.None.ToString());
                    _commissionValue = reader.ReadLine().ToDecimal();
                    _lastInSample = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out _orderExecutionType);
                    _slippageToSimpleOrder = Convert.ToInt32(reader.ReadLine());
                    _slippageToStopOrder = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Progress of the optimization process

        private void _optimizerExecutor_PrimeProgressChangeEvent(int curVal, int maxVal)
        {
            if (PrimeProgressBarStatus.CurrentValue != curVal)
            {
                PrimeProgressBarStatus.CurrentValue = curVal;
            }

            if (PrimeProgressBarStatus.MaxValue != maxVal)
            {
                PrimeProgressBarStatus.MaxValue = maxVal;
            }
        }

        private void _optimizerExecutor_TestReadyEvent(List<OptimizerFazeReport> reports)
        {
            if (PrimeProgressBarStatus.CurrentValue != PrimeProgressBarStatus.MaxValue)
            {
                PrimeProgressBarStatus.CurrentValue = PrimeProgressBarStatus.MaxValue;
            }

            if (TestReadyEvent != null)
            {
                TestReadyEvent(reports);
            }
        }

        private void _optimizerExecutor_TimeToEndChangeEvent(TimeSpan timeToEnd)
        {
            if (TimeToEndChangeEvent != null)
            {
                TimeToEndChangeEvent(timeToEnd);
            }
        }

        public event Action<TimeSpan> TimeToEndChangeEvent;

        public event Action<List<OptimizerFazeReport>> TestReadyEvent;

        private void _optimizerExecutor_TestingProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            ProgressBarStatus status;
            try
            {
                status = ProgressBarStatuses.Find(st => st.Num == numServer);
            }
            catch
            {
                return;
            }

            if (status == null)
            {
                status = new ProgressBarStatus();
                status.Num = numServer;
                ProgressBarStatuses.Add(status);
            }

            status.CurrentValue = curVal;
            status.MaxValue = maxVal;
        }

        public List<ProgressBarStatus> ProgressBarStatuses;

        public ProgressBarStatus PrimeProgressBarStatus;

        #endregion

        #region Data store

        public bool ShowDataStorageDialog()
        {
            TesterSourceDataType storageSource = Storage.SourceDataType;
            string folder = Storage.PathToFolder;
            TesterDataType storageDataType = Storage.TypeTesterData;
            string setName = Storage.ActiveSet;

            Storage.ShowDialog(this);

            if (storageSource != Storage.SourceDataType
                || folder != Storage.PathToFolder
                || storageDataType != Storage.TypeTesterData
                || setName != Storage.ActiveSet)
            {
                return true;
            }

            return false;
        }

        public OptimizerDataStorage Storage;

        private void _storage_TimeChangeEvent(DateTime timeStart, DateTime timeEnd)
        {
            TimeStart = timeStart;
            TimeEnd = timeEnd;
        }

        private void _storage_SecuritiesChangeEvent(List<Security> securities)
        {
            if (NewSecurityEvent != null)
            {
                NewSecurityEvent(securities);
            }

            TimeStart = Storage.TimeStart;
            TimeEnd = Storage.TimeEnd;
        }

        public event Action<List<Security>> NewSecurityEvent;

        #endregion

        #region Management

        public int ThreadsCount
        {
            get { return _threadsCount; }
            set
            {
                _threadsCount = value;
                Save();
            }
        }
        private int _threadsCount;

        public string StrategyName
        {
            get { return _strategyName; }
            set
            {
                _strategyName = value;
                Save();
            }
        }
        private string _strategyName;

        public bool IsScript
        {
            get { return _isScript; }
            set
            {
                _isScript = value;
                Save();
            }
        }
        private bool _isScript;

        public List<SecurityTester> SecurityTester
        {
            get { return Storage.SecuritiesTester; }
        }

        public BotManualControl ManualControl;

        public BotPanel BotToTest;

        public OptimizerServer ServerToTestBot;

        public void ShowManualControlDialog()
        {
            ManualControl.ShowDialog(StartProgram.IsOsOptimizer);
        }

        public void UpdateBotManualControlSettings()
        {
            if (string.IsNullOrEmpty(_strategyName))
            {
                return;
            }

            if (BotToTest == null)
            {
                string botName = "OptimizerBot" + _strategyName.RemoveExcessFromSecurityName();

                BotToTest = BotFactory.GetStrategyForName(_strategyName, botName, StartProgram.IsTester, _isScript);
            }

            List<IIBotTab> sources = BotToTest.GetTabs();

            for (int i = 0; i < sources.Count; i++)
            {
                IIBotTab curTab = sources[i];

                if (curTab.TabType == BotTabType.Simple)
                {
                    BotTabSimple simpleTab = (BotTabSimple)curTab;
                    simpleTab.Connector.ServerType = Market.ServerType.Optimizer;
                    simpleTab.Connector.ServerUid = -1;
                    simpleTab.CommissionType = CommissionType;
                    simpleTab.CommissionValue = CommissionValue;

                    CopyManualSupportSettings(simpleTab.ManualPositionSupport);
                }
                if (curTab.TabType == BotTabType.Screener)
                {
                    BotTabScreener screenerTab = (BotTabScreener)curTab;
                    screenerTab.ServerType = Market.ServerType.Optimizer;
                    screenerTab.ServerUid = -1;
                    screenerTab.CommissionType = CommissionType;
                    screenerTab.CommissionValue = CommissionValue;
                }
            }

            UpdateServerToSettings();
        }

        public void CreateBot()
        {
            try
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return;
                }

                /* string storageName = "";

                 if(Storage.SourceDataType == TesterSourceDataType.Set)
                 {
                     if (string.IsNullOrEmpty(Storage.ActiveSet) == false)
                     {
                         storageName = Storage.ActiveSet;
                     }
                 }*/

                string botName = "OptimizerBot" + _strategyName.RemoveExcessFromSecurityName();

                if (Storage.SourceDataType == TesterSourceDataType.Set
                    && string.IsNullOrEmpty(Storage.ActiveSet) == false)
                {
                    string[] setNameArray = Storage.ActiveSet.Split('_');

                    botName += setNameArray[setNameArray.Length - 1];
                }

                BotToTest = BotFactory.GetStrategyForName(_strategyName, botName, StartProgram.IsTester, _isScript);

                if(BotToTest == null)
                {
                    return;
                }

                List<IIBotTab> sources = BotToTest.GetTabs();

                for (int i = 0; i < sources.Count; i++)
                {
                    IIBotTab curTab = sources[i];

                    if (curTab.TabType == BotTabType.Simple)
                    {
                        BotTabSimple simpleTab = (BotTabSimple)curTab;
                        simpleTab.Connector.ServerType = Market.ServerType.Optimizer;
                        simpleTab.Connector.ServerUid = -1;
                        simpleTab.CommissionType = CommissionType;
                        simpleTab.CommissionValue = CommissionValue;

                        CopyManualSupportSettings(simpleTab.ManualPositionSupport);
                    }
                    if (curTab.TabType == BotTabType.Screener)
                    {
                        BotTabScreener screenerTab = (BotTabScreener)curTab;
                        screenerTab.ServerType = Market.ServerType.Optimizer;
                        screenerTab.ServerUid = -1;
                        screenerTab.CommissionType = CommissionType;
                        screenerTab.CommissionValue = CommissionValue;
                        screenerTab.ManualPositionSupportFromOptimizer = ManualControl;
                        screenerTab.TryLoadTabs();
                        screenerTab.NeedToReloadTabs = true;
                        screenerTab.TryReLoadTabs();
                    }
                }

                UpdateServerToSettings();
            }
            catch (Exception ex)
            {
                SendLogMessage("Can`t create bot " + _strategyName + " Exception: " + ex.ToString(), LogMessageType.Error);
            }
        }

        public void UpdateServerToSettings()
        {
            List<Market.Servers.IServer> servers = ServerMaster.GetServers();

            for (int i = 0; servers != null && i < servers.Count; i++)
            {
                if (servers[i].ServerType != ServerType.Optimizer)
                {
                    continue;
                }
                OptimizerServer curServer = (OptimizerServer)servers[i];

                if (curServer.NumberServer == -1)
                {

                    ServerMaster.RemoveOptimizerServer(curServer);
                    break;
                }
            }

            ServerToTestBot = ServerMaster.CreateNextOptimizerServer(Storage, -1, 10000);
        }

        public void CopyManualSupportSettings(BotManualControl manualControlTo)
        {

            manualControlTo.DoubleExitIsOn = ManualControl.DoubleExitIsOn;
            manualControlTo.DoubleExitSlippage = ManualControl.DoubleExitSlippage;
            manualControlTo.OrderTypeTime = ManualControl.OrderTypeTime;
            manualControlTo.ProfitDistance = ManualControl.ProfitDistance;
            manualControlTo.ProfitIsOn = ManualControl.ProfitIsOn;
            manualControlTo.ProfitSlippage = ManualControl.ProfitSlippage;
            manualControlTo.SecondToClose = ManualControl.SecondToClose;
            manualControlTo.SecondToCloseIsOn = ManualControl.SecondToCloseIsOn;
            manualControlTo.SecondToOpen = ManualControl.SecondToOpen;
            manualControlTo.SecondToOpenIsOn = ManualControl.SecondToOpenIsOn;
            manualControlTo.SetbackToCloseIsOn = ManualControl.SetbackToCloseIsOn;
            manualControlTo.SetbackToClosePosition = ManualControl.SetbackToClosePosition;
            manualControlTo.SetbackToOpenIsOn = ManualControl.SetbackToOpenIsOn;
            manualControlTo.SetbackToOpenPosition = ManualControl.SetbackToOpenPosition;
            manualControlTo.StopDistance = ManualControl.StopDistance;
            manualControlTo.StopIsOn = ManualControl.StopIsOn;
            manualControlTo.StopSlippage = ManualControl.StopSlippage;
            manualControlTo.TypeDoubleExitOrder = ManualControl.TypeDoubleExitOrder;
            manualControlTo.ValuesType = ManualControl.ValuesType;

        }

        #endregion

        #region Trade servers settings

        public OrderExecutionType OrderExecutionType
        {
            get { return _orderExecutionType; }
            set
            {
                _orderExecutionType = value;
                Save();
            }
        }
        private OrderExecutionType _orderExecutionType;

        public int SlippageToSimpleOrder
        {
            get { return _slippageToSimpleOrder; }
            set
            {
                if (_slippageToSimpleOrder == value)
                {
                    return;
                }

                _slippageToSimpleOrder = value;
                Save();
            }
        }
        private int _slippageToSimpleOrder;

        public int SlippageToStopOrder
        {
            get { return _slippageToStopOrder; }
            set
            {
                if (_slippageToStopOrder == value)
                {
                    return;
                }

                _slippageToStopOrder = value;
                Save();
            }
        }
        private int _slippageToStopOrder;

        public decimal StartDeposit
        {
            get { return _startDeposit; }
            set
            {
                _startDeposit = value;
                Save();
            }
        }
        private decimal _startDeposit;

        public CommissionType CommissionType
        {
            get => _commissionType;
            set
            {
                if (_commissionType == value)
                {
                    return;
                }

                _commissionType = value;
                Save();
                UpdateBotManualControlSettings();
            }
        }
        private CommissionType _commissionType;

        public decimal CommissionValue
        {
            get => _commissionValue;
            set
            {
                if (_commissionValue == value)
                {
                    return;
                }

                _commissionValue = value;
                Save();
                UpdateBotManualControlSettings();
            }
        }
        private decimal _commissionValue;

        #endregion

        #region Clearing system 

        public List<OrderClearing> ClearingTimes = new List<OrderClearing>();

        public void SaveClearingInfo()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"OptimizerMasterClearings.txt", false))
                {
                    for (int i = 0; i < ClearingTimes.Count; i++)
                    {
                        writer.WriteLine(ClearingTimes[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LoadClearingInfo()
        {
            if (!File.Exists(@"Engine\" + @"OptimizerMasterClearings.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"OptimizerMasterClearings.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (str != "")
                        {
                            OrderClearing clearings = new OrderClearing();
                            clearings.SetFromString(str);
                            ClearingTimes.Add(clearings);
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void CreateNewClearing()
        {
            OrderClearing newClearing = new OrderClearing();

            newClearing.Time = new DateTime(2000, 1, 1, 19, 0, 0);
            ClearingTimes.Add(newClearing);
            SaveClearingInfo();
        }

        public void RemoveClearing(int num)
        {
            if (num > ClearingTimes.Count)
            {
                return;
            }

            ClearingTimes.RemoveAt(num);
            SaveClearingInfo();
        }

        #endregion

        #region Non-trade periods

        public List<NonTradePeriod> NonTradePeriods = new List<NonTradePeriod>();

        public void SaveNonTradePeriods()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"OptimizerMasterNonTradePeriods.txt", false))
                {
                    for (int i = 0; i < NonTradePeriods.Count; i++)
                    {
                        writer.WriteLine(NonTradePeriods[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LoadNonTradePeriods()
        {
            if (!File.Exists(@"Engine\" + @"OptimizerMasterNonTradePeriods.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"OptimizerMasterNonTradePeriods.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (str != "")
                        {
                            NonTradePeriod period = new NonTradePeriod();
                            period.SetFromString(str);
                            NonTradePeriods.Add(period);
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void CreateNewNonTradePeriod()
        {
            NonTradePeriod newClearing = new NonTradePeriod();

            NonTradePeriods.Add(newClearing);
            SaveNonTradePeriods();
        }

        public void RemoveNonTradePeriod(int num)
        {
            if (num > NonTradePeriods.Count)
            {
                return;
            }

            NonTradePeriods.RemoveAt(num);
            SaveNonTradePeriods();
        }

        #endregion

        #region Filters

        public decimal FilterProfitValue
        {
            get { return _filterProfitValue; }
            set
            {
                _filterProfitValue = value;
                Save();
            }
        }
        private decimal _filterProfitValue;

        public bool FilterProfitIsOn
        {
            get { return _filterProfitIsOn; }
            set
            {
                _filterProfitIsOn = value;
                Save();
            }
        }
        private bool _filterProfitIsOn;

        public decimal FilterMaxDrawDownValue
        {
            get { return _filterMaxDrawDownValue; }
            set
            {
                _filterMaxDrawDownValue = value;
                Save();
            }
        }
        private decimal _filterMaxDrawDownValue;

        public bool FilterMaxDrawDownIsOn
        {
            get { return _filterMaxDrawDownIsOn; }
            set
            {
                _filterMaxDrawDownIsOn = value;
                Save();
            }
        }
        private bool _filterMaxDrawDownIsOn;

        public decimal FilterMiddleProfitValue
        {
            get { return _filterMiddleProfitValue; }
            set
            {
                _filterMiddleProfitValue = value;
                Save();
            }
        }
        private decimal _filterMiddleProfitValue;

        public bool FilterMiddleProfitIsOn
        {
            get { return _filterMiddleProfitIsOn; }
            set
            {
                _filterMiddleProfitIsOn = value;
                Save();
            }
        }
        private bool _filterMiddleProfitIsOn;

        public decimal FilterProfitFactorValue
        {
            get { return _filterProfitFactorValue; }
            set
            {
                _filterProfitFactorValue = value;
                Save();
            }
        }
        private decimal _filterProfitFactorValue;

        public bool FilterProfitFactorIsOn
        {
            get { return _filterProfitFactorIsOn; }
            set
            {
                _filterProfitFactorIsOn = value;
                Save();
            }
        }
        private bool _filterProfitFactorIsOn;

        public int FilterDealsCountValue
        {
            get { return _filterDealsCountValue; }
            set
            {
                _filterDealsCountValue = value;
                Save();
            }
        }
        private int _filterDealsCountValue;

        public bool FilterDealsCountIsOn
        {
            get { return _filterDealsCountIsOn; }
            set
            {
                _filterDealsCountIsOn = value;
                Save();
            }
        }
        private bool _filterDealsCountIsOn;

        #endregion

        #region Optimization phases

        public bool IsAcceptedByFilter(OptimizerReport report)
        {
            if (report == null)
            {
                return false;
            }

            if (FilterMiddleProfitIsOn && report.AverageProfitPercentOneContract < FilterMiddleProfitValue)
            {
                return false;
            }

            if (FilterProfitIsOn && report.TotalProfit < FilterProfitValue)
            {
                return false;
            }

            if (FilterMaxDrawDownIsOn && report.MaxDrawDawn < FilterMaxDrawDownValue)
            {
                return false;
            }

            if (FilterProfitFactorIsOn && report.ProfitFactor < FilterProfitFactorValue)
            {
                return false;
            }

            if (FilterDealsCountIsOn && report.PositionsCount < FilterDealsCountValue)
            {
                return false;
            }

            return true;
        }

        public List<OptimizerFaze> Fazes;

        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
                Save();

                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeStart;

        public DateTime TimeEnd
        {
            get { return _timeEnd; }
            set
            {
                _timeEnd = value;
                Save();
                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeEnd;

        public decimal PercentOnFiltration
        {
            get { return _percentOnFiltration; }
            set
            {
                _percentOnFiltration = value;
                Save();
            }
        }
        private decimal _percentOnFiltration;

        public int IterationCount
        {
            get { return _iterationCount; }
            set
            {
                _iterationCount = value;
                Save();
            }
        }

        private int _iterationCount = 1;

        public bool LastInSample
        {
            get
            {
                return _lastInSample;
            }
            set
            {
                _lastInSample = value;
                Save();
            }
        }

        private bool _lastInSample;

        private decimal GetInSampleRecurs(decimal curLengthInSample, int fazeCount, bool lastInSample, int allDays)
        {
            // х = Y + Y/P * С;
            // x - общая длинна в днях. Уже известна
            // Y - длинна InSample
            // P - процент OutOfSample от InSample
            // C - количество отрезков

            decimal outOfSampleLength = curLengthInSample * (_percentOnFiltration / 100);

            int count = fazeCount;

            if (lastInSample)
            {
                count--;
            }

            int allLength = Convert.ToInt32(curLengthInSample + outOfSampleLength * count);

            if (allLength > allDays)
            {
                if (Convert.ToDecimal(allLength) / allDays > 1.2m)
                {
                    if (curLengthInSample > 1000)
                    {
                        curLengthInSample -= 10;
                    }
                    else
                    {
                        curLengthInSample -= 5;
                    }
                }

                curLengthInSample--;
                return GetInSampleRecurs(curLengthInSample, fazeCount, lastInSample, allDays);
            }
            else
            {
                return curLengthInSample;
            }
        }

        public void ReloadFazes()
        {
            int fazeCount = IterationCount;

            if (fazeCount < 1)
            {
                fazeCount = 1;
            }

            if (TimeEnd == DateTime.MinValue ||
                TimeStart == DateTime.MinValue)
            {
                SendLogMessage(OsLocalization.Optimizer.Message12, LogMessageType.System);
                return;
            }

            int dayAll = Convert.ToInt32((TimeEnd - TimeStart).TotalDays);

            if (dayAll < 2)
            {
                SendLogMessage(OsLocalization.Optimizer.Message12, LogMessageType.System);
                return;
            }

            int daysOnInSample = (int)GetInSampleRecurs(dayAll, fazeCount, _lastInSample, dayAll);

            int daysOnForward = Convert.ToInt32(daysOnInSample * (_percentOnFiltration / 100));

            Fazes = new List<OptimizerFaze>();

            DateTime time = _timeStart;

            for (int i = 0; i < fazeCount; i++)
            {
                OptimizerFaze newFaze = new OptimizerFaze();
                newFaze.TypeFaze = OptimizerFazeType.InSample;
                newFaze.TimeStart = time;
                newFaze.TimeEnd = time.AddDays(daysOnInSample);
                time = time.AddDays(daysOnForward);
                newFaze.Days = daysOnInSample;
                Fazes.Add(newFaze);

                if (_lastInSample
                    && i + 1 == fazeCount)
                {
                    newFaze.Days = daysOnInSample;
                    break;
                }

                OptimizerFaze newFazeOut = new OptimizerFaze();
                newFazeOut.TypeFaze = OptimizerFazeType.OutOfSample;
                newFazeOut.TimeStart = newFaze.TimeStart.AddDays(daysOnInSample);
                newFazeOut.TimeEnd = newFazeOut.TimeStart.AddDays(daysOnForward);
                newFazeOut.TimeStart = newFazeOut.TimeStart.AddDays(1);
                newFazeOut.Days = daysOnForward;
                Fazes.Add(newFazeOut);
            }

            for (int i = 0; i < Fazes.Count; i++)
            {
                if (Fazes[i].Days <= 0)
                {
                    SendLogMessage(OsLocalization.Optimizer.Label50, LogMessageType.Error);
                    Fazes = new List<OptimizerFaze>();
                    return;
                }
            }


            /*while (DaysInFazes(Fazes) != dayAll)
            {
                int daysGone = DaysInFazes(Fazes) - dayAll;

                for (int i = 0; i < Fazes.Count && daysGone != 0; i++)
                {

                    if (daysGone > 0)
                    {
                        Fazes[i].Days--;
                        if (Fazes[i].TypeFaze == OptimizerFazeType.InSample &&
                            i + 1 != Fazes.Count)
                        {
                            Fazes[i + 1].TimeStart = Fazes[i + 1].TimeStart.AddDays(-1);
                        }
                        else
                        {
                            Fazes[i].TimeStart = Fazes[i].TimeStart.AddDays(-1);
                        }
                        daysGone--;
                    }
                    else if (daysGone < 0)
                    {
                        Fazes[i].Days++;
                        if (Fazes[i].TypeFaze == OptimizerFazeType.InSample && 
                            i + 1 != Fazes.Count)
                        {
                            Fazes[i + 1].TimeStart = Fazes[i + 1].TimeStart.AddDays(+1);
                        }
                        else
                        {
                            Fazes[i].TimeStart = Fazes[i].TimeStart.AddDays(+1);
                        }
                        daysGone++;
                    }
                }
            }*/
        }

        public event Action DateTimeStartEndChange;

        #endregion

        #region Optimization parameters

        public List<IIStrategyParameter> Parameters
        {
            get
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return null;
                }

                BotPanel bot = BotFactory.GetStrategyForName(_strategyName, "", StartProgram.IsOsOptimizer, _isScript);

                if (bot == null)
                {
                    return null;
                }

                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    return null;
                }

                if (_parameters != null)
                {
                    _parameters.Clear();
                    _parameters = null;
                }

                _parameters = new List<IIStrategyParameter>();

                for (int i = 0; i < bot.Parameters.Count; i++)
                {
                    _parameters.Add(bot.Parameters[i]);
                }

                for (int i = 0; i < _parameters.Count; i++)
                {
                    GetValueParameterSaveByUser(_parameters[i]);
                }

                bot.Delete();

                return _parameters;
            }
        }

        public List<IIStrategyParameter> ParametersStandard
        {
            get
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return null;
                }

                BotPanel bot = BotFactory.GetStrategyForName(_strategyName, "", StartProgram.IsOsOptimizer, _isScript);

                if (bot == null)
                {
                    return null;
                }

                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    return null;
                }

                if (_parameters != null)
                {
                    _parameters.Clear();
                    _parameters = null;
                }

                _parameters = new List<IIStrategyParameter>();

                for (int i = 0; i < bot.Parameters.Count; i++)
                {
                    _parameters.Add(bot.Parameters[i]);
                }

                return _parameters;
            }
        }

        private List<IIStrategyParameter> _parameters;

        private void GetValueParameterSaveByUser(IIStrategyParameter parameter)
        {
            if (!File.Exists(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('#');

                        if (save[0] == parameter.Name)
                        {
                            parameter.LoadParamFromString(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void SaveStandardParameters()
        {
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt", false)
                    )
                {
                    for (int i = 0; i < _parameters.Count; i++)
                    {
                        writer.WriteLine(_parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            SaveParametersOnOffByStrategy();
        }

        public List<bool> ParametersOn
        {
            get
            {

                _parametersOn = new List<bool>();
                for (int i = 0; _parameters != null && i < _parameters.Count; i++)
                {
                    _parametersOn.Add(false);
                }

                List<bool> parametersOnSaveBefore = GetParametersOnOffByStrategy();

                if (parametersOnSaveBefore != null &&
                    parametersOnSaveBefore.Count == _parametersOn.Count)
                {
                    _parametersOn = parametersOnSaveBefore;
                }

                return _parametersOn;
            }
        }
        private List<bool> _parametersOn;

        private List<bool> GetParametersOnOffByStrategy()
        {
            List<bool> result = new List<bool>();

            if (!File.Exists(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt"))
            {
                return result;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        result.Add(Convert.ToBoolean(reader.ReadLine()));
                    }
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return result;
        }

        private void SaveParametersOnOffByStrategy()
        {
            if (_parametersOn == null ||
               _parametersOn.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt", false)
                    )
                {
                    for (int i = 0; i < _parametersOn.Count; i++)
                    {
                        writer.WriteLine(_parametersOn[i].ToString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        #endregion

        #region Start optimization algorithm

        public OptimizerExecutor _optimizerExecutor;

        public bool Start()
        {
            if (CheckReadyData() == false)
            {
                return false;
            }

            if (_optimizerExecutor.Start(_parametersOn, _parameters))
            {
                ProgressBarStatuses = new List<ProgressBarStatus>();
                PrimeProgressBarStatus = new ProgressBarStatus();
            }
            return true;
        }

        public void Stop()
        {
            _optimizerExecutor.Stop();
        }

        private bool CheckReadyData()
        {
            if (Fazes == null || Fazes.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message14);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message14, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.Fazes);
                }
                return false;
            }

            List<IIBotTab> sources = BotToTest.GetTabs();

            // проверяем наличие данных в источниках

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].TabType == BotTabType.Simple)
                {// BotTabSimple
                    BotTabSimple simple = (BotTabSimple)sources[i];

                    if (string.IsNullOrEmpty(simple.Connector.SecurityName))
                    {
                        CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message15);
                        ui.ShowDialog();
                        SendLogMessage(OsLocalization.Optimizer.Message15, LogMessageType.System);
                        if (NeedToMoveUiToEvent != null)
                        {
                            NeedToMoveUiToEvent(NeedToMoveUiTo.TabsAndTimeFrames);
                        }
                        return false;
                    }

                    if (HaveSecurityAndTfInStorage(
                        simple.Connector.SecurityName, simple.Connector.TimeFrame) == false)
                    {
                        return false;
                    }
                }
                else if (sources[i].TabType == BotTabType.Index)
                {// BotTabIndex
                    BotTabIndex index = (BotTabIndex)sources[i];

                    if (index.Tabs == null ||
                        index.Tabs.Count == 0)
                    {
                        CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message15);
                        ui.ShowDialog();
                        SendLogMessage(OsLocalization.Optimizer.Message15, LogMessageType.System);
                        if (NeedToMoveUiToEvent != null)
                        {
                            NeedToMoveUiToEvent(NeedToMoveUiTo.TabsAndTimeFrames);
                        }
                        return false;
                    }

                    for (int i2 = 0; i2 < index.Tabs.Count; i2++)
                    {
                        if (HaveSecurityAndTfInStorage(
                            index.Tabs[i2].SecurityName, index.Tabs[i2].TimeFrame) == false)
                        {
                            return false;
                        }
                    }
                }
                else if (sources[i].TabType == BotTabType.Screener)
                {// BotTabScreener
                    BotTabScreener screener = (BotTabScreener)sources[i];

                    if (screener.Tabs == null ||
                        screener.Tabs.Count == 0)
                    {
                        CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message15);
                        ui.ShowDialog();
                        SendLogMessage(OsLocalization.Optimizer.Message15, LogMessageType.System);
                        if (NeedToMoveUiToEvent != null)
                        {
                            NeedToMoveUiToEvent(NeedToMoveUiTo.TabsAndTimeFrames);
                        }
                        return false;
                    }

                    for (int i2 = 0; i2 < screener.Tabs.Count; i2++)
                    {
                        if (HaveSecurityAndTfInStorage(
                            screener.Tabs[i2].Connector.SecurityName, screener.Tabs[i2].Connector.TimeFrame) == false)
                        {
                            return false;
                        }
                    }
                }
            }

            if ((string.IsNullOrEmpty(Storage.ActiveSet)
                && Storage.SourceDataType == TesterSourceDataType.Set)
                ||
                Storage.SecuritiesTester == null
                ||
                Storage.SecuritiesTester.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message16);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message16, LogMessageType.System);

                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.Storage);
                }
                return false;
            }

            if (string.IsNullOrEmpty(_strategyName))
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message17);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message17, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.NameStrategy);
                }
                return false;
            }

            bool onParametersReady = false;

            if(_parametersOn == null)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message44);
                ui.ShowDialog();
                return false;
            }

            for (int i = 0; i < _parametersOn.Count; i++)
            {
                if (_parametersOn[i])
                {
                    onParametersReady = true;
                    break;
                }
            }

            if (onParametersReady == false)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message18);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message18, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {

                    NeedToMoveUiToEvent(NeedToMoveUiTo.Parameters);
                }
                return false;
            }


            // проверка наличия и состояния параметра Regime 
            bool onRgimeOff = false;

            for (int i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].Name == "Regime" && _parameters[i].Type == StrategyParameterType.String)
                {
                    if (((StrategyParameterString)_parameters[i]).ValueString == "Off")
                    {
                        onRgimeOff = true;
                    }
                }

                else if (_parameters[i].Name == "Regime" && _parameters[i].Type == StrategyParameterType.CheckBox)
                {
                    if (((StrategyParameterCheckBox)_parameters[i]).CheckState == System.Windows.Forms.CheckState.Unchecked)
                    {
                        onRgimeOff = true;
                    }
                }
            }

            if (onRgimeOff == true)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message41);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message41, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.RegimeRow);
                }
                return false;
            }
            // Regime / конец

            return true;
        }

        private bool HaveSecurityAndTfInStorage(string secName, TimeFrame timeFrame)
        {
            // проверяем наличие тайм-фрейма в обойме

            bool isInArray = false;

            for (int j = 0; j < Storage.SecuritiesTester.Count; j++)
            {
                if (Storage.SecuritiesTester[j].Security.Name == secName
                    &&
                    (Storage.SecuritiesTester[j].TimeFrame == timeFrame
                    || Storage.SecuritiesTester[j].TimeFrame == TimeFrame.Sec1
                    || Storage.SecuritiesTester[j].TimeFrame == TimeFrame.Tick))
                {
                    isInArray = true;
                }
            }

            if (isInArray == false)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message43);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message43, LogMessageType.System);

                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.NameStrategy);
                }
                return false;
            }

            return true;
        }

        private void _optimizerExecutor_NeedToMoveUiToEvent(NeedToMoveUiTo moveUiTo)
        {
            if (NeedToMoveUiToEvent != null)
            {
                NeedToMoveUiToEvent(moveUiTo);
            }
        }

        public event Action<NeedToMoveUiTo> NeedToMoveUiToEvent;

        #endregion

        #region One bot test

        public BotPanel TestBot(OptimizerFazeReport faze, OptimizerReport report)
        {
            if (_aloneTestIsOver == false)
            {
                return null;
            }

            _resultBotAloneTest = null;

            _aloneTestIsOver = false;

            _fazeToTestAloneTest = faze;
            _reportToTestAloneTest = report;
            _awaitUiMasterAloneTest = new AwaitObject(OsLocalization.Optimizer.Label52, 100, 0, true);

            Task.Run(RunAloneBotTest);

            AwaitUi ui = new AwaitUi(_awaitUiMasterAloneTest);
            ui.ShowDialog();

            Thread.Sleep(500);

            return _resultBotAloneTest;
        }

        private OptimizerFazeReport _fazeToTestAloneTest;

        private OptimizerReport _reportToTestAloneTest;

        private AwaitObject _awaitUiMasterAloneTest;

        private BotPanel _resultBotAloneTest;

        private bool _aloneTestIsOver = true;

        private async void RunAloneBotTest()
        {
            await Task.Delay(2000);
            _resultBotAloneTest =
                _optimizerExecutor.TestBot(_fazeToTestAloneTest, _reportToTestAloneTest,
                StartProgram.IsTester, _awaitUiMasterAloneTest);

            _aloneTestIsOver = true;
        }

        #endregion

        #region Log

        private Log _log;

        public void StartPaintLog(WindowsFormsHost logHost)
        {
            _log.StartPaint(logHost);
        }

        public void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class ProgressBarStatus
    {
        public int CurrentValue;

        public int MaxValue;

        public int Num;

        public bool IsFinalized;
    }

    public class OptimizerFaze
    {
        public OptimizerFazeType TypeFaze;

        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
                Days = Convert.ToInt32((TimeEnd - _timeStart).TotalDays);
            }
        }
        private DateTime _timeStart;

        public DateTime TimeEnd
        {
            get { return _timeEnd; }
            set
            {
                _timeEnd = value;
                Days = Convert.ToInt32((value - TimeStart).TotalDays);
            }
        }
        private DateTime _timeEnd;

        public int Days;

        public string GetSaveString()
        {
            string result = "";

            result += TypeFaze.ToString() + "%";

            result += _timeStart.ToString(CultureInfo.InvariantCulture) + "%";

            result += _timeEnd.ToString(CultureInfo.InvariantCulture) + "%";

            result += Days.ToString() + "%";

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            Enum.TryParse(str[0], out TypeFaze);

            _timeStart = Convert.ToDateTime(str[1], CultureInfo.InvariantCulture);

            _timeEnd = Convert.ToDateTime(str[2], CultureInfo.InvariantCulture);

            Days = Convert.ToInt32(str[3]);
        }

    }

    public enum OptimizerFazeType
    {
        InSample,

        OutOfSample
    }

    public class TabSimpleEndTimeFrame
    {
        public int NumberOfTab;

        public string NameSecurity;

        public TimeFrame TimeFrame;

        public string GetSaveString()
        {
            string result = "";
            result += NumberOfTab + "%";
            result += NameSecurity + "%";
            result += TimeFrame;

            return result;
        }

        public void SetFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            NumberOfTab = Convert.ToInt32(str[0]);
            NameSecurity = str[1];
            Enum.TryParse(str[2], out TimeFrame);
        }
    }

    public class TabIndexEndTimeFrame
    {
        public int NumberOfTab;

        public List<string> NamesSecurity = new List<string>();

        public TimeFrame TimeFrame;

        public string Formula;

        public string GetSaveString()
        {
            string result = "";
            result += NumberOfTab + "%";
            result += TimeFrame + "%";
            result += Formula + "%";

            for (int i = 0; i < NamesSecurity.Count; i++)
            {
                result += NamesSecurity[i];

                if (i + 1 != NamesSecurity.Count)
                {
                    result += "^";
                }
            }

            return result;
        }

        public void SetFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            NumberOfTab = Convert.ToInt32(str[0]);
            Enum.TryParse(str[1], out TimeFrame);
            Formula = str[2];

            if (str.Length > 2)
            {
                string[] secs = str[3].Split('^');

                for (int i = 0; i < secs.Length; i++)
                {
                    string sec = secs[i];
                    NamesSecurity.Add(sec);
                }
            }
        }
    }

    public enum NeedToMoveUiTo
    {
        NameStrategy,

        Fazes,

        Storage,

        TabsAndTimeFrames,

        Parameters,

        Filters,

        RegimeRow
    }
}
