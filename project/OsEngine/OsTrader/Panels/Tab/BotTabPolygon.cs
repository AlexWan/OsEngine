/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Threading;
using OsEngine.Market;
using OsEngine.Market.Servers;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabPolygon : IIBotTab
    {

        #region Service. Constructor. Override for the interface

        public BotTabPolygon(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;
            LoadStandartSettings();
            LoadPairs();

            Thread worker = new Thread(WorkerPlace);
            worker.Start();
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
                return BotTabType.Polygon;
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

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        private bool _emulatorIsOn = false;

        public event Action TabDeletedEvent;

        private bool _isDeleted = false;

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
                    if (File.Exists(@"Engine\" + TabName + @"StandartPolygonSettings.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"StandartPolygonSettings.txt");
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
                    if (File.Exists(@"Engine\" + TabName + @"PolygonsNamesToLoad.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"PolygonsNamesToLoad.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].Delete();
                }

                if (Pairs != null)
                {
                    Pairs.Clear();
                    Pairs = null;
                }

                /* if (_grid != null)
                 {
                     _grid.Rows.Clear();
                     _grid.CellClick -= _grid_CellClick;
                     DataGridFactory.ClearLinks(_grid);
                 }
                 if (_host != null)
                 {
                     _host.Child = null;
                     _host = null;
                 }*/

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

        #endregion

        #region Common settings

        public void ApplyStandartSettingsToAllSequence()
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                SetStandartSettingsInSequence(Pairs[i]);
            }
        }

        private void SetStandartSettingsInSequence(PolygonToTrade pair)
        {
            pair.SeparatorToSecurities = SeparatorToSecurities;
            pair.ComissionType = ComissionType;
            pair.ComissionValue = ComissionValue;
            pair.CommisionIsSubstract = CommisionIsSubstract;
            pair.DelayType = DelayType;
            pair.DelayMls = DelayMls;
            pair.QtyStart = QtyStart;
            pair.SlippagePercent = SlippagePercent;
            pair.OrderPriceType = OrderPriceType;
            pair.ActionOnSignalType = ActionOnSignalType;
            pair.ProfitToSignal = ProfitToSignal;
            pair.Save();
        }

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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"StandartPolygonSettings.txt", false))
                {
                    writer.WriteLine(_eventsIsOn);
                    writer.WriteLine(_emulatorIsOn);

                    writer.WriteLine(SeparatorToSecurities);
                    writer.WriteLine(ComissionType);
                    writer.WriteLine(ComissionValue);
                    writer.WriteLine(CommisionIsSubstract);
                    writer.WriteLine(DelayType);
                    writer.WriteLine(DelayMls);
                    writer.WriteLine(QtyStart);
                    writer.WriteLine(SlippagePercent);
                    writer.WriteLine(ProfitToSignal);
                    writer.WriteLine(ActionOnSignalType);
                    writer.WriteLine(OrderPriceType);

                    writer.WriteLine(AutoCreatorSequenceBaseCurrency);
                    writer.WriteLine(AutoCreatorSequenceSeparator);

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
            if (!File.Exists(@"Engine\" + TabName + @"StandartPolygonSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"StandartPolygonSettings.txt"))
                {
                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());

                    SeparatorToSecurities = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out ComissionType);
                    ComissionValue = reader.ReadLine().ToDecimal();
                    CommisionIsSubstract = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out DelayType);
                    DelayMls = Convert.ToInt32(reader.ReadLine());
                    QtyStart = reader.ReadLine().ToDecimal();
                    SlippagePercent = reader.ReadLine().ToDecimal();
                    ProfitToSignal = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out ActionOnSignalType);
                    Enum.TryParse(reader.ReadLine(), out OrderPriceType);

                    AutoCreatorSequenceBaseCurrency = reader.ReadLine();
                    AutoCreatorSequenceSeparator = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public decimal ProfitToSignal;

        public PolygonActionOnSignalType ActionOnSignalType;

        public string SeparatorToSecurities;

        public ComissionPolygonType ComissionType;

        public decimal ComissionValue;

        public bool CommisionIsSubstract;

        public DelayPolygonType DelayType;

        public int DelayMls;

        public decimal QtyStart;

        public decimal SlippagePercent;

        public OrderPriceType OrderPriceType;

        public string AutoCreatorSequenceBaseCurrency;

        public string AutoCreatorSequenceSeparator;

        #endregion

        #region Storage, creation and deletion of polygon

        /// <summary>
        /// Pair array 
        /// </summary>
        public List<PolygonToTrade> Pairs = new List<PolygonToTrade>();

        private string _pairsLocker = "pairsLocker";

        private void TrySortPairs()
        {
            lock (_pairsLocker)
            {
                for (int j = 0; j < Pairs.Count; j++)
                {
                    for (int i = 1; i < Pairs.Count; i++)
                    {
                        decimal lastProfit = Pairs[i - 1].ProfitToDealPercent;
                        decimal curProfit = Pairs[i].ProfitToDealPercent;

                        if (curProfit != 0 && lastProfit < curProfit)
                        {
                            PolygonToTrade polygonToTrade = Pairs[i];
                            Pairs[i] = Pairs[i - 1];
                            Pairs[i - 1] = polygonToTrade;
                        }
                    }
                }
            }
        }

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

                PolygonToTrade pair = new PolygonToTrade(TabName + number, StartProgram);
                pair.PairNum = number;

                pair.EmulatorIsOn = _emulatorIsOn;
                pair.EventsIsOn = _eventsIsOn;
                pair.Save();

                SetStandartSettingsInSequence(pair);

                Pairs.Add(pair);

                SavePairNames();

                pair.LogMessageEvent += SendNewLogMessage;


                if (PairToTradeCreateEvent != null)
                {
                    PairToTradeCreateEvent(pair);
                }
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
        public void DeletePair(int numberInArray)
        {
            try
            {
                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].PairNum == numberInArray)
                    {
                        Pairs[i].LogMessageEvent -= SendNewLogMessage;
                        Pairs[i].Delete();
                        Pairs.RemoveAt(i);
                        SavePairNames();
                        RePaintGrid();
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
        /// Save pairs
        /// </summary>
        public void SavePairNames()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"PolygonsNamesToLoad.txt", false))
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
            if (!File.Exists(@"Engine\" + TabName + @"PolygonsNamesToLoad.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"PolygonsNamesToLoad.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string pairName = reader.ReadLine();
                        PolygonToTrade newPair = new PolygonToTrade(pairName, StartProgram);
                        newPair.LogMessageEvent += SendNewLogMessage;
                        Pairs.Add(newPair);
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public List<string> SecuritiesActivated
        {
            get
            {
                List<string> list = new List<string>();

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (string.IsNullOrEmpty(Pairs[i].Tab1.Connector.SecurityName) == false)
                    {
                        list.Add(Pairs[i].Tab1.Connector.SecurityName);
                    }
                }

                return list;
            }
        }

        public void CreateSequence(string sec1, string sec2, string sec3, string baseCurrency, string portfolio, ServerType server)
        {
            lock (_pairsLocker)
            {
                if (ThisSequenceIsCreated(sec1, sec2, sec3))
                {
                    return;
                }

                CreatePair();

                PolygonToTrade mySequence = Pairs[Pairs.Count - 1];
                mySequence.BaseCurrency = baseCurrency;

                if (mySequence.QtyStart == 0)
                {
                    mySequence.QtyStart = 10;
                }

                mySequence.Tab1TradeSide = Side.Buy;
                mySequence.Tab2TradeSide = Side.Sell;
                mySequence.Tab3TradeSide = Side.Sell;

                StartThisTab(mySequence.Tab1, server, portfolio, sec1);
                StartThisTab(mySequence.Tab2, server, portfolio, sec2);
                StartThisTab(mySequence.Tab3, server, portfolio, sec3);

                mySequence.Save();
            }
        }

        private void StartThisTab(BotTabSimple tab, ServerType server, string portfolioName, string securityName)
        {
            // 1 берём сервер
            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null)
            {
                return;
            }

            IServer myServer = null;

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType == server)
                {
                    myServer = servers[i];
                    break;
                }
            }

            if (myServer == null)
            {
                return;
            }

            // 2 берём портфель

            Portfolio myPortfolio = null;

            List<Portfolio> portfolios = myServer.Portfolios;

            if (portfolios == null)
            {
                return;
            }

            for (int i = 0; i < portfolios.Count; i++)
            {
                if (portfolios[i].Number == portfolioName)
                {
                    myPortfolio = portfolios[i];
                    break;
                }
            }

            if (myPortfolio == null)
            {
                return;
            }

            // 3 берём бумагу

            Security mySecurity = null;

            List<Security> securities = myServer.Securities;

            if (securities == null)
            {
                return;
            }

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name.ToLower() == securityName)
                {
                    mySecurity = securities[i];
                    break;
                }
            }

            if (mySecurity == null)
            {
                return;
            }

            tab.Connector.SecurityName = mySecurity.Name;
            tab.Connector.SecurityClass = mySecurity.NameClass;
            tab.TimeFrameBuilder.TimeFrame = TimeFrame.Hour1;
            tab.Connector.PortfolioName = myPortfolio.Number;
            tab.Connector.ServerType = server;
            tab.Connector.EmulatorIsOn = _emulatorIsOn;
            tab.Connector.CandleMarketDataType = CandleMarketDataType.Tick;
            tab.Connector.CandleCreateMethodType = CandleCreateMethodType.Simple;
            tab.Connector.SaveTradesInCandles = false;
            tab.Connector.Save();
        }

        private bool ThisSequenceIsCreated(string sec1, string sec2, string sec3)
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                if (string.IsNullOrEmpty(Pairs[i].Tab1.Connector.SecurityName))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(Pairs[i].Tab2.Connector.SecurityName))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(Pairs[i].Tab3.Connector.SecurityName))
                {
                    continue;
                }

                if (Pairs[i].Tab1.Connector.SecurityName.ToLower() == sec1
                    && Pairs[i].Tab2.Connector.SecurityName.ToLower() == sec2
                    && Pairs[i].Tab3.Connector.SecurityName.ToLower() == sec3)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Outgoing events

        /// <summary>
        /// The source has a new pair for trading
        /// </summary>
        public event Action<PolygonToTrade> PairToTradeCreateEvent;

        public event Action<decimal, PolygonToTrade> ProfitBySequenceChangeEvent;

        public event Action<decimal, PolygonToTrade> ProfitGreaterThanSignalValue;

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

                    if (_host == null)
                    {
                        continue;
                    }

                    TrySortPairs();
                    TryRePaintGrid();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// The area where the table of pairs is drawn
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// Start drawing the table of pairs
        /// </summary>
        public void StartPaint(WindowsFormsHost host)
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
                _host = null;
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

            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                TryRePaintRow(_grid.Rows[i], rows[i]);
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
                if (rowInGrid.Cells[0].Value != null &&
                   rowInGrid.Cells[0].Value.ToString() != rowInArray.Cells[0].Value.ToString())
                {
                    rowInGrid.Cells[0].Value = rowInArray.Cells[0].Value.ToString();
                }

                if (rowInGrid.Cells[1].Value != null &&
                                   rowInGrid.Cells[1].Value.ToString() != rowInArray.Cells[1].Value.ToString())
                {
                    rowInGrid.Cells[1].Value = rowInArray.Cells[1].Value.ToString();
                }

                if (rowInGrid.Cells[1].Value != null &&
                   rowInGrid.Cells[1].Style.BackColor != rowInArray.Cells[1].Style.BackColor)
                {
                    rowInGrid.Cells[1].Style.BackColor = rowInArray.Cells[1].Style.BackColor;
                }

                if (rowInGrid.Cells[2].Value != null && rowInArray.Cells[2].Value != null &&
                    rowInGrid.Cells[2].Value.ToString() != rowInArray.Cells[2].Value.ToString())
                {
                    rowInGrid.Cells[2].Value = rowInArray.Cells[2].Value.ToString();
                }

                if (rowInGrid.Cells[2].Value == null &&
                    rowInArray.Cells[2].Value != null)
                {
                    rowInGrid.Cells[2].Value = rowInArray.Cells[2].Value.ToString();
                }

                if (rowInGrid.Cells[3].Value != null && rowInArray.Cells[3].Value != null &&
                    rowInGrid.Cells[3].Value.ToString() != rowInArray.Cells[3].Value.ToString())
                {
                    rowInGrid.Cells[3].Value = rowInArray.Cells[3].Value.ToString();
                }

                if (rowInGrid.Cells[3].Value == null &&
                    rowInArray.Cells[3].Value != null)
                {
                    rowInGrid.Cells[3].Value = rowInArray.Cells[3].Value.ToString();
                }

                if (rowInGrid.Cells[4].Value != null && rowInArray.Cells[4].Value != null &&
                    rowInGrid.Cells[4].Value.ToString() != rowInArray.Cells[4].Value.ToString())
                {
                    rowInGrid.Cells[4].Value = rowInArray.Cells[4].Value.ToString();
                }

                if (rowInGrid.Cells[4].Value == null &&
                    rowInArray.Cells[4].Value != null)
                {
                    rowInGrid.Cells[4].Value = rowInArray.Cells[4].Value.ToString();
                }

                if (rowInGrid.Cells[5].Value != null &&
                    rowInGrid.Cells[5].Value.ToString() != rowInArray.Cells[5].Value.ToString())
                {
                    rowInGrid.Cells[5].Value = rowInArray.Cells[5].Value.ToString();
                }
            }
            catch (Exception error)
            {

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
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button1 = new DataGridViewButtonCell(); // авто создание пар
            button1.Value = OsLocalization.Trader.Label310;
            nRow.Cells.Add(button1);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell(); // Общие настройки
            button2.Value = OsLocalization.Trader.Label232;
            nRow.Cells.Add(button2);



            return nRow;
        }

        private DataGridViewRow GetPairRowOne(PolygonToTrade pair)
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

        private DataGridViewRow GetPairRowTwo(PolygonToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = pair.PairNum.ToString();

            decimal curProfit = pair.ProfitToDealPercent;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = "Profit %: " + curProfit;

            if (curProfit > 0)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkGreen;
            }
            if (curProfit < 0)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkRed;
            }

            DataGridViewButtonCell buttonGetDeal = new DataGridViewButtonCell(); // Кнопка совершить сделку
            buttonGetDeal.Value = OsLocalization.Trader.Label311;
            nRow.Cells.Add(buttonGetDeal);

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // Чарт
            button.Value = OsLocalization.Trader.Label172;
            nRow.Cells.Add(button);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell(); // Удалить
            button2.Value = OsLocalization.Trader.Label39;
            nRow.Cells.Add(button2);

            return nRow;
        }

        private DataGridViewRow GetPairRowThree(PolygonToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = "base: " + pair.BaseCurrency;

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (pair.Tab1.Securiti != null)
            {
                nRow.Cells[2].Value = pair.Tab1.Securiti.Name + " " + pair.Tab1TradeSide;
            }


            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (pair.Tab2.Securiti != null)
            {
                nRow.Cells[3].Value = pair.Tab2.Securiti.Name + " " + pair.Tab2TradeSide;
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (pair.Tab3.Securiti != null)
            {
                nRow.Cells[4].Value = pair.Tab3.Securiti.Name + " " + pair.Tab3TradeSide;
            }


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
                else if (column == 2)
                {
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

                    PolygonToTrade pair = null;

                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        if (Pairs[i].PairNum == tabNum)
                        {
                            pair = Pairs[i];
                            break;
                        }
                    }

                    if (pair == null)
                    {
                        return;
                    }

                    Thread worker = new Thread(pair.TradeLogic);
                    worker.Start();
                }

                else if (column == 5 && row == 0)
                { // кнопка открытия общих настроек

                    if (_commonSettingsUi != null)
                    {
                        _commonSettingsUi.Activate();
                        return;
                    }

                    _commonSettingsUi = new BotTabPolygonCommonSettingsUi(this);
                    _commonSettingsUi.Show();
                    _commonSettingsUi.Closed += _commonSettingsUi_Closed;
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

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label312);

                    ui.ShowDialog();

                    if (ui.UserAcceptActioin)
                    {
                        DeletePair(tabNum);
                    }
                }

                else if (column == 4 && row == 0)
                { // кнопка открытия окна авто генерации пар
                    if (_autoSelectPairsUi != null)
                    {
                        _autoSelectPairsUi.Activate();
                        return;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {// if connection server to exhange hasn't been created yet
                        return;
                    }

                    _autoSelectPairsUi = new BotTabPolygonAutoSelectSequenceUi(this);
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

                    PolygonToTrade pair = null;

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
                        if (_uiList[i].Name == pair.Name)
                        {
                            _uiList[i].Activate();
                            return;
                        }
                    }

                    BotTabPolygonUi ui = new BotTabPolygonUi(pair);
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
        private List<BotTabPolygonUi> _uiList = new List<BotTabPolygonUi>();

        /// <summary>
        /// Event handler for closing a separate pair window
        /// </summary>
        private void Ui_Closed(object sender, EventArgs e)
        {
            try
            {

                string name = ((BotTabPolygonUi)sender).Name;

                for (int i = 0; i < _uiList.Count; i++)
                {
                    if (_uiList[i].Name == name)
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
        private BotTabPolygonCommonSettingsUi _commonSettingsUi;

        /// <summary>
        /// Event handler for closing a common settings window
        /// </summary>
        private void _commonSettingsUi_Closed(object sender, EventArgs e)
        {
            _commonSettingsUi.Closed -= _commonSettingsUi_Closed;
            _commonSettingsUi = null;
        }

        BotTabPolygonAutoSelectSequenceUi _autoSelectPairsUi;

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

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region ThreadWorkerPlace

        private void WorkerPlace()
        {
            while (true)
            {
                Thread.Sleep(100);

                if (_isDeleted)
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        Pairs[i].CheckProfitAndSignal();
                    }
                }
                catch (Exception e)
                {
                    SendNewLogMessage(e.ToString(), LogMessageType.Error);
                }
            }
        }


        #endregion
    }

    public class PolygonToTrade
    {
        /// <summary>
        /// Pair for trading constructor
        /// </summary>
        /// <param name="name">unique pair name</param>
        /// <param name="startProgram">The program in which this source is running</param>
        public PolygonToTrade(string name, StartProgram startProgram)
        {
            Name = name;

            Tab1 = new BotTabSimple(name + 1, startProgram);
            Tab2 = new BotTabSimple(name + 2, startProgram);
            Tab3 = new BotTabSimple(name + 3, startProgram);

            Tab1.MarketDepthUpdateEvent += Tab1_MarketDepthUpdateEvent;
            Tab2.MarketDepthUpdateEvent += Tab2_MarketDepthUpdateEvent;
            Tab3.MarketDepthUpdateEvent += Tab3_MarketDepthUpdateEvent;

            Tab1.PositionOpeningSuccesEvent += Tab1_PositionOpeningSuccesEvent;
            Tab2.PositionOpeningSuccesEvent += Tab2_PositionOpeningSuccesEvent;
            Tab3.PositionOpeningSuccesEvent += Tab3_PositionOpeningSuccesEvent;

            Tab1.PositionOpeningFailEvent += Tab1_PositionOpeningFailEvent;
            Tab2.PositionOpeningFailEvent += Tab2_PositionOpeningFailEvent;
            Tab3.PositionOpeningFailEvent += Tab3_PositionOpeningFailEvent;

            Tab1.LogMessageEvent += SendNewLogMessage;
            Tab2.LogMessageEvent += SendNewLogMessage;
            Tab3.LogMessageEvent += SendNewLogMessage;

            Load();
            _logMaster = new Log(Name, startProgram);
            _logMaster.Listen(this);
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
            if (!File.Exists(@"Engine\" + Name + @"PolygonSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"PolygonSettings.txt"))
                {
                    PairNum = Convert.ToInt32(reader.ReadLine());
                    _showTradePanelOnChart = Convert.ToBoolean(reader.ReadLine());

                    BaseCurrency = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out Tab1TradeSide);
                    Enum.TryParse(reader.ReadLine(), out Tab2TradeSide);
                    Enum.TryParse(reader.ReadLine(), out Tab3TradeSide);
                    SeparatorToSecurities = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out ComissionType);
                    ComissionValue = reader.ReadLine().ToDecimal();
                    CommisionIsSubstract = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out DelayType);
                    DelayMls = Convert.ToInt32(reader.ReadLine());
                    QtyStart = reader.ReadLine().ToDecimal();
                    SlippagePercent = reader.ReadLine().ToDecimal();
                    ProfitToSignal = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out ActionOnSignalType);
                    Enum.TryParse(reader.ReadLine(), out OrderPriceType);

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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"PolygonSettings.txt", false))
                {
                    writer.WriteLine(PairNum);
                    writer.WriteLine(_showTradePanelOnChart);

                    writer.WriteLine(BaseCurrency);
                    writer.WriteLine(Tab1TradeSide);
                    writer.WriteLine(Tab2TradeSide);
                    writer.WriteLine(Tab3TradeSide);
                    writer.WriteLine(SeparatorToSecurities);
                    writer.WriteLine(ComissionType);
                    writer.WriteLine(ComissionValue);
                    writer.WriteLine(CommisionIsSubstract);
                    writer.WriteLine(DelayType);
                    writer.WriteLine(DelayMls);
                    writer.WriteLine(QtyStart);
                    writer.WriteLine(SlippagePercent);
                    writer.WriteLine(ProfitToSignal);
                    writer.WriteLine(ActionOnSignalType);
                    writer.WriteLine(OrderPriceType);
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
                if (File.Exists(@"Engine\" + Name + @"PolygonSettings.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"PolygonSettings.txt");
                }
            }
            catch
            {
                // ignore
            }

            Tab1.MarketDepthUpdateEvent -= Tab1_MarketDepthUpdateEvent;
            Tab2.MarketDepthUpdateEvent -= Tab2_MarketDepthUpdateEvent;
            Tab3.MarketDepthUpdateEvent -= Tab3_MarketDepthUpdateEvent;

            Tab1.LogMessageEvent -= SendNewLogMessage;
            Tab2.LogMessageEvent -= SendNewLogMessage;
            Tab3.LogMessageEvent -= SendNewLogMessage;

            Tab1.PositionOpeningSuccesEvent -= Tab1_PositionOpeningSuccesEvent;
            Tab2.PositionOpeningSuccesEvent -= Tab2_PositionOpeningSuccesEvent;
            Tab3.PositionOpeningSuccesEvent -= Tab3_PositionOpeningSuccesEvent;

            Tab1.PositionOpeningFailEvent -= Tab1_PositionOpeningFailEvent;
            Tab2.PositionOpeningFailEvent -= Tab2_PositionOpeningFailEvent;
            Tab3.PositionOpeningFailEvent -= Tab3_PositionOpeningFailEvent;

            Tab1.Delete();
            Tab2.Delete();
            Tab3.Delete();

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
                Tab3.EventsIsOn = value;
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
                Tab3.EmulatorIsOn = value;
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
        /// Trading Security source 3
        /// </summary>
        public BotTabSimple Tab3;

        public string SecuritiesInSequence
        {
            get
            {
                string result = "";

                if (string.IsNullOrEmpty(Tab1.Connector.SecurityName) == false)
                {
                    result += Tab1.Connector.SecurityName + " " + Tab1TradeSide + " ";
                }

                if (string.IsNullOrEmpty(Tab2.Connector.SecurityName) == false)
                {
                    result += Tab2.Connector.SecurityName + " " + Tab2TradeSide + " ";
                }

                if (string.IsNullOrEmpty(Tab3.Connector.SecurityName) == false)
                {
                    result += Tab3.Connector.SecurityName + " " + Tab3TradeSide + " ";
                }

                return result;
            }
        }

        public decimal ProfitToDealAbs
        {
            get
            {
                if (string.IsNullOrEmpty(BaseCurrency))
                {
                    return 0;
                }

                if (EndCurrencyTab3 != BaseCurrency.ToLower())
                {
                    return 0;
                }

                if (Tab1.IsConnected == false
                    || Tab1.IsReadyToTrade == false)
                {
                    return 0;
                }

                if (Tab2.IsConnected == false
                   || Tab2.IsReadyToTrade == false)
                {
                    return 0;
                }

                if (Tab3.IsConnected == false
                   || Tab3.IsReadyToTrade == false)
                {
                    return 0;
                }

                decimal startVol = QtyStart;
                decimal endVol = EndQtyTab3;

                if (startVol == 0
                    || endVol == 0)
                {
                    return 0;
                }

                decimal result = endVol - startVol;

                return result;
            }
        }

        public decimal ProfitToDealPercent
        {
            get
            {
                if (string.IsNullOrEmpty(BaseCurrency))
                {
                    return 0;
                }

                if (EndCurrencyTab3 != BaseCurrency.ToLower())
                {
                    return 0;
                }


                if (Tab1.IsConnected == false
                    || Tab1.IsReadyToTrade == false)
                {
                    return 0;
                }

                if (Tab2.IsConnected == false
                   || Tab2.IsReadyToTrade == false)
                {
                    return 0;
                }


                if (Tab3.IsConnected == false
                   || Tab3.IsReadyToTrade == false)
                {
                    return 0;
                }

                decimal startVol = QtyStart;
                decimal endVol = EndQtyTab3;

                if (startVol == 0
                    || endVol == 0)
                {
                    return 0;
                }

                decimal resultAbs = endVol - startVol;

                decimal result = Math.Round(resultAbs / (startVol / 100), 5);

                return result;
            }
        }

        public decimal ProfitToSignal;

        public PolygonActionOnSignalType ActionOnSignalType;

        public Side Tab1TradeSide;

        public Side Tab2TradeSide;

        public Side Tab3TradeSide;

        public string BaseCurrency;

        public string EndCurrencyTab1
        {
            get
            {
                if (string.IsNullOrEmpty(Tab1.Connector.SecurityName))
                {
                    return "";
                }

                string sec = Tab1.Connector.SecurityName.Replace(".txt", "");

                if (string.IsNullOrEmpty(SeparatorToSecurities) == false)
                {
                    sec = sec.Replace(SeparatorToSecurities, "");
                }

                sec = sec.ToLower();

                sec = sec.Replace(BaseCurrency.ToLower(), "");

                return sec.ToLower();
            }
        }

        public string EndCurrencyTab2
        {
            get
            {
                if (string.IsNullOrEmpty(Tab2.Connector.SecurityName))
                {
                    return "";
                }

                string sec = Tab2.Connector.SecurityName.Replace(".txt", "");

                if (string.IsNullOrEmpty(SeparatorToSecurities) == false)
                {
                    sec = sec.Replace(SeparatorToSecurities, "");
                }

                sec = sec.ToLower();

                sec = sec.Replace(EndCurrencyTab1, "");

                return sec.ToLower();
            }
        }

        public string EndCurrencyTab3
        {
            get
            {
                if (string.IsNullOrEmpty(Tab3.Connector.SecurityName))
                {
                    return "";
                }

                string sec = Tab3.Connector.SecurityName.Replace(".txt", "");

                if (string.IsNullOrEmpty(SeparatorToSecurities) == false)
                {
                    sec = sec.Replace(SeparatorToSecurities, "");
                }

                sec = sec.ToLower();

                sec = sec.Replace(EndCurrencyTab2, "");

                return sec.ToLower();
            }
        }

        public decimal QtyStart;

        public decimal EndQtyTab1
        {
            get
            {
                decimal bestBuy = Tab1.PriceBestBid;
                decimal bestSell = Tab1.PriceBestAsk;

                if (bestBuy == 0 ||
                    bestSell == 0)
                {
                    return 0;
                }

                decimal qtyStart = QtyStart;

                if (qtyStart == 0)
                {
                    return 0;
                }

                decimal result = 0;

                if (Tab1TradeSide == Side.Buy)
                {
                    result = qtyStart / bestSell;
                }

                if (Tab1TradeSide == Side.Sell)
                {
                    result = bestBuy * qtyStart;
                }

                if (ComissionType == ComissionPolygonType.Percent
                    && ComissionValue != 0)
                {
                    result = result - result * (ComissionValue / 100);
                }

                return result;
            }
        }

        public decimal EndQtyTab2
        {
            get
            {
                decimal bestBuy = Tab2.PriceBestBid;
                decimal bestSell = Tab2.PriceBestAsk;

                if (bestBuy == 0 ||
                    bestSell == 0)
                {
                    return 0;
                }

                decimal qtyStart = EndQtyTab1;

                if (qtyStart == 0)
                {
                    return 0;
                }

                decimal result = 0;

                if (Tab2TradeSide == Side.Buy)
                {
                    result = qtyStart / bestSell;
                }

                if (Tab2TradeSide == Side.Sell)
                {
                    result = bestBuy * qtyStart;
                }

                if (ComissionType == ComissionPolygonType.Percent
                    && ComissionValue != 0)
                {
                    result = result - result * (ComissionValue / 100);
                }

                return result;
            }
        }

        public decimal EndQtyTab3
        {
            get
            {
                decimal bestBuy = Tab3.PriceBestBid;
                decimal bestSell = Tab3.PriceBestAsk;

                if (bestBuy == 0 ||
                    bestSell == 0)
                {
                    return 0;
                }

                decimal qtyStart = EndQtyTab2;

                if (qtyStart == 0)
                {
                    return 0;
                }

                decimal result = 0;

                if (Tab3TradeSide == Side.Buy)
                {
                    result = qtyStart / bestSell;
                }

                if (Tab3TradeSide == Side.Sell)
                {
                    result = bestBuy * qtyStart;
                }

                if (ComissionType == ComissionPolygonType.Percent
                    && ComissionValue != 0)
                {
                    result = result - result * (ComissionValue / 100);
                }

                return result;
            }
        }

        public string SeparatorToSecurities;

        public ComissionPolygonType ComissionType;

        public decimal ComissionValue;

        public bool CommisionIsSubstract;

        public DelayPolygonType DelayType;

        public int DelayMls;

        public decimal SlippagePercent;

        public OrderPriceType OrderPriceType;

        public void CheckSequence()
        {
            CheckSecurityInTab(Tab1, BaseCurrency);
            CheckSecurityInTab(Tab2, EndCurrencyTab1);
            CheckSecurityInTab(Tab3, EndCurrencyTab2);
        }

        private void CheckSecurityInTab(BotTabSimple tab, string currency)
        {
            currency = currency.ToLower();

            string secInTab = tab.Connector.SecurityName;

            if (string.IsNullOrEmpty(secInTab) == true)
            {
                return;
            }

            secInTab = secInTab.ToLower();

            if (string.IsNullOrEmpty(currency) == false
                && secInTab.Contains(currency))
            {
                return;
            }

            tab.Connector.SecurityName = "";
        }

        #endregion

        #region Logic

        public void TradeLogic()
        {
            SendNewLogMessage(OsLocalization.Trader.Label366 + " " + SecuritiesInSequence, LogMessageType.System);

            string baseCurrency = BaseCurrency;
            string endCurrency = EndCurrencyTab3;

            if (string.IsNullOrEmpty(baseCurrency))
            {
                SendNewLogMessage(OsLocalization.Trader.Label361, LogMessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(endCurrency))
            {
                SendNewLogMessage(OsLocalization.Trader.Label362, LogMessageType.Error);
                return;
            }

            if (baseCurrency != endCurrency)
            {
                SendNewLogMessage(OsLocalization.Trader.Label363, LogMessageType.Error);
                return;
            }

            CheckCloseSequence();

            if (Tab1.PositionsOpenAll.Count != 0
                || Tab2.PositionsOpenAll.Count != 0
                || Tab3.PositionsOpenAll.Count != 0)
            {
                SendNewLogMessage(OsLocalization.Trader.Label363, LogMessageType.Error);
                return;
            }

            // берём грубые объёмы

            decimal tradeQty1Buy = EndQtyTab1;
            decimal tradeQty2Sell = tradeQty1Buy;
            decimal tradeQty3Sell = EndQtyTab2;

            if (tradeQty1Buy == 0
                || tradeQty2Sell == 0
                || tradeQty3Sell == 0)
            {
                SendNewLogMessage(OsLocalization.Trader.Label364, LogMessageType.Error);
                return;
            }

            // отсекаем комиссию от объёмов, если это включено

            if (CommisionIsSubstract
                && ComissionType == ComissionPolygonType.Percent
                && ComissionValue != 0)
            {
                tradeQty2Sell = tradeQty2Sell - (tradeQty2Sell * (ComissionValue / 100));
                tradeQty3Sell = tradeQty3Sell - (tradeQty3Sell * ((ComissionValue * 2) / 100));
            }

            // обрезаем объёмы по кол-ву знаков

            tradeQty1Buy = Math.Round(tradeQty1Buy, Tab1.Securiti.DecimalsVolume);
            tradeQty2Sell = Math.Round(tradeQty2Sell, Tab2.Securiti.DecimalsVolume);
            tradeQty3Sell = Math.Round(tradeQty3Sell, Tab2.Securiti.DecimalsVolume);

            Tab1PosStatus = PolygonPositionStatus.None;
            Tab2PosStatus = PolygonPositionStatus.None;
            Tab3PosStatus = PolygonPositionStatus.None;

            // выставляем ордера

            SendOrder(Tab1, tradeQty1Buy, Side.Buy, 1);
            SendNewLogMessage("Trade " + Tab1.Connector.SecurityName + " Qty: " + tradeQty1Buy + " Side: " + Side.Buy, LogMessageType.System);

            if (Tab1PosStatus == PolygonPositionStatus.Fail)
            {
                SendNewLogMessage(OsLocalization.Trader.Label367, LogMessageType.Error);
                return;
            }

            SendOrder(Tab2, tradeQty2Sell, Side.Sell, 2);
            SendNewLogMessage("Trade " + Tab2.Connector.SecurityName + " Qty: " + tradeQty2Sell + " Side: " + Side.Sell, LogMessageType.System);

            if (Tab2PosStatus == PolygonPositionStatus.Fail)
            {
                SendNewLogMessage(OsLocalization.Trader.Label367, LogMessageType.Error);
                return;
            }

            SendOrder(Tab3, tradeQty3Sell, Side.Sell, 3);
            SendNewLogMessage("Trade " + Tab3.Connector.SecurityName + " Qty: " + tradeQty3Sell + " Side: " + Side.Sell, LogMessageType.System);

        }

        private void SendOrder(BotTabSimple tab, decimal volume, Side side, int tabNum)
        {
            if (OrderPriceType == OrderPriceType.Limit)
            {
                decimal slippage = 0;

                if (SlippagePercent != 0)
                {
                    slippage = tab.PriceBestAsk * (SlippagePercent / 100);
                }

                if (side == Side.Buy)
                {
                    decimal price = tab.PriceBestAsk + slippage;
                    tab.BuyAtLimit(volume, price);
                }
                else if (side == Side.Sell)
                {
                    decimal price = tab.PriceBestBid - slippage;
                    tab.SellAtLimit(volume, price);
                }
            }
            else if (OrderPriceType == OrderPriceType.Market)
            {
                if (side == Side.Buy)
                {
                    tab.BuyAtMarket(volume);
                }
                else if (side == Side.Sell)
                {
                    tab.SellAtMarket(volume);
                }
            }

            if (DelayType == DelayPolygonType.InMLS
               && DelayMls != 0)
            {
                Thread.Sleep(DelayMls);
            }

            DateTime timeStartWaiting = DateTime.Now;

            if (DelayType == DelayPolygonType.ByExecution)
            {
                while (true)
                {
                    Thread.Sleep(10);

                    if (tabNum == 1)
                    {
                        if (Tab1PosStatus == PolygonPositionStatus.Done ||
                            Tab1PosStatus == PolygonPositionStatus.Fail)
                        {
                            break;
                        }
                    }

                    if (tabNum == 2)
                    {
                        if (Tab2PosStatus == PolygonPositionStatus.Done ||
                            Tab2PosStatus == PolygonPositionStatus.Fail)
                        {
                            break;
                        }
                    }

                    if (tabNum == 3)
                    {
                        if (Tab3PosStatus == PolygonPositionStatus.Done ||
                            Tab3PosStatus == PolygonPositionStatus.Fail)
                        {
                            break;
                        }
                    }

                    if (timeStartWaiting.AddSeconds(10) < DateTime.Now)
                    {
                        if (tabNum == 1)
                        {
                            Tab1PosStatus = PolygonPositionStatus.Fail;
                            Tab1.CloseAllOrderInSystem();
                        }

                        if (tabNum == 2)
                        {
                            Tab2PosStatus = PolygonPositionStatus.Fail;
                            Tab2.CloseAllOrderInSystem();
                        }

                        if (tabNum == 3)
                        {
                            Tab3PosStatus = PolygonPositionStatus.Fail;
                            Tab3.CloseAllOrderInSystem();
                        }

                        SendNewLogMessage(OsLocalization.Trader.Label368, LogMessageType.Error);
                        break;
                    }
                }
            }
        }

        private void CheckCloseSequence()
        {
            // считается что когда все три позиции в статусе OPEN 
            // треугольник завершён штатно

            List<Position> posesTab1 = Tab1.PositionsOpenAll;
            List<Position> posesTab2 = Tab2.PositionsOpenAll;
            List<Position> posesTab3 = Tab3.PositionsOpenAll;

            if (posesTab1.Count == 0
                || posesTab2.Count == 0
                || posesTab3.Count == 0)
            {
                return;
            }

            Position pos1 = null;
            Position pos2 = null;
            Position pos3 = null;

            if (posesTab1.Count > 0)
            {
                pos1 = posesTab1[0];
            }
            if (posesTab2.Count > 0)
            {
                pos2 = posesTab2[0];
            }
            if (posesTab3.Count > 0)
            {
                pos3 = posesTab3[0];
            }

            if (pos1 == null || pos2 == null || pos3 == null)
            {
                return;
            }

            if (pos1.State == PositionStateType.Open &&
                pos2.State == PositionStateType.Open &&
                pos3.State == PositionStateType.Open)
            {
                // очищаем журналы
                Tab1._journal.Clear();
                Tab2._journal.Clear();
                Tab3._journal.Clear();

                SendNewLogMessage("Trade is over success " + SecuritiesInSequence + " journal is clear", LogMessageType.System);
            }
        }

        private PolygonPositionStatus Tab1PosStatus;

        private PolygonPositionStatus Tab2PosStatus;

        private PolygonPositionStatus Tab3PosStatus;

        private void Tab1_PositionOpeningSuccesEvent(Position pos)
        {
            Tab1PosStatus = PolygonPositionStatus.Done;

            SendNewLogMessage("Position 1 executed. Sec: " + pos.SecurityName, LogMessageType.System);

            CheckCloseSequence();
        }

        private void Tab2_PositionOpeningSuccesEvent(Position pos)
        {
            Tab2PosStatus = PolygonPositionStatus.Done;

            SendNewLogMessage("Position 2 executed. Sec: " + pos.SecurityName, LogMessageType.System);

            CheckCloseSequence();
        }

        private void Tab3_PositionOpeningSuccesEvent(Position pos)
        {
            Tab3PosStatus = PolygonPositionStatus.Done;

            SendNewLogMessage("Position 3 executed. Sec: " + pos.SecurityName, LogMessageType.System);

            CheckCloseSequence();
        }

        private void Tab1_PositionOpeningFailEvent(Position pos)
        {
            SendNewLogMessage("Position 1 FAIL. Sec: " + pos.SecurityName, LogMessageType.Error);

            Tab1PosStatus = PolygonPositionStatus.Fail;
        }

        private void Tab2_PositionOpeningFailEvent(Position pos)
        {
            SendNewLogMessage("Position 2 FAIL. Sec: " + pos.SecurityName, LogMessageType.Error);
            Tab2PosStatus = PolygonPositionStatus.Fail;
        }

        private void Tab3_PositionOpeningFailEvent(Position pos)
        {
            SendNewLogMessage("Position 3 FAIL. Sec: " + pos.SecurityName, LogMessageType.Error);

            Tab3PosStatus = PolygonPositionStatus.Fail;
        }

        #endregion

        #region Events

        private void Tab1_MarketDepthUpdateEvent(MarketDepth md)
        {
            _neadToCheckProfit = true;
        }

        private void Tab2_MarketDepthUpdateEvent(MarketDepth md)
        {
            _neadToCheckProfit = true;
        }

        private void Tab3_MarketDepthUpdateEvent(MarketDepth md)
        {
            _neadToCheckProfit = true;
        }

        private bool _neadToCheckProfit = false;

        public void CheckProfitAndSignal()
        {
            if (_neadToCheckProfit == false)
            {
                return;
            }

            _neadToCheckProfit = false;

            decimal profitPercent = ProfitToDealPercent;

            if (profitPercent == 0)
            {
                return;
            }

            if (ProfitBySequenceChangeEvent != null)
            {
                ProfitBySequenceChangeEvent(profitPercent, this);
            }

            if (ActionOnSignalType == PolygonActionOnSignalType.None)
            {
                return;
            }

            if (profitPercent < ProfitToSignal)
            {
                return;
            }

            if (ActionOnSignalType == PolygonActionOnSignalType.Alert
                || ActionOnSignalType == PolygonActionOnSignalType.All)
            {
                if (_timeLastAlerToUser.AddSeconds(30) < DateTime.Now)
                {
                    _timeLastAlerToUser = DateTime.Now;

                    string message = "Signal by Profit in polygon tab! " + Name + "\n";

                    message += "Securities: "
                        + SecuritiesInSequence + "\n";

                    message += "Profit % " + profitPercent;

                    SendNewLogMessage(message, LogMessageType.Error);
                }
            }

            if (ActionOnSignalType == PolygonActionOnSignalType.Bot_Event
                || ActionOnSignalType == PolygonActionOnSignalType.All)
            {
                if (ProfitGreaterThanSignalValue != null)
                {
                    ProfitGreaterThanSignalValue(profitPercent, this);
                }
            }

        }

        private DateTime _timeLastAlerToUser = DateTime.MinValue;

        public event Action<decimal, PolygonToTrade> ProfitBySequenceChangeEvent;

        public event Action<decimal, PolygonToTrade> ProfitGreaterThanSignalValue;

        #endregion

        #region Logging

        Log _logMaster;

        public void StartPaintLog(WindowsFormsHost host)
        {
            _logMaster.StartPaint(host);
        }

        public void StopPaintLog()
        {
            _logMaster.StopPaint();
        }

        /// <summary>
        /// Send new log message
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
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

    public enum PolygonPositionStatus
    {
        None,
        Done,
        Fail
    }

    public enum ComissionPolygonType
    {
        None,
        Percent
    }

    public enum DelayPolygonType
    {
        ByExecution,
        InMLS,
        Instantly
    }

    public enum PolygonActionOnSignalType
    {
        Bot_Event,
        Alert,
        All,
        None,
    }
}