/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Forms;

namespace OsEngine.Market.Connectors
{
    /// <summary>
    /// interaction login for ConnectorCandlesUi
    /// Логика взаимодействия для ConnectorCandlesUi
    /// </summary>
    public partial class ConnectorCandlesUi
    {
        public ConnectorCandlesUi(ConnectorCandles connectorBot)
        {
            try
            {
                InitializeComponent();

                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;

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
                    _selectedType = connectorBot.ServerType;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerType;
                    _selectedType = servers[0].ServerType;
                }

                if (connectorBot.StartProgram == StartProgram.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    CheckBoxIsEmulator.IsEnabled = false;
                    CheckBoxSetForeign.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester;
                    ComboBoxPortfolio.Items.Add(ServerMaster.GetServers()[0].Portfolios[0].Number);
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedType = ServerType.Tester;

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

                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Simple);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Renko);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.HeikenAshi);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Delta);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Volume);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Ticks);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Range);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Rеvers);

                ComboBoxCandleCreateMethodType.SelectedItem = _connectorBot.CandleCreateMethodType;

                CheckBoxSetForeign.IsChecked = _connectorBot.SetForeign;

                LoadTimeFrameBox();

                TextBoxCountTradesInCandle.Text = _connectorBot.CountTradeInCandle.ToString();
                _countTradesInCandle = _connectorBot.CountTradeInCandle;
                TextBoxCountTradesInCandle.TextChanged += TextBoxCountTradesInCandle_TextChanged;

                TextBoxVolumeToClose.Text = _connectorBot.VolumeToCloseCandleInVolumeType.ToString();
                _volumeToClose = _connectorBot.VolumeToCloseCandleInVolumeType;
                TextBoxVolumeToClose.TextChanged += TextBoxVolumeToClose_TextChanged;

                TextBoxRencoPunkts.Text = _connectorBot.RencoPunktsToCloseCandleInRencoType.ToString();
                _rencoPuncts = _connectorBot.RencoPunktsToCloseCandleInRencoType;
                TextBoxRencoPunkts.TextChanged += TextBoxRencoPunkts_TextChanged;

                if (_connectorBot.RencoIsBuildShadows)
                {
                    CheckBoxRencoIsBuildShadows.IsChecked = true;
                }

                TextBoxDeltaPeriods.Text = _connectorBot.DeltaPeriods.ToString();
                TextBoxDeltaPeriods.TextChanged += TextBoxDeltaPeriods_TextChanged;
                _deltaPeriods = _connectorBot.DeltaPeriods;

                TextBoxRangeCandlesPunkts.Text = _connectorBot.RangeCandlesPunkts.ToString();
                TextBoxRangeCandlesPunkts.TextChanged += TextBoxRangeCandlesPunkts_TextChanged;
                _rangeCandlesPunkts = _connectorBot.RangeCandlesPunkts;

                TextBoxReversCandlesPunktsMinMove.Text = _connectorBot.ReversCandlesPunktsMinMove.ToString();
                TextBoxReversCandlesPunktsMinMove.TextChanged += TextBoxReversCandlesPunktsMinMove_TextChanged;
                _reversCandlesPunktsBackMove = _connectorBot.ReversCandlesPunktsBackMove;

                TextBoxReversCandlesPunktsBackMove.Text = _connectorBot.ReversCandlesPunktsBackMove.ToString();
                TextBoxReversCandlesPunktsBackMove.TextChanged += TextBoxReversCandlesPunktsBackMove_TextChanged;
                _reversCandlesPunktsMinMove = _connectorBot.ReversCandlesPunktsMinMove;

                ShowDopCandleSettings();

                ComboBoxCandleCreateMethodType.SelectionChanged += ComboBoxCandleCreateMethodType_SelectionChanged;

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
                LabelTimeFrame.Content = OsLocalization.Market.Label10;
                LabelCountTradesInCandle.Content = OsLocalization.Market.Label11;
                CheckBoxSetForeign.Content = OsLocalization.Market.Label12;
                LabelDeltaPeriods.Content = OsLocalization.Market.Label13;
                LabelVolumeToClose.Content = OsLocalization.Market.Label14;
                LabelRencoPunkts.Content = OsLocalization.Market.Label15;
                CheckBoxRencoIsBuildShadows.Content = OsLocalization.Market.Label16;
                LabelRangeCandlesPunkts.Content = OsLocalization.Market.Label17;
                LabelReversCandlesPunktsMinMove.Content = OsLocalization.Market.Label18;
                LabelReversCandlesPunktsBackMove.Content = OsLocalization.Market.Label19;
                ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
                LabelComissionType.Content = OsLocalization.Market.LabelComissionType;
                LabelComissionValue.Content = OsLocalization.Market.LabelComissionValue;
                CheckBoxSaveTradeArrayInCandle.Content = OsLocalization.Market.Label59;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelCandleType.Content = OsLocalization.Market.Label65;

                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Closing += ConnectorCandlesUi_Closing;

            this.Activate();
            this.Focus();
        }

        private void CheckBoxSaveTradeArrayInCandle_Click(object sender, RoutedEventArgs e)
        {
            _saveTradesInCandles = CheckBoxSaveTradeArrayInCandle.IsChecked.Value;
        }

        private void ConnectorCandlesUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _connectorBot = null;

            List<IServer> serversAll = ServerMaster.GetServers();

            for(int i = 0; serversAll != null && i < serversAll.Count;i++)
            {
                if(serversAll[i] == null)
                {
                    continue;
                }
                serversAll[i].SecuritiesChangeEvent -= server_SecuritiesCharngeEvent;
                serversAll[i].PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
            }

            ComboBoxClass.SelectionChanged -= ComboBoxClass_SelectionChanged;
            ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;
            TextBoxCountTradesInCandle.TextChanged -= TextBoxCountTradesInCandle_TextChanged;
            TextBoxVolumeToClose.TextChanged -= TextBoxVolumeToClose_TextChanged;
            TextBoxRencoPunkts.TextChanged -= TextBoxRencoPunkts_TextChanged;
            TextBoxDeltaPeriods.TextChanged -= TextBoxDeltaPeriods_TextChanged;
            TextBoxRangeCandlesPunkts.TextChanged -= TextBoxRangeCandlesPunkts_TextChanged;
            TextBoxReversCandlesPunktsMinMove.TextChanged -= TextBoxReversCandlesPunktsMinMove_TextChanged;
            TextBoxReversCandlesPunktsBackMove.TextChanged -= TextBoxReversCandlesPunktsBackMove_TextChanged;
            ComboBoxCandleCreateMethodType.SelectionChanged -= ComboBoxCandleCreateMethodType_SelectionChanged;
            CheckBoxSaveTradeArrayInCandle.Click -= CheckBoxSaveTradeArrayInCandle_Click;
            TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
            ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;

            DeleteGridSecurities();
        }

        public void IsCanChangeSaveTradesInCandles(bool canChangeSettingsSaveCandlesIn)
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

        private void TextBoxReversCandlesPunktsBackMove_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxReversCandlesPunktsBackMove.Text == "" ||
                    TextBoxReversCandlesPunktsBackMove.Text.EndsWith(",") ||
                    TextBoxReversCandlesPunktsBackMove.Text.EndsWith(".") ||
                    TextBoxReversCandlesPunktsBackMove.Text == "0")
                {
                    return;
                }
                if (

                        TextBoxReversCandlesPunktsBackMove.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _reversCandlesPunktsBackMove =
                        TextBoxReversCandlesPunktsBackMove.Text.ToDecimal();
            }
            catch
            {
                TextBoxReversCandlesPunktsBackMove.Text = _connectorBot.ReversCandlesPunktsBackMove.ToString();
            }
        }

        private void TextBoxReversCandlesPunktsMinMove_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxReversCandlesPunktsMinMove.Text == "" ||
                    TextBoxReversCandlesPunktsMinMove.Text.EndsWith(",") ||
                    TextBoxReversCandlesPunktsMinMove.Text.EndsWith(".") ||
                    TextBoxReversCandlesPunktsMinMove.Text == "0")
                {
                    return;
                }
                if (
                        TextBoxReversCandlesPunktsMinMove.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _reversCandlesPunktsMinMove =
                        TextBoxReversCandlesPunktsMinMove.Text.ToDecimal();
            }
            catch
            {
                TextBoxReversCandlesPunktsMinMove.Text = _connectorBot.ReversCandlesPunktsMinMove.ToString();
            }
        }

        private void TextBoxRangeCandlesPunkts_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxRangeCandlesPunkts.Text == "" ||
                    TextBoxRangeCandlesPunkts.Text.EndsWith(",") ||
                    TextBoxRangeCandlesPunkts.Text.EndsWith(".") ||
                    TextBoxRangeCandlesPunkts.Text == "0")
                {
                    return;
                }
                if (
                        TextBoxRangeCandlesPunkts.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _rangeCandlesPunkts =
                        TextBoxRangeCandlesPunkts.Text.ToDecimal();
            }
            catch
            {
                TextBoxRangeCandlesPunkts.Text = _connectorBot.RangeCandlesPunkts.ToString();
            }
        }

        void ComboBoxCandleCreateMethodType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ShowDopCandleSettings(); ;
        }

        private int _countTradesInCandle;

        private decimal _rencoPuncts;

        private decimal _volumeToClose;

        private decimal _deltaPeriods;

        private decimal _rangeCandlesPunkts;

        private decimal _reversCandlesPunktsMinMove;

        private decimal _reversCandlesPunktsBackMove;

        private bool _saveTradesInCandles;

        void TextBoxDeltaPeriods_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxDeltaPeriods.Text == "" ||
                    TextBoxDeltaPeriods.Text.EndsWith(",") ||
                    TextBoxDeltaPeriods.Text.EndsWith(".") ||
                    TextBoxDeltaPeriods.Text == "0")
                {
                    return;
                }
                if (
                        TextBoxDeltaPeriods.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _deltaPeriods =
                        TextBoxDeltaPeriods.Text.ToDecimal();
            }
            catch
            {
                TextBoxDeltaPeriods.Text = _deltaPeriods.ToString();
            }
        }

        void TextBoxCountTradesInCandle_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxCountTradesInCandle.Text) <= 0)
                {
                    throw new Exception();
                }
                _countTradesInCandle = Convert.ToInt32(TextBoxCountTradesInCandle.Text);
            }
            catch
            {
                TextBoxCountTradesInCandle.Text = _countTradesInCandle.ToString();
            }
        }

        private void TextBoxRencoPunkts_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxRencoPunkts.Text == "" ||
                    TextBoxRencoPunkts.Text.EndsWith(",") ||
                    TextBoxRencoPunkts.Text.EndsWith(".") ||
                    TextBoxRencoPunkts.Text == "0")
                {
                    return;
                }
                if (
                        TextBoxRencoPunkts.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _rencoPuncts =
                        TextBoxRencoPunkts.Text.ToDecimal();
            }
            catch
            {
                TextBoxRencoPunkts.Text = _rencoPuncts.ToString();
            }
        }

        private void TextBoxVolumeToClose_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxVolumeToClose.Text == "" ||
                    TextBoxVolumeToClose.Text.EndsWith(",") ||
                    TextBoxVolumeToClose.Text.EndsWith(".") ||
                    TextBoxVolumeToClose.Text == "0")
                {
                    return;
                }

                if (
                        TextBoxVolumeToClose.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _volumeToClose =
                        TextBoxVolumeToClose.Text.ToDecimal();
            }
            catch
            {
                TextBoxVolumeToClose.Text = _volumeToClose.ToString();
            }
        }

        void ComboBoxSecurities_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            GetTimeFramesInTester();
        }

        private void GetTimeFramesInTester()
        {
            string security = GetSelectedSecurity();

            TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

            if (server.TypeTesterData != TesterDataType.Candle)
            {
                return;
            }

            List<SecurityTester> securities = server.SecuritiesTester;

            string name = security;

            string lastTf = null;

            if (ComboBoxTimeFrame.SelectedItem != null)
            {
                lastTf = ComboBoxTimeFrame.SelectedItem.ToString();
            }

            ComboBoxTimeFrame.Items.Clear();

            for (int i = 0; i < securities.Count; i++)
            {
                if (name == securities[i].Security.Name)
                {
                    ComboBoxTimeFrame.Items.Add(securities[i].TimeFrame);
                }
            }
            if (lastTf == null)
            {
                ComboBoxTimeFrame.SelectedItem = securities[0].TimeFrame;
            }
            else
            {
                TimeFrame oldFrame;
                Enum.TryParse(lastTf, out oldFrame);

                ComboBoxTimeFrame.SelectedItem = oldFrame;
            }
        }

        /// <summary>
        /// server connector
        /// коннектор к серверу
        /// </summary>
        private ConnectorCandles _connectorBot;

        /// <summary>
        /// selected server for now
        /// выбранный в данным момент сервер
        /// </summary>
        private ServerType _selectedType;

        /// <summary>
        /// user changed server type to connect
        /// пользователь изменил тип сервера для подключения
        /// </summary>
        void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server != null)
                {
                    server.SecuritiesChangeEvent -= server_SecuritiesCharngeEvent;
                    server.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                }

                Enum.TryParse(ComboBoxTypeServer.SelectedItem.ToString(), true, out _selectedType);

                IServer server2 = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server2 != null)
                {
                    server2.SecuritiesChangeEvent += server_SecuritiesCharngeEvent;
                    server2.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
                }
                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();
                LoadTimeFrameBox();
                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// happens after switching the class of displayed instruments
        /// происходит после переключения класса отображаемых инструментов
        /// </summary>
        void ComboBoxClass_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
        }

        /// <summary>
        /// new securities arrived at the server
        /// на сервер пришли новые бумаги
        /// </summary>
        void server_SecuritiesCharngeEvent(List<Security> securities)
        {
            if(_connectorBot == null)
            {
                return;
            }
            LoadClassOnBox();
        }

        /// <summary>
        /// new accounts arrived at the server
        /// на сервер пришли новые счета
        /// </summary>
        void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            if (_connectorBot == null)
            {
                return;
            }
            LoadPortfolioOnBox();
        }

        /// <summary>
        /// unload accounts to the form
        /// выгружает счета на форму
        /// </summary>
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// place classes in the window
        /// поместить классы в окно
        /// </summary>
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
                if (_connectorBot.Security != null)
                {
                    ComboBoxClass.SelectedItem = _connectorBot.Security.NameClass;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LoadTimeFrameBox()
        {
            ComboBoxTimeFrame.Items.Clear();

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

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = true;
                }
                else
                {
                    // then if we use ready-made candles, then we need to use only those Timeframe that are
                    // and they are inserted only when we select the security in the method
                    // далее, если используем готовые свечки, то нужно ставить только те ТФ, которые есть
                    // и вставляются они только когда мы выбираем бумагу в методе 
                    GetTimeFramesInTester();
                    ComboBoxCandleCreateMethodType.SelectedItem = CandleCreateMethodType.Simple;
                    ComboBoxCandleCreateMethodType.IsEnabled = false;

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
            }
            else
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                IServerPermission permission = ServerMaster.GetServerPermission(_selectedType);

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



                CandleMarketDataType createType = CandleMarketDataType.Tick;

                if (ComboBoxCandleMarketDataType.SelectedItem != null)
                {
                    Enum.TryParse(ComboBoxCandleMarketDataType.SelectedItem.ToString(), true, out createType);
                }

            }

            ComboBoxTimeFrame.SelectedItem = _connectorBot.TimeFrame;

            if (ComboBoxTimeFrame.SelectedItem == null)
            {
                ComboBoxTimeFrame.SelectedItem = TimeFrame.Min1;
            }

            if (ComboBoxTimeFrame.SelectedItem == null 
                && ComboBoxTimeFrame.Items != null 
                && ComboBoxTimeFrame.Items.Count != 0)
            {
                ComboBoxTimeFrame.SelectedItem = ComboBoxTimeFrame.Items[0];
            }
        }

        #region работа с бумагами в таблице

        private void LoadSecurityOnBox()
        {
           try
            {
                _gridSecurities.Rows.Clear();

                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server == null)
                {
                    return;
                }
                // clear all
                // стираем всё


                List<Security> securities = server.Securities;

                if(securities == null ||
                    securities.Count == 0)
                {
                    return;
                }

                if(ComboBoxClass.SelectedItem != null)
                {
                    string classSec = ComboBoxClass.SelectedItem.ToString();

                    List<Security> securitiesOfMyClass = new List<Security>();

                    for(int i = 0;i < securities.Count;i++)
                    {
                        if(securities[i].NameClass == classSec)
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

        DataGridView _gridSecurities;

        private void DeleteGridSecurities()
        {
            DataGridFactory.ClearLinks(_gridSecurities);
            _gridSecurities.CellClick -= _gridSecurities_CellClick;
            SecurityTable.Child = null;
        }

        private void CreateGridSecurities()
        {
            // номер, тип, сокращонное название бумаги, полное имя, влк/выкл

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
            colum0.Width = 100;
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

        private void _gridSecurities_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int columnInd = e.ColumnIndex;

            int rowInd = e.RowIndex;

            if(columnInd != 4)
            {
                return;
            }

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                if(checkBox.Value == null)
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

            LoadTimeFrameBox();
        }

        private void UpdateGridSec(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            _gridSecurities.ClearSelection();

            if(securities == null 
                || securities.Count == 0)
            {
                return;
            }

            // номер, тип, сокращонное название бумаги, полное имя, площадка, влк/выкл

            string selectedName = _connectorBot.SecurityName;
            string selectedClass = _connectorBot.SecurityClass;

            if(string.IsNullOrEmpty(selectedClass) &&
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

            for(int i = 0; i < _gridSecurities.Rows.Count;i++)
            {

                DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                if(checkBox.Value == null)
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

        #region поиск по таблице бумаг

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

        List<int> _searchResults = new List<int>();

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

        private void UpdateSearchPanel()
        {
            if(_searchResults.Count == 0)
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

            int realInd = _searchResults[indexRow-1];

            _gridSecurities.Rows[realInd].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1 + 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if(indexRow >= maxRowIndex)
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

        #endregion


        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
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

                

                TimeFrame timeFrame;
                Enum.TryParse(ComboBoxTimeFrame.Text, out timeFrame);

                _connectorBot.TimeFrame = timeFrame;
                _connectorBot.SecurityName = security;

                _connectorBot.SecurityClass = ComboBoxClass.Text;

                CandleMarketDataType createType;
                Enum.TryParse(ComboBoxCandleMarketDataType.Text, true, out createType);
                _connectorBot.CandleMarketDataType = createType;

                CandleCreateMethodType methodType;
                Enum.TryParse(ComboBoxCandleCreateMethodType.Text, true, out methodType);

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

                _connectorBot.CandleCreateMethodType = methodType;

                if (CheckBoxSetForeign.IsChecked.HasValue)
                {
                    _connectorBot.SetForeign = CheckBoxSetForeign.IsChecked.Value;
                }

                _connectorBot.RencoPunktsToCloseCandleInRencoType = _rencoPuncts;
                _connectorBot.CountTradeInCandle = _countTradesInCandle;
                _connectorBot.VolumeToCloseCandleInVolumeType = _volumeToClose;
                _connectorBot.DeltaPeriods = _deltaPeriods;
                _connectorBot.RangeCandlesPunkts = _rangeCandlesPunkts;
                _connectorBot.ReversCandlesPunktsMinMove = _reversCandlesPunktsMinMove;
                _connectorBot.ReversCandlesPunktsBackMove = _reversCandlesPunktsBackMove;
                _connectorBot.SaveTradesInCandles = _saveTradesInCandles;

                if (CheckBoxRencoIsBuildShadows.IsChecked != null)
                {
                    _connectorBot.RencoIsBuildShadows = CheckBoxRencoIsBuildShadows.IsChecked.Value;
                }

                _connectorBot.Save();
                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region сообщения в лог 

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region дополнительные настройки разных типов свечей

        private void ShowDopCandleSettings()
        {
            ClearDopCandleSettings();

            CandleCreateMethodType type;

            Enum.TryParse(ComboBoxCandleCreateMethodType.SelectedItem.ToString(), out type);

            if (type == CandleCreateMethodType.Simple)
            {
                CreateSimpleCandleSettings();
            }

            if (type == CandleCreateMethodType.Delta)
            {
                CreateDeltaCandleSettings();
            }

            if (type == CandleCreateMethodType.Ticks)
            {
                CreateTicksCandleSettings();
            }

            if (type == CandleCreateMethodType.Renko)
            {
                CreateRencoCandleSettings();
            }

            if (type == CandleCreateMethodType.Volume)
            {
                CreateVolumeCandleSettings();
            }

            if (type == CandleCreateMethodType.HeikenAshi)
            {
                CreateHaikenAshiCandleSettings();
            }

            if (type == CandleCreateMethodType.Range)
            {
                CreateRangeCandleSettings();
            }

            if (type == CandleCreateMethodType.Rеvers)
            {
                CreateReversCandleSettings();
            }
        }

        private void ClearDopCandleSettings()
        {
            CheckBoxRencoIsBuildShadows.Visibility = Visibility.Hidden;
            TextBoxDeltaPeriods.Visibility = Visibility.Hidden;
            LabelDeltaPeriods.Visibility = Visibility.Hidden;
            ComboBoxTimeFrame.Visibility = Visibility.Hidden;
            CheckBoxSetForeign.Visibility = Visibility.Hidden;
            TextBoxCountTradesInCandle.Visibility = Visibility.Hidden;
            LabelTimeFrame.Visibility = Visibility.Hidden;
            LabelCountTradesInCandle.Visibility = Visibility.Hidden;
            TextBoxVolumeToClose.Visibility = Visibility.Hidden;
            LabelVolumeToClose.Visibility = Visibility.Hidden;
            TextBoxRencoPunkts.Visibility = Visibility.Hidden;
            LabelRencoPunkts.Visibility = Visibility.Hidden;

            LabelRangeCandlesPunkts.Visibility = Visibility.Hidden;
            LabelReversCandlesPunktsBackMove.Visibility = Visibility.Hidden;
            LabelReversCandlesPunktsMinMove.Visibility = Visibility.Hidden;
            TextBoxRangeCandlesPunkts.Visibility = Visibility.Hidden;
            TextBoxReversCandlesPunktsBackMove.Visibility = Visibility.Hidden;
            TextBoxReversCandlesPunktsMinMove.Visibility = Visibility.Hidden;
        }

        private void CreateSimpleCandleSettings()
        {
            ComboBoxTimeFrame.Visibility = Visibility.Visible;
            ComboBoxTimeFrame.Margin = new Thickness(213, 114, 0, 0);

            LabelTimeFrame.Visibility = Visibility.Visible;
            LabelTimeFrame.Margin = new Thickness(10, 114, 0, 0);

            CheckBoxSetForeign.Visibility = Visibility.Visible;
            CheckBoxSetForeign.Margin = new Thickness(87, 140, 0, 0);

            this.Height = 670;
        }

        private void CreateDeltaCandleSettings()
        {
            TextBoxDeltaPeriods.Visibility = Visibility.Visible;
            LabelDeltaPeriods.Visibility = Visibility.Visible;

            TextBoxDeltaPeriods.Margin = new Thickness(213, 114, 0, 0);
            LabelDeltaPeriods.Margin = new Thickness(10, 114, 0, 0);

            this.Height = 670;
        }

        private void CreateTicksCandleSettings()
        {
            TextBoxCountTradesInCandle.Visibility = Visibility.Visible;
            LabelCountTradesInCandle.Visibility = Visibility.Visible;

            TextBoxCountTradesInCandle.Margin = new Thickness(213, 114, 0, 0);
            LabelCountTradesInCandle.Margin = new Thickness(10, 114, 0, 0);
            Height = 670;
        }

        private void CreateRencoCandleSettings()
        {
            TextBoxRencoPunkts.Visibility = Visibility.Visible;
            TextBoxRencoPunkts.Margin = new Thickness(213, 114, 0, 0);

            LabelRencoPunkts.Visibility = Visibility.Visible;
            LabelRencoPunkts.Margin = new Thickness(10, 114, 0, 0);

            CheckBoxRencoIsBuildShadows.Visibility = Visibility.Visible;
            CheckBoxRencoIsBuildShadows.Margin = new Thickness(87, 140, 0, 0);
            Height = 670;
        }

        private void CreateVolumeCandleSettings()
        {
            LabelVolumeToClose.Visibility = Visibility.Visible;
            TextBoxVolumeToClose.Visibility = Visibility.Visible;

            TextBoxVolumeToClose.Margin = new Thickness(213, 114, 0, 0);
            LabelVolumeToClose.Margin = new Thickness(10, 114, 0, 0);
            Height = 670;
        }

        private void CreateHaikenAshiCandleSettings()
        {
            ComboBoxTimeFrame.Visibility = Visibility.Visible;
            ComboBoxTimeFrame.Margin = new Thickness(213, 114, 0, 0);

            LabelTimeFrame.Visibility = Visibility.Visible;
            LabelTimeFrame.Margin = new Thickness(10, 114, 0, 0);

            Height = 670;
        }

        private void CreateRangeCandleSettings()
        {
            LabelRangeCandlesPunkts.Visibility = Visibility.Visible;
            TextBoxRangeCandlesPunkts.Visibility = Visibility.Visible;

            TextBoxRangeCandlesPunkts.Margin = new Thickness(213, 114, 0, 0);
            LabelRangeCandlesPunkts.Margin = new Thickness(10, 114, 0, 0);
            Height = 670;

        }

        private void CreateReversCandleSettings()
        {
            TextBoxReversCandlesPunktsMinMove.Visibility = Visibility.Visible;
            TextBoxReversCandlesPunktsMinMove.Margin = new Thickness(213, 114, 0, 0);

            LabelReversCandlesPunktsMinMove.Visibility = Visibility.Visible;
            LabelReversCandlesPunktsMinMove.Margin = new Thickness(10, 114, 0, 0);

            TextBoxReversCandlesPunktsBackMove.Visibility = Visibility.Visible;
            TextBoxReversCandlesPunktsBackMove.Margin = new Thickness(213, 140, 0, 0);

            LabelReversCandlesPunktsBackMove.Visibility = Visibility.Visible;
            LabelReversCandlesPunktsBackMove.Margin = new Thickness(10, 140, 0, 0);

            Height = 700;
        }

        #endregion
    }

}