/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using OsEngine.Language;
using System.Windows.Forms;
using OsEngine.Market.Connectors;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabPoligonSecurityAddUi : Window
    {
        #region Constructor

        public BotTabPoligonSecurityAddUi(ConnectorCandles connectorBot, string baseSecurity, Side side)
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
                TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
                TextBoxSearchSecurity.KeyDown += TextBoxSearchSecurity_KeyDown;
				
                CreateGridSecurities();

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null)
                {// if connection server to exchange hasn't been created yet / если сервер для подключения к бирже ещё не создан
                    Close();
                    return;
                }

                // save connectors
                // сохраняем коннекторы
                _connectorBot = connectorBot;
                _baseSecurity = baseSecurity;

                TextBoxBaseCurrency.Text = baseSecurity;

                ComboBoxOperationType.Items.Add(Side.Buy.ToString());
                ComboBoxOperationType.Items.Add(Side.Sell.ToString());
                ComboBoxOperationType.SelectedItem = side.ToString();
                ComboBoxOperationType.SelectionChanged += ComboBoxOperationType_SelectionChanged;

                OperationSide = side;

                // upload settings to controls
                // загружаем настройки в контролы
                for (int i = 0; i < servers.Count; i++)
                {
                    ComboBoxTypeServer.Items.Add(servers[i].ServerNameAndPrefix);
                }

                if (connectorBot.ServerType != ServerType.None)
                {
                    ComboBoxTypeServer.SelectedItem = connectorBot.ServerFullName;
                    _selectedServerType = connectorBot.ServerType;
                    _selectedServerName = connectorBot.ServerFullName;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerNameAndPrefix.ToString();
                    _selectedServerType = servers[0].ServerType;
                    _selectedServerName = servers[0].ServerNameAndPrefix;
                }

                if (connectorBot.StartProgram == StartProgram.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    CheckBoxIsEmulator.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester.ToString();
                    ComboBoxPortfolio.Items.Add(ServerMaster.GetServers()[0].Portfolios[0].Number);
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedServerType = ServerType.Tester;
                    _selectedServerName = ServerType.Tester.ToString();

                    ComboBoxPortfolio.IsEnabled = false;
                    ComboBoxTypeServer.IsEnabled = false;
                }
                else
                {
                    LoadPortfolioOnBox();
                }

                LoadClassOnBox();

                LoadSecurityOnBox();

                ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

                CheckBoxIsEmulator.IsChecked = _connectorBot.EmulatorIsOn;

                ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

                ComboBoxCommissionType.Items.Add(CommissionType.None.ToString());
                ComboBoxCommissionType.Items.Add(CommissionType.OneLotFix.ToString());
                ComboBoxCommissionType.Items.Add(CommissionType.Percent.ToString());
                ComboBoxCommissionType.SelectedItem = _connectorBot.CommissionType.ToString();

                TextBoxCommissionValue.Text = _connectorBot.CommissionValue.ToString();

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label7;
                Label6.Content = OsLocalization.Market.Label6;
                ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
                LabelCommissionType.Content = OsLocalization.Market.LabelCommissionType;
                LabelCommissionValue.Content = OsLocalization.Market.LabelCommissionValue;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelOperation.Content = OsLocalization.Trader.Label343;
                LabelBaseCurrency.Content = OsLocalization.Trader.Label317;

                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            Closing += ConnectorCandlesUi_Closing;

            this.Activate();
            this.Focus();
        }

        private ConnectorCandles _connectorBot;

        private string _baseSecurity;

        public Side OperationSide;

        private void ConnectorCandlesUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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

            }
            catch
            {
                // ignore
            }

            try
            {
                ComboBoxClass.SelectionChanged -= ComboBoxClass_SelectionChanged;
                ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;
                Closing -= ConnectorCandlesUi_Closing;

                DeleteGridSecurities();
            }
            catch
            {
                // ignore
            }

            _connectorBot = null;
        }

        #endregion

        #region Other income events

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string security = GetSelectedSecurity();

                if (string.IsNullOrEmpty(security))
                {
                    return;
                }

                Enum.TryParse(ComboBoxTypeServer.Text.Split('_')[0], true, out _connectorBot.ServerType);

                _connectorBot.ServerFullName = _selectedServerName;

                _connectorBot.PortfolioName = ComboBoxPortfolio.Text;

                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _connectorBot.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }

                _connectorBot.CandleCreateMethodType = "Simple";
                _connectorBot.TimeFrame = TimeFrame.Hour1;
                _connectorBot.SecurityName = security;

                Enum.TryParse(ComboBoxOperationType.Text, out OperationSide);

                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private ServerType _selectedServerType;

        private string _selectedServerName;

        private void ComboBoxOperationType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                LoadSecurityOnBox();
                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Portfolio and class controls

        void server_SecuritiesChangeEvent(List<Security> securities)
        {
            try
            {
                if (_connectorBot == null)
                {
                    return;
                }
                LoadClassOnBox();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            try
            {
                if (_connectorBot == null)
                {
                    return;
                }
                LoadPortfolioOnBox();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
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


                string portfolio = _connectorBot.PortfolioName;


                if (portfolio != null)
                {
                    ComboBoxPortfolio.Items.Add(_connectorBot.PortfolioName);
                    ComboBoxPortfolio.Text = _connectorBot.PortfolioName;
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
                if (_connectorBot.Security != null)
                {
                    ComboBoxClass.SelectedItem = _connectorBot.Security.NameClass;
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

        #endregion

        #region Securities grid

        private void LoadSecurityOnBox()
        {
            try
            {
                _gridSecurities.Rows.Clear();

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
                // стираем всё


                List<Security> securities = server.Securities;

                if (securities == null ||
                    securities.Count == 0)
                {
                    return;
                }

                string baseCurrency = TextBoxBaseCurrency.Text.ToLower();

                if (ComboBoxClass.SelectedItem != null)
                {
                    string classSec = ComboBoxClass.SelectedItem.ToString();

                    if (ComboBoxOperationType.SelectedItem == null)
                    {
                        return;
                    }

                    Side operation = Side.None;
                    Enum.TryParse(ComboBoxOperationType.SelectedItem.ToString(), out operation);

                    if (operation == Side.None)
                    {
                        return;
                    }

                    List<Security> securitiesOfMyClass = new List<Security>();

                    for (int i = 0; i < securities.Count; i++)
                    {
                        if (securities[i].NameClass == classSec)
                        {
                            string secName = securities[i].Name.ToLower().Replace(".txt", "");

                            if (operation == Side.Buy && secName.EndsWith(baseCurrency))
                            {
                                securitiesOfMyClass.Add(securities[i]);
                            }
                            else if (operation == Side.Sell && secName.StartsWith(baseCurrency))
                            {
                                securitiesOfMyClass.Add(securities[i]);
                            }
                        }
                    }

                    securities = securitiesOfMyClass;
                }

                UpdateGridSec(securities);

                UpdateSearchResults();
                UpdateSearchPanel();

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        DataGridView _gridSecurities;

        private void DeleteGridSecurities()
        {
            DataGridFactory.ClearLinks(_gridSecurities);
            _gridSecurities.CellClick -= _gridSecurities_CellClick;
            _gridSecurities.DataError -= _gridSecurities_DataError;
            SecurityTable.Child = null;
        }

        private void CreateGridSecurities()
        {
            // номер, тип, сокращонное название бумаги, полное имя, влк/выкл

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
            colum0.Width = 50;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label167;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label169;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label168;
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            DataGridViewCheckBoxColumn colum7 = new DataGridViewCheckBoxColumn();
            colum7.HeaderText = OsLocalization.Trader.Label171;
            colum7.ReadOnly = false;
            colum7.Width = 50;
            newGrid.Columns.Add(colum7);

            _gridSecurities = newGrid;
            SecurityTable.Child = _gridSecurities;

            _gridSecurities.CellClick += _gridSecurities_CellClick;
            _gridSecurities.DataError += _gridSecurities_DataError;
        }

        private void _gridSecurities_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void _gridSecurities_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                _gridSecurities.ClearSelection();

                int columnInd = e.ColumnIndex;
                int rowInd = e.RowIndex;

                for (int i = 0; i < _gridSecurities.RowCount; i++)
                {
                    if (i == rowInd)
                    {
                        for (int y = 0; y < _gridSecurities.ColumnCount; y++)
                        {
                            _gridSecurities.Rows[rowInd].Cells[y].Style.ForeColor = System.Drawing.ColorTranslator.FromHtml("#ffffff");
                        }
                    }
                    else
                    {
                        for (int y = 0; y < _gridSecurities.ColumnCount; y++)
                        {
                            _gridSecurities.Rows[i].Cells[y].Style.ForeColor = System.Drawing.ColorTranslator.FromHtml("#FFA1A1A1");
                        }
                    }
                }

                if (columnInd != 4)
                {
                    return;
                }

                for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                {

                    DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                    if (checkBox.Value == null)
                    {
                        continue;
                    }

                    if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                    {
                        checkBox.Value = false;

                        break;
                    }
                }

                DataGridViewCheckBoxCell checkBoxActive = (DataGridViewCheckBoxCell)_gridSecurities.Rows[rowInd].Cells[4];
                checkBoxActive.Value = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateGridSec(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            _gridSecurities.ClearSelection();

            if (securities == null
                || securities.Count == 0)
            {
                return;
            }

            // номер, тип, сокращонное название бумаги, полное имя, площадка, влк/выкл

            string selectedName = _connectorBot.SecurityName;
            string selectedClass = _connectorBot.SecurityClass;

            if (string.IsNullOrEmpty(selectedClass) &&
                _connectorBot.Security != null)
            {
                selectedClass = _connectorBot.Security.NameClass;
            }

            if (string.IsNullOrEmpty(selectedClass) &&
                  string.IsNullOrEmpty(ComboBoxClass.Text) == false)
            {
                selectedClass = ComboBoxClass.Text;
            }

            int selectedRow = 0;

            for (int indexSecuriti = 0; indexSecuriti < securities.Count; indexSecuriti++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = (indexSecuriti + 1).ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecuriti].SecurityType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecuriti].Name;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecuriti].NameFull;

                DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
                nRow.Cells.Add(checkBox);


                if (securities[indexSecuriti].NameClass == selectedClass
                        &&
                       securities[indexSecuriti].Name == selectedName)
                {
                    checkBox.Value = true;
                    selectedRow = indexSecuriti;
                }

                _gridSecurities.Rows.Add(nRow);
            }

            _gridSecurities.Rows[selectedRow].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = selectedRow;
        }

        private string GetSelectedSecurity()
        {
            string security = "";

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                if (checkBox.Value == null)
                {
                    continue;
                }

                if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                {
                    security = _gridSecurities.Rows[i].Cells[2].Value.ToString();

                    break;
                }
            }

            return security;
        }

        #endregion

        #region Search in securities grid

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == ""
                    && TextBoxSearchSecurity.IsKeyboardFocused == false)
                {
                    TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
                {
                    TextBoxSearchSecurity.Text = "";
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == "")
                {
                    TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            try
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

                    if (_gridSecurities.Rows[i].Cells[2].Value != null)
                    {
                        security = _gridSecurities.Rows[i].Cells[2].Value.ToString();
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSearchPanel()
        {
            try
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

                    DataGridViewCheckBoxCell checkBox;
                    for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                    {
                        checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                        if (checkBox.Value == null)
                        {
                            continue;
                        }
                        if (i == rowIndex)
                        {
                            continue;
                        }
                        if (Convert.ToBoolean(checkBox.Value) == true)
                        {
                            checkBox.Value = false;
                            break;
                        }
                    }

                    checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[rowIndex].Cells[4];
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

        #region Logging

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