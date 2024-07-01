﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Forms;
using OsEngine.Candles;
using OsEngine.Candles.Factory;
using OsEngine.Candles.Series;

namespace OsEngine.Market.Connectors
{
    public partial class ConnectorCandlesUi
    {
        #region Constructor

        public ConnectorCandlesUi(ConnectorCandles connectorBot)
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
                {// if connection server to exhange hasn't been created yet / если сервер для подключения к бирже ещё не создан
                    Close();
                    return;
                }

                // save connectors
                // сохраняем коннекторы
                _connectorBot = connectorBot;

                // upload settings to controls
                // загружаем настройки в контролы
                for (int i = 0; i < servers.Count; i++)
                {
                    ComboBoxTypeServer.Items.Add(servers[i].ServerType);
                }

                if (connectorBot.ServerType != ServerType.None)
                {
                    ComboBoxTypeServer.SelectedItem = connectorBot.ServerType;
                    _selectedServerType = connectorBot.ServerType;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerType;
                    _selectedServerType = servers[0].ServerType;
                }

                if (connectorBot.StartProgram == StartProgram.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    CheckBoxIsEmulator.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester;
                    ComboBoxPortfolio.Items.Add(ServerMaster.GetServers()[0].Portfolios[0].Number);
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedServerType = ServerType.Tester;

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

                ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.Tick);
                ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.MarketDepth);
                ComboBoxCandleMarketDataType.SelectedItem = _connectorBot.CandleMarketDataType;


                ComboBoxComissionType.Items.Add(ComissionType.None.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.OneLotFix.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.Percent.ToString());
                ComboBoxComissionType.SelectedItem = _connectorBot.ComissionType.ToString();

                TextBoxComissionValue.Text = _connectorBot.ComissionValue.ToString();

                CheckBoxSaveTradeArrayInCandle.IsChecked = _connectorBot.SaveTradesInCandles;
                CheckBoxSaveTradeArrayInCandle.Click += CheckBoxSaveTradeArrayInCandle_Click;

                _saveTradesInCandles = _connectorBot.SaveTradesInCandles;

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label7;
                Label6.Content = OsLocalization.Market.Label6;
                Label8.Content = OsLocalization.Market.Label8;
                Label9.Content = OsLocalization.Market.Label9;
                ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
                LabelComissionType.Content = OsLocalization.Market.LabelComissionType;
                LabelComissionValue.Content = OsLocalization.Market.LabelComissionValue;
                CheckBoxSaveTradeArrayInCandle.Content = OsLocalization.Market.Label59;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelCandleType.Content = OsLocalization.Market.Label65;

                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;

                ComboBoxTypeServer_SelectionChanged(null, null);

                ActivateCandlesTypesControls();

            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Closing += ConnectorCandlesUi_Closing;

