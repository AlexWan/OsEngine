/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabPolygonAutoSelectSequenceUi : Window
    {
        private BotTabPolygon _tabPolygon;

        public BotTabPolygonAutoSelectSequenceUi(BotTabPolygon tabPolygon)
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

            _tabPolygon = tabPolygon;

            // upload settings to controls
            for (int i = 0; i < servers.Count; i++)
            {
                ComboBoxTypeServer.Items.Add(servers[i].ServerNameAndPrefix);
            }

            if (_tabPolygon.Sequences.Count != 0 &&
               _tabPolygon.Sequences[0].Tab1.Connector.ServerType != ServerType.None)
            {
                ComboBoxTypeServer.SelectedItem = _tabPolygon.Sequences[0].Tab1.Connector.ServerFullName.ToString();
                _selectedServerType = _tabPolygon.Sequences[0].Tab1.Connector.ServerType;
                _selectedServerName = _tabPolygon.Sequences[0].Tab1.Connector.ServerFullName;
            }
            else
            {
                ComboBoxTypeServer.SelectedItem = servers[0].ServerType.ToString();
                _selectedServerType = servers[0].ServerType;
                _selectedServerName = servers[0].ServerNameAndPrefix;
            }


            TextBoxBaseCurrency.Text = _tabPolygon.AutoCreatorSequenceBaseCurrency;
            TextBoxBaseCurrency.TextChanged += TextBoxBaseCurrency_TextChanged;
            TextBoxSeparatorToSecurities.Text = _tabPolygon.AutoCreatorSequenceSeparator;
            TextBoxSeparatorToSecurities.TextChanged += TextBoxSeparatorToSecurities_TextChanged;

            CreateGridFirstStep();
            LoadSecurityOnBox();

            CreateGridSecondStep();
            CreateGridThirdStep();

            LoadPortfolioOnBox();

            ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

            // localization

            Title = OsLocalization.Trader.Label355;
            Label1.Content = OsLocalization.Market.Label1;
            Label3.Content = OsLocalization.Market.Label3;
            LabelFirstStepSecurities.Content = OsLocalization.Trader.Label350;

            CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
            CheckBoxSelectAllInSecondStep.Content = OsLocalization.Trader.Label173;
            CheckBoxSelectAllInFinalStep.Content = OsLocalization.Trader.Label173;

            TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            LabelSecondStep.Content = OsLocalization.Trader.Label351;
            LabelFinalSequence.Content = OsLocalization.Trader.Label352;
            ButtonCreateTableFinal.Content = OsLocalization.Trader.Label353;
            ButtonCreateTableSecondStep.Content = OsLocalization.Trader.Label353;
            ButtonCreateSelectedSequence.Content = OsLocalization.Trader.Label354;
            LabelBaseCurrency.Content = OsLocalization.Trader.Label317;
            LabelSeparator.Content = OsLocalization.Trader.Label319;

            CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
            CheckBoxSelectAllInSecondStep.Click += CheckBoxSelectAllInSecondStep_Click;
            CheckBoxSelectAllInFinalStep.Click += CheckBoxSelectAllInFinalStep_Click;
            ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
            TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
            ButtonCreateTableSecondStep.Click += ButtonCreateTableSecondStep_Click;
            ButtonCreateTableFinal.Click += ButtonCreateTableFinal_Click;
            ButtonCreateSelectedSequence.Click += ButtonCreateSelectedSequence_Click;
            TextBoxSearchSecurity.KeyDown += TextBoxSearchSecurity_KeyDown;

            Closed += BotTabPolygonAutoSelectSequenceUi_Closed;
        }

        public bool IsClosed;

        private void BotTabPolygonAutoSelectSequenceUi_Closed(object sender, EventArgs e)
        {
            try
            {
                IsClosed = true;

                ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;
                TextBoxBaseCurrency.TextChanged -= TextBoxBaseCurrency_TextChanged;
                CheckBoxSelectAllCheckBox.Click -= CheckBoxSelectAllCheckBox_Click;
                CheckBoxSelectAllInSecondStep.Click -= CheckBoxSelectAllInSecondStep_Click;
                CheckBoxSelectAllInFinalStep.Click -= CheckBoxSelectAllInFinalStep_Click;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                ButtonCreateTableSecondStep.Click -= ButtonCreateTableSecondStep_Click;
                ButtonCreateTableFinal.Click -= ButtonCreateTableFinal_Click;
                ButtonCreateSelectedSequence.Click -= ButtonCreateSelectedSequence_Click;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;
                Closed -= BotTabPolygonAutoSelectSequenceUi_Closed;

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

                _tabPolygon = null;

                if (HostFirdStep != null)
                {
                    HostFirdStep.Child = null;
                }

                if (SecuritiesHost != null)
                {
                    SecuritiesHost.Child = null;
                }

                if (SecuritiesSecondStep != null)
                {
                    SecuritiesSecondStep.Child = null;
                }

                if (_gridSecuritiesFirstStep != null)
                {
                    DataGridFactory.ClearLinks(_gridSecuritiesFirstStep);
                    _gridSecuritiesFirstStep.DataError -= _gridSecuritiesFirstStep_DataError;
                    _gridSecuritiesFirstStep.Rows.Clear();
                    _gridSecuritiesFirstStep.Columns.Clear();
                    _gridSecuritiesFirstStep = null;
                }

                if (_gridSecondStep != null)
                {
                    DataGridFactory.ClearLinks(_gridSecondStep);
                    _gridSecondStep.DataError -= _gridSecuritiesFirstStep_DataError;
                    _gridSecondStep.Rows.Clear();
                    _gridSecondStep.Columns.Clear();
                    _gridSecondStep = null;
                }

                if (_gridThirdStep != null)
                {
                    DataGridFactory.ClearLinks(_gridThirdStep);
                    _gridThirdStep.DataError -= _gridSecuritiesFirstStep_DataError;
                    _gridThirdStep.Rows.Clear();
                    _gridThirdStep.Columns.Clear();
                    _gridThirdStep = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSeparatorToSecurities_TextChanged(object sender, TextChangedEventArgs e)
        {
            _tabPolygon.AutoCreatorSequenceSeparator = TextBoxSeparatorToSecurities.Text;
            _tabPolygon.SaveStandartSettings();
        }

        private void TextBoxBaseCurrency_TextChanged(object sender, TextChangedEventArgs e)
        {
            _tabPolygon.AutoCreatorSequenceBaseCurrency = TextBoxBaseCurrency.Text;
            _tabPolygon.SaveStandartSettings();
            LoadSecurityOnBox();
        }

        private void LoadPortfolioOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                  serversAll.Find(
                  server1 =>
                  server1.ServerType == _selectedServerType
                  && server1.ServerNameAndPrefix == _selectedServerName);

                if (server == null)
                {
                    return;
                }

                if (!TextBoxSearchSecurity.CheckAccess())
                {
                    TextBoxSearchSecurity.Dispatcher.Invoke(LoadPortfolioOnBox);
                    return;
                }

                string curPortfolio = null;

                if (ComboBoxPortfolio.SelectedItem != null)
                {
                    curPortfolio = ComboBoxPortfolio.SelectedItem.ToString();
                }

                ComboBoxPortfolio.Items.Clear();

                if (_tabPolygon.Sequences.Count != 0)
                {
                    string portfolio = _tabPolygon.Sequences[0].Tab1.Connector.PortfolioName;

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
                LoadSecurityOnBox();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void server_SecuritiesChangeEvent(List<Security> securities)
        {
            LoadSecurityOnBox();
        }

        private void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            LoadPortfolioOnBox();
        }

        #region Work with papers on the grid

        private DataGridView _gridSecuritiesFirstStep;

        private void LoadSecurityOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server =
                  serversAll.Find(
                  server1 =>
                  server1.ServerType == _selectedServerType
                  && server1.ServerNameAndPrefix == _selectedServerName);

                if (server == null)
                {
                    return;
                }

                // clear all

                // download available instruments

                if (string.IsNullOrEmpty(TextBoxBaseCurrency.Text))
                {
                    return;
                }

                string baseCurrency = TextBoxBaseCurrency.Text.ToLower();

                if (string.IsNullOrEmpty(baseCurrency))
                {
                    return;
                }

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

                        if (securities[i].Name.ToLower().EndsWith(baseCurrency) == false)
                        {
                            continue;
                        }

                        securitiesToLoad.Add(securities[i]);

                    }
                }

                // download already running instruments

                UpdateGridFirstStep(securitiesToLoad);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CreateGridFirstStep()
        {
            // number, class, type, name, full name, additional name, on/off

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label165; // 0 Number
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label166; // 1 class
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label167; // 2 type
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label168; // 3 name code
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label169; // 4 name
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Trader.Label170;  // 5 full name
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum5);

            DataGridViewCheckBoxColumn colum6 = new DataGridViewCheckBoxColumn();
            //colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label171; // 6 on/off
            colum6.ReadOnly = false;
            colum6.Width = 50;
            newGrid.Columns.Add(colum6);

            _gridSecuritiesFirstStep = newGrid;
            SecuritiesHost.Child = _gridSecuritiesFirstStep;

            _gridSecuritiesFirstStep.DataError += _gridSecuritiesFirstStep_DataError;
        }

        private void _gridSecuritiesFirstStep_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void UpdateGridFirstStep(List<Security> securities)
        {
            _gridSecuritiesFirstStep.Rows.Clear();

            // number, class, type, paper abbreviation, full name, additional name, on/off

            List<string> alreadyActiveatedSecurity = _tabPolygon.SecuritiesActivated;

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

                _gridSecuritiesFirstStep.Rows.Add(nRow);
            }
        }

        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

            for (int i = 0; i < _gridSecuritiesFirstStep.Rows.Count; i++)
            {
                _gridSecuritiesFirstStep.Rows[i].Cells[6].Value = isCheck;
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

            for (int i = 0; i < _gridSecuritiesFirstStep.Rows.Count; i++)
            {
                string security = "";
                string secSecond = "";

                if (_gridSecuritiesFirstStep.Rows[i].Cells[4].Value != null)
                {
                    security = _gridSecuritiesFirstStep.Rows[i].Cells[4].Value.ToString();
                }

                if (_gridSecuritiesFirstStep.Rows[i].Cells[3].Value != null)
                {
                    secSecond = _gridSecuritiesFirstStep.Rows[i].Cells[3].Value.ToString();
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

            _gridSecuritiesFirstStep.Rows[firstRow].Selected = true;
            _gridSecuritiesFirstStep.FirstDisplayedScrollingRowIndex = firstRow;

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

            _gridSecuritiesFirstStep.Rows[realInd].Selected = true;
            _gridSecuritiesFirstStep.FirstDisplayedScrollingRowIndex = realInd;
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

            _gridSecuritiesFirstStep.Rows[realInd].Selected = true;
            _gridSecuritiesFirstStep.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void TextBoxSearchSecurity_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    int rowIndex = 0;
                    for (int i = 0; i < _gridSecuritiesFirstStep.Rows.Count; i++)
                    {
                        if (_gridSecuritiesFirstStep.Rows[i].Selected == true)
                        {
                            rowIndex = i;
                            break;
                        }
                        if (i == _gridSecuritiesFirstStep.Rows.Count - 1)
                        {
                            return;
                        }
                    }

                    DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecuritiesFirstStep.Rows[rowIndex].Cells[6];
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

        #region Second step

        private DataGridView _gridSecondStep;

        private void CreateGridSecondStep()
        {
            // number, currency, entry count, exit count, on/off

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label165; // Number
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label356; // currency
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label357; // entry count
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewCheckBoxColumn colum4 = new DataGridViewCheckBoxColumn();
            //colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label184; // on/off
            colum4.ReadOnly = false;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            _gridSecondStep = newGrid;
            SecuritiesSecondStep.Child = _gridSecondStep;

            _gridSecondStep.DataError += _gridSecuritiesFirstStep_DataError;
        }

        private void ButtonCreateTableSecondStep_Click(object sender, RoutedEventArgs e)
        {
            List<CurrencyToSequence> currencies = GetCurrencyToStepTwo();
            UpdateGridSecondStep(currencies);
        }

        private void UpdateGridSecondStep(List<CurrencyToSequence> securities)
        {
            _gridSecondStep.Rows.Clear();

            if (securities == null)
            {
                return;
            }

            // number, currency, entry count, exit count, on/off

            for (int indexSecuriti = 0; indexSecuriti < securities.Count; indexSecuriti++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = (indexSecuriti + 1).ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecuriti].Currency;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecuriti].EntryCount;

                DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
                nRow.Cells.Add(checkBox);

                _gridSecondStep.Rows.Add(nRow);
            }
        }

        private List<CurrencyToSequence> GetCurrencyToStepTwo()
        {
            List<string> currenciesOnSecondStep = GetCurrenciesAfterFirstStep();

            if (currenciesOnSecondStep == null)
            {
                return null;
            }

            List<IServer> serversAll = ServerMaster.GetServers();

            IServer server =
              serversAll.Find(
              server1 =>
              server1.ServerType == _selectedServerType
              && server1.ServerNameAndPrefix == _selectedServerName);

            if (server == null)
            {
                return null;
            }

            // clear all

            List<Security> securities = server.Securities;

            if (securities == null)
            {
                return null;
            }

            List<CurrencyToSequence> currencies = new List<CurrencyToSequence>();

            for (int i = 0; i < currenciesOnSecondStep.Count; i++)
            {
                string curency = currenciesOnSecondStep[i];

                List<CurrencyToSequence> curCurrencies = GetCurrenciesToExit(curency, securities);

                for (int j = 0; j < curCurrencies.Count; j++)
                {
                    CurrencyToSequence c = curCurrencies[j];

                    bool isInArray = false;

                    for (int k = 0; k < currencies.Count; k++)
                    {
                        if (currencies[k].Currency == c.Currency)
                        {
                            currencies[k].EntryCount += c.EntryCount;
                            isInArray = true;
                            break;
                        }
                    }
                    if (isInArray == false)
                    {
                        currencies.Add(c);
                    }
                }

            }

            return currencies;
        }

        private List<CurrencyToSequence> GetCurrenciesToExit(string currency, List<Security> securities)
        {
            List<CurrencyToSequence> result = new List<CurrencyToSequence>();

            for (int i = 0; i < securities.Count; i++)
            {
                string name = securities[i].Name.ToLower();

                if (name.StartsWith(currency) &&
                    name.EndsWith(_tabPolygon.AutoCreatorSequenceBaseCurrency) == false)
                {
                    if (string.IsNullOrEmpty(_tabPolygon.AutoCreatorSequenceSeparator) == false)
                    {
                        name = name.Replace(_tabPolygon.AutoCreatorSequenceSeparator, "");
                    }

                    name = name.Replace(currency, "");

                    CurrencyToSequence newCurrencyToExit = new CurrencyToSequence();
                    newCurrencyToExit.Currency = name;
                    newCurrencyToExit.EntryCount = 1;
                    result.Add(newCurrencyToExit);
                }
            }

            for (int i = 0; i < result.Count; i++)
            {
                string exitSecName = result[i].Currency;

                if (string.IsNullOrEmpty(_tabPolygon.AutoCreatorSequenceSeparator) == false)
                {
                    exitSecName += _tabPolygon.AutoCreatorSequenceSeparator;
                }

                exitSecName += _tabPolygon.AutoCreatorSequenceBaseCurrency.ToLower();

                bool isInArray = false;

                for (int j = 0; j < securities.Count; j++)
                {
                    string curName = securities[j].Name.ToLower();

                    if (curName == exitSecName)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    result.RemoveAt(i);
                    i--;
                }
            }

            return result;
        }

        private List<string> GetCurrenciesAfterFirstStep()
        {
            List<string> securities = GetSelectedSecuritiesInFirstStep();

            if (securities == null)
            {
                return null;
            }

            string baseCurrency = _tabPolygon.AutoCreatorSequenceBaseCurrency;

            if (baseCurrency != null)
            {
                baseCurrency = baseCurrency.ToLower();
            }
            else
            {
                return null;
            }

            string separator = _tabPolygon.AutoCreatorSequenceSeparator;

            List<string> currencies = new List<string>();

            for (int i = 0; i < securities.Count; i++)
            {
                string curSecurity = securities[i];

                if (string.IsNullOrEmpty(separator) == false)
                {
                    curSecurity = curSecurity.Replace(separator, "");
                }

                curSecurity = curSecurity.ToLower();

                curSecurity = curSecurity.Replace(baseCurrency, "");

                currencies.Add(curSecurity);
            }


            return currencies;
        }

        private List<string> GetSelectedSecuritiesInFirstStep()
        {
            List<string> securities = new List<string>();

            for (int i = 0; i < _gridSecuritiesFirstStep.Rows.Count; i++)
            {
                DataGridViewRow curRow = _gridSecuritiesFirstStep.Rows[i];

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)curRow.Cells[curRow.Cells.Count - 1];

                if (checkBox.Value == null)
                {
                    continue;
                }

                if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                {
                    string name = curRow.Cells[3].Value.ToString().ToLower();

                    securities.Add(name);
                }
            }

            return securities;
        }

        private void CheckBoxSelectAllInSecondStep_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllInSecondStep.IsChecked.Value;

            for (int i = 0; i < _gridSecondStep.Rows.Count; i++)
            {
                _gridSecondStep.Rows[i].Cells[3].Value = isCheck;
            }
        }

        #endregion

        #region Third step

        private DataGridView _gridThirdStep;

        private void CreateGridThirdStep()
        {
            // number, step 1, step 2, step 3, on/off

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label165; // Number
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label359 + " 1 Buy"; // step 1
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label359 + " 2 Sell"; // step 2
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label359 + " 3 Sell";// step 3
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewCheckBoxColumn colum4 = new DataGridViewCheckBoxColumn();
            colum4.HeaderText = OsLocalization.Trader.Label184; // on/off
            colum4.ReadOnly = false;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            _gridThirdStep = newGrid;
            HostFirdStep.Child = _gridThirdStep;

            _gridThirdStep.DataError += _gridSecuritiesFirstStep_DataError;
        }

        private void UpdateGridSequence(List<PairsToSequence> securities)
        {
            _gridThirdStep.Rows.Clear();

            if (securities == null)
            {
                return;
            }

            // number, step 1, step 2, step 3, on/off

            for (int indexSecuriti = 0; indexSecuriti < securities.Count; indexSecuriti++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = (indexSecuriti + 1).ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecuriti].Pair1;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecuriti].Pair2;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecuriti].Pair3;

                DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
                nRow.Cells.Add(checkBox);

                _gridThirdStep.Rows.Add(nRow);
            }
        }

        private void ButtonCreateTableFinal_Click(object sender, RoutedEventArgs e)
        {
            List<string> startCurrencies = GetCurrenciesAfterFirstStep();

            if (startCurrencies == null)
            {
                return;
            }

            string baseCurrency = _tabPolygon.AutoCreatorSequenceBaseCurrency;

            if (baseCurrency != null)
            {
                baseCurrency = baseCurrency.ToLower();
            }
            else
            {
                return;
            }

            string separator = _tabPolygon.AutoCreatorSequenceSeparator;

            List<string> stepTwoCurrencies = GetCurrenciesStepTwo();

            if (stepTwoCurrencies == null)
            {
                return;
            }

            List<PairsToSequence> pairsToSequences = new List<PairsToSequence>();

            for (int i = 0; i < startCurrencies.Count; i++)
            {
                for (int j = 0; j < stepTwoCurrencies.Count; j++)
                {
                    PairsToSequence newSequence = new PairsToSequence();

                    if (string.IsNullOrEmpty(separator))
                    {
                        newSequence.Pair1 = startCurrencies[i] + baseCurrency;
                        newSequence.Pair2 = startCurrencies[i] + stepTwoCurrencies[j];
                        newSequence.Pair3 = stepTwoCurrencies[j] + baseCurrency;
                    }
                    else
                    {
                        newSequence.Pair1 = startCurrencies[i] + separator + baseCurrency;
                        newSequence.Pair2 = startCurrencies[i] + separator + stepTwoCurrencies[j];
                        newSequence.Pair3 = stepTwoCurrencies[j] + separator + baseCurrency;
                    }
                    pairsToSequences.Add(newSequence);
                }
            }

            // проверяем существуют ли в действительности все бумаги последовательности

            List<IServer> serversAll = ServerMaster.GetServers();

            IServer server =
              serversAll.Find(
              server1 =>
              server1.ServerType == _selectedServerType
              && server1.ServerNameAndPrefix == _selectedServerName);

            if (server == null)
            {
                return;
            }

            // clear all

            List<Security> securities = server.Securities;

            if (securities == null)
            {
                return;
            }

            for (int i = 0; i < pairsToSequences.Count; i++)
            {
                string pair1 = pairsToSequences[i].Pair1;
                string pair2 = pairsToSequences[i].Pair2;
                string pair3 = pairsToSequences[i].Pair3;

                for (int j = 0; j < securities.Count; j++)
                {
                    string curName = securities[j].Name.ToLower();
                    if (curName == pair1)
                    {
                        pair1 = null;
                    }
                    if (curName == pair2)
                    {
                        pair2 = null;
                    }
                    if (curName == pair3)
                    {
                        pair3 = null;
                    }
                }

                if (pair1 != null
                    || pair2 != null
                    || pair3 != null)
                {
                    pairsToSequences.RemoveAt(i);
                    i--;
                }
            }

            UpdateGridSequence(pairsToSequences);
        }

        private List<string> GetCurrenciesStepTwo()
        {
            List<string> currencies = new List<string>();

            for (int i = 0; i < _gridSecondStep.Rows.Count; i++)
            {
                DataGridViewRow curRow = _gridSecondStep.Rows[i];

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)curRow.Cells[3];

                if (checkBox.Value == null)
                {
                    continue;
                }

                if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                {
                    string name = curRow.Cells[1].Value.ToString();
                    currencies.Add(name);
                }
            }

            return currencies;
        }

        private void CheckBoxSelectAllInFinalStep_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllInFinalStep.IsChecked.Value;

            for (int i = 0; i < _gridThirdStep.Rows.Count; i++)
            {
                _gridThirdStep.Rows[i].Cells[4].Value = isCheck;
            }
        }

        #endregion

        #region  Sequence creation

        private void ButtonCreateSelectedSequence_Click(object sender, RoutedEventArgs e)
        {
            string baseCurrency = _tabPolygon.AutoCreatorSequenceBaseCurrency;
            string separator = _tabPolygon.AutoCreatorSequenceSeparator;
            string portfolio = null;

            if (ComboBoxPortfolio.SelectedItem == null
                || string.IsNullOrEmpty(ComboBoxPortfolio.SelectedItem.ToString()))
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label360);
                ui.ShowDialog();
                return;
            }

            portfolio = ComboBoxPortfolio.SelectedItem.ToString();

            List<PairsToSequence> sequences = GetSelectedSequence();

            if (sequences == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(separator) == false)
            {
                _tabPolygon.SeparatorToSecurities = _tabPolygon.AutoCreatorSequenceSeparator;
            }

            for (int i = 0; i < sequences.Count; i++)
            {
                _tabPolygon.CreateSequence(
                    sequences[i].Pair1, sequences[i].Pair2, sequences[i].Pair3,
                    baseCurrency, portfolio, _selectedServerType, _selectedServerName);

            }
        }

        private List<PairsToSequence> GetSelectedSequence()
        {
            List<PairsToSequence> currencies = new List<PairsToSequence>();

            for (int i = 0; i < _gridThirdStep.Rows.Count; i++)
            {
                DataGridViewRow curRow = _gridThirdStep.Rows[i];

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)curRow.Cells[4];

                if (checkBox.Value == null)
                {
                    continue;
                }

                if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                {
                    PairsToSequence newSequence = new PairsToSequence();

                    newSequence.Pair1 = curRow.Cells[1].Value.ToString();
                    newSequence.Pair2 = curRow.Cells[2].Value.ToString();
                    newSequence.Pair3 = curRow.Cells[3].Value.ToString();

                    currencies.Add(newSequence);
                }
            }

            return currencies;
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

    public class CurrencyToSequence
    {
        public string Currency;

        public int EntryCount;
    }

    public class PairsToSequence
    {
        public string Pair1;

        public string Pair2;

        public string Pair3;
    }
}