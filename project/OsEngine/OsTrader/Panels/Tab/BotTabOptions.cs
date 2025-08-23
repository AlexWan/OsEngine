using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Robots.Engines;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabOptions : IIBotTab
    {
        #region Fields
        private TableLayoutPanel _mainControl;
        private DataGridView _uaGrid;
        private DataGridView _optionsGrid;
        private ComboBox _expirationComboBox;
        private NumericUpDown _strikesToShowNumericUpDown;

        private List<OptionDataRow> _allOptionsData; // Master list of all options
        private List<UnderlyingAssetDataRow> _uaData;

        private Dictionary<string, DataGridViewRow> _uaGridRows;
        private Dictionary<decimal, DataGridViewRow> _strikeGridRows; // Maps strike to the wide row

        private Dictionary<string, BotTabSimple> _simpleTabs;
        //private List<CandleEngine> _chartEngines = new List<CandleEngine>();
        private Thread _highlightingThread;
        private bool _isDisposed;
        private BotTabOptionsUi _ui;
        #endregion

        #region Properties
        public List<string> UnderlyingAssets { get; private set; }
        public string PortfolioName { get; private set; }
        public string TabName { get; set; }
        public int TabNum { get; set; }
        public BotTabType TabType => BotTabType.Options;
        public StartProgram StartProgram { get; set; }
        public bool EmulatorIsOn { get; set; }
        public bool IsConnected { get; private set; }
        public bool IsReadyToTrade { get; private set; }
        public DateTime LastTimeCandleUpdate { get; set; }
        public bool EventsIsOn { get; set; }
        #endregion

        #region Events
        public event Action<string, LogMessageType> LogMessageEvent;
        public event Action TabDeletedEvent;
        #endregion

        #region Constructor and Initialization
        public BotTabOptions(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;

            _allOptionsData = new List<OptionDataRow>();
            _uaData = new List<UnderlyingAssetDataRow>();
            _simpleTabs = new Dictionary<string, BotTabSimple>();
            _uaGridRows = new Dictionary<string, DataGridViewRow>();
            _strikeGridRows = new Dictionary<decimal, DataGridViewRow>();

            CreateGrids();

            _highlightingThread = new Thread(UpdateLoop) { IsBackground = true };
            _highlightingThread.Start();
        }

        public void SetUnderlyingAssetsAndStart(List<string> underlyingAssets, string portfolioName, IServer server)
        {
            UnderlyingAssets = underlyingAssets;
            PortfolioName = portfolioName;
            if (server == null) return;

            var allSecurities = server.Securities;
            if (allSecurities == null) return;

            // Clear previous data
            _allOptionsData.Clear();
            _uaData.Clear();
            foreach (var tab in _simpleTabs.Values) { tab.Delete(); }
            _simpleTabs.Clear();

            var optionsToTrade = allSecurities.Where(s => s.SecurityType == SecurityType.Option && UnderlyingAssets.Contains(s.UnderlyingAsset))
                                             .OrderBy(o => o.Expiration).ToList();
            var underlyingAssetSecurities = allSecurities.Where(s => UnderlyingAssets.Contains(s.Name)).ToList();

            foreach (var uaSec in underlyingAssetSecurities)
            {
                _uaData.Add(new UnderlyingAssetDataRow { Security = uaSec });
                CreateSimpleTab(uaSec, server);
            }

            foreach (var option in optionsToTrade)
            {
                var tab = CreateSimpleTab(option, server);
                _allOptionsData.Add(new OptionDataRow { Security = option, SimpleTab = tab });
            }

            PopulateExpirationFilter(optionsToTrade);
            InitializeUaGrid();
            RefreshOptionsGrid();
        }
        #endregion

        #region Grid Creation and Management
        private void CreateGrids()
        {
            _mainControl = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            _mainControl.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            _mainControl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            _mainControl.RowStyles.Add(new RowStyle(SizeType.Percent, 75));

            _uaGrid = CreateNewGrid();
            _uaGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Underlying Asset", Name = "Name", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120 });
            _uaGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bid", Name = "Bid", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            _uaGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ask", Name = "Ask", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            _uaGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Price", Name = "LastPrice", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            _uaGrid.SelectionChanged += (sender, args) => RefreshOptionsGrid();

            var filterPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(21, 26, 30) };
            filterPanel.Controls.Add(new Label() { Text = "Expiration:", Margin = new Padding(5, 6, 0, 0), ForeColor = Color.FromArgb(154, 156, 158), AutoSize = true });
            _expirationComboBox = new ComboBox() { Margin = new Padding(0, 3, 0, 0), BackColor = Color.FromArgb(21, 26, 30), ForeColor = Color.FromArgb(154, 156, 158), FlatStyle = FlatStyle.Flat };
            _expirationComboBox.SelectedIndexChanged += (sender, args) => RefreshOptionsGrid();
            filterPanel.Controls.Add(_expirationComboBox);
            filterPanel.Controls.Add(new Label() { Text = "Strikes:", Margin = new Padding(15, 6, 0, 0), ForeColor = Color.FromArgb(154, 156, 158), AutoSize = true });
            _strikesToShowNumericUpDown = new NumericUpDown() { Minimum = 0, Maximum = 100, Value = 4, Margin = new Padding(0, 3, 0, 0), BackColor = Color.FromArgb(21, 26, 30), ForeColor = Color.FromArgb(154, 156, 158), BorderStyle = BorderStyle.FixedSingle };
            _strikesToShowNumericUpDown.ValueChanged += (sender, args) => RefreshOptionsGrid();
            filterPanel.Controls.Add(_strikesToShowNumericUpDown);

            _optionsGrid = CreateNewGrid();
            string[] callHeaders = { "Theta", "Vega", "Gamma", "Delta", "Last", "Ask", "Bid", "IV", "Name" };
            string[] putHeaders = { "Bid", "Ask", "Last", "Delta", "Gamma", "Vega", "Theta", "IV", "Name" };
            foreach (var header in callHeaders) { _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, Name = "Call" + header, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells }); }
            _optionsGrid.Columns.Insert(callHeaders.Length, new DataGridViewButtonColumn { HeaderText = "Chart", Name = "CallChart", UseColumnTextForButtonValue = true, Text = "Open" });

            _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Strike", Name = "Strike", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });

            foreach (var header in putHeaders) { _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, Name = "Put" + header, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells }); }

            _optionsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Chart", Name = "PutChart", UseColumnTextForButtonValue = true, Text = "Open" });
            _optionsGrid.Columns["CallName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _optionsGrid.Columns["PutName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _optionsGrid.Columns["Strike"].DefaultCellStyle.Font = new Font(_optionsGrid.Font, FontStyle.Bold);
            _optionsGrid.CellClick += _optionsGrid_CellClick;

            _mainControl.Controls.Add(_uaGrid, 0, 0);
            _mainControl.Controls.Add(filterPanel, 0, 1);
            _mainControl.Controls.Add(_optionsGrid, 0, 2);
        }

        private DataGridView CreateNewGrid() { var grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells); grid.Dock = DockStyle.Fill; grid.MultiSelect = false; grid.ScrollBars = ScrollBars.Both; return grid; }

        private void InitializeUaGrid() { if (_uaGrid.InvokeRequired) { _uaGrid.Invoke(new Action(InitializeUaGrid)); return; } _uaGrid.Rows.Clear(); _uaGridRows.Clear(); foreach (var data in _uaData) { var row = new DataGridViewRow(); row.CreateCells(_uaGrid, data.Security.Name, data.Bid, data.Ask, data.LastPrice); _uaGridRows.Add(data.Security.Name, row); _uaGrid.Rows.Add(row); } }

        private void PopulateExpirationFilter(List<Security> options) { if (_expirationComboBox.InvokeRequired) { _expirationComboBox.Invoke(new Action<List<Security>>(PopulateExpirationFilter), options); return; } var dates = options.Select(o => o.Expiration.Date).Distinct().OrderBy(d => d).ToList(); _expirationComboBox.Items.Clear(); _expirationComboBox.Items.Add("All"); foreach (var date in dates) { _expirationComboBox.Items.Add(date.ToShortDateString()); } if (_expirationComboBox.Items.Count > 1) { _expirationComboBox.SelectedIndex = 1; } else { _expirationComboBox.SelectedItem = "All"; } }

        private void RefreshOptionsGrid()
        {
            if (_uaGrid.SelectedRows.Count == 0) { PopulateOptionsGrid(new List<StrikeDataRow>()); return; }

            var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString();
            DateTime? selectedDate = null;
            string selectedExpirationStr = null;
            _expirationComboBox.Invoke((MethodInvoker)(() => selectedExpirationStr = _expirationComboBox.SelectedItem?.ToString()));
            if (!string.IsNullOrEmpty(selectedExpirationStr) && selectedExpirationStr != "All") { selectedDate = Convert.ToDateTime(selectedExpirationStr); }

            // Step 1: Filter master list
            var optionsForDisplay = _allOptionsData.Where(o => o.Security.UnderlyingAsset == selectedUaName &&
                                                       (!selectedDate.HasValue || o.Security.Expiration.Date == selectedDate.Value.Date)).ToList();

            // Step 2: Group by Strike and create wide rows
            var strikesToDisplay = optionsForDisplay.GroupBy(o => o.Security.Strike)
                .Select(group =>
                {
                    var strikeRow = new StrikeDataRow { Strike = group.Key };
                    var callOption = group.FirstOrDefault(o => o.Security.OptionType == OptionType.Call);
                    var putOption = group.FirstOrDefault(o => o.Security.OptionType == OptionType.Put);
                    if (callOption != null) { strikeRow.CallData = callOption; }
                    if (putOption != null) { strikeRow.PutData = putOption; }
                    return strikeRow;
                }).ToList();

            // Step 3: Filter by Strike Count
            var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == selectedUaName);
            var uaPrice = uaData?.LastPrice ?? 0;
            if (uaPrice == 0 && uaData != null && uaData.Bid != 0 && uaData.Ask != 0) { uaPrice = (uaData.Bid + uaData.Ask) / 2; }

            var strikes = strikesToDisplay.Select(s => s.Strike).Distinct().OrderBy(s => s).ToList();
            if (strikes.Count == 0) { PopulateOptionsGrid(new List<StrikeDataRow>()); return; }

            decimal atmStrike;
            if (uaPrice != 0) { atmStrike = strikes.Aggregate((x, y) => Math.Abs(x - uaPrice) < Math.Abs(y - uaPrice) ? x : y); }
            else { int middleIndex = strikes.Count / 2; atmStrike = strikes[middleIndex]; }

            int atmStrikeIndex = strikes.IndexOf(atmStrike);
            int strikesToShowCount = (int)_strikesToShowNumericUpDown.Value;
            int startIndex = Math.Max(0, atmStrikeIndex - strikesToShowCount);
            int endIndex = Math.Min(strikes.Count - 1, atmStrikeIndex + strikesToShowCount);
            var allowedStrikes = new HashSet<decimal>(strikes.GetRange(startIndex, endIndex - startIndex + 1));

            var finalStrikes = strikesToDisplay.Where(s => allowedStrikes.Contains(s.Strike)).OrderBy(s => s.Strike).ToList();

            // Step 4: Populate Grid
            PopulateOptionsGrid(finalStrikes);
        }

        private void PopulateOptionsGrid(List<StrikeDataRow> strikes) 
        { 
            if (_optionsGrid.InvokeRequired) { _optionsGrid.Invoke(new Action<List<StrikeDataRow>>(PopulateOptionsGrid), strikes); return; } 
            _optionsGrid.Rows.Clear(); 
            _strikeGridRows.Clear(); 
            foreach (var data in strikes) 
            { 
                var row = new DataGridViewRow(); 
                row.CreateCells(_optionsGrid, 
                    data.CallData?.Theta, data.CallData?.Vega, data.CallData?.Gamma, data.CallData?.Delta, data.CallData?.LastPrice, data.CallData?.Ask, data.CallData?.Bid, data.CallIV, data.CallData?.Security.Name,"chart",
                    data.Strike,
                    data.PutData?.Bid, data.PutData?.Ask, data.PutData?.LastPrice, data.PutData?.Delta, data.PutData?.Gamma, data.PutData?.Vega, data.PutData?.Theta, data.PutIV, data.PutData?.Security.Name, "chart");
                _strikeGridRows.Add(data.Strike, row);
                _optionsGrid.Rows.Add(row);
            } 
        }

        private void _optionsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var isCallChart = e.ColumnIndex == _optionsGrid.Columns["CallChart"].Index;
            var isPutChart = e.ColumnIndex == _optionsGrid.Columns["PutChart"].Index;

            if (!isCallChart && !isPutChart) return;

            var strike = (decimal)_optionsGrid.Rows[e.RowIndex].Cells["Strike"].Value;
            var strikeData = _allOptionsData.Where(o => o.Security.Strike == strike).ToList();
            var optionData = isCallChart ? strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Call) : strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Put);

            if (optionData?.SimpleTab != null)
            {
                ShowChart(optionData.SimpleTab);
            }
        }

        private void ShowChart(BotTabSimple tab)
        {
            try
            {
                string botName = tab.TabName + "_Engine";
                CandleEngine bot = new CandleEngine(botName, StartProgram);

                bot.GetTabs().Clear();
                bot.GetTabs().Add(tab);
                bot.TabsSimple[0] = tab;
                bot.ActiveTab = tab;

                bot.ChartClosedEvent += (string nameBot) =>
                {
                };

                bot.ShowChartDialog();
            }
            catch (Exception ex)
            {
                //SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
        #endregion

        #region Connectors and Data Handling
        private BotTabSimple CreateSimpleTab(Security security, IServer server)
        {
            var tab = new BotTabSimple(TabName + security.Name, StartProgram);
            tab.Connector.ServerType = server.ServerType;
            tab.Connector.PortfolioName = this.PortfolioName;
            tab.Connector.SecurityName = security.Name;
            tab.Connector.SecurityClass = security.NameClass;
            tab.Connector.ServerFullName = server.ServerType.ToString();
            tab.Connector.SaveTradesInCandles = false;//  SaveTradesInCandles;

            //tab.CommissionType = CommissionType;
            //tab.CommissionValue = CommissionValue;
            

            //tab.Connector.EmulatorIsOn = _emulatorIsOn;

            //tab.Connector.ServerUid = ServerUid;

            //newTab.TimeFrameBuilder.TimeFrame = frame;
            //newTab.Connector.CandleMarketDataType = CandleMarketDataType;
            //newTab.Connector.CandleCreateMethodType = CandleCreateMethodType;
            //newTab.Connector.TimeFrame = frame;
            //newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(CandleSeriesRealization.GetSaveString());
            //newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.OnStateChange(CandleSeriesState.ParametersChange);

            tab.Connector.AdditionalDataEvent += Connector_AdditionalDataEvent;
            tab.Connector.GlassChangeEvent += Connector_GlassChangeEvent;
            tab.Connector.TickChangeEvent += Connector_TickChangeEvent;

            tab.IsCreatedByScreener = true;

            _simpleTabs.Add(security.Name, tab);
            return tab;
        }

        private void Connector_AdditionalDataEvent(OptionMarketData data)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action<OptionMarketData>(Connector_AdditionalDataEvent), data);
                return;
            }

            var optionData = _allOptionsData.FirstOrDefault(o => o.Security.Name == data.SecurityName);
            if (optionData != null)
            {
                optionData.Delta = data.Delta;
                optionData.Gamma = data.Gamma;
                optionData.Vega = data.Vega;
                optionData.Theta = data.Theta;

                if (_strikeGridRows.TryGetValue(optionData.Security.Strike, out var row))
                {
                    if (optionData.Security.OptionType == OptionType.Call)
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallDelta", data.Delta);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallGamma", data.Gamma);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallVega", data.Vega);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallTheta", data.Theta);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallIV", data.MarkIV);
                    }
                    else
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutDelta", data.Delta);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutGamma", data.Gamma);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutVega", data.Vega);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutTheta", data.Theta);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutIV", data.MarkIV);
                    }
                }
            }
        }

        private void Connector_TickChangeEvent(List<Trade> trades)
        {
            if (_isDisposed)
            {
                return;
            }

            if (trades == null || trades.Count == 0)
            {
                return;
            }

            var trade = trades[trades.Count - 1];

            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action<List<Trade>>(Connector_TickChangeEvent), trades);
                return;
            }

            var optionData = _allOptionsData.FirstOrDefault(o => o.Security.Name == trade.SecurityNameCode);
            if (optionData != null)
            {
                optionData.LastPrice = trade.Price;

                if (_strikeGridRows.TryGetValue(optionData.Security.Strike, out var row))
                {
                    if (optionData.Security.OptionType == OptionType.Call)
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallLast", trade.Price);
                    }
                    else
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutLast", trade.Price);
                    }
                }
                return;
            }

            var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == trade.SecurityNameCode);
            if (uaData != null)
            {
                uaData.LastPrice = trade.Price;
                UpdateGridCell(_uaGridRows, uaData.Security.Name, "LastPrice", uaData.LastPrice);
            }
        }

        private void Connector_GlassChangeEvent(MarketDepth marketDepth)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action<MarketDepth>(Connector_GlassChangeEvent), marketDepth);
                return;
            }

            var optionData = _allOptionsData.FirstOrDefault(o => o.Security.Name == marketDepth.SecurityNameCode);
            if (optionData != null)
            {
                decimal bestBid = 0;
                if (marketDepth.Bids != null && marketDepth.Bids.Count > 0)
                {
                    bestBid = marketDepth.Bids[0].Price;
                }

                decimal bestAsk = 0;
                if (marketDepth.Asks != null && marketDepth.Asks.Count > 0)
                {
                    bestAsk = marketDepth.Asks[0].Price;
                }

                optionData.Bid = bestBid;
                optionData.Ask = bestAsk;

                if (_strikeGridRows.TryGetValue(optionData.Security.Strike, out var row))
                {
                    if (optionData.Security.OptionType == OptionType.Call)
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallBid", bestBid);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "CallAsk", bestAsk);
                    }
                    else
                    {
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutBid", bestBid);
                        UpdateGridCell(_strikeGridRows, optionData.Security.Strike, "PutAsk", bestAsk);
                    }
                }
                return;
            }

            var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == marketDepth.SecurityNameCode);
            if (uaData != null)
            {
                decimal bestBid = 0;
                if (marketDepth.Bids != null && marketDepth.Bids.Count > 0)
                {
                    bestBid = marketDepth.Bids[0].Price;
                }

                decimal bestAsk = 0;
                if (marketDepth.Asks != null && marketDepth.Asks.Count > 0)
                {
                    bestAsk = marketDepth.Asks[0].Price;
                }

                uaData.Bid = bestBid;
                uaData.Ask = bestAsk;
                UpdateGridCell(_uaGridRows, uaData.Security.Name, "Bid", uaData.Bid);
                UpdateGridCell(_uaGridRows, uaData.Security.Name, "Ask", uaData.Ask);
            }
        }

        
        #endregion

        #region Highlighting and UI Updates
        private void UpdateLoop()
        {
            while (!_isDisposed)
            {
                Thread.Sleep(2000);
                if (_mainControl.IsHandleCreated && !_isDisposed)
                {
                    _mainControl.Invoke(new Action(() =>
                    {
                        UpdateHighlighting();
                    }));
                }
            }
        }

        private void UpdateHighlighting() 
        {
             if (_uaGrid.SelectedRows.Count == 0) return; 

            var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString(); 
            var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == selectedUaName); 
            var uaPrice = uaData?.LastPrice ?? 0; 

            if (uaPrice == 0 && uaData != null && uaData.Bid != 0 && uaData.Ask != 0) 
            { 
                uaPrice = (uaData.Bid + uaData.Ask) / 2; 
            } 

            if (uaPrice == 0) return; 

            decimal centralStrike = 0; 
            decimal minDiff = decimal.MaxValue; 

            var strikes = _strikeGridRows.Keys.OrderBy(s => s).ToList(); 

            foreach (var strike in strikes) 
            { 
                var diff = Math.Abs(strike - uaPrice); 
                if (diff < minDiff) { minDiff = diff; centralStrike = strike; } 
            } 

            if (centralStrike == 0) return; 

            foreach (var entry in _strikeGridRows) 
            { 
                entry.Value.DefaultCellStyle.BackColor = entry.Key == centralStrike ? Color.FromArgb(40, 80, 40) : _optionsGrid.DefaultCellStyle.BackColor; 
            } 
        }

        private void UpdateGridCell(Dictionary<string, DataGridViewRow> rows, string securityName, string cellName, object value) { if (_mainControl.InvokeRequired) { _mainControl.Invoke(new Action<Dictionary<string, DataGridViewRow>, string, string, object>(UpdateGridCell), rows, securityName, cellName, value); return; } if (rows.TryGetValue(securityName, out var row)) { row.Cells[cellName].Value = value; } }
        private void UpdateGridCell(Dictionary<decimal, DataGridViewRow> rows, decimal strike, string cellName, object value) { if (_mainControl.InvokeRequired) { _mainControl.Invoke(new Action<Dictionary<decimal, DataGridViewRow>, decimal, string, object>(UpdateGridCell), rows, strike, cellName, value); return; } if (rows.TryGetValue(strike, out var row)) { row.Cells[cellName].Value = value; } }
        #endregion

        #region IIBotTab Implementation
        public void Clear() { }
        public void Delete() { _isDisposed = true; if (_highlightingThread != null && _highlightingThread.IsAlive) _highlightingThread.Abort(); foreach (var tab in _simpleTabs.Values) { tab.Connector.AdditionalDataEvent -= Connector_AdditionalDataEvent; tab.Connector.TickChangeEvent -= Connector_TickChangeEvent; tab.Connector.GlassChangeEvent -= Connector_GlassChangeEvent; tab.Delete(); } TabDeletedEvent?.Invoke(); }
        public void ShowDialog() { if (ServerMaster.GetServers() == null || ServerMaster.GetServers().Count == 0) { new AlertMessageSimpleUi(OsLocalization.Market.Message1).Show(); return; } if (_ui != null && !_ui.IsLoaded) _ui = null; if (_ui == null) { _ui = new BotTabOptionsUi(this); _ui.Closed += (sender, args) => { _ui = null; }; _ui.Show(); } else { _ui.Activate(); } }
        public void StartPaint(System.Windows.Forms.Integration.WindowsFormsHost hostChart) { hostChart.Child = _mainControl; }
        public void StopPaint() { }
        #endregion

        #region DataRow Classes
        public class StrikeDataRow { public decimal Strike { get; set; } public OptionDataRow CallData { get; set; } public OptionDataRow PutData { get; set; } public decimal CallIV { get; set; } public decimal PutIV { get; set; } }
        public class OptionDataRow { public Security Security { get; set; } public BotTabSimple SimpleTab { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } public decimal LastPrice { get; set; } public decimal Delta { get; set; } public decimal Gamma { get; set; } public decimal Vega { get; set; } public decimal Theta { get; set; } }
        public class UnderlyingAssetDataRow { public Security Security { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } public decimal LastPrice { get; set; } }
        #endregion
    }
}