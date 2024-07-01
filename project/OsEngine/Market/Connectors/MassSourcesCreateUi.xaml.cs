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
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Forms;
using System.Windows.Input;
using System.IO;
using OsEngine.Candles;
using OsEngine.Candles.Factory;
using OsEngine.Candles.Series;

namespace OsEngine.Market.Connectors
{
    public partial class MassSourcesCreateUi : Window
    {
        #region Constructor

        public MassSourcesCreateUi(MassSourcesCreator connectorBot)
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

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null)
                {// if connection server to exhange hasn't been created yet / если сервер для подключения к бирже ещё не создан
                    Close();
                    return;
                }

                // save connectors
                // сохраняем коннекторы
                SourcesCreator = connectorBot;

                ActivateGui();

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label5;
                Label6.Content = OsLocalization.Market.Label6;
                Label8.Content = OsLocalization.Market.Label8;
                Label9.Content = OsLocalization.Market.Label9;
                ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
                LabelComissionType.Content = OsLocalization.Market.LabelComissionType;
                LabelComissionValue.Content = OsLocalization.Market.LabelComissionValue;
                CheckBoxSaveTradeArrayInCandle.Content = OsLocalization.Market.Label59;
                CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelSecurities.Content = OsLocalization.Market.Label66;
                ButtonLoadSet.Content = OsLocalization.Market.Label98;
                ButtonSaveSet.Content = OsLocalization.Market.Label99;

                CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;

                ComboBoxTypeServer_SelectionChanged(null, null);

                Closed += MassSourcesCreateUi_Closed;

