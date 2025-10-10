/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabPairAutoSelectPairsUi : Window
    {
        public BotTabPairAutoSelectPairsUi(BotTabPair connectorBot)
        {
            try
            {
                InitializeComponent();
                OsEngine.Layout.StickyBorders.Listen(this);
                OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null)
                {// if connection server to exchange hasn't been created yet
                    Close();
                    return;
                }

                // save connectors
                _pairTrader = connectorBot;

                // upload settings to controls
                for (int i = 0; i < servers.Count; i++)
                {
                    ComboBoxTypeServer.Items.Add(servers[i].ServerNameAndPrefix);
                }

                if (connectorBot.Pairs.Count != 0 &&
                    connectorBot.Pairs[0].Tab1.Connector.ServerType != ServerType.None)
                {
                    ComboBoxTypeServer.SelectedItem = connectorBot.Pairs[0].Tab1.Connector.ServerFullName;
                    _selectedServerType = connectorBot.Pairs[0].Tab1.Connector.ServerType;
                    _selectedServerName = connectorBot.Pairs[0].Tab1.Connector.ServerFullName;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerNameAndPrefix;
                    _selectedServerType = servers[0].ServerType;
                    _selectedServerName = servers[0].ServerNameAndPrefix;
                }

                if (connectorBot.StartProgram == StartProgram.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    CheckBoxIsEmulator.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester.ToString();
                    //ComboBoxClass.SelectedItem = ServerMaster.GetServers()[0].Securities[0].NameClass;
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;
                    ComboBoxPortfolio.IsEnabled = false;

                    _selectedServerType = ServerType.Tester;
                    _selectedServerName = ServerType.Tester.ToString();
                }

                CreateGrid();

                CheckBoxSaveAlreadyCreatedPairs.IsChecked = true;

                CreatePairsGrid();
                UpdatePairsGrid();

                LoadClassOnBox();

                LoadSecurityOnBox();

                LoadPortfolioOnBox();

                ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

                CheckBoxIsEmulator.IsChecked = _pairTrader.EmulatorIsOn;

                ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

                LoadTimeFrameBox();

                TextBoxMaxPairsToSecurity.Text = "5";

                ComboBoxCommissionType.Items.Add(CommissionType.None.ToString());
                ComboBoxCommissionType.Items.Add(CommissionType.OneLotFix.ToString());
                ComboBoxCommissionType.Items.Add(CommissionType.Percent.ToString());
                ComboBoxCommissionType.SelectedItem = CommissionType.None.ToString();

                TextBoxCommissionValue.Text = "0";

                CheckBoxSaveAlreadyCreatedPairs.IsChecked = true;

                Title = OsLocalization.Trader.Label257;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label5;
                Label6.Content = OsLocalization.Market.Label6;
                LabelTimeFrame.Content = OsLocalization.Market.Label10;

                LabelCommissionType.Content = OsLocalization.Market.LabelCommissionType;
                LabelCommissionValue.Content = OsLocalization.Market.LabelCommissionValue;
                CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
                CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelSecurities.Content = OsLocalization.Market.Label66;
                CheckBoxSaveAlreadyCreatedPairs.Content = OsLocalization.Trader.Label258;
                LabelTextBoxMaxPairsToSecurity.Content = OsLocalization.Trader.Label259;
                LabelPairs.Content = OsLocalization.Trader.Label260;
                ButtonConvertToPairs.Content = OsLocalization.Trader.Label261;
                LabelConverter.Content = OsLocalization.Trader.Label263;
                ButtonAccept.Content = OsLocalization.Trader.Label264;

                CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
                TextBoxSearchSecurity.KeyDown += TextBoxSearchSecurity_KeyDown;

                Closed += BotTabScreenerUi_Closed;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            this.Activate();
            this.Focus();

            _pairTrader.TabDeletedEvent += _pairTrader_TabDeletedEvent;
        }

        private void _pairTrader_TabDeletedEvent()
        {
            Close();
        }

        private void BotTabScreenerUi_Closed(object sender, EventArgs e)
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                for (int i = 0; serversAll != null && i < serversAll.Count; i++)
                {
                    if (serversAll[i] == null)
                    {
                        continue;
                    }
                    serversAll[i].SecuritiesChangeEvent -= server_SecuritiesChangeEvent;
                    serversAll[i].PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                }

                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                ComboBoxClass.SelectionChanged -= ComboBoxClass_SelectionChanged;
                ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;
                CheckBoxSelectAllCheckBox.Click -= CheckBoxSelectAllCheckBox_Click;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;

                Closed -= BotTabScreenerUi_Closed;

                if (SecuritiesHost != null)
                {
                    SecuritiesHost.Child = null;
                }

                if (_gridSecurities != null)
                {
                    DataGridFactory.ClearLinks(_gridSecurities);
                    _gridSecurities.DataError -= _gridSecurities_DataError;
                    _gridSecurities = null;
                }

                _pairTrader.TabDeletedEvent -= _pairTrader_TabDeletedEvent;
                _pairTrader = null;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private BotTabPair _pairTrader;

        private void ComboBoxSecurities_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            GetTimeFramesInTester();
        }

        private void GetTimeFramesInTester()
        {
            TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

            if (server.TypeTesterData != TesterDataType.Candle)
            {
                return;
            }

            string lastTf = null;

            if (ComboBoxTimeFrame.SelectedItem != null)
            {
                lastTf = ComboBoxTimeFrame.SelectedItem.ToString();
            }

            ComboBoxTimeFrame.Items.Clear();

            List<SecurityTester> securities = server.SecuritiesTester;

            if (securities == null)
            {
                return;
            }

            List<string> frames = new List<string>();

            for (int i = 0; i < securities.Count; i++)
            {
                if (frames.Find(f => f == securities[i].TimeFrame.ToString()) == null)
                {
                    frames.Add(securities[i].TimeFrame.ToString());
                }
            }

            for (int i = 0; i < frames.Count; i++)
            {
                ComboBoxTimeFrame.Items.Add(frames[i]);
            }

            if (lastTf == null)
            {
                ComboBoxTimeFrame.SelectedItem = securities[0].TimeFrame.ToString();
            }
            else
            {
                TimeFrame oldFrame;
                Enum.TryParse(lastTf, out oldFrame);

                ComboBoxTimeFrame.SelectedItem = oldFrame;
            }
        }

        private ServerType _selectedServerType;

        private string _selectedServerName;

        private void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBoxTypeServer.SelectedValue == null)
                {
                    return;
                }

                string serverName = ComboBoxTypeServer.SelectedValue.ToString();

                ServerType serverType;
                if (Enum.TryParse(serverName.Split('_')[0], out serverType) == false)
                {
                    return;
                }

                _selectedServerType = serverType;
                _selectedServerName = serverName;

                if (_selectedServerType == ServerType.None)
                {
                    return;
                }

                List<IServer> serversAll = ServerMaster.GetServers();

                if (serversAll == null ||
                    serversAll.Count == 0)
                {
                    return;
                }

                IServer server =
                    serversAll.Find(
                        server1 =>
                        server1.ServerType == _selectedServerType
                        && server1.ServerNameAndPrefix == _selectedServerName);

                if (server != null)
                {
                    server.SecuritiesChangeEvent -= server_SecuritiesChangeEvent;
                    server.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                    server.SecuritiesChangeEvent += server_SecuritiesChangeEvent;
                    server.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
                }

                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxClass_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
        }

        private void server_SecuritiesChangeEvent(List<Security> securities)
        {
            LoadClassOnBox();
        }

        private void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            LoadPortfolioOnBox();
        }

        private void LoadPortfolioOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                    serversAll.Find(server1 =>
                    server1.ServerType == _selectedServerType
                    && server1.ServerNameAndPrefix == _selectedServerName);

                if (server == null)
                {
                    return;
                }

                if (!ComboBoxClass.CheckAccess())
                {
                    ComboBoxClass.Dispatcher.Invoke(LoadPortfolioOnBox);
                    return;
                }

                string curPortfolio = null;

                if (ComboBoxPortfolio.SelectedItem != null)
                {
                    curPortfolio = ComboBoxPortfolio.SelectedItem.ToString();
                }

                ComboBoxPortfolio.Items.Clear();

                if (_pairTrader.Pairs.Count != 0)
                {
                    string portfolio = _pairTrader.Pairs[0].Tab1.Connector.PortfolioName;

                    if (string.IsNullOrEmpty(portfolio) == false)
                    {
                        ComboBoxPortfolio.Items.Add(portfolio);
                        ComboBoxPortfolio.Text = portfolio;
                    }
                }

                List<Portfolio> portfolios = server.Portfolios;

                if (portfolios == null)
                {
                    return;
                }

                for (int i = 0; i < portfolios.Count; i++)
                {
                    bool isInArray = false;

                    for (int i2 = 0; i2 < ComboBoxPortfolio.Items.Count; i2++)
                    {
                        if (ComboBoxPortfolio.Items[i2].ToString() == portfolios[i].Number)
                        {
                            isInArray = true;
                        }
                    }

                    if (isInArray == true)
                    {
                        continue;
                    }
                    ComboBoxPortfolio.Items.Add(portfolios[i].Number);
                }
                if (curPortfolio != null)
                {
                    for (int i = 0; i < ComboBoxPortfolio.Items.Count; i++)
                    {
                        if (ComboBoxPortfolio.Items[i].ToString() == curPortfolio)
                        {
                            ComboBoxPortfolio.SelectedItem = curPortfolio;
                            break;
                        }
                    }
                }

                if (ComboBoxPortfolio.SelectedItem == null
                    && ComboBoxPortfolio.Items.Count != 0)
                {
                    ComboBoxPortfolio.SelectedItem = ComboBoxPortfolio.Items[0];
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LoadClassOnBox()
        {
            try
            {
                if (!ComboBoxClass.Dispatcher.CheckAccess())
                {
                    ComboBoxClass.Dispatcher.Invoke(LoadClassOnBox);
                    return;
                }
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                    serversAll.Find(server1 =>
                    server1.ServerType == _selectedServerType
                    && server1.ServerNameAndPrefix == _selectedServerName);

                if (server == null)
                {
                    return;
                }

                var securities = server.Securities;

                ComboBoxClass.Items.Clear();

                if (securities == null)
                {
                    return;
                }

                for (int i1 = 0; i1 < securities.Count; i1++)
                {
                    if (securities[i1] == null)
                    {
                        continue;
                    }
                    string clas = securities[i1].NameClass;
                    if (ComboBoxClass.Items.IndexOf(clas) == -1)
                        ComboBoxClass.Items.Add(clas);
                }

                if (_pairTrader.Pairs.Count > 0)
                {
                    string firstClass = _pairTrader.Pairs[0].Tab1.Connector.SecurityClass;

                    if (string.IsNullOrEmpty(firstClass) == false)
                    {
                        ComboBoxClass.SelectedItem = firstClass;
                    }
                }

                if (ComboBoxClass.SelectedItem == null
                    && ComboBoxClass.Items.Count != 0)
                {
                    ComboBoxClass.SelectedItem = ComboBoxClass.Items[0];
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Work with papers on the grid

        private void LoadSecurityOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                    serversAll.Find(server1 =>
                    server1.ServerType == _selectedServerType
                    && server1.ServerNameAndPrefix == _selectedServerName);

                if (server == null)
                {
                    return;
                }

                // clear all

                // download available instruments

                var securities = server.Securities;

                List<Security> securitiesToLoad = new List<Security>();

                if (securities != null)
                {
                    for (int i = 0; i < securities.Count; i++)
                    {
                        if (securities[i] == null)
                        {
                            continue;
                        }
                        string classSec = securities[i].NameClass;
                        if (ComboBoxClass.SelectedItem != null && ComboBoxClass.SelectedItem.Equals(classSec))
                        {
                            securitiesToLoad.Add(securities[i]);
                        }
                    }
                }

                // download already running instruments

                UpdateGrid(securitiesToLoad);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DataGridView _gridSecurities;

        private void CreateGrid()
        {
            // number, class, type, paper abbreviation, full name, additional name, on/off

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label165;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label166;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label167;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label168;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label169;
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Trader.Label170;
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum5);

            DataGridViewCheckBoxColumn colum6 = new DataGridViewCheckBoxColumn();
            //colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label171;
            colum6.ReadOnly = false;
            colum6.Width = 50;
            newGrid.Columns.Add(colum6);

            _gridSecurities = newGrid;
            SecuritiesHost.Child = _gridSecurities;

            _gridSecurities.DataError += _gridSecurities_DataError;
        }

        private void _gridSecurities_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void UpdateGrid(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            // number, class, type, paper abbreviation, full name, additional name, on/off

            List<string> alreadyActiveatedSecurity = _pairTrader.SecuritiesActivated;

            for (int indexSecuriti = 0; indexSecuriti < securities.Count; indexSecuriti++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = (indexSecuriti + 1).ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecuriti].NameClass;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecuriti].SecurityType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecuriti].Name;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = securities[indexSecuriti].NameFull;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = securities[indexSecuriti].NameId;

                DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
                nRow.Cells.Add(checkBox);

                bool activatedSecurity = false;

                for (int i = 0; alreadyActiveatedSecurity != null && i < alreadyActiveatedSecurity.Count; i++)
                {
                    if (alreadyActiveatedSecurity[i].Equals(securities[indexSecuriti].Name))
                    {
                        activatedSecurity = true;
                        break;
                    }
                }

                if (activatedSecurity == true)
                {
                    checkBox.Value = true;
                }

                _gridSecurities.Rows.Add(nRow);
            }
        }

        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                _gridSecurities.Rows[i].Cells[6].Value = isCheck;
            }
        }

        private void LoadTimeFrameBox()
        {
            ComboBoxTimeFrame.Items.Clear();

            if (_pairTrader.StartProgram == StartProgram.IsTester)
            {
                // Timeframe

                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                if (server.TypeTesterData != TesterDataType.Candle)
                {
                    // if we build data on ticks or depths, then any Timeframe can be used
                    // candle manager builds any Timeframe
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Day);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour4);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour1);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min45);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min30);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min20);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min15);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min10);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min5);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min3);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min1);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec30);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec20);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec15);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec10);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec5);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec1);

                }
                else
                {
                    // then if we use ready-made candles, then we need to use only those Timeframe that are
                    // and they are inserted only when we select the security in the method

                    GetTimeFramesInTester();
                }
            }
            else
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                    serversAll.Find(server1 =>
                    server1.ServerType == _selectedServerType
                    && server1.ServerNameAndPrefix == _selectedServerName);

                IServerPermission permission = ServerMaster.GetServerPermission(_selectedServerType);

                if (server == null
                    || permission == null)
                {
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Day);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour4);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Hour1);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min45);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min30);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min20);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min15);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min10);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min5);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min3);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Min1);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec30);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec20);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec15);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec10);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec5);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec2);
                    ComboBoxTimeFrame.Items.Add(TimeFrame.Sec1);
                }
                else
                {
                    if (permission.TradeTimeFramePermission.TimeFrameDayIsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Day);

                    if (permission.TradeTimeFramePermission.TimeFrameHour4IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Hour4);
                    if (permission.TradeTimeFramePermission.TimeFrameHour2IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Hour2);

                    if (permission.TradeTimeFramePermission.TimeFrameHour1IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Hour1);

                    if (permission.TradeTimeFramePermission.TimeFrameMin45IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min45);

                    if (permission.TradeTimeFramePermission.TimeFrameMin30IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min30);

                    if (permission.TradeTimeFramePermission.TimeFrameMin20IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min20);

                    if (permission.TradeTimeFramePermission.TimeFrameMin15IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min15);

                    if (permission.TradeTimeFramePermission.TimeFrameMin10IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min10);

                    if (permission.TradeTimeFramePermission.TimeFrameMin5IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min5);

                    if (permission.TradeTimeFramePermission.TimeFrameMin3IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min3);

                    if (permission.TradeTimeFramePermission.TimeFrameMin2IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min2);

                    if (permission.TradeTimeFramePermission.TimeFrameMin1IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Min1);

                    if (permission.TradeTimeFramePermission.TimeFrameSec30IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec30);

                    if (permission.TradeTimeFramePermission.TimeFrameSec20IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec20);

                    if (permission.TradeTimeFramePermission.TimeFrameSec15IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec15);

                    if (permission.TradeTimeFramePermission.TimeFrameSec10IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec10);

                    if (permission.TradeTimeFramePermission.TimeFrameSec5IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec5);

                    if (permission.TradeTimeFramePermission.TimeFrameSec2IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec2);

                    if (permission.TradeTimeFramePermission.TimeFrameSec1IsOn)
                        ComboBoxTimeFrame.Items.Add(TimeFrame.Sec1);
                }

            }

            ComboBoxTimeFrame.SelectedItem = TimeFrame.Min15;

            if (ComboBoxTimeFrame.SelectedItem == null)
            {
                ComboBoxTimeFrame.SelectedItem = TimeFrame.Min1;
            }
        }

        private ActivatedSecurity GetSecurity(DataGridViewRow row)
        {
            ActivatedSecurity sec = new ActivatedSecurity();
            sec.SecurityClass = row.Cells[1].Value.ToString();
            sec.SecurityName = row.Cells[3].Value.ToString();

            if (row.Cells[6].Value != null)
            {
                sec.IsOn = Convert.ToBoolean(row.Cells[6].Value);
            }

            return sec;
        }

        #endregion

        #region Auto creation pairs

        private DataGridView _pairsGrid;

        private void CreatePairsGrid()
        {
            _pairsGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            _pairsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _pairsGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = _pairsGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn(); // номер
            colum0.CellTemplate = cell0;
            colum0.ReadOnly = true;
            colum0.Width = 50;
            _pairsGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn(); // пара
            colum1.CellTemplate = cell0;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _pairsGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn(); // удалить
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label167;
            colum2.ReadOnly = true;
            colum2.Width = 100;
            _pairsGrid.Columns.Add(colum2);

            HostPairs.Child = _pairsGrid;

            _pairsGrid.CellClick += _pairsGrid_CellClick;
            _pairsGrid.DataError += _gridSecurities_DataError;
        }

        private void _pairsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int row = e.RowIndex;
            int col = e.ColumnIndex;

            if (col == 2)
            {
                if (row >= _pairsGrid.Rows.Count)
                {
                    return;
                }
                _pairsGrid.Rows.RemoveAt(row);
            }
        }

        private void ButtonConvertToPairs_Click(object sender, RoutedEventArgs e)
        {
            CreatePairNames();
            UpdatePairsGrid();
        }

        private void CreatePairNames()
        {
            _pairNamesNew.Clear();

            List<string> securities = new List<string>();

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                ActivatedSecurity sec = GetSecurity(_gridSecurities.Rows[i]);

                if (sec == null)
                {
                    continue;
                }

                if (sec.IsOn == false)
                {
                    continue;
                }

                securities.Add(sec.SecurityName);
            }

            int maxOneNamePairsCount = 5;

            try
            {
                maxOneNamePairsCount = Convert.ToInt32(TextBoxMaxPairsToSecurity.Text);
            }
            catch
            {
                // ignore
            }

            if (maxOneNamePairsCount == 0)
            {
                return;
            }

            for (int i = 0; i < securities.Count; i++)
            {
                List<string> newPairs = GetPairsToSecurity(securities[i], securities, _pairNamesNew, maxOneNamePairsCount);

                if (newPairs == null ||
                    newPairs.Count == 0)
                {
                    continue;
                }

                _pairNamesNew.AddRange(newPairs);
            }
        }

        private List<string> GetPairsToSecurity(string security, List<string> securities, List<string> pairs, int maxCountPairsWithOneName)
        {
            List<string> result = new List<string>();

            int countNamesInExistPairs = 0;

            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Contains(security))
                {
                    countNamesInExistPairs++;
                }
            }

            if (countNamesInExistPairs >= maxCountPairsWithOneName)
            {
                return result;
            }

            for (int i = 0; i < securities.Count; i++)
            {
                string curSec = securities[i];

                if (countNamesInExistPairs >= maxCountPairsWithOneName)
                {
                    break;
                }

                if (curSec.Equals(security))
                {
                    continue;
                }

                int curSecInPairsCount = 0;

                for (int i2 = 0; i2 < pairs.Count; i2++)
                {
                    if (pairs[i2].Contains(curSec))
                    {
                        curSecInPairsCount++;
                    }

                }

                if (curSecInPairsCount >= maxCountPairsWithOneName)
                {
                    continue;
                }

                string newPair = security + "_|_" + curSec;
                string newPair2 = curSec + "_|_" + security;

                bool isInArray = false;

                for (int i2 = 0; i2 < pairs.Count; i2++)
                {
                    if (pairs[i2].Equals(newPair))
                    {
                        isInArray = true;
                    }
                    if (pairs[i2].Equals(newPair2))
                    {
                        isInArray = true;
                    }
                }

                if (isInArray == false)
                {
                    result.Add(newPair);
                    countNamesInExistPairs++;
                }
            }

            return result;
        }

        private List<string> _pairNamesNew = new List<string>();

        private void UpdatePairsGrid()
        {
            try
            {
                _pairsGrid.Rows.Clear();

                if (CheckBoxSaveAlreadyCreatedPairs.IsChecked.Value)
                {
                    List<string> alreadyCreatedPairs = _pairTrader.CreatedPairs;

                    for (int i = 0; alreadyCreatedPairs != null && i < alreadyCreatedPairs.Count; i++)
                    {
                        _pairsGrid.Rows.Add(GetPairRow(alreadyCreatedPairs[i], _pairsGrid.Rows.Count + 1));
                    }
                }

                for (int i = 0; _pairNamesNew != null && i < _pairNamesNew.Count; i++)
                {

                    if (CheckBoxSaveAlreadyCreatedPairs.IsChecked.Value)
                    {
                        List<string> alreadyCreatedPairs = _pairTrader.CreatedPairs;

                        bool isInArray = false;
                        string name1 = _pairNamesNew[i];
                        string name2 = _pairNamesNew[i].Replace("_|_", "*");
                        name2 = name2.Split('*')[1] + "_|_" + name2.Split('*')[0];

                        for (int i2 = 0; alreadyCreatedPairs != null && i2 < alreadyCreatedPairs.Count; i2++)
                        {
                            string curName = alreadyCreatedPairs[i2];

                            if (curName.Equals(name1)
                                || curName.Equals(name2))
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == true)
                        {
                            continue;
                        }
                    }

                    _pairsGrid.Rows.Add(GetPairRow(_pairNamesNew[i], _pairsGrid.Rows.Count + 1));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

        }

        private DataGridViewRow GetPairRow(string name, int num)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = name;

            DataGridViewButtonCell button1 = new DataGridViewButtonCell(); // авто создание пар
            button1.Value = OsLocalization.Trader.Label39;
            row.Cells.Add(button1);

            return row;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TimeFrame timeFrame;
                Enum.TryParse(ComboBoxTimeFrame.Text, out timeFrame);

                string serverName = ComboBoxTypeServer.Text;

                ServerType server;
                Enum.TryParse(serverName.Split('_')[0], true, out server);

                CommissionType typeCommission;
                Enum.TryParse(ComboBoxCommissionType.Text, true, out typeCommission);

                decimal commissionValue = 0;

                try
                {
                    commissionValue = TextBoxCommissionValue.Text.ToDecimal();
                }
                catch
                {
                    // ignore
                }

                string secClass = ComboBoxClass.Text;

                if (string.IsNullOrEmpty(secClass))
                {
                    MessageBox.Show("Creation pairs is Stop. Securities class is null");
                    return;
                }

                string portfolio = ComboBoxPortfolio.Text;

                if (string.IsNullOrEmpty(portfolio))
                {
                    MessageBox.Show("Creation pairs is Stop. Portfolio is null");
                    return;
                }

                for (int i = 0; i < _pairNamesNew.Count; i++)
                {
                    string sec1 = _pairNamesNew[i].Replace("_|_", "*").Split('*')[0];
                    string sec2 = _pairNamesNew[i].Replace("_|_", "*").Split('*')[1];

                    if (_pairTrader.HaveThisPairInTrade(sec1, sec2, secClass, timeFrame, server, serverName) == true)
                    {
                        continue;
                    }

                    _pairTrader.CreateNewPair(sec1, sec2, secClass, timeFrame,
                        server, typeCommission, commissionValue, portfolio, serverName);

                }

                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _pairTrader.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }

                _pairTrader.ApplySettingsFromStandardToAll();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Search by securities table

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == ""
                && TextBoxSearchSecurity.IsKeyboardFocused == false)
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
            {
                TextBoxSearchSecurity.Text = "";
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == "")
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        private List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            _searchResults.Clear();

            string key = TextBoxSearchSecurity.Text;

            if (key == "")
            {
                UpdateSearchPanel();
                return;
            }

            key = key.ToLower();

            int indexFirstSec = int.MaxValue;

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                string security = "";
                string secSecond = "";

                if (_gridSecurities.Rows[i].Cells[4].Value != null)
                {
                    security = _gridSecurities.Rows[i].Cells[4].Value.ToString();
                }

                if (_gridSecurities.Rows[i].Cells[3].Value != null)
                {
                    secSecond = _gridSecurities.Rows[i].Cells[3].Value.ToString();
                }

                security = security.ToLower();
                secSecond = secSecond.ToLower();

                if (security.Contains(key) || secSecond.Contains(key))
                {
                    if (security.IndexOf(key) == 0 || secSecond.IndexOf(key) == 0)
                    {
                        indexFirstSec = i;
                    }

                    _searchResults.Add(i);
                }
            }

            if (_searchResults.Count > 1 && _searchResults.Contains(indexFirstSec) && _searchResults.IndexOf(indexFirstSec) != 0)
            {
                int index = _searchResults.IndexOf(indexFirstSec);
                _searchResults.RemoveAt(index);
                _searchResults.Insert(0, indexFirstSec);
            }
        }

        private void UpdateSearchPanel()
        {
            if (_searchResults.Count == 0)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            int firstRow = _searchResults[0];

            _gridSecurities.Rows[firstRow].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = firstRow;

            if (_searchResults.Count < 2)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            LabelCurrentResultShow.Content = 1.ToString();
            LabelCountResultsShow.Content = (_searchResults.Count).ToString();

            ButtonRightInSearchResults.Visibility = Visibility.Visible;
            ButtonLeftInSearchResults.Visibility = Visibility.Visible;
            LabelCurrentResultShow.Visibility = Visibility.Visible;
            LabelCommasResultShow.Visibility = Visibility.Visible;
            LabelCountResultsShow.Visibility = Visibility.Visible;
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow <= 0)
            {
                indexRow = maxRowIndex;
                LabelCurrentResultShow.Content = maxRowIndex.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow).ToString();
            }

            int realInd = _searchResults[indexRow - 1];

            _gridSecurities.Rows[realInd].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1 + 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow >= maxRowIndex)
            {
                indexRow = 0;
                LabelCurrentResultShow.Content = 1.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow + 1).ToString();
            }

            int realInd = _searchResults[indexRow];

            _gridSecurities.Rows[realInd].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void TextBoxSearchSecurity_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    int rowIndex = 0;
                    for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                    {
                        if (_gridSecurities.Rows[i].Selected == true)
                        {
                            rowIndex = i;
                            break;
                        }

                        if (i == _gridSecurities.Rows.Count - 1)
                        {
                            return;
                        }
                    }

                    DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[rowIndex].Cells[6];
                    if (Convert.ToBoolean(checkBox.Value) == false)
                    {
                        checkBox.Value = true;
                        TextBoxSearchSecurity.Text = "";
                    }
                    else
                    {
                        checkBox.Value = false;
                        TextBoxSearchSecurity.Text = "";
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Work with papers on the grid

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}