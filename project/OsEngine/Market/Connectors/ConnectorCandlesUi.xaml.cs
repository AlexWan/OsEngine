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
                    //ComboBoxClass.SelectedItem = ServerMaster.GetServers()[0].Securities[0].NameClass;
                    //ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedType = ServerType.Tester;
                }

                LoadClassOnBox();

                LoadSecurityOnBox();

                LoadPortfolioOnBox();

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
                ComboBoxSecurities.KeyDown += ComboBoxSecuritiesOnKeyDown;

                ComboBoxComissionType.Items.Add(ComissionType.None.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.OneLotFix.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.Percent.ToString());
                ComboBoxComissionType.SelectedItem = _connectorBot.ComissionType.ToString();

                TextBoxComissionValue.Text = _connectorBot.ComissionValue.ToString();

                CheckBoxSaveTradeArrayInCandle.IsChecked = _connectorBot.SaveTradesInCandles;
                CheckBoxSaveTradeArrayInCandle.Click += delegate (object sender, RoutedEventArgs args)
                {
                    _saveTradesInCandles = CheckBoxSaveTradeArrayInCandle.IsChecked.Value;
                };
                _saveTradesInCandles = _connectorBot.SaveTradesInCandles;

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label5;
                Label6.Content = OsLocalization.Market.Label6;
                Label7.Content = OsLocalization.Market.Label7;
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
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
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

        /// <summary>
        /// search tools by first letter in the title
        /// поиск инструментов по первой букве в названии
        /// </summary>
        private void ComboBoxSecuritiesOnKeyDown(object sender, KeyEventArgs e)
        {
            List<IServer> serversAll = ServerMaster.GetServers();

            IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

            if (server == null)
            {
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.Enter)
            {
                return;
            }

            string curText = ComboBoxSecurities.Text;

            ComboBoxSecurities.Items.Clear();

            // upload instruments matching the search terms
            // грузим инструменты подходящие под условия поиска

            List<Security> needSecurities = null;
            string findStr = "";
            if (curText != null)
            {
                findStr = curText;
            }

            if (e.Key == Key.Back)
            {
                if (findStr.Length != 0)
                    findStr = findStr.Remove(findStr.Length - 1);
            }
            else
            {
                findStr += e.Key;
            }

            ComboBoxSecurities.Text = findStr;
            ComboBoxSecurities.Items.Add(findStr);
            ComboBoxSecurities.SelectedItem = findStr;

            needSecurities = server.Securities.FindAll(
                sec => sec.Name.StartsWith(ComboBoxSecurities.Text, StringComparison.CurrentCultureIgnoreCase));


            for (int i = 0;
                needSecurities != null &&
                i < needSecurities.Count;
                i++)
            {
                string classSec = needSecurities[i].NameClass;
                if (ComboBoxClass.SelectedItem != null && classSec == ComboBoxClass.SelectedItem.ToString())
                {
                    ComboBoxSecurities.Items.Add(needSecurities[i].Name);
                    //ComboBoxSecurities.SelectedItem = needSecurities[i];
                }
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


        /// <summary>
        /// the number of trades in the candle with timeframe "Trades" has changed
        /// изменилось кол-во трейдов в свече с ТФ "трейды"
        /// </summary>
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

        /// <summary>
        /// it's need when security changes. For test connection we look at Timeframe for this security
        /// нужно когда изменяется бумага. При тестовом подключении смотрим здесь ТайФреймы для этой бумаги
        /// </summary>
        void ComboBoxSecurities_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            GetTimeFramesInTester();
        }

        private void GetTimeFramesInTester()
        {
            if (ComboBoxSecurities.SelectedItem == null)
            {
                return;
            }
            TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

            if (server.TypeTesterData != TesterDataType.Candle)
            {
                return;
            }

            List<SecurityTester> securities = server.SecuritiesTester;

            string name = ComboBoxSecurities.SelectedItem.ToString();

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
            LoadClassOnBox();
        }

        /// <summary>
        /// new accounts arrived at the server
        /// на сервер пришли новые счета
        /// </summary>
        void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
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

        /// <summary>
        /// upload data from storage to form
        /// выгрузить данные из хранилища на форму
        /// </summary>
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

                ComboBoxSecurities.Items.Clear();
                // download available instruments
                // грузим инструменты доступные для скачивания

                var securities = server.Securities;

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
                            ComboBoxSecurities.Items.Add(securities[i].Name);
                            ComboBoxSecurities.SelectedItem = securities[i].Name;
                        }
                    }
                }

                // download already running instruments
                // грузим уже запущенные инструменты

                string paper = _connectorBot.NamePaper;

                if (paper != null)
                {
                    ComboBoxSecurities.Text = paper;
                    ComboBoxSecurities.SelectedItem = paper;
                    if (ComboBoxSecurities.Text == null)
                    {
                        ComboBoxSecurities.Items.Add(_connectorBot.NamePaper);
                        ComboBoxSecurities.Text = _connectorBot.NamePaper;
                    }
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
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
                else
                {
                    // then if we use ready-made candles, then we need to use only those Timeframe that are
                    // and they are inserted only when we select the security in the method
                    // далее, если используем готовые свечки, то нужно ставить только те ТФ, которые есть
                    // и вставляются они только когда мы выбираем бумагу в методе 

                    ComboBoxSecurities.SelectionChanged += ComboBoxSecurities_SelectionChanged;
                    GetTimeFramesInTester();
                    ComboBoxCandleCreateMethodType.SelectedItem = CandleCreateMethodType.Simple;
                    ComboBoxCandleCreateMethodType.IsEnabled = false;

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
            }
            else
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
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(ComboBoxSecurities.Text))
                {
                    return;
                }
                _connectorBot.PortfolioName = ComboBoxPortfolio.Text;
                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _connectorBot.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }
                TimeFrame timeFrame;
                Enum.TryParse(ComboBoxTimeFrame.Text, out timeFrame);

                _connectorBot.TimeFrame = timeFrame;
                _connectorBot.NamePaper = ComboBoxSecurities.Text;
                Enum.TryParse(ComboBoxTypeServer.Text, true, out _connectorBot.ServerType);

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

        // log messages
        // сообщения в лог 

        /// <summary>
        /// send new message to up
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // additional settings for different types of candles
        // дополнительные настройки разных типов свечей

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
            ComboBoxTimeFrame.Margin = new Thickness(206, 360, 0, 0);

            LabelTimeFrame.Visibility = Visibility.Visible;
            LabelTimeFrame.Margin = new Thickness(41, 360, 0, 0);

            CheckBoxSetForeign.Visibility = Visibility.Visible;
            CheckBoxSetForeign.Margin = new Thickness(120, 400, 0, 0);

            this.Height = 490;
        }

        private void CreateDeltaCandleSettings()
        {
            TextBoxDeltaPeriods.Visibility = Visibility.Visible;
            LabelDeltaPeriods.Visibility = Visibility.Visible;

            TextBoxDeltaPeriods.Margin = new Thickness(246, 360, 0, 0);
            LabelDeltaPeriods.Margin = new Thickness(41, 360, 0, 0);

            this.Height = 465;
        }

        private void CreateTicksCandleSettings()
        {
            TextBoxCountTradesInCandle.Visibility = Visibility.Visible;
            LabelCountTradesInCandle.Visibility = Visibility.Visible;

            TextBoxCountTradesInCandle.Margin = new Thickness(206, 360, 0, 0);
            LabelCountTradesInCandle.Margin = new Thickness(41, 360, 0, 0);
            Height = 465;
        }

        private void CreateRencoCandleSettings()
        {
            TextBoxRencoPunkts.Visibility = Visibility.Visible;
            TextBoxRencoPunkts.Margin = new Thickness(206, 360, 0, 0);

            LabelRencoPunkts.Visibility = Visibility.Visible;
            LabelRencoPunkts.Margin = new Thickness(41, 360, 0, 0);

            CheckBoxRencoIsBuildShadows.Visibility = Visibility.Visible;
            CheckBoxRencoIsBuildShadows.Margin = new Thickness(120, 400, 0, 0);
            Height = 500;
        }

        private void CreateVolumeCandleSettings()
        {
            LabelVolumeToClose.Visibility = Visibility.Visible;
            TextBoxVolumeToClose.Visibility = Visibility.Visible;

            LabelVolumeToClose.Margin = new Thickness(41, 360, 0, 0);
            TextBoxVolumeToClose.Margin = new Thickness(206, 360, 0, 0);
            Height = 465;
        }

        private void CreateHaikenAshiCandleSettings()
        {
            ComboBoxTimeFrame.Visibility = Visibility.Visible;
            ComboBoxTimeFrame.Margin = new Thickness(206, 360, 0, 0);

            LabelTimeFrame.Visibility = Visibility.Visible;
            LabelTimeFrame.Margin = new Thickness(41, 360, 0, 0);

            Height = 465;
        }

        private void CreateRangeCandleSettings()
        {
            LabelRangeCandlesPunkts.Visibility = Visibility.Visible;
            TextBoxRangeCandlesPunkts.Visibility = Visibility.Visible;

            LabelRangeCandlesPunkts.Margin = new Thickness(41, 360, 0, 0);
            TextBoxRangeCandlesPunkts.Margin = new Thickness(206, 360, 0, 0);
            Height = 465;

        }

        private void CreateReversCandleSettings()
        {
            TextBoxReversCandlesPunktsMinMove.Visibility = Visibility.Visible;
            TextBoxReversCandlesPunktsMinMove.Margin = new Thickness(206, 360, 0, 0);

            LabelReversCandlesPunktsMinMove.Visibility = Visibility.Visible;
            LabelReversCandlesPunktsMinMove.Margin = new Thickness(41, 360, 0, 0);

            TextBoxReversCandlesPunktsBackMove.Visibility = Visibility.Visible;
            TextBoxReversCandlesPunktsBackMove.Margin = new Thickness(206, 390, 0, 0);

            LabelReversCandlesPunktsBackMove.Visibility = Visibility.Visible;
            LabelReversCandlesPunktsBackMove.Margin = new Thickness(41, 390, 0, 0);

            Height = 490;
        }
    }
}