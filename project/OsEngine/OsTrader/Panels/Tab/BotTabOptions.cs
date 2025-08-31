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
using System.IO;
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
        private bool _isDisposed;
        private BotTabOptionsUi _ui;
        private System.Threading.Timer _updateTimer;
        private readonly object _locker = new object();
        #endregion

        #region Properties
        public List<string> UnderlyingAssets { get; private set; }
        public string PortfolioName { get; private set; }
        public ServerType ServerType { get; set; }
        public string ServerName { get; set; }
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
        // Existing events
        public event Action<string, LogMessageType> LogMessageEvent;
        public event Action TabDeletedEvent;

        // Market Data Events
        public event Action<MarketDepth, BotTabSimple> NewMarketDepthEvent;
        public event Action<List<Trade>, BotTabSimple> NewTickEvent;
        public event Action<MyTrade, BotTabSimple> NewMyTradeEvent;
        public event Action<OptionMarketData, BotTabSimple> NewOptionsDataEvent;

        // Position Events
        public event Action<Position, BotTabSimple> PositionOpeningSuccessEvent;
        public event Action<Position, BotTabSimple> PositionOpeningFailEvent;
        public event Action<Position, BotTabSimple> PositionClosingSuccessEvent;
        public event Action<Position, BotTabSimple> PositionClosingFailEvent;
        public event Action<Position, BotTabSimple> PositionStopActivateEvent;
        public event Action<Position, BotTabSimple> PositionProfitActivateEvent;

        // Order Events
        public event Action<Order, BotTabSimple> OrderUpdateEvent;
        #endregion

        #region Settings

        private string SettingsFilePath => $"Engine\\{TabName}\\OptionsSettings.txt";
        private string SettingsFolderPath => $"Engine\\{TabName}";

        public void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(SettingsFolderPath))
                {
                    Directory.CreateDirectory(SettingsFolderPath);
                }

                using (var writer = new StreamWriter(SettingsFilePath, false))
                {
                    writer.WriteLine($"{nameof(PortfolioName)}:{PortfolioName}");
                    writer.WriteLine($"{nameof(UnderlyingAssets)}:{string.Join(",", UnderlyingAssets ?? new List<string>())}");
                    writer.WriteLine($"StrikesToShow:{_strikesToShowNumericUpDown.Value}");
                    writer.WriteLine($"{nameof(ServerType)}:{ServerType}");
                    writer.WriteLine($"{nameof(ServerName)}:{ServerName}");
                }
            }
            catch (Exception e)
            {
                LogMessageEvent?.Invoke(e.ToString(), LogMessageType.Error);
            }
        }

        public void LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return;
            }
            try
            {
                using (var reader = new StreamReader(SettingsFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine()?.Split(':');
                        if (line == null || line.Length < 2) continue;

                        var key = line[0];
                        var value = string.Join(":", line.Skip(1));

                        if (key == nameof(PortfolioName)) { PortfolioName = value; }
                        else if (key == nameof(UnderlyingAssets)) { UnderlyingAssets = value.Split(',').ToList(); }
                        else if (key == "StrikesToShow") { _strikesToShowNumericUpDown.Value = Convert.ToDecimal(value); }
                        else if (key == nameof(ServerType)) { Enum.TryParse(value, out ServerType serverType); ServerType = serverType; }
                        else if (key == nameof(ServerName)) { ServerName = value; }
                    }
                }
            }
            catch (Exception e)
            {
                LogMessageEvent?.Invoke(e.ToString(), LogMessageType.Error);
            }
        }

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

            LoadSettings();
            TryToReloadTabsFromSettings();

            _updateTimer = new System.Threading.Timer(TimerCallback, null, 500, 500);
        }

        private void TimerCallback(object state)
        {
            if (_isDisposed || _mainControl.IsHandleCreated == false)
            {
                return;
            }

            _mainControl.Invoke(new Action(RedrawGrid));
        }

        private void RedrawGrid()
        {
            lock (_locker)
            {
                // Update options grid
                foreach (DataGridViewRow row in _optionsGrid.Rows)
                {
                    if (!row.Displayed)
                    {
                        continue;
                    }

                    decimal strike = (decimal)row.Cells["Strike"].Value;
                    var strikeData = _allOptionsData.Where(o => o.Security.Strike == strike).ToList();
                    var callData = strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Call);
                    var putData = strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Put);

                    if (callData != null)
                    {
                        row.Cells["CallBid"].Value = callData.Bid;
                        row.Cells["CallAsk"].Value = callData.Ask;
                        row.Cells["CallLast"].Value = callData.LastPrice;
                        row.Cells["CallDelta"].Value = callData.Delta;
                        row.Cells["CallGamma"].Value = callData.Gamma;
                        row.Cells["CallVega"].Value = callData.Vega;
                        row.Cells["CallTheta"].Value = callData.Theta;
                        row.Cells["CallIV"].Value = callData.IV;
                    }

                    if (putData != null)
                    {
                        row.Cells["PutBid"].Value = putData.Bid;
                        row.Cells["PutAsk"].Value = putData.Ask;
                        row.Cells["PutLast"].Value = putData.LastPrice;
                        row.Cells["PutDelta"].Value = putData.Delta;
                        row.Cells["PutGamma"].Value = putData.Gamma;
                        row.Cells["PutVega"].Value = putData.Vega;
                        row.Cells["PutTheta"].Value = putData.Theta;
                        row.Cells["PutIV"].Value = putData.IV;
                    }
                }

                // Update underlying assets grid
                foreach (DataGridViewRow row in _uaGrid.Rows)
                {
                    if (!row.Displayed)
                    {
                        continue;
                    }

                    string securityName = row.Cells["Name"].Value.ToString();
                    var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == securityName);

                    if (uaData != null)
                    {
                        row.Cells["Bid"].Value = uaData.Bid;
                        row.Cells["Ask"].Value = uaData.Ask;
                        row.Cells["LastPrice"].Value = uaData.LastPrice;
                    }
                }

                // Update highlighting
                if (_uaGrid.SelectedRows.Count > 0)
                {
                    var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString();
                    var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == selectedUaName);
                    var uaPrice = uaData?.LastPrice ?? 0;

                    if (uaPrice == 0 && uaData != null && uaData.Bid != 0 && uaData.Ask != 0)
                    {
                        uaPrice = (uaData.Bid + uaData.Ask) / 2;
                    }

                    if (uaPrice != 0)
                    {
                        decimal centralStrike = 0;
                        decimal minDiff = decimal.MaxValue;

                        var strikes = _strikeGridRows.Keys.OrderBy(s => s).ToList();

                        foreach (var strike in strikes)
                        {
                            var diff = Math.Abs(strike - uaPrice);
                            if (diff < minDiff) { minDiff = diff; centralStrike = strike; }
                        }

                        if (centralStrike != 0)
                        {
                            foreach (var entry in _strikeGridRows)
                            {
                                entry.Value.DefaultCellStyle.BackColor = entry.Key == centralStrike ? Color.FromArgb(40, 40, 40) : _optionsGrid.DefaultCellStyle.BackColor;
                            }
                        }
                    }
                }
            }
        }

        private void TryToReloadTabsFromSettings()
        {
            if (UnderlyingAssets == null || UnderlyingAssets.Count == 0 || string.IsNullOrEmpty(PortfolioName) || ServerType == ServerType.None)
            {
                return;
            }

            List<IServer> servers = ServerMaster.GetServers();
            IServer server = null;
            if (servers != null)
            {
                server = servers.Find(s => s.ServerType == ServerType && s.ServerNameAndPrefix == ServerName);
            }

            if (server != null && server.ServerStatus == ServerConnectStatus.Connect)
            {
                SetUnderlyingAssetsAndStart(UnderlyingAssets, PortfolioName, server);
            }
            else if(server != null)
            {
                server.ConnectStatusChangeEvent += ServerOnConnectStatusChangeEvent;
            }
            else
            {
                ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
            }

            ServerMaster.SetServerToAutoConnection(ServerType, ServerName);
        }

        private void ServerMaster_ServerCreateEvent(IServer newServer)
        {
            if (newServer.ServerType == ServerType && newServer.ServerNameAndPrefix == ServerName)
            {
                newServer.ConnectStatusChangeEvent += ServerOnConnectStatusChangeEvent;
                ServerMaster.ServerCreateEvent -= ServerMaster_ServerCreateEvent;
            }
        }

        private void ServerOnConnectStatusChangeEvent(string status)
        {
            if (status == ServerConnectStatus.Connect.ToString())
            {
                List<IServer> servers = ServerMaster.GetServers();
                IServer server = null;
                if (servers != null)
                {
                    server = servers.Find(s => s.ServerType == ServerType && s.ServerNameAndPrefix == ServerName);
                }

                if (server != null)
                {
                    if (server.Securities != null && server.Securities.Count > 0)
                    {
                        SetUnderlyingAssetsAndStart(UnderlyingAssets, PortfolioName, server);
                    }
                    else
                    {
                        server.SecuritiesChangeEvent += Server_SecuritiesChangeEvent;
                    }

                    server.ConnectStatusChangeEvent -= ServerOnConnectStatusChangeEvent;
                }
            }
        }

        private void Server_SecuritiesChangeEvent(List<Security> securities)
        {
            List<IServer> servers = ServerMaster.GetServers();
            IServer server = null;
            if (servers != null)
            {
                server = servers.Find(s => s.ServerType == ServerType && s.ServerNameAndPrefix == ServerName);
            }

            if (server != null)
            {
                SetUnderlyingAssetsAndStart(UnderlyingAssets, PortfolioName, server);
                server.SecuritiesChangeEvent -= Server_SecuritiesChangeEvent;
            }
        }

        public void SetUnderlyingAssetsAndStart(List<string> underlyingAssets, string portfolioName, IServer server)
        {
            UnderlyingAssets = underlyingAssets;
            PortfolioName = portfolioName;

            if (server == null) return;

            ServerName = server.ServerNameAndPrefix;
            ServerType = server.ServerType;

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
            SelectFirstUnderlyingAsset();

            SaveSettings();
        }

        private void SelectFirstUnderlyingAsset()
        {
            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action(SelectFirstUnderlyingAsset));
                return;
            }

            if (_uaGrid.Rows.Count > 0)
            {
                _uaGrid.Rows[0].Selected = true;
                RefreshOptionsGrid();
            }
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
            _uaGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Chart", Name = "UaChart", UseColumnTextForButtonValue = true, Text = "Open" });
            _uaGrid.SelectionChanged += (sender, args) => RefreshOptionsGrid();
            _uaGrid.CellClick += _uaGrid_CellClick;

            var filterPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(21, 26, 30) };
            filterPanel.Controls.Add(new Label() { Text = "Expiration:", Margin = new Padding(5, 6, 0, 0), ForeColor = Color.FromArgb(154, 156, 158), AutoSize = true });
            _expirationComboBox = new ComboBox() { Margin = new Padding(0, 3, 0, 0), BackColor = Color.FromArgb(21, 26, 30), ForeColor = Color.FromArgb(154, 156, 158), FlatStyle = FlatStyle.Flat };
            _expirationComboBox.SelectedIndexChanged += (sender, args) => RefreshOptionsGrid();
            filterPanel.Controls.Add(_expirationComboBox);
            filterPanel.Controls.Add(new Label() { Text = "Strikes:", Margin = new Padding(15, 6, 0, 0), ForeColor = Color.FromArgb(154, 156, 158), AutoSize = true });
            _strikesToShowNumericUpDown = new NumericUpDown() { Minimum = 0, Maximum = 100, Value = 4, Margin = new Padding(0, 3, 0, 0), BackColor = Color.FromArgb(21, 26, 30), ForeColor = Color.FromArgb(154, 156, 158), BorderStyle = BorderStyle.FixedSingle };
            _strikesToShowNumericUpDown.ValueChanged += (sender, args) => 
            { 
                RefreshOptionsGrid(); 
                SaveSettings();
            };
            filterPanel.Controls.Add(_strikesToShowNumericUpDown);

            var buildChartButton = new Button() { Text = "Build PNL Chart", Margin = new Padding(25, 3, 0, 0), ForeColor = Color.FromArgb(154, 156, 158),  };
            buildChartButton.Click += BuildChartButton_Click;
            filterPanel.Controls.Add(buildChartButton);

            _optionsGrid = CreateNewGrid();

            // Call side
            _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty", Name = "CallQty", ReadOnly = false, Width = 40 });
            string[] callHeaders = { "Theta", "Vega", "Gamma", "Delta", "Last", "Ask", "Bid", "IV", "Name" };
            foreach (var header in callHeaders) { _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, Name = "Call" + header, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells }); }
            _optionsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Chart", Name = "CallChart", UseColumnTextForButtonValue = true, Text = "Open" });
            _optionsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "PNL", Name = "CallPnl", UseColumnTextForButtonValue = true, Text = "Profile" });

            // Center
            _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Strike", Name = "Strike", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });

            // Put side
            string[] putHeaders = { "Bid", "Ask", "Last", "Delta", "Gamma", "Vega", "Theta", "IV", "Name" };
            foreach (var header in putHeaders) { _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, Name = "Put" + header, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells }); }
            _optionsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Chart", Name = "PutChart", UseColumnTextForButtonValue = true, Text = "Open" });
            _optionsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "PNL", Name = "PutPnl", UseColumnTextForButtonValue = true, Text = "Profile" });
            _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty", Name = "PutQty", ReadOnly = false, Width = 40 });
            _optionsGrid.Columns["CallName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _optionsGrid.Columns["PutName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _optionsGrid.Columns["Strike"].DefaultCellStyle.Font = new Font(_optionsGrid.Font, FontStyle.Bold);
            _optionsGrid.CellClick += _optionsGrid_CellClick;
            _optionsGrid.CellValueChanged += _optionsGrid_CellValueChanged;

            _mainControl.Controls.Add(_uaGrid, 0, 0);
            _mainControl.Controls.Add(filterPanel, 0, 1);
            _mainControl.Controls.Add(_optionsGrid, 0, 2);
        }

        private void _optionsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var grid = (DataGridView)sender;
            var colName = grid.Columns[e.ColumnIndex].Name;

            if (colName != "CallQty" && colName != "PutQty") return;

            try
            {
                var strike = (decimal)grid.Rows[e.RowIndex].Cells["Strike"].Value;
                var valueStr = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();

                if (!int.TryParse(valueStr, out int quantity))
                {
                    // Optional: handle invalid input, for now we just ignore
                    return;
                }

                OptionDataRow optionData = null;
                if (colName == "CallQty")
                {
                    optionData = _allOptionsData.FirstOrDefault(o => o.Security.Strike == strike && o.Security.OptionType == OptionType.Call);
                }
                else // PutQty
                {
                    optionData = _allOptionsData.FirstOrDefault(o => o.Security.Strike == strike && o.Security.OptionType == OptionType.Put);
                }

                if (optionData != null)
                {
                    optionData.Quantity = quantity;
                }
            }
            catch (Exception ex)
            {
                LogMessageEvent?.Invoke(ex.ToString(), LogMessageType.Error);
            }
        }

        private void BuildChartButton_Click(object sender, EventArgs e)
        {
            var strategyLegs = _allOptionsData.Where(o => o.Quantity != 0).ToList();
            if (strategyLegs.Count == 0)
            {
                MessageBox.Show("No position selected. Please enter a quantity for one or more options.");
                return;
            }

            var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString();
            var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == selectedUaName);

            if (uaData != null)
            {
                StrategyPnlChartUi ui = new StrategyPnlChartUi(strategyLegs, uaData);
                ui.Show();
            }
        }

        private void _uaGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _uaGrid.Columns["UaChart"].Index)
            {
                return;
            }

            var uaName = _uaGrid.Rows[e.RowIndex].Cells["Name"].Value.ToString();

            if (_simpleTabs.TryGetValue(uaName, out var tab))
            {
                ShowChart(tab);
            }
        }

        private DataGridView CreateNewGrid() { var grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells); grid.Dock = DockStyle.Fill; grid.MultiSelect = false; grid.ScrollBars = ScrollBars.Both; return grid; }

        private void InitializeUaGrid() { if (_uaGrid.InvokeRequired) { _uaGrid.Invoke(new Action(InitializeUaGrid)); return; } _uaGrid.Rows.Clear(); _uaGridRows.Clear(); foreach (var data in _uaData) { var row = new DataGridViewRow(); row.CreateCells(_uaGrid, data.Security.Name, data.Bid, data.Ask, data.LastPrice, "Open"); _uaGridRows.Add(data.Security.Name, row); _uaGrid.Rows.Add(row); } }

        private void PopulateExpirationFilter(List<Security> options) { if (_expirationComboBox.InvokeRequired) { _expirationComboBox.Invoke(new Action<List<Security>>(PopulateExpirationFilter), options); return; } var dates = options.Select(o => o.Expiration.Date).Distinct().OrderBy(d => d).ToList(); _expirationComboBox.Items.Clear(); _expirationComboBox.Items.Add("All"); foreach (var date in dates) { _expirationComboBox.Items.Add(date.ToShortDateString()); } if (_expirationComboBox.Items.Count > 1) { _expirationComboBox.SelectedIndex = 1; } else { _expirationComboBox.SelectedItem = "All"; } }

        private void RefreshOptionsGrid()
        {
            if (_uaGrid.SelectedRows.Count == 0) { PopulateOptionsGrid(new List<StrikeDataRow>()); return; }

            var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString();
            DateTime? selectedDate = null;
            string selectedExpirationStr = null;
            if (_expirationComboBox.IsHandleCreated)
            {
                if (_expirationComboBox.InvokeRequired)
                {
                    _expirationComboBox.Invoke((MethodInvoker)(() => selectedExpirationStr = _expirationComboBox.SelectedItem?.ToString()));
                }
                else
                {
                    selectedExpirationStr = _expirationComboBox.SelectedItem?.ToString();
                }
            }
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
                    data.CallData?.Quantity, // Call Qty
                    data.CallData?.Theta, 
                    data.CallData?.Vega, 
                    data.CallData?.Gamma, 
                    data.CallData?.Delta, 
                    data.CallData?.LastPrice, 
                    data.CallData?.Ask, 
                    data.CallData?.Bid, 
                    data.CallData?.IV, 
                    data.CallData?.Security.Name,
                    "Open", // CallChart
                    "Profile", // CallPnl
                    data.Strike,
                    data.PutData?.Bid, 
                    data.PutData?.Ask, 
                    data.PutData?.LastPrice, 
                    data.PutData?.Delta, 
                    data.PutData?.Gamma, 
                    data.PutData?.Vega, 
                    data.PutData?.Theta, 
                    data.PutData?.IV, 
                    data.PutData?.Security.Name, 
                    "Open", // PutChart
                    "Profile", // PutPnl
                    data.PutData?.Quantity // Put Qty
                    );
                _strikeGridRows.Add(data.Strike, row);
                _optionsGrid.Rows.Add(row);
            } 
        }

        private void _optionsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var isCallChart = e.ColumnIndex == _optionsGrid.Columns["CallChart"].Index;
            var isPutChart = e.ColumnIndex == _optionsGrid.Columns["PutChart"].Index;
            var isCallPnl = e.ColumnIndex == _optionsGrid.Columns["CallPnl"].Index;
            var isPutPnl = e.ColumnIndex == _optionsGrid.Columns["PutPnl"].Index;

            if (!isCallChart && !isPutChart && !isCallPnl && !isPutPnl) return;

            var strike = (decimal)_optionsGrid.Rows[e.RowIndex].Cells["Strike"].Value;
            var strikeData = _allOptionsData.Where(o => o.Security.Strike == strike).ToList();

            if (isCallPnl || isPutPnl)
            {
                var optionData = isCallPnl ? strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Call) : strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Put);
                var selectedUaName = _uaGrid.SelectedRows[0].Cells["Name"].Value.ToString();
                var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == selectedUaName);
                if (optionData != null && uaData != null)
                {
                    OptionPnlChartUi ui = new OptionPnlChartUi(optionData, uaData);
                    ui.Show();
                }
                return; // Prevent falling through to ShowChart logic
            }

            var optionDataChart = isCallChart ? strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Call) : strikeData.FirstOrDefault(o => o.Security.OptionType == OptionType.Put);

            if (optionDataChart?.SimpleTab != null)
            {
                ShowChart(optionDataChart.SimpleTab);
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

            // Market Data Events from Connector
            tab.Connector.GlassChangeEvent += (MarketDepth marketDepth) =>
            {
                NewMarketDepthEvent?.Invoke(marketDepth, tab);
                Connector_GlassChangeEvent(marketDepth);
            };
            tab.Connector.TickChangeEvent += (List<Trade> trades) =>
            {
                NewTickEvent?.Invoke(trades, tab);
                Connector_TickChangeEvent(trades);
            };
            tab.Connector.AdditionalDataEvent += (OptionMarketData data) =>
            {
                if (_simpleTabs.TryGetValue(data.SecurityName, out var simpleTab))
                {
                    NewOptionsDataEvent?.Invoke(data, simpleTab);
                }
                Connector_AdditionalDataEvent(data);
            };
            tab.Connector.MyTradeEvent += (MyTrade myTrade) =>
            {
                NewMyTradeEvent?.Invoke(myTrade, tab);
            };

            // Order and Position Events from Tab
            tab.OrderUpdateEvent += (Order order) => { OrderUpdateEvent?.Invoke(order, tab); };
            tab.PositionOpeningSuccesEvent += (Position pos) => { PositionOpeningSuccessEvent?.Invoke(pos, tab); };
            tab.PositionOpeningFailEvent += (Position pos) => { PositionOpeningFailEvent?.Invoke(pos, tab); };
            tab.PositionClosingSuccesEvent += (Position pos) => { PositionClosingSuccessEvent?.Invoke(pos, tab); };
            tab.PositionClosingFailEvent += (Position pos) => { PositionClosingFailEvent?.Invoke(pos, tab); };
            tab.PositionStopActivateEvent += (Position pos) => { PositionStopActivateEvent?.Invoke(pos, tab); };
            tab.PositionProfitActivateEvent += (Position pos) => { PositionProfitActivateEvent?.Invoke(pos, tab); };


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

            lock (_locker)
            {
                var optionData = _allOptionsData.FirstOrDefault(o => o.Security.Name == data.SecurityName);
                if (optionData != null)
                {
                    if (data.Delta != 0)
                    {
                        optionData.Delta = data.Delta;
                    }
                    if (data.Gamma != 0)
                    {
                        optionData.Gamma = data.Gamma;
                    }
                    if (data.Vega != 0)
                    {
                        optionData.Vega = data.Vega;
                    }
                    if (data.Theta != 0)
                    {
                        optionData.Theta = data.Theta;
                    }
                    if (data.MarkIV > 0)
                    {
                        optionData.IV = data.MarkIV;
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

            lock (_locker)
            {
                var optionData = _allOptionsData.FirstOrDefault(o => o.Security.Name == trade.SecurityNameCode);
                if (optionData != null)
                {
                    if (trade.Price > 0)
                    {
                        optionData.LastPrice = trade.Price;
                    }
                    return;
                }

                var uaData = _uaData.FirstOrDefault(ud => ud.Security.Name == trade.SecurityNameCode);
                if (uaData != null)
                {
                    if (trade.Price > 0)
                    {
                        uaData.LastPrice = trade.Price;
                    }
                }
            }
        }

        private void Connector_GlassChangeEvent(MarketDepth marketDepth)
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_locker)
            {
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

                    if (bestBid > 0)
                    {
                        optionData.Bid = bestBid;
                    }

                    if (bestAsk > 0)
                    {
                        optionData.Ask = bestAsk;
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

                    if (bestBid > 0)
                    {
                        uaData.Bid = bestBid;
                    }

                    if (bestAsk > 0)
                    {
                        uaData.Ask = bestAsk;
                    }
                }
            }
        }

        
        #endregion

        #region Highlighting and UI Updates

        

        private void UpdateGridCell(Dictionary<string, DataGridViewRow> rows, string securityName, string cellName,
            object value)
        {
            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action<Dictionary<string, DataGridViewRow>, string, string, object>(UpdateGridCell), rows, securityName, cellName, value); 
                return;
            }

            if (rows.TryGetValue(securityName, out var row))
            {
                row.Cells[cellName].Value = value;
            }
        }
        private void UpdateGridCell(Dictionary<decimal, DataGridViewRow> rows, decimal strike, string cellName, object value) 
        {
            if (_mainControl.InvokeRequired)
            {
                _mainControl.Invoke(new Action<Dictionary<decimal, DataGridViewRow>, decimal, string, object>(UpdateGridCell), rows, strike, cellName, value); 
                return;
            }

            if (rows.TryGetValue(strike, out var row))
            {
                row.Cells[cellName].Value = value;
            }
        }
        #endregion

        #region IIBotTab Implementation
        public void Clear() { }
        public void Delete() { _isDisposed = true; _updateTimer.Dispose(); foreach (var tab in _simpleTabs.Values) { tab.Delete(); } TabDeletedEvent?.Invoke(); }
        public void ShowDialog() { if (ServerMaster.GetServers() == null || ServerMaster.GetServers().Count == 0) { new AlertMessageSimpleUi(OsLocalization.Market.Message1).Show(); return; } if (_ui != null && !_ui.IsLoaded) _ui = null; if (_ui == null) { _ui = new BotTabOptionsUi(this); _ui.Closed += (sender, args) => { _ui = null; }; _ui.Show(); } else { _ui.Activate(); } }
        public void StartPaint(System.Windows.Forms.Integration.WindowsFormsHost hostChart) { hostChart.Child = _mainControl; }
        public void StopPaint() { }
        #endregion

        #region DataRow Classes
        public class StrikeDataRow { public decimal Strike { get; set; } public OptionDataRow CallData { get; set; } public OptionDataRow PutData { get; set; } }
        public class OptionDataRow { public Security Security { get; set; } public BotTabSimple SimpleTab { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } public decimal LastPrice { get; set; } public decimal Delta { get; set; } public decimal Gamma { get; set; } public decimal Vega { get; set; } public decimal Theta { get; set; } public decimal IV { get; set; } public int Quantity { get; set; } }
        public class UnderlyingAssetDataRow { public Security Security { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } public decimal LastPrice { get; set; } }
        #endregion
    }
}