            this.Activate();
            this.Focus();
        }

        private ConnectorCandles _connectorBot;

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
                ComboBoxCandleCreateMethodType.SelectionChanged -= ComboBoxCandleCreateMethodType_SelectionChanged;
                CheckBoxSaveTradeArrayInCandle.Click -= CheckBoxSaveTradeArrayInCandle_Click;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;

                DeleteGridSecurities();
                DeleteCandleRealizationGrid();

            }
            catch
            {
                // ignore
            }

            try
            {
                _connectorBot = null;
                _selectedSeries = null;
                _series.Clear();
                _series = null;
                _searchResults.Clear();
                _searchResults = null;
            }
            catch
            {
                // ignore
            }
        }

        public void IsCanChangeSaveTradesInCandles(bool canChangeSettingsSaveCandlesIn)
        {
            try
            {
                if (CheckBoxSaveTradeArrayInCandle.Dispatcher.CheckAccess() == false)
                {
                    CheckBoxSaveTradeArrayInCandle.Dispatcher.Invoke(new Action<bool>(IsCanChangeSaveTradesInCandles), canChangeSettingsSaveCandlesIn);
                    return;
                }

                if (canChangeSettingsSaveCandlesIn == false)
                {
                    CheckBoxSaveTradeArrayInCandle.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

                Enum.TryParse(ComboBoxTypeServer.Text, true, out _connectorBot.ServerType);

                _connectorBot.PortfolioName = ComboBoxPortfolio.Text;

                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _connectorBot.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }

                _connectorBot.SecurityName = security;
                _connectorBot.SecurityClass = ComboBoxClass.Text;

                CandleMarketDataType createType;
                Enum.TryParse(ComboBoxCandleMarketDataType.Text, true, out createType);
                _connectorBot.CandleMarketDataType = createType;

                ComissionType typeComission;
                Enum.TryParse(ComboBoxComissionType.Text, true, out typeComission);
                _connectorBot.ComissionType = typeComission;

                try
                {
                    _connectorBot.ComissionValue = TextBoxComissionValue.Text.ToDecimal();
                }
                catch
                {
                    // ignore
                }

                _connectorBot.CandleCreateMethodType = ComboBoxCandleCreateMethodType.Text;
                _connectorBot.SaveTradesInCandles = _saveTradesInCandles;

                ACandlesSeriesRealization candlesCur = _connectorBot.TimeFrameBuilder.CandleSeriesRealization;

                for (int i = 0; i < _series.Count; i++)
                {
                    if (candlesCur.GetType().Name == _series[i].GetType().Name)
                    {
                        for (int j = 0; j < _series[i].Parameters.Count; j++)
                        {
                            if (_series[i].Parameters[j].Type == CandlesParameterType.StringCollection)
                            {
                                ((CandlesParameterString)candlesCur.Parameters[j]).ValueString = ((CandlesParameterString)_series[i].Parameters[j]).ValueString;
                            }
                            else if (_series[i].Parameters[j].Type == CandlesParameterType.Int)
                            {
                                ((CandlesParameterInt)candlesCur.Parameters[j]).ValueInt = ((CandlesParameterInt)_series[i].Parameters[j]).ValueInt;
                            }
                            else if (_series[i].Parameters[j].Type == CandlesParameterType.Bool)
                            {
                                ((CandlesParameterBool)candlesCur.Parameters[j]).ValueBool = ((CandlesParameterBool)_series[i].Parameters[j]).ValueBool;
                            }
                            else if (_series[i].Parameters[j].Type == CandlesParameterType.Decimal)
                            {
                                ((CandlesParameterDecimal)candlesCur.Parameters[j]).ValueDecimal = ((CandlesParameterDecimal)_series[i].Parameters[j]).ValueDecimal;
                            }

                            if (candlesCur.Parameters[j].SysName == "TimeFrame"
                                && candlesCur.Parameters[j].Type == CandlesParameterType.StringCollection)
                            {
                                string tfStr = ((CandlesParameterString)candlesCur.Parameters[j]).ValueString;

                                TimeFrame tf = TimeFrame.Sec1;

                                if (Enum.TryParse(tfStr, out tf))
                                {
                                    _connectorBot.TimeFrame = tf;
                                }
                            }
                        }
                        break;
                    }
                }
               
                _connectorBot.TimeFrameBuilder.Save();
                _connectorBot.Save();

                _connectorBot.ReconnectHard();

                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxSaveTradeArrayInCandle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _saveTradesInCandles = CheckBoxSaveTradeArrayInCandle.IsChecked.Value;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool _saveTradesInCandles;

        private void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
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

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

                if (server != null)
                {
                    server.SecuritiesChangeEvent -= server_SecuritiesChangeEvent;
                    server.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                }

                if (ComboBoxTypeServer.SelectedItem == null)
                {
                    return;
                }

                Enum.TryParse(ComboBoxTypeServer.SelectedItem.ToString(), true, out _selectedServerType);

                IServer server2 = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

                if (server2 != null)
                {
                    server2.SecuritiesChangeEvent += server_SecuritiesChangeEvent;
                    server2.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
                }
                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();
                UpdateSearchResults();
                UpdateSearchPanel();
                RepaintCandleRealizationGrid(_selectedSeries);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private ServerType _selectedServerType;

        #endregion

        #region Portfolio and class controls

        private void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
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

        private void server_SecuritiesChangeEvent(List<Security> securities)
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

        private void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
        }

        private void LoadPortfolioOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

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

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

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

        private DataGridView _gridSecurities;

        private void CreateGridSecurities()
        {
            try
            {
                DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.DisplayedCells);

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
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LoadSecurityOnBox()
        {
            try
            {
                _gridSecurities.Rows.Clear();

                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

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

                if (ComboBoxClass.SelectedItem != null)
                {
                    string classSec = ComboBoxClass.SelectedItem.ToString();

                    List<Security> securitiesOfMyClass = new List<Security>();

                    for (int i = 0; i < securities.Count; i++)
                    {
                        if (securities[i].NameClass == classSec)
                        {
                            securitiesOfMyClass.Add(securities[i]);
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

        private void DeleteGridSecurities()
        {
            DataGridFactory.ClearLinks(_gridSecurities);
            _gridSecurities.CellClick -= _gridSecurities_CellClick;
            SecurityTable.Child = null;
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

                RepaintCandleRealizationGrid(_selectedSeries);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateGridSec(List<Security> securities)
        {
            try
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

        private List<int> _searchResults = new List<int>();

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

                    if (security.Contains(key) ||
                        secSecond.Contains(key))
                    {
                        _searchResults.Add(i);
                    }
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

        #region TimeFrame selection. Candles type selection

        private List<ACandlesSeriesRealization> _series = new List<ACandlesSeriesRealization>();

        private ACandlesSeriesRealization _selectedSeries;

        private void ActivateCandlesTypesControls()
        {
            try
            {
                List<string> types = CandleFactory.GetCandlesNames();

                for (int i = 0; i < types.Count; i++)
                {
                    ComboBoxCandleCreateMethodType.Items.Add(types[i]);
                }

                ComboBoxCandleCreateMethodType.SelectedItem = _connectorBot.CandleCreateMethodType.ToString();
                ComboBoxCandleCreateMethodType.SelectionChanged += ComboBoxCandleCreateMethodType_SelectionChanged;

                for (int i = 0; i < types.Count; i++)
                {
                    _series.Add(CandleFactory.CreateCandleSeriesRealization(types[i]));
                    _series[_series.Count - 1].Init(_connectorBot.StartProgram);
                }

                ACandlesSeriesRealization candlesCur = _connectorBot.TimeFrameBuilder.CandleSeriesRealization;

                for (int i = 0; i < _series.Count; i++)
                {
                    if (candlesCur.GetType().Name == _series[i].GetType().Name)
                    {
                        for (int j = 0; j < _series[i].Parameters.Count; j++)
                        {
                            _series[i].Parameters[j].LoadParamFromString(candlesCur.Parameters[j].GetStringToSave().Split('#')[1]);

                        }
                        _selectedSeries = _series[i];
                        break;
                    }
                }

                CreateCandleRealizationGrid();
                RepaintCandleRealizationGrid(_selectedSeries);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridView _candlesRealizationGrid;

        private void CreateCandleRealizationGrid()
        {
            try
            {
                DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.DisplayedCells);

                newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                newGrid.ScrollBars = ScrollBars.Vertical;
                DataGridViewCellStyle style = newGrid.DefaultCellStyle;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = style;

                DataGridViewColumn colum0 = new DataGridViewColumn();
                colum0.CellTemplate = cell0;
                colum0.HeaderText = "Parameter name";
                colum0.ReadOnly = true;
                colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum0);

                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = "Value";
                colum2.ReadOnly = false;
                colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum2);

                _candlesRealizationGrid = newGrid;
                HostCandleSeriesParameters.Child = _candlesRealizationGrid;

                _candlesRealizationGrid.CellEndEdit += _candlesRealizationGrid_CellEndEdit;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteCandleRealizationGrid()
        {
            DataGridFactory.ClearLinks(_candlesRealizationGrid);
            _candlesRealizationGrid.CellEndEdit -= _candlesRealizationGrid_CellEndEdit;
            _candlesRealizationGrid = null;
            HostCandleSeriesParameters.Child = null;
        }

        private void RepaintCandleRealizationGrid(ACandlesSeriesRealization candlesRealization)
        {
            try
            {
                if (_candlesRealizationGrid == null)
                {
                    return;
                }
                _candlesRealizationGrid.Rows.Clear();

                List<ICandleSeriesParameter> parameters = candlesRealization.Parameters;

                for (int i = 0; i < parameters.Count; i++)
                {
                    _candlesRealizationGrid.Rows.Add(GetRowCandlesParameters(parameters[i]));
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRowCandlesParameters(ICandleSeriesParameter param)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = param.Label;

            if (param.Type == CandlesParameterType.Int)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = ((CandlesParameterInt)param).ValueInt.ToString();
            }
            else if (param.Type == CandlesParameterType.Decimal)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = ((CandlesParameterDecimal)param).ValueDecimal.ToString();
            }
            else if (param.Type == CandlesParameterType.Bool)
            {
                DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                cell.Value = ((CandlesParameterBool)param).ValueBool;
                row.Cells.Add(cell);
            }
            else if (param.Type == CandlesParameterType.StringCollection)
            {
                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                CandlesParameterString paramStr = (CandlesParameterString)param;

                if (paramStr.SysName == "TimeFrame")
                {
                    LoadTimeFrameBox(cell);
                }
                else
                {
                    for (int i = 0; i < paramStr.ValuesString.Count; i++)
                    {
                        cell.Items.Add(paramStr.ValuesString[i]);
                    }
                }

                for (int i = 0; i < cell.Items.Count; i++)
                {
                    if (cell.Items[i].ToString() == paramStr.ValueString)
                    {
                        cell.Value = paramStr.ValueString;
                        break;
                    }
                }

                if (cell.Value == null &&
                    cell.Items.Count > 0)
                {
                    cell.Value = cell.Items[0].ToString();
                    paramStr.ValueString = cell.Items[0].ToString();
                }

                row.Cells.Add(cell);
            }

            return row;
        }

        private void LoadTimeFrameBox(DataGridViewComboBoxCell box)
        {
            if (_connectorBot.StartProgram == StartProgram.IsTester)
            {
                // Timeframe
                // таймФрейм
                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                if (server.TypeTesterData != TesterDataType.Candle)
                {
                    // if we build data on ticks or depths, then any Timeframe can be used
                    // candle manager builds any Timeframe
                    // если строим данные на тиках или стаканах, то можно использовать любой ТФ
                    // менеджер свечей построит любой
                    box.Items.Add(TimeFrame.Day.ToString());
                    box.Items.Add(TimeFrame.Hour4.ToString());
                    box.Items.Add(TimeFrame.Hour2.ToString());
                    box.Items.Add(TimeFrame.Hour1.ToString());
                    box.Items.Add(TimeFrame.Min45.ToString());
                    box.Items.Add(TimeFrame.Min30.ToString());
                    box.Items.Add(TimeFrame.Min20.ToString());
                    box.Items.Add(TimeFrame.Min15.ToString());
                    box.Items.Add(TimeFrame.Min10.ToString());
                    box.Items.Add(TimeFrame.Min5.ToString());
                    box.Items.Add(TimeFrame.Min3.ToString());
                    box.Items.Add(TimeFrame.Min2.ToString());
                    box.Items.Add(TimeFrame.Min1.ToString());
                    box.Items.Add(TimeFrame.Sec30.ToString());
                    box.Items.Add(TimeFrame.Sec20.ToString());
                    box.Items.Add(TimeFrame.Sec15.ToString());
                    box.Items.Add(TimeFrame.Sec10.ToString());
                    box.Items.Add(TimeFrame.Sec5.ToString());
                    box.Items.Add(TimeFrame.Sec2.ToString());
                    box.Items.Add(TimeFrame.Sec1.ToString());

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = true;
                }
                else
                {
                    // then if we use ready-made candles, then we need to use only those Timeframe that are
                    // and they are inserted only when we select the security in the method
                    // далее, если используем готовые свечки, то нужно ставить только те ТФ, которые есть
                    // и вставляются они только когда мы выбираем бумагу в методе 
                    string security = GetSelectedSecurity();

                    TesterServer serverr = (TesterServer)ServerMaster.GetServers()[0];

                    if (serverr.TypeTesterData != TesterDataType.Candle)
                    {
                        return;
                    }

                    List<SecurityTester> securities = serverr.SecuritiesTester;

                    string name = security;

                    if (securities == null ||
                        securities.Count == 0)
                    {
                        return;
                    }

                    for (int i = 0; i < securities.Count; i++)
                    {
                        if (name == securities[i].Security.Name)
                        {
                            box.Items.Add(securities[i].TimeFrame.ToString());
                        }
                    }

                    //box.Value = securities[0].TimeFrame.ToString();

                    ComboBoxCandleCreateMethodType.SelectedItem = CandleCreateMethodType.Simple;
                    ComboBoxCandleCreateMethodType.IsEnabled = false;

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
            }
            else
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer serverr = serversAll.Find(server1 => server1.ServerType == _selectedServerType);

                IServerPermission permission = ServerMaster.GetServerPermission(_selectedServerType);

                if (serverr == null
                    || permission == null)
                {
                    box.Items.Add(TimeFrame.Day.ToString());
                    box.Items.Add(TimeFrame.Hour4.ToString());
                    box.Items.Add(TimeFrame.Hour2.ToString());
                    box.Items.Add(TimeFrame.Hour1.ToString());
                    box.Items.Add(TimeFrame.Min45.ToString());
                    box.Items.Add(TimeFrame.Min30.ToString());
                    box.Items.Add(TimeFrame.Min20.ToString());
                    box.Items.Add(TimeFrame.Min15.ToString());
                    box.Items.Add(TimeFrame.Min10.ToString());
                    box.Items.Add(TimeFrame.Min5.ToString());
                    box.Items.Add(TimeFrame.Min3.ToString());
                    box.Items.Add(TimeFrame.Min2.ToString());
                    box.Items.Add(TimeFrame.Min1.ToString());
                    box.Items.Add(TimeFrame.Sec30.ToString());
                    box.Items.Add(TimeFrame.Sec20.ToString());
                    box.Items.Add(TimeFrame.Sec15.ToString());
                    box.Items.Add(TimeFrame.Sec10.ToString());
                    box.Items.Add(TimeFrame.Sec5.ToString());
                    box.Items.Add(TimeFrame.Sec2.ToString());
                    box.Items.Add(TimeFrame.Sec1.ToString());
                }
                else
                {
                    if (permission.TradeTimeFramePermission.TimeFrameDayIsOn)
                        box.Items.Add(TimeFrame.Day.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameHour4IsOn)
                        box.Items.Add(TimeFrame.Hour4.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameHour2IsOn)
                        box.Items.Add(TimeFrame.Hour2.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameHour1IsOn)
                        box.Items.Add(TimeFrame.Hour1.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin45IsOn)
                        box.Items.Add(TimeFrame.Min45.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin30IsOn)
                        box.Items.Add(TimeFrame.Min30.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin20IsOn)
                        box.Items.Add(TimeFrame.Min20.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin15IsOn)
                        box.Items.Add(TimeFrame.Min15.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin10IsOn)
                        box.Items.Add(TimeFrame.Min10.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin5IsOn)
                        box.Items.Add(TimeFrame.Min5.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin3IsOn)
                        box.Items.Add(TimeFrame.Min3.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin2IsOn)
                        box.Items.Add(TimeFrame.Min2.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameMin1IsOn)
                        box.Items.Add(TimeFrame.Min1.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec30IsOn)
                        box.Items.Add(TimeFrame.Sec30.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec20IsOn)
                        box.Items.Add(TimeFrame.Sec20.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec15IsOn)
                        box.Items.Add(TimeFrame.Sec15.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec10IsOn)
                        box.Items.Add(TimeFrame.Sec10.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec5IsOn)
                        box.Items.Add(TimeFrame.Sec5.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec2IsOn)
                        box.Items.Add(TimeFrame.Sec2.ToString());

                    if (permission.TradeTimeFramePermission.TimeFrameSec1IsOn)
                        box.Items.Add(TimeFrame.Sec1.ToString());
                }

            }
        }

        private void _candlesRealizationGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;

                if (_candlesRealizationGrid.Rows[row].Cells[1].Value == null)
                {
                    return;
                }

                string value = _candlesRealizationGrid.Rows[row].Cells[1].Value.ToString();

                ICandleSeriesParameter param = _selectedSeries.Parameters[row];

                if (param.Type == CandlesParameterType.Int)
                {
                    try
                    {
                        ((CandlesParameterInt)param).ValueInt = Convert.ToInt32(value);
                    }
                    catch
                    {
                        _candlesRealizationGrid.Rows[row].Cells[1].Value = ((CandlesParameterInt)param).ValueInt.ToString();
                    }
                }
                else if (param.Type == CandlesParameterType.Decimal)
                {
                    try
                    {
                        ((CandlesParameterDecimal)param).ValueDecimal = value.ToDecimal();
                    }
                    catch
                    {
                        _candlesRealizationGrid.Rows[row].Cells[1].Value = ((CandlesParameterDecimal)param).ValueDecimal.ToString();
                    }
                }
                else if (param.Type == CandlesParameterType.Bool)
                {
                    try
                    {
                        ((CandlesParameterBool)param).ValueBool = Convert.ToBoolean(value);
                    }
                    catch
                    {
                        _candlesRealizationGrid.Rows[row].Cells[1].Value = ((CandlesParameterBool)param).ValueBool;
                    }
                }
                else if (param.Type == CandlesParameterType.StringCollection)
                {
                    ((CandlesParameterString)param).ValueString = value;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxCandleCreateMethodType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                string seriesType = ComboBoxCandleCreateMethodType.SelectedValue.ToString();

                for (int i = 0; i < _series.Count; i++)
                {
                    if (_series[i].GetType().Name == seriesType)
                    {
                        _selectedSeries = _series[i];
                        break;
                    }
                }

                RepaintCandleRealizationGrid(_selectedSeries);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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