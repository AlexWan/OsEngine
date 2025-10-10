/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels.Tab.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    ///  tab - for trading pairs
    /// </summary>
    public class BotTabPair : IIBotTab
    {
        #region Service. Constructor. Override for the interface

        public BotTabPair(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;
            StandardManualControl = new BotManualControl(name, null, StartProgram);

            LoadStandartSettings();
            LoadPairs();
        }

        /// <summary>
        /// The program in which this source is running.
        /// </summary>
        public StartProgram StartProgram;

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Pair;
            }
        }

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].EventsIsOn = value;
                }

                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
                SaveStandartSettings();
            }
        }
        private bool _eventsIsOn = true;

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn
        {
            get
            {
                return _emulatorIsOn;
            }
            set
            {

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].EmulatorIsOn = value;
                }

                if (_emulatorIsOn == value)
                {
                    return;
                }
                _emulatorIsOn = value;
                SaveStandartSettings();
            }
        }
        private bool _emulatorIsOn = false;

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        public void Clear()
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                Pairs[i].Tab1.Clear();
                Pairs[i].Tab2.Clear();
            }
        }

        /// <summary>
        /// Delete the source and clean up the data behind
        /// </summary>
        public void Delete()
        {
            try
            {
                _isDeleted = true;

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"StandartPairsSettings.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"StrategSettings.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"StrategSettings.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"PairsNamesToLoad.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].Delete();

                    Pairs[i].CointegrationPositionSideChangeEvent -= Pair_CointegrationPositionSideChangeEvent;
                    Pairs[i].CorrelationChangeEvent -= NewPair_CorrelationChangeEvent;
                    Pairs[i].CointegrationChangeEvent -= Pair_CointegrationChangeEvent;
                    Pairs[i].CandlesInPairSyncFinishedEvent -= Pair_CandlesInPairSyncFinishedEvent;
                    Pairs[i].LogMessageEvent -= SendNewLogMessage;
                }

                if (Pairs != null)
                {
                    Pairs.Clear();
                    Pairs = null;
                }

                if (_grid != null)
                {
                    _grid.Rows.Clear();
                    _grid.CellClick -= _grid_CellClick;
                    _grid.DataError -= _grid_DataError;
                    DataGridFactory.ClearLinks(_grid);
                }
                if (_host != null)
                {
                    _host.Child = null;
                    _host = null;
                }

                if (_positionViewer != null)
                {
                    _positionViewer.UserSelectActionEvent -= _globalController_UserSelectActionEvent;
                    _positionViewer.UserClickOnPositionShowBotInTableEvent -= _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
                    _positionViewer.Delete();
                }

                if (TabDeletedEvent != null)
                {
                    TabDeletedEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Take all the journals for all the pairs
        /// </summary>
        public List<Journal.Journal> GetJournals()
        {
            try
            {
                List<Journal.Journal> journals = new List<Journal.Journal>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    journals.Add(Pairs[i].Tab1.GetJournal());
                    journals.Add(Pairs[i].Tab2.GetJournal());
                }

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action TabDeletedEvent;

        public List<string> SecuritiesActivated
        {
            get
            {
                if (Pairs.Count == 0)
                {
                    return null;
                }

                List<string> result = new List<string>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    BotTabSimple tab1 = Pairs[i].Tab1;
                    BotTabSimple tab2 = Pairs[i].Tab2;

                    if (string.IsNullOrEmpty(tab1.Connector.SecurityName) == false)
                    {
                        result.Add(tab1.Connector.SecurityName);
                    }

                    if (string.IsNullOrEmpty(tab2.Connector.SecurityName) == false)
                    {
                        result.Add(tab2.Connector.SecurityName);
                    }
                }

                return result;
            }
        }

        #endregion

        #region Common settings

        /// <summary>
        /// Cointegration calculation enabled
        /// </summary>
        public bool AutoRebuildCointegration = true;

        /// <summary>
        /// Correlation calculation enabled
        /// </summary>
        public bool AutoRebuildCorrelation = true;

        /// <summary>
        /// Security 1. Trade Mode
        /// </summary>
        public PairTraderSecurityTradeRegime Sec1TradeRegime;

        /// <summary>
        /// Security 1. Slippage calculation Mode
        /// </summary>
        public PairTraderSlippageType Sec1SlippageType;

        /// <summary>
        /// Security 1. Slippage value
        /// </summary>
        public decimal Sec1Slippage = 0;

        /// <summary>
        /// Security 1. Volume calculation Mode
        /// </summary>
        public PairTraderVolumeType Sec1VolumeType;

        /// <summary>
        /// Security 1. Volume value
        /// </summary>
        public decimal Sec1Volume = 7;

        /// <summary>
        /// Security 2. Trade Mode
        /// </summary>
        public PairTraderSecurityTradeRegime Sec2TradeRegime;

        /// <summary>
        /// Security 2. Slippage calculation Mode
        /// </summary>
        public PairTraderSlippageType Sec2SlippageType;

        /// <summary>
        /// Security 2. Slippage value
        /// </summary>
        public decimal Sec2Slippage = 0;

        /// <summary>
        /// Security 2. Volume calculation Mode
        /// </summary>
        public PairTraderVolumeType Sec2VolumeType;

        /// <summary>
        /// Security 2. Volume value
        /// </summary>
        public decimal Sec2Volume = 7;

        /// <summary>
        /// Length of correlation calculation
        /// </summary>
        public int CorrelationLookBack = 50;

        /// <summary>
        /// Length of cointegration calculation
        /// </summary>
        public int CointegrationLookBack = 50;

        /// <summary>
        /// Deviation for calculating cointegration
        /// </summary>
        public decimal CointegrationDeviation = 1;

        /// <summary>
        /// Module support position. The standard settings from it will be copied to all pairs
        /// </summary>
        public BotManualControl StandardManualControl;

        /// <summary>
        /// Type of pair sorting in the pair list
        /// </summary>
        public MainGridPairSortType PairSortType;

        /// <summary>
        /// Save Settings
        /// </summary>
        public void SaveStandartSettings()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"StandartPairsSettings.txt", false))
                {

                    writer.WriteLine(Sec1Slippage);
                    writer.WriteLine(Sec1Volume);
                    writer.WriteLine(Sec2Slippage);
                    writer.WriteLine(Sec2Volume);
                    writer.WriteLine(CorrelationLookBack);
                    writer.WriteLine(CointegrationDeviation);
                    writer.WriteLine(CointegrationLookBack);
                    writer.WriteLine(PairSortType);
                    writer.WriteLine(Sec1SlippageType);
                    writer.WriteLine(Sec1VolumeType);
                    writer.WriteLine(Sec2SlippageType);
                    writer.WriteLine(Sec2VolumeType);
                    writer.WriteLine(_eventsIsOn);
                    writer.WriteLine(_emulatorIsOn);
                    writer.WriteLine(Sec1TradeRegime);
                    writer.WriteLine(Sec2TradeRegime);
                    writer.WriteLine(AutoRebuildCointegration);
                    writer.WriteLine(AutoRebuildCorrelation);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// Load Settings
        /// </summary>
        private void LoadStandartSettings()
        {
            if (!File.Exists(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
                {
                    Sec1Slippage = reader.ReadLine().ToDecimal();
                    Sec1Volume = reader.ReadLine().ToDecimal();
                    Sec2Slippage = reader.ReadLine().ToDecimal();
                    Sec2Volume = reader.ReadLine().ToDecimal();
                    CorrelationLookBack = Convert.ToInt32(reader.ReadLine());
                    CointegrationDeviation = reader.ReadLine().ToDecimal();
                    CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out PairSortType);
                    Enum.TryParse(reader.ReadLine(), out Sec1SlippageType);
                    Enum.TryParse(reader.ReadLine(), out Sec1VolumeType);
                    Enum.TryParse(reader.ReadLine(), out Sec2SlippageType);
                    Enum.TryParse(reader.ReadLine(), out Sec2VolumeType);

                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out Sec1TradeRegime);
                    Enum.TryParse(reader.ReadLine(), out Sec2TradeRegime);

                    AutoRebuildCointegration = Convert.ToBoolean(reader.ReadLine());
                    AutoRebuildCorrelation = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region Storage, creation and deletion of pairs

        /// <summary>
        /// Pair array 
        /// </summary>
        public List<PairToTrade> Pairs = new List<PairToTrade>();

        /// <summary>
        /// Create a new trading pair
        /// </summary>
        public void CreatePair()
        {
            try
            {
                int number = 0;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].PairNum >= number)
                    {
                        number = Pairs[i].PairNum + 1;
                    }
                }

                PairToTrade pair = new PairToTrade(TabName + number, StartProgram);
                pair.PairNum = number;

                pair.Sec1Slippage = Sec1Slippage;
                pair.Sec1Volume = Sec1Volume;
                pair.Sec2Slippage = Sec2Slippage;
                pair.Sec2Volume = Sec2Volume;
                pair.CorrelationLookBack = CorrelationLookBack;
                pair.CointegrationDeviation = CointegrationDeviation;
                pair.CointegrationLookBack = CointegrationLookBack;
                pair.Sec1SlippageType = Sec1SlippageType;
                pair.Sec2SlippageType = Sec2SlippageType;
                pair.Sec1VolumeType = Sec1VolumeType;
                pair.Sec2VolumeType = Sec2VolumeType;
                pair.Sec1TradeRegime = Sec1TradeRegime;
                pair.Sec2TradeRegime = Sec2TradeRegime;

                CopyPositionControllerSettings(pair.Tab1, StandardManualControl);
                CopyPositionControllerSettings(pair.Tab2, StandardManualControl);

                pair.EmulatorIsOn = _emulatorIsOn;
                pair.EventsIsOn = _eventsIsOn;
                pair.Save();

                Pairs.Add(pair);

                SavePairNames();

                pair.CointegrationPositionSideChangeEvent += Pair_CointegrationPositionSideChangeEvent;
                pair.CorrelationChangeEvent += NewPair_CorrelationChangeEvent;
                pair.CointegrationChangeEvent += Pair_CointegrationChangeEvent;
                pair.LogMessageEvent += SendNewLogMessage;
                pair.CandlesInPairSyncFinishedEvent += Pair_CandlesInPairSyncFinishedEvent;

                if (PairToTradeCreateEvent != null)
                {
                    PairToTradeCreateEvent(pair);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            SetJournalsInPosViewer();
        }

        /// <summary>
        /// Move the default position tracking settings to the pair
        /// </summary>
        private void CopyPositionControllerSettings(BotTabSimple tab, BotManualControl control)
        {
            try
            {
                tab.ManualPositionSupport.SecondToClose = control.SecondToClose;
                tab.ManualPositionSupport.SecondToOpen = control.SecondToOpen;
                tab.ManualPositionSupport.DoubleExitIsOn = control.DoubleExitIsOn;
                tab.ManualPositionSupport.DoubleExitSlippage = control.DoubleExitSlippage;
                tab.ManualPositionSupport.ProfitDistance = control.ProfitDistance;
                tab.ManualPositionSupport.ProfitIsOn = control.ProfitIsOn;
                tab.ManualPositionSupport.ProfitSlippage = control.ProfitSlippage;
                tab.ManualPositionSupport.SecondToCloseIsOn = control.SecondToCloseIsOn;
                tab.ManualPositionSupport.SecondToOpenIsOn = control.SecondToOpenIsOn;
                tab.ManualPositionSupport.SetbackToCloseIsOn = control.SetbackToCloseIsOn;
                tab.ManualPositionSupport.SetbackToClosePosition = control.SetbackToOpenPosition;
                tab.ManualPositionSupport.SetbackToOpenIsOn = control.SetbackToOpenIsOn;
                tab.ManualPositionSupport.SetbackToOpenPosition = control.SetbackToOpenPosition;
                tab.ManualPositionSupport.StopDistance = control.StopDistance;
                tab.ManualPositionSupport.StopIsOn = control.StopIsOn;
                tab.ManualPositionSupport.StopSlippage = control.StopSlippage;
                tab.ManualPositionSupport.TypeDoubleExitOrder = control.TypeDoubleExitOrder;
                tab.ManualPositionSupport.ValuesType = control.ValuesType;
                tab.ManualPositionSupport.OrderTypeTime = control.OrderTypeTime;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Delete a trading pair
        /// </summary>
        /// <param name="numberInArray"></param>
        public void DeletePair(int numberInArray, bool needToRepaint)
        {
            try
            {
                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].PairNum == numberInArray)
                    {
                        Pairs[i].CointegrationPositionSideChangeEvent -= Pair_CointegrationPositionSideChangeEvent;
                        Pairs[i].CorrelationChangeEvent -= NewPair_CorrelationChangeEvent;
                        Pairs[i].CointegrationChangeEvent -= Pair_CointegrationChangeEvent;
                        Pairs[i].LogMessageEvent -= SendNewLogMessage;
                        Pairs[i].CandlesInPairSyncFinishedEvent -= Pair_CandlesInPairSyncFinishedEvent;
                        Pairs[i].Delete();
                        Pairs.RemoveAt(i);
                        SavePairNames();

                        if (needToRepaint)
                        {
                            RePaintGrid();
                        }

                        return;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Delete all pairs
        /// </summary>
        public void DeleteAllPairs()
        {
            if (Pairs.Count == 0)
            {
                return;
            }

            PairToTrade[] pairs = Pairs.ToArray();

            for (int i = 0; i < pairs.Length; i++)
            {
                DeletePair(pairs[i].PairNum, false);
            }
            RePaintGrid();
        }

        /// <summary>
        /// Save pairs
        /// </summary>
        public void SavePairNames()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"PairsNamesToLoad.txt", false))
                {

                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        writer.WriteLine(Pairs[i].Name);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// Load pairs
        /// </summary>
        private void LoadPairs()
        {
            if (!File.Exists(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string pairName = reader.ReadLine();
                        PairToTrade newPair = new PairToTrade(pairName, StartProgram);
                        newPair.CointegrationPositionSideChangeEvent += Pair_CointegrationPositionSideChangeEvent;
                        newPair.CorrelationChangeEvent += NewPair_CorrelationChangeEvent;
                        newPair.CointegrationChangeEvent += Pair_CointegrationChangeEvent;
                        newPair.LogMessageEvent += SendNewLogMessage;
                        newPair.CandlesInPairSyncFinishedEvent += Pair_CandlesInPairSyncFinishedEvent;
                        Pairs.Add(newPair);
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            SetJournalsInPosViewer();
        }

        /// <summary>
        /// Apply the default settings to all pairs
        /// </summary>
        public void ApplySettingsFromStandardToAll()
        {
            try
            {
                for (int i = 0; i < Pairs.Count; i++)
                {
                    PairToTrade pair = Pairs[i];

                    pair.Sec1Slippage = Sec1Slippage;
                    pair.Sec1Volume = Sec1Volume;
                    pair.Sec2Slippage = Sec2Slippage;
                    pair.Sec2Volume = Sec2Volume;
                    pair.CorrelationLookBack = CorrelationLookBack;
                    pair.CointegrationDeviation = CointegrationDeviation;
                    pair.CointegrationLookBack = CointegrationLookBack;
                    pair.Sec1SlippageType = Sec1SlippageType;
                    pair.Sec2SlippageType = Sec2SlippageType;
                    pair.Sec1VolumeType = Sec1VolumeType;
                    pair.Sec2VolumeType = Sec2VolumeType;
                    pair.Sec1TradeRegime = Sec1TradeRegime;
                    pair.Sec2TradeRegime = Sec2TradeRegime;
                    pair.AutoRebuildCointegration = AutoRebuildCointegration;
                    pair.AutoRebuildCorrelation = AutoRebuildCorrelation;

                    CopyPositionControllerSettings(pair.Tab1, StandardManualControl);
                    CopyPositionControllerSettings(pair.Tab2, StandardManualControl);

                    pair.Save();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Sort the array with pairs
        /// </summary>
        public void SortPairsArray()
        {
            try
            {
                if (Pairs == null ||
                    Pairs.Count == 0)
                {
                    return;
                }

                if (PairSortType == MainGridPairSortType.No)
                { // по номерам
                    for (int i = 1; i < Pairs.Count; i++)
                    {
                        for (int i2 = i; i2 < Pairs.Count; i2++)
                        {
                            if (Pairs[i2].PairNum < Pairs[i2 - 1].PairNum)
                            {
                                PairToTrade pair = Pairs[i2];
                                Pairs[i2] = Pairs[i2 - 1];
                                Pairs[i2 - 1] = pair;
                            }
                        }
                    }
                }
                else if (PairSortType == MainGridPairSortType.Side)
                { // по стороне коинтеграции

                    for (int i = 1; i < Pairs.Count; i++)
                    {
                        for (int i2 = i; i2 < Pairs.Count; i2++)
                        {// up - в начало
                            if (Pairs[i2].SideCointegrationValue == CointegrationLineSide.Up
                                && Pairs[i2 - 1].SideCointegrationValue != CointegrationLineSide.Up)
                            {
                                PairToTrade pair = Pairs[i2];
                                Pairs[i2] = Pairs[i2 - 1];
                                Pairs[i2 - 1] = pair;
                            }
                        }

                        for (int i2 = i; i2 < Pairs.Count; i2++)
                        { // no - в конец
                            if (Pairs[i2].SideCointegrationValue != CointegrationLineSide.No
                                && Pairs[i2 - 1].SideCointegrationValue == CointegrationLineSide.No)
                            {
                                PairToTrade pair = Pairs[i2];
                                Pairs[i2] = Pairs[i2 - 1];
                                Pairs[i2 - 1] = pair;
                            }
                        }
                    }

                }
                else if (PairSortType == MainGridPairSortType.Correlation)
                { // по наивысшему значению корреляции
                    for (int i = 1; i < Pairs.Count; i++)
                    {
                        for (int i2 = i; i2 < Pairs.Count; i2++)
                        {
                            if (Pairs[i2].CorrelationLast > Pairs[i2 - 1].CorrelationLast
                                || Pairs[i2].CorrelationLast != 0 && Pairs[i2 - 1].CorrelationLast == 0)
                            {
                                PairToTrade pair = Pairs[i2];
                                Pairs[i2] = Pairs[i2 - 1];
                                Pairs[i2 - 1] = pair;
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Number of pairs with positions
        /// </summary>
        public int PairsWithPositionsCount
        {
            get
            {
                int result = 0;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].HavePositions)
                    {
                        result++;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// PositionsCount[0] - longs count, PositionsCount[1] - shorts count
        /// </summary>
        public List<int> PositionsDirectionsCount
        {
            get
            {
                List<int> count = new List<int>();

                if (Pairs.Count == 0)
                {
                    return count;
                }

                int longCount = 0;
                int shortCount = 0;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    PairToTrade curPair = Pairs[i];

                    List<Position> posesTab1 = curPair.Tab1.PositionsOpenAll;
                    List<Position> posesTab2 = curPair.Tab2.PositionsOpenAll;

                    if (posesTab1.Count > 0)
                    {
                        for (int j = 0; j < posesTab1.Count; j++)
                        {
                            Position pos = posesTab1[j];

                            if (pos.Direction == Side.Buy)
                            {
                                longCount++;
                            }
                            else if (pos.Direction == Side.Sell)
                            {
                                shortCount++;
                            }
                        }
                    }
                    if (posesTab2.Count > 0)
                    {
                        for (int j = 0; j < posesTab2.Count; j++)
                        {
                            Position pos = posesTab2[j];

                            if (pos.Direction == Side.Buy)
                            {
                                longCount++;
                            }
                            else if (pos.Direction == Side.Sell)
                            {
                                shortCount++;
                            }
                        }
                    }
                }

                count.Add(longCount);
                count.Add(shortCount);

                return count;
            }
        }

        /// <summary>
        /// PositionsCount[0] - longs count, PositionsCount[1] - shorts count
        /// </summary>
        public List<int> PositionsDirectionsCountBySecurity(string security)
        {

            List<int> count = new List<int>();

            if (Pairs.Count == 0)
            {
                return count;
            }

            int longCount = 0;
            int shortCount = 0;

            for (int i = 0; i < Pairs.Count; i++)
            {
                PairToTrade curPair = Pairs[i];

                if (curPair.Tab1.Security.Name == security)
                {
                    List<Position> posesTab1 = curPair.Tab1.PositionsOpenAll;
                    if (posesTab1.Count > 0)
                    {
                        for (int j = 0; j < posesTab1.Count; j++)
                        {
                            Position pos = posesTab1[j];

                            if (pos.Direction == Side.Buy)
                            {
                                longCount++;
                            }
                            else if (pos.Direction == Side.Sell)
                            {
                                shortCount++;
                            }
                        }
                    }
                }

                if (curPair.Tab2.Security.Name == security)
                {
                    List<Position> posesTab2 = curPair.Tab2.PositionsOpenAll;
                    if (posesTab2.Count > 0)
                    {
                        for (int j = 0; j < posesTab2.Count; j++)
                        {
                            Position pos = posesTab2[j];

                            if (pos.Direction == Side.Buy)
                            {
                                longCount++;
                            }
                            else if (pos.Direction == Side.Sell)
                            {
                                shortCount++;
                            }
                        }
                    }
                }
            }

            count.Add(longCount);
            count.Add(shortCount);

            return count;
        }


        #endregion

        #region Automatic creation of trading pairs

        /// <summary>
        /// Make a list of previously created pairs
        /// </summary>
        public List<string> CreatedPairs
        {
            get
            {
                if (Pairs.Count == 0)
                {
                    return null;
                }

                List<string> createdPairs = new List<string>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (string.IsNullOrEmpty(Pairs[i].Tab1.Connector.SecurityName)
                        || string.IsNullOrEmpty(Pairs[i].Tab2.Connector.SecurityName))
                    {
                        continue;
                    }

                    string name1 = Pairs[i].Tab1.Connector.SecurityName;
                    string name2 = Pairs[i].Tab2.Connector.SecurityName;

                    string resultNamePair = name1 + "_|_" + name2;
                    createdPairs.Add(resultNamePair);
                }

                return createdPairs;
            }
        }

        /// <summary>
        /// Is this pair of securities in trade?
        /// </summary>
        public bool HaveThisPairInTrade(string sec1, string sec2, string secClass,
            TimeFrame timeFrame, ServerType serverType, string serverName)
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                string curSecName1 = Pairs[i].Tab1.Connector.SecurityName;
                string curSecName2 = Pairs[i].Tab2.Connector.SecurityName;
                string curSecClass1 = Pairs[i].Tab1.Connector.SecurityClass;
                string curSecClass2 = Pairs[i].Tab2.Connector.SecurityClass;

                ServerType serverType1 = Pairs[i].Tab1.Connector.ServerType;
                ServerType serverType2 = Pairs[i].Tab2.Connector.ServerType;

                string serverName1 = Pairs[i].Tab1.Connector.ServerFullName;
                string serverName2 = Pairs[i].Tab2.Connector.ServerFullName;

                TimeFrame timeFrame1 = Pairs[i].Tab1.Connector.TimeFrame;
                TimeFrame timeFrame2 = Pairs[i].Tab2.Connector.TimeFrame;

                if (sec1 == curSecName1 &&
                    sec2 == curSecName2 &&
                    secClass == curSecClass1 &&
                    secClass == curSecClass2 &&
                    timeFrame == timeFrame1 &&
                    timeFrame == timeFrame2 &&
                    serverType == serverType1 &&
                    serverType == serverType2 &&
                    serverName == serverName1 &&
                    serverName == serverName2)
                {
                    return true;
                }

                if (sec1 == curSecName2 &&
                    sec2 == curSecName1 &&
                    secClass == curSecClass2 &&
                    secClass == curSecClass1 &&
                    timeFrame == timeFrame2 &&
                    timeFrame == timeFrame1 &&
                    serverType == serverType2 &&
                    serverType == serverType1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Create a new pair according to the settings
        /// </summary>
        public void CreateNewPair(
            string sec1, string sec2, string secClass,
            TimeFrame timeFrame, ServerType serverType,
            CommissionType commissionType, decimal commissionValue,
            string portfolio, string serverName)
        {
            CreatePair();

            PairToTrade newPair = Pairs[Pairs.Count - 1];

            newPair.Tab1.CommissionType = commissionType;
            newPair.Tab1.CommissionValue = commissionValue;
            newPair.Tab1.Connector.ServerType = serverType;
            newPair.Tab1.Connector.ServerFullName = serverName;
            newPair.Tab1.Connector.TimeFrame = timeFrame;
            newPair.Tab1.Connector.SecurityName = sec1;
            newPair.Tab1.Connector.SecurityClass = secClass;
            newPair.Tab1.Connector.PortfolioName = portfolio;

            newPair.Tab2.CommissionType = commissionType;
            newPair.Tab2.CommissionValue = commissionValue;
            newPair.Tab2.Connector.ServerType = serverType;
            newPair.Tab2.Connector.ServerFullName = serverName;
            newPair.Tab2.Connector.TimeFrame = timeFrame;
            newPair.Tab2.Connector.SecurityName = sec2;
            newPair.Tab2.Connector.SecurityClass = secClass;
            newPair.Tab2.Connector.PortfolioName = portfolio;
        }

        #endregion

        #region External position management

        /// <summary>
        /// Close all market positions
        /// </summary>
        public void CloseAllPositionAtMarket()
        {
            try
            {
                if (Pairs == null)
                {
                    return;
                }

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].ClosePositions();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Check all pairs for this position with a certain number. Service function
        /// </summary>
        public BotTabSimple GetTabWithThisPosition(int positionNum)
        {
            try
            {
                BotTabSimple tabWithPosition = null;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    List<Position> posOnThisTab = Pairs[i].Tab1.PositionsAll;

                    for (int i2 = 0; i2 < posOnThisTab.Count; i2++)
                    {
                        if (posOnThisTab[i2].Number == positionNum)
                        {
                            return Pairs[i].Tab1;
                        }
                    }

                    posOnThisTab = Pairs[i].Tab2.PositionsAll;

                    for (int i2 = 0; i2 < posOnThisTab.Count; i2++)
                    {
                        if (posOnThisTab[i2].Number == positionNum)
                        {
                            return Pairs[i].Tab2;
                        }
                    }
                }

                return tabWithPosition;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// All pairs positions
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get
            {
                List<Position> positions = new List<Position>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    List<Position> curPoses = Pairs[i].Tab1.PositionsOpenAll;

                    if (Pairs[i].Tab2.PositionsOpenAll != null
                        && Pairs[i].Tab2.PositionsOpenAll.Count > 0)
                    {
                        curPoses.AddRange(Pairs[i].Tab2.PositionsOpenAll);
                    }

                    if (curPoses.Count != 0)
                    {
                        positions.AddRange(curPoses);
                    }
                }

                return positions;
            }
        }

        #endregion

        #region Outgoing events

        /// <summary>
        /// The pair has updated correlation value
        /// </summary>
        /// <param name="correlationArray">An array with correlation values. The actual value is the last</param>
        /// <param name="pair">Pair of instruments for which the correlation was updated</param>
        private void NewPair_CorrelationChangeEvent(List<PairIndicatorValue> correlationArray, PairToTrade pair)
        {
            try
            {
                if (PairSortType == MainGridPairSortType.Correlation)
                {
                    SortPairsArray();
                }

                if (CorrelationChangeEvent != null)
                {
                    CorrelationChangeEvent(correlationArray, pair);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// The pair has updated cointegration side value
        /// </summary>
        /// <param name="side">The side in which the current value of the deviation between the instruments is located, 
        /// relative to the lines on the cointegration graph. 
        /// No - on the middle. Up - above the top line. Down - below the bottom line</param>
        /// <param name="pair">Pair of instruments for which the cointegration was updated</param>
        private void Pair_CointegrationPositionSideChangeEvent(CointegrationLineSide side, PairToTrade pair)
        {
            try
            {

                if (PairSortType == MainGridPairSortType.Side)
                {
                    SortPairsArray();
                }

                if (CointegrationPositionSideChangeEvent != null)
                {
                    CointegrationPositionSideChangeEvent(side, pair);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        ///  The pair has updated cointegration value
        /// </summary>
        /// <param name="cointegrationArray">An array with cointegration values. The actual value is the last</param>
        /// <param name="pair">Pair of instruments for which the cointegration was updated</param>
        private void Pair_CointegrationChangeEvent(List<PairIndicatorValue> cointegrationArray, PairToTrade pair)
        {
            try
            {
                if (CointegrationChangeEvent != null)
                {
                    CointegrationChangeEvent(cointegrationArray, pair);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Pair_CandlesInPairSyncFinishedEvent(List<Candle> arg1, BotTabSimple arg2, List<Candle> arg3, BotTabSimple arg4, PairToTrade arg5)
        {
            if (CandlesInPairSyncFinishedEvent != null)
            {
                CandlesInPairSyncFinishedEvent(arg1, arg2, arg3, arg4, arg5);
            }
        }

        /// <summary>
        /// Some pair has updated correlation value. 
        /// List<PairIndicatorValue> - An array with correlation values. The actual value is the last. 
        /// PairToTrade - Pair of instruments for which the correlation was updated
        /// </summary>
        public event Action<List<PairIndicatorValue>, PairToTrade> CorrelationChangeEvent;

        /// <summary>
        /// Some pair has updated cointegration value. 
        /// List<PairIndicatorValue> - An array with cointegration values. The actual value is the last. 
        /// PairToTrade - Pair of instruments for which the cointegration was updated
        /// </summary>
        public event Action<List<PairIndicatorValue>, PairToTrade> CointegrationChangeEvent;

        /// <summary>
        /// Some pair has updated cointegration side value.  
        /// CointegrationLineSide - The side in which the current value of the deviation between the instruments is located, 
        /// relative to the lines on the cointegration graph. 
        /// No - on the middle. Up - above the top line. Down - below the bottom line. 
        /// PairToTrade - Pair of instruments for which the cointegration side was updated
        /// </summary>
        public event Action<CointegrationLineSide, PairToTrade> CointegrationPositionSideChangeEvent;

        /// <summary>
        /// The source has a new pair for trading
        /// </summary>
        public event Action<PairToTrade> PairToTradeCreateEvent;

        /// <summary>
        /// Candlesticks on the instruments in the pair have completed and have the same times
        /// 1. List<Candle> - candles of first security
        /// 2. BotTabSimple - source of first security
        /// 3. List<Candle> - candles of second security
        /// 4. BotTabSimple - source of second security
        /// 5. PairToTrade - the pair on which the event occurred
        /// </summary>
        public event Action<List<Candle>, BotTabSimple, List<Candle>, BotTabSimple, PairToTrade> CandlesInPairSyncFinishedEvent;

        #endregion

        #region Drawing the source table

        /// <summary>
        /// Thread drawing interface
        /// </summary>
        Thread painterThread;

        /// <summary>
        /// A method in which interface drawing methods are periodically called
        /// </summary>
        private void PainterThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);

                    if (_isDeleted)
                    {
                        return;
                    }

                    TryRePaintGrid();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// Flag indicating whether the source has been removed or not. false - not deleted. true - source deleted
        /// </summary>
        private bool _isDeleted;

        private GlobalPositionViewer _positionViewer;

        /// <summary>
        /// The area where the table of pairs is drawn
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// Start drawing the table of pairs
        /// </summary>
        public void StartPaint(WindowsFormsHost host,
            WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals)
        {
            try
            {
                _host = host;

                if (_grid == null)
                {
                    CreateGrid();
                }

                RePaintGrid();

                _host.Child = _grid;

                if (painterThread == null)
                {
                    painterThread = new Thread(PainterThread);
                    painterThread.Start();
                }

                if (_positionViewer == null)
                {
                    _positionViewer = new GlobalPositionViewer(StartProgram);
                    _positionViewer.LogMessageEvent += SendNewLogMessage;
                    _positionViewer.UserSelectActionEvent += _globalController_UserSelectActionEvent;
                    _positionViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
                }

                SetJournalsInPosViewer();
                _positionViewer.StartPaint(hostOpenDeals, hostCloseDeals);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Stop drawing the table of pairs
        /// </summary>
        public void StopPaint()
        {
            if (_host != null)
            {
                _host.Child = null;
            }

            if (_positionViewer != null)
            {
                _positionViewer.StopPaint();
            }
        }

        private void SetJournalsInPosViewer()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (_positionViewer == null)
            {
                return;
            }

            try
            {

                _positionViewer.ClearJournalsArray();

                List<Journal.Journal> journals = new List<Journal.Journal>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    PairToTrade curPair = Pairs[i];

                    if (curPair.Tab1 != null)
                    {
                        Journal.Journal journal = curPair.Tab1.GetJournal();
                        journals.Add(journal);
                    }
                    if (curPair.Tab2 != null)
                    {
                        Journal.Journal journal = curPair.Tab2.GetJournal();
                        journals.Add(journal);
                    }
                }

                if (journals.Count > 0)
                {
                    _positionViewer.SetJournals(journals);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Position, SignalType> UserSelectActionEvent;

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        private void _globalController_UserSelectActionEvent(Position pos, SignalType signal)
        {
            if (UserSelectActionEvent != null)
            {
                UserSelectActionEvent(pos, signal);
            }
        }

        private void _globalPositionViewer_UserClickOnPositionShowBotInTableEvent(string botTabName)
        {
            if (UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }
        }

        /// <summary>
        /// Method for creating a table for drawing pairs
        /// </summary>
        private void CreateGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "";// pairNum
            colum0.ReadOnly = true;
            colum0.Width = 70;
            newGrid.Columns.Add(colum0);

            for (int i = 0; i < 5; i++)
            {
                DataGridViewColumn columN = new DataGridViewColumn();
                columN.CellTemplate = cell0;
                columN.HeaderText = "";
                columN.ReadOnly = false;
                columN.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(columN);
            }

            _grid = newGrid;
            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        /// <summary>
        /// The method of full redrawing of the table with pairs
        /// </summary>
        private void RePaintGrid()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(RePaintGrid));
                    return;
                }

                int showRow = _grid.FirstDisplayedScrollingRowIndex;

                _grid.Rows.Clear();

                List<DataGridViewRow> rows = GetRowsToGrid();

                if (rows == null)
                {
                    return;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    _grid.Rows.Add(rows[i]);
                }

                if (showRow > 0 &&
                    showRow < _grid.Rows.Count)
                {
                    _grid.FirstDisplayedScrollingRowIndex = showRow;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Simplified method of redrawing a table with pairs. Redraws the table only if there are updates in it
        /// </summary>
        private void TryRePaintGrid()
        {
            List<DataGridViewRow> rows = GetRowsToGrid();

            if (rows == null)
            {
                return;
            }

            if (rows.Count != _grid.Rows.Count)
            {// 1 кол-во строк изменилось - перерисовываем полностью
                RePaintGrid();
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Cells[0].Value != null && _grid.Rows[i].Cells[0].Value != null)
                {
                    if (rows[i].Cells[0].Value.ToString() != _grid.Rows[i].Cells[0].Value.ToString())
                    {
                        // 2 сортировка поменялась. Перерисовываем полностью
                        RePaintGrid();
                        return;
                    }
                    continue;
                }
                if (rows[i].Cells[0].Value == null && _grid.Rows[i].Cells[0].Value != null)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }
                if (rows[i].Cells[0].Value != null && _grid.Rows[i].Cells[0].Value == null)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }

                if (rows[i].Cells[0].Value != _grid.Rows[i].Cells[0].Value)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }
            }

            DataGridViewRow firstOldRow = _grid.Rows[0];

            MainGridPairSortType sideFromTable;

            if (firstOldRow.Cells[5].Value.ToString().EndsWith("Side"))
            {
                sideFromTable = MainGridPairSortType.Side;
            }
            else if (firstOldRow.Cells[5].Value.ToString().EndsWith("Correlation"))
            {
                sideFromTable = MainGridPairSortType.Correlation;
            }
            else
            {
                sideFromTable = MainGridPairSortType.No;
            }

            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                if (i >= _grid.Rows.Count)
                {
                    break;
                }

                TryRePaintRow(_grid.Rows[i], rows[i]);
            }

            if (sideFromTable != PairSortType)
            {
                PairSortType = sideFromTable;
                SortPairsArray();
                SaveStandartSettings();
            }
        }

        /// <summary>
        /// Redraw the row in the table
        /// </summary>
        private void TryRePaintRow(DataGridViewRow rowInGrid, DataGridViewRow rowInArray)
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<DataGridViewRow, DataGridViewRow>(TryRePaintRow), rowInGrid, rowInArray);
                return;
            }

            try
            {
                if (rowInGrid.Cells[1].Value.ToString() != rowInArray.Cells[1].Value.ToString())
                {
                    rowInGrid.Cells[1].Value = rowInArray.Cells[1].Value.ToString();

                    if (rowInGrid.Cells[1].Value.ToString().EndsWith("No"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.Black;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.Black;
                    }
                    else if (rowInGrid.Cells[1].Value.ToString().EndsWith("Up"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.DarkGreen;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkGreen;
                    }
                    else if (rowInGrid.Cells[1].Value.ToString().EndsWith("Down"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.DarkRed;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkRed;
                    }
                }
                if (rowInGrid.Cells[2].Value.ToString() != rowInArray.Cells[2].Value.ToString())
                {
                    rowInGrid.Cells[2].Value = rowInArray.Cells[2].Value.ToString();
                }
                if (rowInGrid.Cells[3].Value != null &&
                    rowInGrid.Cells[3].Value.ToString() != rowInArray.Cells[3].Value.ToString())
                {
                    rowInGrid.Cells[3].Value = rowInArray.Cells[3].Value.ToString();
                }
                if (rowInGrid.Cells[4].Value.ToString() != rowInArray.Cells[4].Value.ToString())
                {
                    rowInGrid.Cells[4].Value = rowInArray.Cells[4].Value.ToString();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Calculate all rows from the table of pairs
        /// </summary>
        private List<DataGridViewRow> GetRowsToGrid()
        {
            try
            {
                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                rows.Add(GetFirstGridRow());

                for (int i = 0; i < Pairs.Count; i++)
                {
                    rows.Add(GetPairRowOne(Pairs[i]));
                    rows.Add(GetPairRowTwo(Pairs[i]));
                    rows.Add(GetPairRowThree(Pairs[i]));
                    rows.Add(GetPairRowFour(Pairs[i]));
                }

                rows.Add(GetLastGridRow());

                return rows;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private DataGridViewRow GetFirstGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            DataGridViewButtonCell button0 = new DataGridViewButtonCell(); // удалить все пары
            button0.Value = OsLocalization.Trader.Label579;
            nRow.Cells.Add(button0);

            DataGridViewButtonCell button1 = new DataGridViewButtonCell(); // авто создание пар
            button1.Value = OsLocalization.Trader.Label257;
            nRow.Cells.Add(button1);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell(); // Общие настройки
            button2.Value = OsLocalization.Trader.Label232;
            nRow.Cells.Add(button2);

            DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();// Сортировка
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.No.ToString());
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.Side.ToString());
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.Correlation.ToString());

            comboBox.Value = OsLocalization.Trader.Label233 + ": " + PairSortType.ToString();

            nRow.Cells.Add(comboBox);

            return nRow;
        }

        private DataGridViewRow GetPairRowOne(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = OsLocalization.Trader.Label234;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            for (int i = 0; i < nRow.Cells.Count; i++)
            {
                nRow.Cells[i].Style.SelectionBackColor = System.Drawing.Color.Black;
                nRow.Cells[i].Style.BackColor = System.Drawing.Color.Black;
            }

            return nRow;
        }

        private DataGridViewRow GetPairRowTwo(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = pair.PairNum.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = "Side: " + pair.SideCointegrationValue;

            if (pair.SideCointegrationValue == CointegrationLineSide.No)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.Black;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.Black;
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkGreen;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkGreen;
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkRed;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkRed;
            }


            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "Corr: " + pair.CorrelationLast;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = "Coin: " + pair.CointegrationLast;

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // Чарт
            button.Value = OsLocalization.Trader.Label172;
            nRow.Cells.Add(button);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell(); // Удалить
            button2.Value = OsLocalization.Trader.Label39;
            nRow.Cells.Add(button2);

            return nRow;
        }

        private DataGridViewRow GetPairRowThree(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // бумага

            if (pair.Tab1.Connector != null &&
                string.IsNullOrEmpty(pair.Tab1.Connector.SecurityName) == false)
            {
                button.Value = pair.Tab1.Connector.SecurityName;
            }
            else
            {
                button.Value = OsLocalization.Trader.Label235;
            }

            nRow.Cells.Add(button);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "bid: " + pair.Tab1.PriceBestBid.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (pair.Tab1.Connector != null)
            {
                nRow.Cells[3].Value = pair.Tab1.Connector.ServerType.ToString();
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = "pos: " + pair.Tab1.VolumeNet.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            return nRow;
        }

        private DataGridViewRow GetPairRowFour(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // бумага

            if (pair.Tab2.Connector != null &&
                string.IsNullOrEmpty(pair.Tab2.Connector.SecurityName) == false)
            {
                button.Value = pair.Tab2.Connector.SecurityName;
            }
            else
            {
                button.Value = OsLocalization.Trader.Label235;
            }

            nRow.Cells.Add(button);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "bid: " + pair.Tab2.PriceBestBid.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (pair.Tab2.Connector != null)
            {
                nRow.Cells[3].Value = pair.Tab2.Connector.ServerType.ToString();
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = "pos: " + pair.Tab2.VolumeNet.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            return nRow;
        }

        private DataGridViewRow GetLastGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // добавить пару
            button.Value = OsLocalization.Trader.Label236;
            nRow.Cells.Add(button);

            return nRow;
        }

        /// <summary>
        /// Pair table for the visual interface
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// Table click event handler in the visual interface
        /// </summary>
        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int column = e.ColumnIndex;
                int row = e.RowIndex;

                if (_grid.Rows.Count == row + 1 &&
                    column == 5)
                { // создание вкладки
                    CreatePair();
                    RePaintGrid();
                }
                else if (column == 5)
                {// возможно удаление

                    int tabNum = -1;

                    try
                    {
                        if (_grid.Rows[row].Cells[0].Value == null)
                        {
                            return;
                        }

                        tabNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                    }
                    catch
                    {
                        return;
                    }

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label237);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction)
                    {
                        DeletePair(tabNum, true);
                    }
                }
                else if (column == 1)
                { // возможно кнопка подключения бумаги
                    if (_grid.Rows[row].Cells[0].Value != null)
                    {
                        return;
                    }

                    int pairNum = -1;
                    int tabNum = -1;

                    for (int i = row - 1; i >= 0; i--)
                    {
                        if (_grid.Rows[i].Cells[0].Value != null)
                        {
                            pairNum = Convert.ToInt32(_grid.Rows[i].Cells[0].Value);

                            if (i == row - 1)
                            {
                                tabNum = 1;
                            }
                            else
                            {
                                tabNum = 2;
                            }
                            break;
                        }
                    }

                    PairToTrade pair = null;

                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        if (Pairs[i].PairNum == pairNum)
                        {
                            pair = Pairs[i];
                            break;
                        }
                    }

                    if (tabNum == 1)
                    {
                        pair.Tab1.ShowConnectorDialog();
                    }
                    else if (tabNum == 2)
                    {
                        pair.Tab2.ShowConnectorDialog();
                    }
                }
                else if (column == 4 && row == 0)
                { // кнопка открытия общих настроек

                    if (_commonSettingsUi != null)
                    {
                        _commonSettingsUi.Activate();
                        return;
                    }

                    _commonSettingsUi = new BotTabPairCommonSettingsUi(this);
                    _commonSettingsUi.Show();
                    _commonSettingsUi.Closed += _commonSettingsUi_Closed;
                }
                else if (column == 2 && row == 0)
                { // кнопка удаления всех пар
                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label580);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction)
                    {
                        DeleteAllPairs();
                    }
                }
                else if (column == 3 && row == 0)
                { // кнопка открытия окна авто генерации пар
                    if (_autoSelectPairsUi != null
                        && _autoSelectPairsUi.IsActive == true)
                    {
                        _autoSelectPairsUi.Activate();
                        return;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {// if connection server to exchange hasn't been created yet
                        return;
                    }

                    _autoSelectPairsUi = new BotTabPairAutoSelectPairsUi(this);
                    _autoSelectPairsUi.Show();
                    _autoSelectPairsUi.Closed += _autoSelectPairsUi_Closed;

                }
                else if (column == 4)
                {
                    // возможно кнопка открытия отдельного окна пары или общих настроек

                    int tabNum = -1;

                    try
                    {
                        if (_grid.Rows[row].Cells[0].Value == null)
                        {
                            return;
                        }

                        tabNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                    }
                    catch
                    {
                        return;
                    }

                    PairToTrade pair = null;

                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        if (Pairs[i].PairNum == tabNum)
                        {
                            pair = Pairs[i];
                            break;
                        }
                    }

                    for (int i = 0; i < _uiList.Count; i++)
                    {
                        if (_uiList[i].NameElement == pair.Name)
                        {
                            _uiList[i].Activate();
                            return;
                        }
                    }

                    BotTabPairUi ui = new BotTabPairUi(pair);
                    ui.Show();
                    _uiList.Add(ui);

                    ui.Closed += Ui_Closed;

                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Array with separate windows for viewing pairs
        /// </summary>
        private List<BotTabPairUi> _uiList = new List<BotTabPairUi>();

        /// <summary>
        /// Event handler for closing a separate pair window
        /// </summary>
        private void Ui_Closed(object sender, EventArgs e)
        {
            try
            {

                string name = ((BotTabPairUi)sender).NameElement;

                for (int i = 0; i < _uiList.Count; i++)
                {
                    if (_uiList[i].NameElement == name)
                    {
                        _uiList[i].Closed -= Ui_Closed;
                        _uiList.RemoveAt(i);
                        return;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Common settings window for all pairs in the source
        /// </summary>
        private BotTabPairCommonSettingsUi _commonSettingsUi;

        /// <summary>
        /// Event handler for closing a common settings window
        /// </summary>
        private void _commonSettingsUi_Closed(object sender, EventArgs e)
        {
            _commonSettingsUi.Closed -= _commonSettingsUi_Closed;
            _commonSettingsUi = null;
        }

        BotTabPairAutoSelectPairsUi _autoSelectPairsUi;

        private void _autoSelectPairsUi_Closed(object sender, EventArgs e)
        {
            _autoSelectPairsUi.Closed -= _autoSelectPairsUi_Closed;
            _autoSelectPairsUi = null;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Send new log message
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// Pair for trading
    /// </summary>
    public class PairToTrade
    {
        #region Service. Constructor

        /// <summary>
        /// Pair for trading constructor
        /// </summary>
        /// <param name="name">unique pair name</param>
        /// <param name="startProgram">The program in which this source is running</param>
        public PairToTrade(string name, StartProgram startProgram)
        {
            Name = name;

            Tab1 = new BotTabSimple(name + 1, startProgram);
            Tab2 = new BotTabSimple(name + 2, startProgram);

            Tab1.CandleFinishedEvent += Tab1_CandleFinishedEvent;
            Tab2.CandleFinishedEvent += Tab2_CandleFinishedEvent;

            Tab1.CandleUpdateEvent += Tab1_CandleUpdateEvent;
            Tab2.CandleUpdateEvent += Tab2_CandleUpdateEvent;

            Tab1.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;
            Tab2.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent1;

            Tab1.PositionOpeningSuccesEvent += Tab1_PositionOpeningSuccesEvent;
            Tab2.PositionOpeningSuccesEvent += Tab2_PositionOpeningSuccesEvent;

            Tab1.LogMessageEvent += SendNewLogMessage;
            Tab2.LogMessageEvent += SendNewLogMessage;

            Load();
        }

        /// <summary>
        /// Unique pair name
        /// </summary>
        public string Name;

        /// <summary>
        /// Unique pair number
        /// </summary>
        public int PairNum;

        /// <summary>
        /// Download the settings
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @"PairsSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"PairsSettings.txt"))
                {
                    PairNum = Convert.ToInt32(reader.ReadLine());

                    Sec1Slippage = reader.ReadLine().ToDecimal();
                    Sec1Volume = reader.ReadLine().ToDecimal();
                    Sec2Slippage = reader.ReadLine().ToDecimal();
                    Sec2Volume = reader.ReadLine().ToDecimal();
                    CorrelationLookBack = Convert.ToInt32(reader.ReadLine());
                    CointegrationDeviation = reader.ReadLine().ToDecimal();
                    CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out Sec1SlippageType);
                    Enum.TryParse(reader.ReadLine(), out Sec1VolumeType);
                    Enum.TryParse(reader.ReadLine(), out Sec2SlippageType);
                    Enum.TryParse(reader.ReadLine(), out Sec2VolumeType);
                    Enum.TryParse(reader.ReadLine(), out Sec1TradeRegime);
                    Enum.TryParse(reader.ReadLine(), out Sec2TradeRegime);
                    Enum.TryParse(reader.ReadLine(), out _lastEntryCointegrationSide);
                    _showTradePanelOnChart = Convert.ToBoolean(reader.ReadLine());
                    AutoRebuildCointegration = Convert.ToBoolean(reader.ReadLine());
                    AutoRebuildCorrelation = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// Save the settings
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"PairsSettings.txt", false))
                {
                    writer.WriteLine(PairNum);

                    writer.WriteLine(Sec1Slippage);
                    writer.WriteLine(Sec1Volume);
                    writer.WriteLine(Sec2Slippage);
                    writer.WriteLine(Sec2Volume);
                    writer.WriteLine(CorrelationLookBack);
                    writer.WriteLine(CointegrationDeviation);
                    writer.WriteLine(CointegrationLookBack);
                    writer.WriteLine(Sec1SlippageType);
                    writer.WriteLine(Sec1VolumeType);
                    writer.WriteLine(Sec2SlippageType);
                    writer.WriteLine(Sec2VolumeType);
                    writer.WriteLine(Sec1TradeRegime);
                    writer.WriteLine(Sec2TradeRegime);
                    writer.WriteLine(_lastEntryCointegrationSide);
                    writer.WriteLine(_showTradePanelOnChart);
                    writer.WriteLine(AutoRebuildCointegration);
                    writer.WriteLine(AutoRebuildCorrelation);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// Delete the pair 
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + Name + @"PairsSettings.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"PairsSettings.txt");
                }
            }
            catch
            {
                // ignore
            }


            Tab1.CandleFinishedEvent -= Tab1_CandleFinishedEvent;
            Tab2.CandleFinishedEvent -= Tab2_CandleFinishedEvent;

            Tab1.CandleUpdateEvent -= Tab1_CandleUpdateEvent;
            Tab2.CandleUpdateEvent -= Tab2_CandleUpdateEvent;

            Tab1.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent;
            Tab2.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent1;

            Tab1.PositionOpeningSuccesEvent -= Tab1_PositionOpeningSuccesEvent;
            Tab2.PositionOpeningSuccesEvent -= Tab2_PositionOpeningSuccesEvent;

            Tab1.LogMessageEvent -= SendNewLogMessage;
            Tab2.LogMessageEvent -= SendNewLogMessage;

            Tab1.Delete();
            Tab2.Delete();

            if (PairDeletedEvent != null)
            {
                PairDeletedEvent();
            }
        }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return Tab1.EventsIsOn;
            }
            set
            {
                if (Tab1.EventsIsOn == value
                    && Tab2.EventsIsOn == value)
                {
                    return;
                }

                Tab1.EventsIsOn = value;
                Tab2.EventsIsOn = value;
                Save();
            }
        }

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn
        {
            get
            {
                return Tab1.EmulatorIsOn;
            }
            set
            {
                if (Tab1.EmulatorIsOn == value
                    && Tab2.EmulatorIsOn == value)
                {
                    return;
                }

                Tab1.EmulatorIsOn = value;
                Tab2.EmulatorIsOn = value;
                Save();
            }
        }

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action PairDeletedEvent;

        /// <summary>
        /// Show Trade Panel On Chart Ui
        /// </summary>
        public bool ShowTradePanelOnChart
        {
            get
            {
                return _showTradePanelOnChart;
            }
            set
            {
                if (_showTradePanelOnChart == value)
                {
                    return;
                }
                _showTradePanelOnChart = value;
                Save();
            }
        }
        private bool _showTradePanelOnChart = true;

        #endregion

        #region Properties and settings

        /// <summary>
        /// Trading Security source 1
        /// </summary>
        public BotTabSimple Tab1;

        /// <summary>
        /// Trading Security source 2
        /// </summary>
        public BotTabSimple Tab2;

        /// <summary>
        /// Security 1. Trade Mode
        /// </summary>
        public PairTraderSecurityTradeRegime Sec1TradeRegime;

        /// <summary>
        /// Security 1. Slippage calculation Mode
        /// </summary>
        public PairTraderSlippageType Sec1SlippageType;

        /// <summary>
        /// Security 1. Slippage value
        /// </summary>
        public decimal Sec1Slippage = 0;

        /// <summary>
        /// Security 1. Volume calculation Mode
        /// </summary>
        public PairTraderVolumeType Sec1VolumeType;

        /// <summary>
        /// Security 1. Volume value
        /// </summary>
        public decimal Sec1Volume = 7;

        /// <summary>
        /// Security 2. Trade Mode
        /// </summary>
        public PairTraderSecurityTradeRegime Sec2TradeRegime;

        /// <summary>
        /// Security 2. Slippage calculation Mode
        /// </summary>
        public PairTraderSlippageType Sec2SlippageType;

        /// <summary>
        /// Security 2. Slippage value
        /// </summary>
        public decimal Sec2Slippage = 0;

        /// <summary>
        /// Security 2. Volume calculation Mode
        /// </summary>
        public PairTraderVolumeType Sec2VolumeType;

        /// <summary>
        /// Security 2. Volume value
        /// </summary>
        public decimal Sec2Volume = 7;

        /// <summary>
        /// Do the sources have open positions
        /// </summary>
        public bool HavePositions
        {
            get
            {
                if (Tab1.PositionsOpenAll.Count != 0)
                {
                    return true;
                }

                if (Tab2.PositionsOpenAll.Count != 0)
                {
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region Trading methods

        /// <summary>
        /// Sell security 1 and buy security 2
        /// </summary>
        public void SellSec1BuySec2()
        {
            if (Sec1TradeRegime != PairTraderSecurityTradeRegime.Off)
            {
                decimal vol = GetVolume(Sec1VolumeType, Sec1Volume, Tab1);

                if (Sec1TradeRegime == PairTraderSecurityTradeRegime.Market)
                {
                    Tab1.SellAtMarket(vol);
                }
                else if (Sec1TradeRegime == PairTraderSecurityTradeRegime.Limit)
                {
                    decimal price = GetPrice(Sec1SlippageType, Sec1Slippage, Side.Sell, Tab1);

                    Tab1.SellAtLimit(vol, price);
                }
            }
            if (Sec2TradeRegime != PairTraderSecurityTradeRegime.Off)
            {
                decimal vol = GetVolume(Sec2VolumeType, Sec2Volume, Tab2);

                if (Sec2TradeRegime == PairTraderSecurityTradeRegime.Market)
                {
                    Tab2.BuyAtMarket(vol);
                }
                else if (Sec2TradeRegime == PairTraderSecurityTradeRegime.Limit)
                {
                    decimal price = GetPrice(Sec2SlippageType, Sec2Slippage, Side.Buy, Tab2);

                    Tab2.BuyAtLimit(vol, price);
                }
            }

            LastEntryCointegrationSide = SideCointegrationValue;
        }

        /// <summary>
        /// Buy security 1 and sell security 2
        /// </summary>
        public void BuySec1SellSec2()
        {
            if (Sec1TradeRegime != PairTraderSecurityTradeRegime.Off)
            {
                decimal vol = GetVolume(Sec1VolumeType, Sec1Volume, Tab1);

                if (Sec1TradeRegime == PairTraderSecurityTradeRegime.Market)
                {
                    Tab1.BuyAtMarket(vol);
                }
                else if (Sec1TradeRegime == PairTraderSecurityTradeRegime.Limit)
                {
                    decimal price = GetPrice(Sec1SlippageType, Sec1Slippage, Side.Buy, Tab1);

                    Tab1.BuyAtLimit(vol, price);
                }
            }
            if (Sec2TradeRegime != PairTraderSecurityTradeRegime.Off)
            {
                decimal vol = GetVolume(Sec2VolumeType, Sec2Volume, Tab2);

                if (Sec2TradeRegime == PairTraderSecurityTradeRegime.Market)
                {
                    Tab2.SellAtMarket(vol);
                }
                else if (Sec2TradeRegime == PairTraderSecurityTradeRegime.Limit)
                {
                    decimal price = GetPrice(Sec2SlippageType, Sec2Slippage, Side.Sell, Tab2);

                    Tab2.SellAtLimit(vol, price);
                }
            }
            LastEntryCointegrationSide = SideCointegrationValue;
        }

        /// <summary>
        /// Close all positions on the pair and recall all orders in the market
        /// </summary>
        public void ClosePositions()
        {
            Tab1.CloseAllOrderInSystem();
            Tab1.CloseAllAtMarket();

            Tab2.CloseAllOrderInSystem();
            Tab2.CloseAllAtMarket();
        }

        /// <summary>
        /// Calculate the volume on security
        /// </summary>
        private decimal GetVolume(PairTraderVolumeType volumeType, decimal volumeValue, BotTabSimple tab)
        {
            decimal volume = 0;

            if (volumeType == PairTraderVolumeType.Currency)
            {
                decimal lastPrice = tab.PriceBestBid;

                if (lastPrice == 0)
                {
                    return 0;
                }

                volume = volumeValue / lastPrice;

                Security mySec = tab.Security;

                if (mySec.Lot > 1)
                {
                    volume = volume / mySec.Lot;
                }
            }
            else if (volumeType == PairTraderVolumeType.Contract)
            {
                return volumeValue;
            }

            // If the robot is running in the tester
            if (tab.StartProgram == StartProgram.IsTester)
            {
                volume = Math.Round(volume, 6);
            }
            else
            {
                volume = Math.Round(volume, tab.Security.DecimalsVolume);
            }
            return volume;
        }

        /// <summary>
        /// Calculate the price on security
        /// </summary>
        private decimal GetPrice(PairTraderSlippageType slippageType, decimal slippageValue, Side side, BotTabSimple tab)
        {
            decimal price = 0;

            if (side == Side.Buy)
            {
                price = tab.PriceBestBid;
            }
            else if (side == Side.Sell)
            {
                price = tab.PriceBestAsk;
            }

            if (slippageValue == 0)
            {
                return price;
            }

            decimal slippage = 0;

            if (slippageType == PairTraderSlippageType.Absolute)
            {
                slippage = slippageValue;
            }
            else if (slippageType == PairTraderSlippageType.Percent)
            {
                slippage = price * (slippageValue / 100);
            }

            if (side == Side.Buy)
            {
                price = price + slippage;
            }
            else if (side == Side.Sell)
            {
                price = price - slippage;
            }

            return price;
        }

        /// <summary>
        /// The location of the deviation last time a position was opened on the pair
        /// </summary>
        public CointegrationLineSide LastEntryCointegrationSide
        {
            get
            {
                return _lastEntryCointegrationSide;
            }
            set
            {
                if (_lastEntryCointegrationSide == value)
                {
                    return;
                }

                _lastEntryCointegrationSide = value;

                if (Tab1.StartProgram == StartProgram.IsOsTrader)
                {
                    Save();
                }
            }

        }
        private CointegrationLineSide _lastEntryCointegrationSide;


        #endregion

        #region Delayed position opening on the second leg

        /// <summary>
        /// A position opened on source 1
        /// </summary>
        private void Tab1_PositionOpeningSuccesEvent(Position pos)
        {
            if (Tab1.StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (Sec2TradeRegime != PairTraderSecurityTradeRegime.Second)
            {
                return;
            }

            List<Position> posOnOppositTab = Tab2.PositionsOpenAll;

            if (posOnOppositTab.Count > 0)
            {
                return;
            }

            // открываемся по рынку по Инструменту 2

            Side side = Side.Buy;

            if (pos.Direction == Side.Buy)
            {
                side = Side.Sell;
            }

            decimal volume = GetVolume(Sec2VolumeType, Sec2Volume, Tab2);

            if (side == Side.Buy)
            {
                Tab2.BuyAtMarket(volume);
            }
            else// if(side == Side.Sell)
            {
                Tab2.SellAtMarket(volume);
            }
        }

        /// <summary>
        /// A position opened on source 2
        /// </summary>
        private void Tab2_PositionOpeningSuccesEvent(Position pos)
        {
            if (Tab1.StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (Sec1TradeRegime != PairTraderSecurityTradeRegime.Second)
            {
                return;
            }

            List<Position> posOnOppositTab = Tab1.PositionsOpenAll;

            if (posOnOppositTab.Count > 0)
            {
                return;
            }

            // открываемся по рынку по Инструменту 1

            Side side = Side.Buy;

            if (pos.Direction == Side.Buy)
            {
                side = Side.Sell;
            }

            decimal volume = GetVolume(Sec1VolumeType, Sec1Volume, Tab1);

            if (side == Side.Buy)
            {
                Tab1.BuyAtMarket(volume);
            }
            else// if(side == Side.Sell)
            {
                Tab1.SellAtMarket(volume);
            }
        }

        #endregion

        #region Correlation calculation and storage

        /// <summary>
        /// Correlation calculation enabled
        /// </summary>
        public bool AutoRebuildCorrelation = true;

        /// <summary>
        /// Object responsible for the calculation of the correlation
        /// </summary>
        CorrelationBuilder correlationBuilder = new CorrelationBuilder();

        /// <summary>
        /// For how many candles the correlation is calculated
        /// </summary>
        public int CorrelationLookBack = 50;

        /// <summary>
        /// Array with correlation values
        /// </summary>
        public List<PairIndicatorValue> CorrelationList = new List<PairIndicatorValue>();

        /// <summary>
        /// The last correlation value
        /// </summary>
        public decimal CorrelationLast
        {
            get
            {
                if (CorrelationList == null
                    || CorrelationList.Count == 0)
                {
                    return 0;
                }
                return CorrelationList[CorrelationList.Count - 1].Value;
            }
        }

        /// <summary>
        /// Recalculate the correlation array
        /// </summary>
        public void ReloadCorrelationHard()
        {
            try
            {
                List<Candle> candles1 = Tab1.CandlesFinishedOnly;
                List<Candle> candles2 = Tab2.CandlesFinishedOnly;

                if (candles1 == null ||
                    candles2 == null)
                {
                    return;
                }

                ReloadCorrelation(candles1, candles2);

            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// Recalculate the correlation array
        /// </summary>
        private void ReloadCorrelation(List<Candle> candles1, List<Candle> candles2)
        {

            if (candles1.Count < CorrelationLookBack
                || candles2.Count < CorrelationLookBack)
            {
                return;
            }

            CorrelationList = correlationBuilder.ReloadCorrelation(candles1, candles2, CorrelationLookBack);

            if (CorrelationChangeEvent != null)
            {
                CorrelationChangeEvent(CorrelationList, this);
            }
        }

        /// <summary>
        /// Pair has updated correlation value. 
        /// List<PairIndicatorValue> - An array with correlation values. The actual value is the last. 
        /// PairToTrade - Pair of instruments for which the correlation was updated
        /// </summary>
        public event Action<List<PairIndicatorValue>, PairToTrade> CorrelationChangeEvent;

        #endregion

        #region Cointegration calculation and storage

        /// <summary>
        /// Cointegration calculation enabled
        /// </summary>
        public bool AutoRebuildCointegration = true;

        /// <summary>
        /// Object responsible for the calculation of the cointegration
        /// </summary>
        CointegrationBuilder _cointegrationBuilder = new CointegrationBuilder();

        /// <summary>
        /// Length of cointegration calculation
        /// </summary>
        public int CointegrationLookBack = 50;

        /// <summary>
        /// Deviation for calculating lines on cointegration
        /// </summary>
        public decimal CointegrationDeviation = 1;

        /// <summary>
        /// An array with cointegration values. The actual value is the last
        /// </summary>
        public List<PairIndicatorValue> Cointegration = new List<PairIndicatorValue>();

        /// <summary>
        /// The last cointegration value
        /// </summary>
        public decimal CointegrationLast
        {
            get
            {
                if (Cointegration == null
                    || Cointegration.Count == 0)
                {
                    return 0;
                }
                return Cointegration[Cointegration.Count - 1].Value;
            }
        }

        /// <summary>
        /// Multiplier for multiplication of the second instrument to obtain minimal deviations on the Cointegration graph
        /// </summary>
        public decimal CointegrationMult;

        /// <summary>
        /// Standard deviation on the cointegration deviation graph
        /// </summary>
        public decimal CointegrationStandartDeviation;

        /// <summary>
        /// The side in which the current value of the deviation between the instruments is located, 
        /// relative to the lines on the cointegration graph. 
        /// No - on the middle. Up - above the top line. Down - below the bottom line. 
        /// </summary>
        public CointegrationLineSide SideCointegrationValue;

        /// <summary>
        /// Value of the upper line on the deviation graph
        /// </summary>
        public decimal LineUpCointegration;

        /// <summary>
        /// The value of the bottom line on the deviation graph 
        /// </summary>
        public decimal LineDownCointegration;

        /// <summary>
        /// Recalculate the cointegration array
        /// </summary>
        public void ReloadCointegrationHard()
        {
            List<Candle> candles1 = Tab1.CandlesFinishedOnly;
            List<Candle> candles2 = Tab2.CandlesFinishedOnly;

            if (candles1 == null ||
                candles2 == null)
            {
                return;
            }

            ReloadCointegration(candles1, candles2);
        }

        /// <summary>
        /// Recalculate the cointegration array
        /// </summary>
        private void ReloadCointegration(List<Candle> candles1, List<Candle> candles2)
        {
            Cointegration.Clear();
            LineUpCointegration = 0;
            LineDownCointegration = 0;
            CointegrationStandartDeviation = 0;

            _cointegrationBuilder.CointegrationLookBack = CointegrationLookBack;
            _cointegrationBuilder.CointegrationDeviation = CointegrationDeviation;

            if (Tab1.StartProgram == StartProgram.IsOsTrader)
            {
                _cointegrationBuilder.ReloadCointegration(candles1, candles2, true);
            }
            else
            {
                // обрезание значения до красивых отнимает много ЦП. В реале можно. В тестере нельзя
                _cointegrationBuilder.ReloadCointegration(candles1, candles2, false);
            }


            CointegrationMult = _cointegrationBuilder.CointegrationMult;
            Cointegration = _cointegrationBuilder.Cointegration;
            CointegrationStandartDeviation = _cointegrationBuilder.CointegrationStandartDeviation;

            LineUpCointegration = CointegrationStandartDeviation * CointegrationDeviation;
            LineDownCointegration = -(CointegrationStandartDeviation * CointegrationDeviation);

            CointegrationLineSide lastSide = SideCointegrationValue;

            if (CointegrationLast > LineUpCointegration)
            {
                SideCointegrationValue = CointegrationLineSide.Up;
            }
            else if (CointegrationLast < LineDownCointegration)
            {
                SideCointegrationValue = CointegrationLineSide.Down;
            }
            else
            {
                SideCointegrationValue = CointegrationLineSide.No;
            }

            if (CointegrationChangeEvent != null)
            {
                CointegrationChangeEvent(Cointegration, this);
            }

            if (lastSide != SideCointegrationValue)
            {
                if (CointegrationPositionSideChangeEvent != null)
                {
                    CointegrationPositionSideChangeEvent(SideCointegrationValue, this);
                }
            }
        }

        /// <summary>
        /// Pair has updated cointegration value. 
        /// List<PairIndicatorValue> - An array with cointegration values. The actual value is the last. 
        /// PairToTrade - Pair of instruments for which the cointegration was updated
        /// </summary>
        public event Action<List<PairIndicatorValue>, PairToTrade> CointegrationChangeEvent;

        /// <summary>
        /// Pair has updated cointegration side value.  
        /// CointegrationLineSide - The side in which the current value of the deviation between the instruments is located, 
        /// relative to the lines on the cointegration graph. 
        /// No - on the middle. Up - above the top line. Down - below the bottom line. 
        /// PairToTrade - Pair of instruments for which the cointegration side was updated
        /// </summary>
        public event Action<CointegrationLineSide, PairToTrade> CointegrationPositionSideChangeEvent;

        /// <summary>
        /// Candlesticks on the instruments in the pair have completed and have the same times
        /// 1. List<Candle> - candles of first security
        /// 2. BotTabSimple - source of first security
        /// 3. List<Candle> - candles of second security
        /// 4. BotTabSimple - source of second security
        /// 5. PairToTrade - the pair on which the event occurred
        /// </summary>
        public event Action<List<Candle>, BotTabSimple, List<Candle>, BotTabSimple, PairToTrade> CandlesInPairSyncFinishedEvent;

        #endregion

        #region Fast start indicators in real

        private bool _isStarted;

        private DateTime _timeToRebuildIndicators;

        private void Tab2_CandleUpdateEvent(List<Candle> candles)
        {
            if (Tab2.StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (_isStarted == true)
            {
                return;
            }

            List<Candle> candles1 = Tab1.CandlesAll;
            List<Candle> candles2 = Tab2.CandlesAll;

            if (candles1 == null
                || candles1.Count == 0
                || candles2 == null
                || candles2.Count == 0)
            {
                return;
            }

            if (candles1[^1].TimeStart == candles2[^1].TimeStart)
            {
                if (_timeToRebuildIndicators == DateTime.MinValue)
                {
                    _timeToRebuildIndicators = DateTime.Now.AddSeconds(10);
                    return;
                }

                if (_timeToRebuildIndicators > DateTime.Now)
                {
                    return;
                }

                _candles1 = Tab1.CandlesFinishedOnly;
                _candles2 = Tab2.CandlesFinishedOnly;
                _isStarted = true;
                TryReloadIndicators(false);
            }
        }

        private void Tab1_CandleUpdateEvent(List<Candle> candles)
        {
            if (Tab1.StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (_isStarted == true)
            {
                return;
            }

            List<Candle> candles1 = Tab1.CandlesAll;
            List<Candle> candles2 = Tab2.CandlesAll;

            if (candles1 == null
                || candles1.Count == 0
                || candles2 == null
                || candles2.Count == 0)
            {
                return;
            }

            if (candles1[^1].TimeStart == candles2[^1].TimeStart)
            {
                if (_timeToRebuildIndicators == DateTime.MinValue)
                {
                    _timeToRebuildIndicators = DateTime.Now.AddSeconds(10);
                    return;
                }

                if (_timeToRebuildIndicators > DateTime.Now)
                {
                    return;
                }

                _candles1 = Tab1.CandlesFinishedOnly;
                _candles2 = Tab2.CandlesFinishedOnly;

                _isStarted = true;
                TryReloadIndicators(false);
            }
        }

        #endregion

        #region Event processing and indicator recalculation call

        /// <summary>
        /// First Source Candles
        /// </summary>
        private List<Candle> _candles1;

        /// <summary>
        /// A candle ended at source 1. Event handler
        /// </summary>
        private void Tab1_CandleFinishedEvent(List<Candle> candles1)
        {
            _candles1 = candles1;
            TryReloadIndicators(true);
        }

        /// <summary>
        /// Second Source Candles
        /// </summary>
        private List<Candle> _candles2;

        /// <summary>
        /// A candle ended at source 2. Event handler
        /// </summary>
        private void Tab2_CandleFinishedEvent(List<Candle> candles2)
        {
            _candles2 = candles2;
            TryReloadIndicators(true);
        }

        /// <summary>
        /// The reconnection to the exchange was started in source 1. Event handler
        /// </summary>
        private void Connector_ConnectorStartedReconnectEvent1(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, string arg5)
        {
            ClearIndicators();
            _isStarted = false;
            _timeToRebuildIndicators = DateTime.MinValue;
            _timeLastReloadIndicators = DateTime.MinValue;
        }

        /// <summary>
        /// The reconnection to the exchange was started in source 2. Event handler
        /// </summary>
        private void Connector_ConnectorStartedReconnectEvent(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, string arg5)
        {
            ClearIndicators();
            _isStarted = false;
            _timeToRebuildIndicators = DateTime.MinValue;
            _timeLastReloadIndicators = DateTime.MinValue;
        }

        /// <summary>
        /// Time of last recalculation of indicators
        /// </summary>
        private DateTime _timeLastReloadIndicators;

        /// <summary>
        /// Recalculate indicators
        /// </summary>
        private void TryReloadIndicators(bool canSendEvent)
        {
            if (_candles1 == null ||
                _candles2 == null)
            {
                return;
            }

            if (_candles1.Count == 0 ||
                _candles2.Count == 0)
            {
                return;
            }

            if (_candles1[_candles1.Count - 1].TimeStart !=
                _candles2[_candles2.Count - 1].TimeStart)
            {
                return;
            }

            if (_candles1[_candles1.Count - 1].TimeStart == _timeLastReloadIndicators)
            {
                return;
            }

            _timeLastReloadIndicators = _candles1[_candles1.Count - 1].TimeStart;

            if (Tab1.TimeFrame != Tab2.TimeFrame)
            {
                SendNewLogMessage(OsLocalization.Trader.Label250 + Name, LogMessageType.Error);
                return;
            }

            if (AutoRebuildCorrelation)
            {
                ReloadCorrelation(_candles1, _candles2);
            }

            if (AutoRebuildCointegration)
            {
                ReloadCointegration(_candles1, _candles2);
            }

            if (canSendEvent == true)
            {
                if (CandlesInPairSyncFinishedEvent != null)
                {
                    CandlesInPairSyncFinishedEvent(_candles1, Tab1, _candles2, Tab2, this);
                }
            }
        }

        /// <summary>
        /// Clear indicators
        /// </summary>
        private void ClearIndicators()
        {
            CorrelationList.Clear();

            Cointegration.Clear();

            CointegrationMult = 0;

            CointegrationStandartDeviation = 0;

            SideCointegrationValue = 0;

            LineUpCointegration = 0;

            LineDownCointegration = 0; ;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Send new log message
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// Type of volume calculation for the pair
    /// </summary>
    public enum PairTraderVolumeType
    {
        /// <summary>
        /// Currency of the contract in which the security is traded
        /// </summary>
        Currency,

        /// <summary>
        /// The contract itself in which the security is traded
        /// </summary>
        Contract
    }

    /// <summary>
    /// Type of slippage calculation for the pair
    /// </summary>
    public enum PairTraderSlippageType
    {
        /// <summary>
        /// The number of percent of the current price
        /// </summary>
        Percent,

        /// <summary>
        /// Absolute value specified by the user
        /// </summary>
        Absolute
    }

    /// <summary>
    /// Sort type of an array of pairs
    /// </summary>
    public enum MainGridPairSortType
    {
        /// <summary>
        /// Pair array is not sorted
        /// </summary>
        No,

        /// <summary>
        /// The array of pairs is sorted by the location of the last deviation value relative to the lines on the cointegration graph
        /// </summary>
        Side,

        /// <summary>
        /// The array of pairs is sorted by the value of the correlation. The greater the correlation, the higher the pair in the array
        /// </summary>
        Correlation
    }

    /// <summary>
    /// Trading mode for the security in the pair
    /// </summary>
    public enum PairTraderSecurityTradeRegime
    {
        /// <summary>
        /// Off. No operations will be performed on this leg
        /// </summary>
        Off,

        /// <summary>
        /// Market orders. Operations on this leg will be performed using market orders
        /// </summary>
        Market,

        /// <summary>
        /// Limit orders. Operations on this leg will be performed using limit orders
        /// </summary>
        Limit,

        /// <summary>
        /// Opening with a delay. For this leg position opening operations will be performed only when the position will be opened for the other leg. 
        /// A market order will be used
        /// </summary>
        Second
    }
}