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
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using WebSocketSharp;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabScreenerUi.xaml
    /// </summary>
    public partial class BotTabScreenerUi : Window
    {
        public BotTabScreenerUi(BotTabScreener connectorBot)
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
                {// if connection server to exhange hasn't been created yet
                    Close();
                    return;
                }

                // save connectors
                _screener = connectorBot;

                // upload settings to controls
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
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;
                    ComboBoxPortfolio.IsEnabled = false;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedType = ServerType.Tester;
                }

                CreateGrid();

                LoadClassOnBox();

                LoadSecurityOnBox();

                LoadPortfolioOnBox();

                ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

                CheckBoxIsEmulator.IsChecked = _screener.EmulatorIsOn;

                ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

                ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.Tick);
                ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.MarketDepth);
                ComboBoxCandleMarketDataType.SelectedItem = _screener.CandleMarketDataType;

                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Simple);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Renko);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.HeikenAshi);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Delta);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Volume);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Ticks);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Range);
                ComboBoxCandleCreateMethodType.Items.Add(CandleCreateMethodType.Rеvers);

                ComboBoxCandleCreateMethodType.SelectedItem = _screener.CandleCreateMethodType;

                CheckBoxSetForeign.IsChecked = _screener.SetForeign;

                LoadTimeFrameBox();

                TextBoxCountTradesInCandle.Text = _screener.CountTradeInCandle.ToString();
                _countTradesInCandle = _screener.CountTradeInCandle;
                TextBoxCountTradesInCandle.TextChanged += TextBoxCountTradesInCandle_TextChanged;

                TextBoxVolumeToClose.Text = _screener.VolumeToCloseCandleInVolumeType.ToString();
                _volumeToClose = _screener.VolumeToCloseCandleInVolumeType;
                TextBoxVolumeToClose.TextChanged += TextBoxVolumeToClose_TextChanged;

                TextBoxRencoPunkts.Text = _screener.RencoPunktsToCloseCandleInRencoType.ToString();
                _rencoPuncts = _screener.RencoPunktsToCloseCandleInRencoType;
                TextBoxRencoPunkts.TextChanged += TextBoxRencoPunkts_TextChanged;

                if (_screener.RencoIsBuildShadows)
                {
                    CheckBoxRencoIsBuildShadows.IsChecked = true;
                }

                TextBoxDeltaPeriods.Text = _screener.DeltaPeriods.ToString();
                TextBoxDeltaPeriods.TextChanged += TextBoxDeltaPeriods_TextChanged;
                _deltaPeriods = _screener.DeltaPeriods;

                TextBoxRangeCandlesPunkts.Text = _screener.RangeCandlesPunkts.ToString();
                TextBoxRangeCandlesPunkts.TextChanged += TextBoxRangeCandlesPunkts_TextChanged;
                _rangeCandlesPunkts = _screener.RangeCandlesPunkts;

                TextBoxReversCandlesPunktsMinMove.Text = _screener.ReversCandlesPunktsMinMove.ToString();
                TextBoxReversCandlesPunktsMinMove.TextChanged += TextBoxReversCandlesPunktsMinMove_TextChanged;
                _reversCandlesPunktsBackMove = _screener.ReversCandlesPunktsBackMove;

                TextBoxReversCandlesPunktsBackMove.Text = _screener.ReversCandlesPunktsBackMove.ToString();
                TextBoxReversCandlesPunktsBackMove.TextChanged += TextBoxReversCandlesPunktsBackMove_TextChanged;
                _reversCandlesPunktsMinMove = _screener.ReversCandlesPunktsMinMove;

                ShowDopCandleSettings();

                ComboBoxCandleCreateMethodType.SelectionChanged += ComboBoxCandleCreateMethodType_SelectionChanged;

                ComboBoxComissionType.Items.Add(ComissionType.None.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.OneLotFix.ToString());
                ComboBoxComissionType.Items.Add(ComissionType.Percent.ToString());
                ComboBoxComissionType.SelectedItem = _screener.ComissionType.ToString();

                TextBoxComissionValue.Text = _screener.ComissionValue.ToString();

                CheckBoxSaveTradeArrayInCandle.IsChecked = _screener.SaveTradesInCandles;
                CheckBoxSaveTradeArrayInCandle.Click += delegate (object sender, RoutedEventArgs args)
                {
                    _saveTradesInCandles = CheckBoxSaveTradeArrayInCandle.IsChecked.Value;
                };
                _saveTradesInCandles = _screener.SaveTradesInCandles;

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                Label2.Content = OsLocalization.Market.Label2;
                Label3.Content = OsLocalization.Market.Label3;
                CheckBoxIsEmulator.Content = OsLocalization.Market.Label4;
                Label5.Content = OsLocalization.Market.Label5;
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
                CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
                CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                LabelSecurities.Content = OsLocalization.Market.Label66;

                CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;
                ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
                TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;

                ComboBoxTypeServer_SelectionChanged(null, null);

                Closed += BotTabScreenerUi_Closed;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// Window close handler
        /// </summary>
        private void BotTabScreenerUi_Closed(object sender, EventArgs e)
        {
            List<IServer> serversAll = ServerMaster.GetServers();

            for (int i = 0; serversAll != null && i < serversAll.Count; i++)
            {
                if (serversAll[i] == null)
                {
                    continue;
                }
                serversAll[i].SecuritiesChangeEvent -= server_SecuritiesCharngeEvent;
                serversAll[i].PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
            }

            TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
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
            CheckBoxSelectAllCheckBox.Click -= CheckBoxSelectAllCheckBox_Click;
            ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;

            _gridSecurities.CellClick -= _gridSecurities_CellClick;

            Closed -= BotTabScreenerUi_Closed;

            DataGridFactory.ClearLinks(_gridSecurities);
            _gridSecurities = null;
            SecuritiesHost.Child = null;
        }

        BotTabScreener _screener;

        /// <summary>
        /// Is it possible to save trades in a candle
        /// </summary>
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
        /// Rollback text change event handler
        /// </summary>
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
                if (TextBoxReversCandlesPunktsBackMove.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _reversCandlesPunktsBackMove =
                        TextBoxReversCandlesPunktsBackMove.Text.ToDecimal();
            }
            catch
            {
                TextBoxReversCandlesPunktsBackMove.Text = _screener.ReversCandlesPunktsBackMove.ToString();
            }
        }

        /// <summary>
        /// Minimum movement for reverse bars changed
        /// </summary>
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
                if (TextBoxReversCandlesPunktsMinMove.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _reversCandlesPunktsMinMove = TextBoxReversCandlesPunktsMinMove.Text.ToDecimal();
            }
            catch
            {
                TextBoxReversCandlesPunktsMinMove.Text = _screener.ReversCandlesPunktsMinMove.ToString();
            }
        }

        /// <summary>
        /// Punkts for reverse bars changed
        /// </summary>
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
                if (TextBoxRangeCandlesPunkts.Text.ToDecimal() <= 0)
                {
                    throw new Exception();
                }
                _rangeCandlesPunkts =
                        TextBoxRangeCandlesPunkts.Text.ToDecimal();
            }
            catch
            {
                TextBoxRangeCandlesPunkts.Text = _screener.RangeCandlesPunkts.ToString();
            }
        }

        /// <summary>
        /// The method of creating candles has changed
        /// </summary>
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

        /// <summary>
        /// The delta period has changed
        /// </summary>
        private void TextBoxDeltaPeriods_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
                _deltaPeriods = TextBoxDeltaPeriods.Text.ToDecimal();
            }
            catch
            {
                TextBoxDeltaPeriods.Text = _deltaPeriods.ToString();
            }
        }

        /// <summary>
        /// The number of trades in the candle with timeframe "Trades" has changed
        /// </summary>
        private void TextBoxCountTradesInCandle_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

        /// <summary>
        /// The number of pips for Renko candles has changed
        /// </summary>
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

        /// <summary>
        /// Changed the value of the volume required to close the candle
        /// </summary>
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
        /// It's need when security changes. For test connection we look at Timeframe for this security
        /// </summary>
        private void ComboBoxSecurities_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            GetTimeFramesInTester();
        }

        /// <summary>
        /// Get timeframe in tester
        /// </summary>
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

        /// <summary>
        /// Selected server for now
        /// </summary>
        private ServerType _selectedType;

        /// <summary>
        /// User changed server type to connect
        /// </summary>
        private void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if(_selectedType == ServerType.None)
                {
                    return;
                }

                List<IServer> serversAll = ServerMaster.GetServers();

                if(serversAll == null ||
                    serversAll.Count == 0)
                {
                    return;
                }

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
        /// Happens after switching the class of displayed instruments
        /// </summary>
        private void ComboBoxClass_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
        }

        /// <summary>
        /// New securities arrived at the server
        /// </summary>
        private void server_SecuritiesCharngeEvent(List<Security> securities)
        {
            LoadClassOnBox();
            LoadSecurityOnBox();
        }

        /// <summary>
        /// New accounts arrived at the server
        /// </summary>
        private void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            LoadPortfolioOnBox();
        }

        /// <summary>
        /// Unload accounts to the form
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

                string portfolio = _screener.PortfolioName;


                if (portfolio != null)
                {
                    ComboBoxPortfolio.Items.Add(_screener.PortfolioName);
                    ComboBoxPortfolio.Text = _screener.PortfolioName;
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

        /// <summary>
        /// Place classes in the window
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

                ComboBoxClass.Items.Add("All");

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
                if (string.IsNullOrEmpty(_screener.SecuritiesClass) == false)
                {
                    ComboBoxClass.SelectedItem = _screener.SecuritiesClass;
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

        #region work with papers on the grid

        /// <summary>
        /// Upload data from storage to form
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
                        if (ComboBoxClass.SelectedItem != null 
                            && ComboBoxClass.SelectedItem.Equals(classSec))
                        {
                            securitiesToLoad.Add(securities[i]);
                        }
                        else if (ComboBoxClass.SelectedItem != null
                                 && ComboBoxClass.SelectedItem.ToString() == "All")
                        {
                            securitiesToLoad.Add(securities[i]);
                        }
                    }
                }

                // download already running instruments

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

        DataGridView _gridSecurities;

        /// <summary>
        /// Create data grid
        /// </summary>
        private void CreateGrid()
        {
            // number, class, type, paper abbreviation, full name, additional name, on/off

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

        private void _gridSecurities_CellClick(object sender, DataGridViewCellEventArgs e)
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

        /// <summary>
        /// Update data geid
        /// </summary>
        private void UpdateGrid(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            // number, class, type, paper abbreviation, full name, additional name, on/off

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
                    _screener.SecuritiesNames.Find(s => s.SecurityName == securities[indexSecuriti].Name);

                if (activatedSecurity != null &&
                    activatedSecurity.IsOn == true)
                {
                    checkBox.Value = true;
                }

                _gridSecurities.Rows.Add(nRow);
            }
        }

        /// <summary>
        /// Choose all
        /// </summary>
        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                _gridSecurities.Rows[i].Cells[6].Value = isCheck;
            }
        }

        #endregion

        /// <summary>
        /// Load combo box with timeframes
        /// </summary>
        private void LoadTimeFrameBox()
        {
            ComboBoxTimeFrame.Items.Clear();

            if (_screener.StartProgram == StartProgram.IsTester)
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

                    ComboBoxCandleMarketDataType.SelectedItem = CandleMarketDataType.Tick;
                    ComboBoxCandleMarketDataType.IsEnabled = false;
                }
                else
                {
                    // then if we use ready-made candles, then we need to use only those Timeframe that are
                    // and they are inserted only when we select the security in the method

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

            ComboBoxTimeFrame.SelectedItem = _screener.TimeFrame;

            if (ComboBoxTimeFrame.SelectedItem == null)
            {
                ComboBoxTimeFrame.SelectedItem = TimeFrame.Min1;
            }
        }

        // log messages
        
        /// <summary>
        /// Send new message to up
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// Outgoing log message
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // additional settings for different types of candles

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

        /// <summary>
        /// Hide advanced candle settings
        /// </summary>
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

        /// <summary>
        /// Hide additional settings for simple candles
        /// </summary>
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

        /// <summary>
        /// Show settings for delta candles
        /// </summary>
        private void CreateDeltaCandleSettings()
        {
            TextBoxDeltaPeriods.Visibility = Visibility.Visible;
            LabelDeltaPeriods.Visibility = Visibility.Visible;

            TextBoxDeltaPeriods.Margin = new Thickness(246, 360, 0, 0);
            LabelDeltaPeriods.Margin = new Thickness(41, 360, 0, 0);

            this.Height = 465;
        }

        /// <summary>
        /// Show settings for tick candles
        /// </summary>
        private void CreateTicksCandleSettings()
        {
            TextBoxCountTradesInCandle.Visibility = Visibility.Visible;
            LabelCountTradesInCandle.Visibility = Visibility.Visible;

            TextBoxCountTradesInCandle.Margin = new Thickness(206, 360, 0, 0);
            LabelCountTradesInCandle.Margin = new Thickness(41, 360, 0, 0);
            Height = 465;
        }

        /// <summary>
        /// Show settings for renko candles
        /// </summary>
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

        /// <summary>
        /// Show settings for bulk candles
        /// </summary>
        private void CreateVolumeCandleSettings()
        {
            LabelVolumeToClose.Visibility = Visibility.Visible;
            TextBoxVolumeToClose.Visibility = Visibility.Visible;

            LabelVolumeToClose.Margin = new Thickness(41, 360, 0, 0);
            TextBoxVolumeToClose.Margin = new Thickness(206, 360, 0, 0);
            Height = 465;
        }

        /// <summary>
        /// Show settings for HaikenAshi candles
        /// </summary>
        private void CreateHaikenAshiCandleSettings()
        {
            ComboBoxTimeFrame.Visibility = Visibility.Visible;
            ComboBoxTimeFrame.Margin = new Thickness(206, 360, 0, 0);

            LabelTimeFrame.Visibility = Visibility.Visible;
            LabelTimeFrame.Margin = new Thickness(41, 360, 0, 0);

            Height = 465;
        }

        /// <summary>
        /// Show settings for Range candles
        /// </summary>
        private void CreateRangeCandleSettings()
        {
            LabelRangeCandlesPunkts.Visibility = Visibility.Visible;
            TextBoxRangeCandlesPunkts.Visibility = Visibility.Visible;

            LabelRangeCandlesPunkts.Margin = new Thickness(41, 360, 0, 0);
            TextBoxRangeCandlesPunkts.Margin = new Thickness(206, 360, 0, 0);
            Height = 465;
        }

        /// <summary>
        /// Show settings for Revers candles
        /// </summary>
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

        /// <summary>
        /// Accept button
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _screener.PortfolioName = ComboBoxPortfolio.Text;
                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _screener.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }
                TimeFrame timeFrame;
                Enum.TryParse(ComboBoxTimeFrame.Text, out timeFrame);

                _screener.TimeFrame = timeFrame;
                Enum.TryParse(ComboBoxTypeServer.Text, true, out _screener.ServerType);

                CandleMarketDataType createType;
                Enum.TryParse(ComboBoxCandleMarketDataType.Text, true, out createType);
                _screener.CandleMarketDataType = createType;

                CandleCreateMethodType methodType;
                Enum.TryParse(ComboBoxCandleCreateMethodType.Text, true, out methodType);

                ComissionType typeComission;
                Enum.TryParse(ComboBoxComissionType.Text, true, out typeComission);
                _screener.ComissionType = typeComission;

                if (ComboBoxClass.SelectedItem != null)
                {
                    _screener.SecuritiesClass = ComboBoxClass.SelectedItem.ToString();
                }

                try
                {
                    _screener.ComissionValue = TextBoxComissionValue.Text.ToDecimal();
                }
                catch
                {
                    // ignore
                }

                _screener.CandleCreateMethodType = methodType;

                if (CheckBoxSetForeign.IsChecked.HasValue)
                {
                    _screener.SetForeign = CheckBoxSetForeign.IsChecked.Value;
                }

                _screener.RencoPunktsToCloseCandleInRencoType = _rencoPuncts;
                _screener.CountTradeInCandle = _countTradesInCandle;
                _screener.VolumeToCloseCandleInVolumeType = _volumeToClose;
                _screener.DeltaPeriods = _deltaPeriods;
                _screener.RangeCandlesPunkts = _rangeCandlesPunkts;
                _screener.ReversCandlesPunktsMinMove = _reversCandlesPunktsMinMove;
                _screener.ReversCandlesPunktsBackMove = _reversCandlesPunktsBackMove;
                _screener.SaveTradesInCandles = _saveTradesInCandles;

                if (CheckBoxRencoIsBuildShadows.IsChecked != null)
                {
                    _screener.RencoIsBuildShadows = CheckBoxRencoIsBuildShadows.IsChecked.Value;
                }

                List<ActivatedSecurity> securities = new List<ActivatedSecurity>();

                for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                {
                    ActivatedSecurity sec = GetSecurity(_gridSecurities.Rows[i]);

                    if (sec == null)
                    {
                        continue;
                    }

                    securities.Add(sec);
                }

                _screener.SecuritiesNames = securities;
                _screener.SaveSettings();

                _screener.NeadToReloadTabs = true;

                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Get security by data from row
        /// </summary>
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

        #region search by securities table

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

        #endregion

        private void ButtonSaveSet_Click(object sender, RoutedEventArgs e)
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

        private void ButtonLoadSet_Click(object sender, RoutedEventArgs e)
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
                MassSourcesCreator sourcesCreator = new MassSourcesCreator(StartProgram.IsOsTrader);

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string fileStr = reader.ReadToEnd();
                    sourcesCreator.LoadFromString(fileStr);
                    LoadSettingsOnGui(sourcesCreator);
                }
            }
            catch (Exception error)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                ui.ShowDialog();
            }
        }

        private MassSourcesCreator GetCurSettings()
        {
            MassSourcesCreator curCreator = new MassSourcesCreator(StartProgram.IsTester);

            curCreator.PortfolioName = ComboBoxPortfolio.Text;
            if (CheckBoxIsEmulator.IsChecked != null)
            {
                curCreator.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
            }
            TimeFrame timeFrame;
            Enum.TryParse(ComboBoxTimeFrame.Text, out timeFrame);

            curCreator.TimeFrame = timeFrame;
            Enum.TryParse(ComboBoxTypeServer.Text, true, out curCreator.ServerType);

            CandleMarketDataType createType;
            Enum.TryParse(ComboBoxCandleMarketDataType.Text, true, out createType);
            curCreator.CandleMarketDataType = createType;

            CandleCreateMethodType methodType;
            Enum.TryParse(ComboBoxCandleCreateMethodType.Text, true, out methodType);

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

            curCreator.CandleCreateMethodType = methodType;

            if (CheckBoxSetForeign.IsChecked.HasValue)
            {
                curCreator.SetForeign = CheckBoxSetForeign.IsChecked.Value;
            }

            curCreator.RencoPunktsToCloseCandleInRencoType = _rencoPuncts;
            curCreator.CountTradeInCandle = _countTradesInCandle;
            curCreator.VolumeToCloseCandleInVolumeType = _volumeToClose;
            curCreator.DeltaPeriods = _deltaPeriods;
            curCreator.RangeCandlesPunkts = _rangeCandlesPunkts;
            curCreator.ReversCandlesPunktsMinMove = _reversCandlesPunktsMinMove;
            curCreator.ReversCandlesPunktsBackMove = _reversCandlesPunktsBackMove;
            curCreator.SaveTradesInCandles = _saveTradesInCandles;

            if (CheckBoxRencoIsBuildShadows.IsChecked != null)
            {
                curCreator.RencoIsBuildShadows = CheckBoxRencoIsBuildShadows.IsChecked.Value;
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

        private void LoadSettingsOnGui(MassSourcesCreator curCreator)
        {
            ComboBoxPortfolio.Text = curCreator.PortfolioName;
            CheckBoxIsEmulator.IsChecked = curCreator.EmulatorIsOn;
            ComboBoxTimeFrame.Text = curCreator.TimeFrame.ToString();
            ComboBoxTypeServer.Text = curCreator.ServerType.ToString();
            ComboBoxCandleMarketDataType.Text = curCreator.CandleMarketDataType.ToString();
            ComboBoxCandleCreateMethodType.Text = curCreator.CandleCreateMethodType.ToString();
            ComboBoxComissionType.Text = curCreator.ComissionType.ToString();
            ComboBoxClass.SelectedItem =  curCreator.SecuritiesClass.ToString();
            TextBoxComissionValue.Text = curCreator.ComissionValue.ToString();

            CheckBoxSetForeign.IsChecked = curCreator.SetForeign;

            _rencoPuncts = curCreator.RencoPunktsToCloseCandleInRencoType;
            _countTradesInCandle = curCreator.CountTradeInCandle;
            _volumeToClose = curCreator.VolumeToCloseCandleInVolumeType;
            _deltaPeriods = curCreator.DeltaPeriods;
            _rangeCandlesPunkts = curCreator.RangeCandlesPunkts;
            _reversCandlesPunktsMinMove = curCreator.ReversCandlesPunktsMinMove;
            _reversCandlesPunktsBackMove = curCreator.ReversCandlesPunktsBackMove;
            _saveTradesInCandles = curCreator.SaveTradesInCandles;

            CheckBoxRencoIsBuildShadows.IsChecked = curCreator.RencoIsBuildShadows;
            
            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                DataGridViewCheckBoxCell checkBoxCell = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[6];

                string securityName = _gridSecurities.Rows[i].Cells[3].Value.ToString();

                bool isInArray = false;

                for(int j = 0; j< curCreator.SecuritiesNames.Count;j++)
                {
                    if (curCreator.SecuritiesNames[j].SecurityName == securityName)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if(isInArray)
                {
                    checkBoxCell.Value = true;
                }
                else
                {
                    checkBoxCell.Value = false;
                }
            }
        }
    }
}