                ActivateCandlesTypesControls();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            this.Activate();
            this.Focus();
        }

        public MassSourcesCreator SourcesCreator;

        private void ActivateGui()
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null)
            {// if connection server to exhange hasn't been created yet / если сервер для подключения к бирже ещё не создан
                Close();
                return;
            }

            // upload settings to controls
            // загружаем настройки в контролы
            for (int i = 0; i < servers.Count; i++)
            {
                ComboBoxTypeServer.Items.Add(servers[i].ServerType);
            }

            if (SourcesCreator.ServerType != ServerType.None)
            {
                ComboBoxTypeServer.SelectedItem = SourcesCreator.ServerType;
                _selectedType = SourcesCreator.ServerType;
            }
            else
            {
                ComboBoxTypeServer.SelectedItem = servers[0].ServerType;
                _selectedType = servers[0].ServerType;
            }

            if (SourcesCreator.StartProgram == StartProgram.IsTester)
            {
                ComboBoxTypeServer.IsEnabled = false;
                CheckBoxIsEmulator.IsEnabled = false;
                ComboBoxTypeServer.SelectedItem = ServerType.Tester;
                //ComboBoxClass.SelectedItem = ServerMaster.GetServers()[0].Securities[0].NameClass;
                //ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                SourcesCreator.ServerType = ServerType.Tester;
                _selectedType = ServerType.Tester;
            }

            CreateGrid();

            LoadClassOnBox();

            LoadSecurityOnBox();

            LoadPortfolioOnBox();

            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

            CheckBoxIsEmulator.IsChecked = SourcesCreator.EmulatorIsOn;

            ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.Tick);
            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.MarketDepth);
            ComboBoxCandleMarketDataType.SelectedItem = SourcesCreator.CandleMarketDataType;

            ComboBoxComissionType.Items.Add(ComissionType.None.ToString());
            ComboBoxComissionType.Items.Add(ComissionType.OneLotFix.ToString());
            ComboBoxComissionType.Items.Add(ComissionType.Percent.ToString());
            ComboBoxComissionType.SelectedItem = SourcesCreator.ComissionType.ToString();

            TextBoxComissionValue.Text = SourcesCreator.ComissionValue.ToString();

            CheckBoxSaveTradeArrayInCandle.IsChecked = SourcesCreator.SaveTradesInCandles;


            CheckBoxSaveTradeArrayInCandle.Click += delegate (object sender, RoutedEventArgs args)
            {
                _saveTradesInCandles = CheckBoxSaveTradeArrayInCandle.IsChecked.Value;
            };

            _saveTradesInCandles = SourcesCreator.SaveTradesInCandles;

        }

        private void MassSourcesCreateUi_Closed(object sender, EventArgs e)
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
                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                ComboBoxClass.SelectionChanged -= ComboBoxClass_SelectionChanged;
                ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;
                ComboBoxCandleCreateMethodType.SelectionChanged -= ComboBoxCandleCreateMethodType_SelectionChanged;
                CheckBoxSelectAllCheckBox.Click -= CheckBoxSelectAllCheckBox_Click;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;
                _gridSecurities.CellClick -= _gridSecurities_CellClick;
                Closed -= MassSourcesCreateUi_Closed;

                DeleteCandleRealizationGrid();
                DeleteGridSecurities();
            }
            catch
            {
                // ignore
            }

            try
            {
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

        #endregion

        #region Other income events

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MassSourcesCreator curSettings = GetCurSettings();

                SourcesCreator.CandleCreateMethodType = curSettings.CandleCreateMethodType;
                SourcesCreator.ComissionType = curSettings.ComissionType;
                SourcesCreator.ComissionValue = curSettings.ComissionValue;
                SourcesCreator.EmulatorIsOn = curSettings.EmulatorIsOn;
                SourcesCreator.PortfolioName = curSettings.PortfolioName;
                SourcesCreator.SaveTradesInCandles = curSettings.SaveTradesInCandles;
                SourcesCreator.SecuritiesClass = curSettings.SecuritiesClass;
                SourcesCreator.SecuritiesNames = curSettings.SecuritiesNames;
                SourcesCreator.ServerType = curSettings.ServerType;
                SourcesCreator.TimeFrame = curSettings.TimeFrame;
                SourcesCreator.CandleMarketDataType = curSettings.CandleMarketDataType;
                SourcesCreator.CandleSeriesRealization.SetSaveString(curSettings.CandleSeriesRealization.GetSaveString());
                IsAccepted = true;
                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool IsAccepted;

        private MassSourcesCreator GetCurSettings()
        {
            MassSourcesCreator curCreator = new MassSourcesCreator(StartProgram.IsTester);

            curCreator.PortfolioName = ComboBoxPortfolio.Text;
            if (CheckBoxIsEmulator.IsChecked != null)
            {
                curCreator.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
            }

            Enum.TryParse(ComboBoxTypeServer.Text, true, out curCreator.ServerType);

            CandleMarketDataType createType;
            Enum.TryParse(ComboBoxCandleMarketDataType.Text, true, out createType);
            curCreator.CandleMarketDataType = createType;

            ComissionType typeComission;
            Enum.TryParse(ComboBoxComissionType.Text, true, out typeComission);
            curCreator.ComissionType = typeComission;

            if (ComboBoxClass.SelectedItem != null)
            {
                curCreator.SecuritiesClass = ComboBoxClass.SelectedItem.ToString();
            }

            try
            {
                curCreator.ComissionValue = TextBoxComissionValue.Text.ToDecimal();
            }
            catch
            {
                // ignore
            }

            curCreator.CandleCreateMethodType = ComboBoxCandleCreateMethodType.Text;
            curCreator.SaveTradesInCandles = _saveTradesInCandles;

            ACandlesSeriesRealization candlesCur = curCreator.CandleSeriesRealization;

            for (int i = 0; i < _series.Count; i++)
            {
                if (candlesCur.GetType().Name == _series[i].GetType().Name)
                {
                    for (int j = 0; j < _series[i].Parameters.Count; j++)
                    {
                        candlesCur.Parameters[j].LoadParamFromString(_series[i].Parameters[j].GetStringToSave().Split('#')[1]);

                        if (candlesCur.Parameters[j].SysName == "TimeFrame"
                            && candlesCur.Parameters[j].Type == CandlesParameterType.StringCollection)
                        {
                            string tfStr = ((CandlesParameterString)candlesCur.Parameters[j]).ValueString;

                            TimeFrame tf = TimeFrame.Sec1;

                            if (Enum.TryParse(tfStr, out tf))
                            {
                                curCreator.TimeFrame = tf;
                            }
                        }
                    }
                    break;
                }
            }

            List<ActivatedSecurity> securities = new List<ActivatedSecurity>();

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                DataGridViewCheckBoxCell checkBoxCell = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[6];

                if (checkBoxCell.Value == null ||
                    Convert.ToBoolean(checkBoxCell.Value.ToString()) == false)
                {
                    continue;
                }

                ActivatedSecurity sec = GetSecurity(_gridSecurities.Rows[i]);

                if (sec == null)
                {
                    continue;
                }

                securities.Add(sec);
            }

            curCreator.SecuritiesNames = securities;

            return curCreator;
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

        private void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (_selectedType == ServerType.None)
                {
                    return;
                }

                List<IServer> serversAll = ServerMaster.GetServers();

                if (serversAll == null ||
                    serversAll.Count == 0)
                {
                    return;
                }

                if (ComboBoxTypeServer.SelectedItem == null)
                {
                    return;
                }

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server != null)
                {
                    server.SecuritiesChangeEvent -= server_SecuritiesChangeEvent;
                    server.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                }

                Enum.TryParse(ComboBoxTypeServer.SelectedItem.ToString(), true, out _selectedType);

                IServer server2 = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server2 != null)
                {
                    server2.SecuritiesChangeEvent -= server_SecuritiesChangeEvent;
                    server2.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                    server2.SecuritiesChangeEvent += server_SecuritiesChangeEvent;
                    server2.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
                }

                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();

                UpdateSearchResults();
                UpdateSearchPanel();
                CheckBoxSelectAllCheckBox.IsChecked = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private ServerType _selectedType;

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

        private bool _saveTradesInCandles;

        #endregion

        #region Portfolio and class controls

        private void server_SecuritiesChangeEvent(List<Security> securities)
        {
            LoadClassOnBox();
        }

        private void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            LoadPortfolioOnBox();
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

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

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


                string portfolio = SourcesCreator.PortfolioName;


                if (portfolio != null)
                {
                    ComboBoxPortfolio.Items.Add(SourcesCreator.PortfolioName);
                    ComboBoxPortfolio.Text = SourcesCreator.PortfolioName;
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

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

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
                if (string.IsNullOrEmpty(SourcesCreator.SecuritiesClass) == false)
                {
                    ComboBoxClass.SelectedItem = SourcesCreator.SecuritiesClass;
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

        private void CreateGrid()
        {
            // номер, класс, тип, сокращонное название бумаги, полное имя, дополнительное имя, влк/выкл

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

            _gridSecurities.CellClick += _gridSecurities_CellClick;
        }

        private void DeleteGridSecurities()
        {
            DataGridFactory.ClearLinks(_gridSecurities);
            _gridSecurities.CellClick -= _gridSecurities_CellClick;
            SecuritiesHost.Child = null;
        }

        private void LoadSecurityOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server == null)
                {
                    return;
                }
                // clear all
                // стираем всё

                // download available instruments
                // грузим инструменты доступные для скачивания

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
                // грузим уже запущенные инструменты

                UpdateGrid(securitiesToLoad);
                UpdateSearchResults();
                UpdateSearchPanel();
                CheckBoxSelectAllCheckBox.IsChecked = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateGrid(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            // номер, класс, тип, сокращонное название бумаги, полное имя, дополнительное имя, влк/выкл

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

                ActivatedSecurity activatedSecurity =
                    SourcesCreator.SecuritiesNames.Find(s => s.SecurityName == securities[indexSecuriti].Name);

                if (activatedSecurity != null &&
                    activatedSecurity.IsOn == true)
                {
                    checkBox.Value = true;
                }
                else
                {

                }

                _gridSecurities.Rows.Add(nRow);
            }
        }

        private void _gridSecurities_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                _gridSecurities.ClearSelection();

                for (int i = 0; i < _gridSecurities.RowCount; i++)
                {
                    if (i == e.RowIndex)
                    {
                        for (int y = 0; y < _gridSecurities.ColumnCount; y++)
                        {
                            _gridSecurities.Rows[e.RowIndex].Cells[y].Style.ForeColor = System.Drawing.ColorTranslator.FromHtml("#ffffff");
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
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

                for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                {
                    _gridSecurities.Rows[i].Cells[6].Value = isCheck;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Search in securities grid

        private List<int> _searchResults = new List<int>();

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

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

                ComboBoxCandleCreateMethodType.SelectedItem = SourcesCreator.CandleCreateMethodType;
                ComboBoxCandleCreateMethodType.SelectionChanged += ComboBoxCandleCreateMethodType_SelectionChanged;

                for (int i = 0; i < types.Count; i++)
                {
                    _series.Add(CandleFactory.CreateCandleSeriesRealization(types[i]));
                    _series[_series.Count - 1].Init(SourcesCreator.StartProgram);
                }

                ACandlesSeriesRealization candlesCur = SourcesCreator.CandleSeriesRealization;

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
            if (ComboBoxCandleCreateMethodType.SelectedItem == null)
            {
                return;
            }

            if (SourcesCreator.StartProgram == StartProgram.IsTester)
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


                    TesterServer serverr = (TesterServer)ServerMaster.GetServers()[0];

                    if (serverr.TypeTesterData != TesterDataType.Candle)
                    {
                        return;
                    }

                    List<SecurityTester> securities = serverr.SecuritiesTester;

                    if (securities == null ||
                        securities.Count == 0)
                    {
                        return;
                    }

                    string name = securities[0].Security.Name;

                    for (int i = 0; i < securities.Count; i++)
                    {
                        if (name == securities[i].Security.Name)
                        {
                            box.Items.Add(securities[i].TimeFrame.ToString());
                        }
                    }

                    ComboBoxCandleCreateMethodType.SelectedItem = CandleCreateMethodType.Simple;
                    ComboBoxCandleCreateMethodType.IsEnabled = false;

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
            }
            else
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer serverr = serversAll.Find(server1 => server1.ServerType == _selectedType);

                IServerPermission permission = ServerMaster.GetServerPermission(_selectedType);

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

        #endregion

        #region Load or save settings

        private void ButtonSaveSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }

                string filePath = saveFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        // do nothin
                    }
                }

                MassSourcesCreator curSettings = GetCurSettings();

                try
                {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        writer.WriteLine(curSettings.GetSaveString());
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLoadSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                string filePath = openFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    return;
                }

                try
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        string fileStr = reader.ReadToEnd();

                        SourcesCreator.LoadFromString(fileStr);
                        LoadSettingsOnGui(SourcesCreator);
                        ActivateGui();
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LoadSettingsOnGui(MassSourcesCreator curCreator)
        {
            try
            {
                ComboBoxPortfolio.Text = curCreator.PortfolioName;
                CheckBoxIsEmulator.IsChecked = curCreator.EmulatorIsOn;
                ComboBoxTypeServer.Text = curCreator.ServerType.ToString();
                ComboBoxCandleMarketDataType.Text = curCreator.CandleMarketDataType.ToString();
                ComboBoxCandleCreateMethodType.Text = curCreator.CandleCreateMethodType.ToString();
                ComboBoxComissionType.Text = curCreator.ComissionType.ToString();
                ComboBoxClass.SelectedItem = curCreator.SecuritiesClass.ToString();
                TextBoxComissionValue.Text = curCreator.ComissionValue.ToString();

                for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                {
                    DataGridViewCheckBoxCell checkBoxCell = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[6];

                    string securityName = _gridSecurities.Rows[i].Cells[3].Value.ToString();

                    bool isInArray = false;

                    for (int j = 0; j < curCreator.SecuritiesNames.Count; j++)
                    {
                        if (curCreator.SecuritiesNames[j].SecurityName == securityName)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray)
                    {
                        checkBoxCell.Value = true;
                    }
                    else
                    {
                        checkBoxCell.Value = false;
                    }
                }
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