/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Charts;
using OsEngine.Market;
using OsEngine.Market.Servers.Tester;
using OsEngine.Journal;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabPairUi.xaml
    /// </summary>
    public partial class BotTabPairUi : Window
    {
        public string NameElement;

        public BotTabPairUi(PairToTrade pair)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);

            _pair = pair;

            NameElement = pair.Name;

            // indicators

            TextBoxCorrelationLookBack.Text = _pair.CorrelationLookBack.ToString();
            TextBoxCointegrationLookBack.Text = _pair.CointegrationLookBack.ToString();
            TextBoxCointegrationDeviation.Text = _pair.CointegrationDeviation.ToString();

            Title = OsLocalization.Trader.Label249;

            LabelCointegration.Content = OsLocalization.Trader.Label238;
            LabelCointegrationDeviation.Content = OsLocalization.Trader.Label239;
            LabelCointegrationLookBack.Content = OsLocalization.Trader.Label240;
            LabelCorrelation.Content = OsLocalization.Trader.Label242;
            LabelCorrelationLookBack.Content = OsLocalization.Trader.Label240;

            ButtonCointegrationReload.Content = OsLocalization.Trader.Label243;
            ButtonCorrelationReload.Content = OsLocalization.Trader.Label243;

            TextBoxCorrelationLookBack.TextChanged += TextBoxCorrelationLookBack_TextChanged;
            TextBoxCointegrationLookBack.TextChanged += TextBoxCointegrationLookBack_TextChanged;
            TextBoxCointegrationDeviation.TextChanged += TextBoxCointegrationDeviation_TextChanged;
            ButtonCorrelationReload.Click += ButtonCorrelationReload_Click;
            ButtonCointegrationReload.Click += ButtonCointegrationReload_Click;

            CheckBoxCointegrationAutoIsOn.Content = OsLocalization.Trader.Label309;
            CheckBoxCorrelationAutoIsOn.Content = OsLocalization.Trader.Label309;

            // trade Logic labels

            LabelSec1Volume.Content = OsLocalization.Trader.Label30;
            LabelSec1Slippage.Content = OsLocalization.Trader.Label92;
            LabelSec1Position.Content = OsLocalization.Trader.Label253;
            LabelSec2Volume.Content = OsLocalization.Trader.Label30;
            LabelSec2Slippage.Content = OsLocalization.Trader.Label92;
            LabelSec2Position.Content = OsLocalization.Trader.Label253;

            ButtonBuy1Sell2.Content = OsLocalization.Trader.Label254;
            ButtonBuy2Sell1.Content = OsLocalization.Trader.Label255;
            ButtonClosePositions.Content = OsLocalization.Trader.Label100;
            ButtonPairJournal.Content = OsLocalization.Trader.Label40;
            LabelSec1Regime.Content = OsLocalization.Trader.Label115;
            LabelSec2Regime.Content = OsLocalization.Trader.Label115;

            UpdateButtonSecConnectionContent();

            // trade Logic settings

            ComboBoxSec1Volume.Items.Add(PairTraderVolumeType.Contract.ToString());
            ComboBoxSec1Volume.Items.Add(PairTraderVolumeType.Currency.ToString());
            ComboBoxSec1Volume.SelectedItem = _pair.Sec1VolumeType.ToString();
            ComboBoxSec1Volume.SelectionChanged += ComboBoxSec1Volume_SelectionChanged;

            ComboBoxSec1Slippage.Items.Add(PairTraderSlippageType.Absolute.ToString());
            ComboBoxSec1Slippage.Items.Add(PairTraderSlippageType.Percent.ToString());
            ComboBoxSec1Slippage.SelectedItem = _pair.Sec1SlippageType.ToString();
            ComboBoxSec1Slippage.SelectionChanged += ComboBoxSec1Slippage_SelectionChanged;

            ComboBoxSec2Volume.Items.Add(PairTraderVolumeType.Contract.ToString());
            ComboBoxSec2Volume.Items.Add(PairTraderVolumeType.Currency.ToString());
            ComboBoxSec2Volume.SelectedItem = _pair.Sec2VolumeType.ToString();
            ComboBoxSec2Volume.SelectionChanged += ComboBoxSec2Volume_SelectionChanged;

            ComboBoxSec2Slippage.Items.Add(PairTraderSlippageType.Absolute.ToString());
            ComboBoxSec2Slippage.Items.Add(PairTraderSlippageType.Percent.ToString());
            ComboBoxSec2Slippage.SelectedItem = _pair.Sec2SlippageType.ToString();
            ComboBoxSec2Slippage.SelectionChanged += ComboBoxSec2Slippage_SelectionChanged;

            ComboBoxSec1Regime.Items.Add(PairTraderSecurityTradeRegime.Off.ToString());
            ComboBoxSec1Regime.Items.Add(PairTraderSecurityTradeRegime.Limit.ToString());
            ComboBoxSec1Regime.Items.Add(PairTraderSecurityTradeRegime.Market.ToString());

            if(_pair.Tab1.StartProgram == StartProgram.IsOsTrader)
            {
                ComboBoxSec1Regime.Items.Add(PairTraderSecurityTradeRegime.Second.ToString());
            }

            ComboBoxSec1Regime.SelectedItem = _pair.Sec1TradeRegime.ToString();
            ComboBoxSec1Regime.SelectionChanged += ComboBoxSec1Regime_SelectionChanged;

            ComboBoxSec2Regime.Items.Add(PairTraderSecurityTradeRegime.Off.ToString());
            ComboBoxSec2Regime.Items.Add(PairTraderSecurityTradeRegime.Limit.ToString());
            ComboBoxSec2Regime.Items.Add(PairTraderSecurityTradeRegime.Market.ToString());

            if (_pair.Tab2.StartProgram == StartProgram.IsOsTrader)
            {
                ComboBoxSec2Regime.Items.Add(PairTraderSecurityTradeRegime.Second.ToString());
            }
            
            ComboBoxSec2Regime.SelectedItem = _pair.Sec2TradeRegime.ToString();
            ComboBoxSec2Regime.SelectionChanged += ComboBoxSec2Regime_SelectionChanged;

            UpdateCurPositionInTextBox();
           
            TextBoxSec1Volume.Text = _pair.Sec1Volume.ToString();
            TextBoxSec1Slippage.Text = _pair.Sec1Slippage.ToString();

            TextBoxSec1Volume.TextChanged += TextBoxSec1Volume_TextChanged;
            TextBoxSec1Slippage.TextChanged += TextBoxSec1Slippage_TextChanged;

            TextBoxSec2Volume.Text = _pair.Sec2Volume.ToString();
            TextBoxSec2Slippage.Text = _pair.Sec2Slippage.ToString();

            TextBoxSec2Volume.TextChanged += TextBoxSec2Volume_TextChanged;
            TextBoxSec2Slippage.TextChanged += TextBoxSec2Slippage_TextChanged;

            _pair.Tab1.PositionNetVolumeChangeEvent += Tab_PositionNetVolumeChangeEvent;
            _pair.Tab2.PositionNetVolumeChangeEvent += Tab_PositionNetVolumeChangeEvent;
            

            ButtonSec1Connection.Click += ButtonSec1Connection_Click;
            ButtonSec2Connection.Click += ButtonSec2Connection_Click;

            ButtonBuy1Sell2.Click += ButtonBuy1Sell2_Click;
            ButtonBuy2Sell1.Click += ButtonBuy2Sell1_Click;
            ButtonClosePositions.Click += ButtonClosePositions_Click;
            ButtonPairJournal.Click += ButtonPairJournal_Click;
            ButtonHideShowRightPanel.Click += ButtonHideShowRightPanel_Click;

            CheckBoxCorrelationAutoIsOn.IsChecked = pair.AutoRebuildCorrelation;
            CheckBoxCointegrationAutoIsOn.IsChecked = pair.AutoRebuildCointegration;
            CheckBoxCorrelationAutoIsOn.Click += CheckBoxCorrelationAutoIsOn_Click;
            CheckBoxCointegrationAutoIsOn.Click += CheckBoxCointegrationAutoIsOn_Click;

            // остальное

            Closed += BotTabPairUi_Closed;

            GlobalGUILayout.Listen(this, "botTabPairUi_" + pair.Name);

            PaintCandles();

            _pair.Tab1.CandleUpdateEvent += Tab1_CandleUpdateEvent;
            _pair.Tab2.CandleUpdateEvent += Tab2_CandleUpdateEvent;

            _pair.Tab1.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;
            _pair.Tab2.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;

            if (pair.Tab1.StartProgram == StartProgram.IsTester)
            {
                _pair.Tab1.CandleFinishedEvent += Tab1_CandleUpdateEvent;
                _pair.Tab2.CandleFinishedEvent += Tab2_CandleUpdateEvent;
            }

            CreateCorrelationChart();
            UpdateCorrelationChart();

            CreateCointegrationChart();
            UpdateCointegrationChart();

            _pair.CorrelationChangeEvent += _pair_CorrelationChangeEvent;
            _pair.CointegrationChangeEvent += _pair_CointegrationChangeEvent;

            if(_pair.Tab1.StartProgram == StartProgram.IsTester)
            { // управление прорисовкой для тестера

                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

                server.TestingEndEvent += Server_TestingEndEvent;
                server.TestingFastEvent += Server_TestingFastEvent;
                server.TestingStartEvent += Server_TestingStartEvent;
                server.TestRegimeChangeEvent += Server_TestRegimeChangeEvent;
            }

            _pair.PairDeletedEvent += _pair_PairDeletedEvent;

            _pair.Tab1.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab1.PositionOpeningFailEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab1.PositionClosingFailEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab1.PositionClosingSuccesEvent += Tab_PositionOpeningSuccesEvent;

            _pair.Tab2.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab2.PositionOpeningFailEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab2.PositionClosingFailEvent += Tab_PositionOpeningSuccesEvent;
            _pair.Tab2.PositionClosingSuccesEvent += Tab_PositionOpeningSuccesEvent;

            UpdatePositionsOnChart();

            if(_pair.ShowTradePanelOnChart == false)
            {
                ButtonHideShowRightPanel_Click(null, null);
            }
        }

        private void BotTabPairUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= BotTabPairUi_Closed;

                TextBoxCorrelationLookBack.TextChanged -= TextBoxCorrelationLookBack_TextChanged;
                TextBoxCointegrationLookBack.TextChanged -= TextBoxCointegrationLookBack_TextChanged;
                TextBoxCointegrationDeviation.TextChanged -= TextBoxCointegrationDeviation_TextChanged;

                ButtonCorrelationReload.Click -= ButtonCorrelationReload_Click;
                ButtonCointegrationReload.Click -= ButtonCointegrationReload_Click;
                ButtonBuy1Sell2.Click -= ButtonBuy1Sell2_Click;
                ButtonBuy2Sell1.Click -= ButtonBuy2Sell1_Click;
                ButtonClosePositions.Click -= ButtonClosePositions_Click;
                ButtonPairJournal.Click -= ButtonPairJournal_Click;
                ButtonHideShowRightPanel.Click -= ButtonHideShowRightPanel_Click;

                CheckBoxCorrelationAutoIsOn.Click -= CheckBoxCorrelationAutoIsOn_Click;
                CheckBoxCointegrationAutoIsOn.Click -= CheckBoxCointegrationAutoIsOn_Click;

                TextBoxSec1Volume.TextChanged -= TextBoxSec1Volume_TextChanged;
                TextBoxSec1Slippage.TextChanged -= TextBoxSec1Slippage_TextChanged;

                TextBoxSec2Volume.TextChanged -= TextBoxSec2Volume_TextChanged;
                TextBoxSec2Slippage.TextChanged -= TextBoxSec2Slippage_TextChanged;

                TextBoxSec1Volume.TextChanged -= TextBoxSec1Volume_TextChanged;
                TextBoxSec1Slippage.TextChanged -= TextBoxSec1Slippage_TextChanged;

                TextBoxSec2Volume.TextChanged -= TextBoxSec2Volume_TextChanged;
                TextBoxSec2Slippage.TextChanged -= TextBoxSec2Slippage_TextChanged;

                if (_chartSec1 != null)
                {
                    if (_chartSec1.Indicators != null)
                    {
                        _chartSec1.Indicators.Clear();
                    }

                    _chartSec1.Delete();
                    _chartSec1 = null;
                }

                if (_chartSec2 != null)
                {
                    if (_chartSec2.Indicators != null)
                    {
                        _chartSec2.Indicators.Clear();
                    }

                    _chartSec2.Delete();
                    _chartSec2 = null;
                }

                if (_chartCorrelation != null)
                {
                    _chartCorrelation.Series.Clear();
                    _chartCorrelation = null;
                }

                if (_chartCointegration != null)
                {
                    _chartCointegration.Series.Clear();
                    _chartCointegration = null;
                }

                if (_pair.Tab1 != null)
                {
                    _pair.Tab1.CandleUpdateEvent -= Tab1_CandleUpdateEvent;
                    _pair.Tab1.PositionOpeningSuccesEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab1.PositionOpeningFailEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab1.PositionClosingFailEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab1.PositionClosingSuccesEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab1.CandleFinishedEvent -= Tab1_CandleUpdateEvent;

                    if(_pair.Tab1.Connector != null)
                    {
                        _pair.Tab1.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent;
                    }
                   
                    _pair.Tab1.PositionNetVolumeChangeEvent -= Tab_PositionNetVolumeChangeEvent;

                    if (_pair.Tab1.StartProgram == StartProgram.IsTester)
                    { // управление прорисовкой для тестера

                        TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                        server.TestingEndEvent -= Server_TestingEndEvent;
                        server.TestingFastEvent -= Server_TestingFastEvent;
                        server.TestingStartEvent -= Server_TestingStartEvent;
                        server.TestRegimeChangeEvent -= Server_TestRegimeChangeEvent;
                    }
                }

                if(_pair.Tab2 != null)
                {
                    _pair.Tab2.CandleUpdateEvent -= Tab2_CandleUpdateEvent;
                    _pair.Tab2.PositionOpeningSuccesEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab2.PositionOpeningFailEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab2.PositionClosingFailEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab2.PositionClosingSuccesEvent -= Tab_PositionOpeningSuccesEvent;
                    _pair.Tab2.CandleFinishedEvent -= Tab2_CandleUpdateEvent;

                    if(_pair.Tab2.Connector != null)
                    {
                        _pair.Tab2.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent;
                    }

                    _pair.Tab2.PositionNetVolumeChangeEvent -= Tab_PositionNetVolumeChangeEvent;
                }

                _pair.CorrelationChangeEvent -= _pair_CorrelationChangeEvent;
                _pair.CointegrationChangeEvent -= _pair_CointegrationChangeEvent;
                _pair.PairDeletedEvent -= _pair_PairDeletedEvent;
                _pair = null;


            }
            catch
            {
                // ignore
            }
        }

        private void ButtonHideShowRightPanel_Click(object sender, RoutedEventArgs e)
        {
            bool showTradePanel = false;

            if (GridTradePanel.Width == 0)
            {
                GridIndicatorsSettings.Visibility = Visibility.Visible;
                GridTradePanel.Visibility = Visibility.Visible;
                GridIndicatorsSettings.Width = 305;
                GridTradePanel.Width = 305;
                ButtonHideShowRightPanel.Content = ">";
                GreedChartPanel.Margin = new Thickness(0, 0, 308, 0);
                showTradePanel = true;
            }
            else
            {
                GridIndicatorsSettings.Visibility = Visibility.Hidden;
                GridTradePanel.Visibility = Visibility.Hidden;
                GridIndicatorsSettings.Width = 0;
                GridTradePanel.Width = 0;
                ButtonHideShowRightPanel.Content = "<";
                GreedChartPanel.Margin = new Thickness(0, 0, 15, 0);
            }

            if(sender != null)
            {
                _pair.ShowTradePanelOnChart = showTradePanel;
            }
        }

        private void CheckBoxCointegrationAutoIsOn_Click(object sender, RoutedEventArgs e)
        {
            _pair.AutoRebuildCointegration = CheckBoxCointegrationAutoIsOn.IsChecked.Value;
            _pair.Save();
        }

        private void CheckBoxCorrelationAutoIsOn_Click(object sender, RoutedEventArgs e)
        {
            _pair.AutoRebuildCorrelation = CheckBoxCorrelationAutoIsOn.IsChecked.Value;
            _pair.Save();
        }

        JournalUi2 _journalUi2;

        private void ButtonPairJournal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_journalUi2 != null)
                {
                    _journalUi2.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();
                List<Journal.Journal> journals = new List<Journal.Journal>();

                journals.Add(_pair.Tab1.GetJournal());
                journals.Add(_pair.Tab2.GetJournal());

                BotPanelJournal botPanel = new BotPanelJournal();
                botPanel.BotName = _pair.Name + " sec1 " + _pair.Tab1.Connector.SecurityName;
                botPanel.BotClass = "PairTrader";
                botPanel._Tabs = new List<BotTabJournal>();

                BotTabJournal botTabJournal = new BotTabJournal();
                botTabJournal.TabNum = 1;
                botTabJournal.Journal = journals[0];

                botPanel._Tabs.Add(botTabJournal);
                panelsJournal.Add(botPanel);


                botPanel = new BotPanelJournal();
                botPanel.BotName = _pair.Name + " sec2 " + _pair.Tab2.Connector.SecurityName;
                botPanel.BotClass = "PairTrader";
                botPanel._Tabs = new List<BotTabJournal>();

                botTabJournal = new BotTabJournal();
                botTabJournal.TabNum = 1;
                botTabJournal.Journal = journals[1];

                botPanel._Tabs.Add(botTabJournal);
                panelsJournal.Add(botPanel);

                _journalUi2 = new JournalUi2(panelsJournal, _pair.Tab1.StartProgram);
                _journalUi2.Closed += _journalUi_Closed;
                _journalUi2.LogMessageEvent += _journalUi2_LogMessageEvent;
                _journalUi2.Show();

            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void _journalUi2_LogMessageEvent(string message, LogMessageType type)
        {
            if (_pair == null)
            {
                return;
            }
            _pair.Tab1.SetNewLogMessage(message, type);
        }

        private void _journalUi_Closed(object sender, EventArgs e)
        {
            _journalUi2.Closed -= _journalUi_Closed;
            _journalUi2.LogMessageEvent -= _journalUi2_LogMessageEvent;
            _journalUi2.IsErase = true;
            _journalUi2 = null;
        }

        private void ComboBoxSec2Regime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec2Regime.SelectedItem.ToString(), out _pair.Sec2TradeRegime);
            _pair.Save();
        }

        private void ComboBoxSec1Regime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec1Regime.SelectedItem.ToString(), out _pair.Sec1TradeRegime);
            _pair.Save();
        }

        private void ButtonClosePositions_Click(object sender, RoutedEventArgs e)
        {
            _pair.ClosePositions();
        }

        private void ButtonBuy2Sell1_Click(object sender, RoutedEventArgs e)
        {
            if(_pair.HavePositions)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label256);

                ui.ShowDialog();

                if(ui.UserAcceptActioin == false)
                {
                    return;
                }
            }

            _pair.SellSec1BuySec2();
        }

        private void ButtonBuy1Sell2_Click(object sender, RoutedEventArgs e)
        {
            if (_pair.HavePositions)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label256);

                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }
            }

            _pair.BuySec1SellSec2();
        }

        private void ButtonSec1Connection_Click(object sender, RoutedEventArgs e)
        {
            _pair.Tab1.ShowConnectorDialog();
        }

        private void ButtonSec2Connection_Click(object sender, RoutedEventArgs e)
        {
            _pair.Tab2.ShowConnectorDialog();
        }

        private void TextBoxSec2Slippage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.Sec2Slippage = TextBoxSec2Slippage.Text.ToString().ToDecimal();
                _pair.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxSec2Volume_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.Sec2Volume = TextBoxSec2Volume.Text.ToString().ToDecimal();
                _pair.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxSec1Slippage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.Sec1Slippage = TextBoxSec1Slippage.Text.ToString().ToDecimal();
                _pair.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxSec1Volume_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.Sec1Volume = TextBoxSec1Volume.Text.ToString().ToDecimal();
                _pair.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxSec2Slippage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec2Slippage.SelectedItem.ToString(), out _pair.Sec2SlippageType);
            _pair.Save();
        }

        private void ComboBoxSec2Volume_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec2Volume.SelectedItem.ToString(), out _pair.Sec2VolumeType);
            _pair.Save();
        }

        private void ComboBoxSec1Slippage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec1Slippage.SelectedItem.ToString(), out _pair.Sec1SlippageType);
            _pair.Save();
        }

        private void ComboBoxSec1Volume_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSec1Volume.SelectedItem.ToString(), out _pair.Sec1VolumeType);
            _pair.Save();
        }

        private void Connector_ConnectorStartedReconnectEvent(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, ServerType arg5)
        {
            UpdateButtonSecConnectionContent();
        }

        private void _pair_PairDeletedEvent()
        {
            Close();
        }

        private void UpdateButtonSecConnectionContent()
        {
            if(ButtonSec1Connection.Dispatcher.CheckAccess() == false)
            {
                ButtonSec1Connection.Dispatcher.Invoke(UpdateButtonSecConnectionContent);
                return;
            }

            ButtonSec1Connection.Content =
                OsLocalization.Trader.Label102 + " 1. "
                + _pair.Tab1.Connector.SecurityName + ". "
                + _pair.Tab1.Connector.TimeFrame + ". "
                + _pair.Tab1.Connector.ServerType + ".";

            ButtonSec2Connection.Content =
                OsLocalization.Trader.Label102 + " 2. "
                + _pair.Tab2.Connector.SecurityName + ". "
                + _pair.Tab2.Connector.TimeFrame + ". "
                + _pair.Tab2.Connector.ServerType + ".";

            Title = OsLocalization.Trader.Label249 + "  " 
                + _pair.Tab1.Connector.SecurityName + " / " + _pair.Tab2.Connector.SecurityName;
        }

        private void Tab_PositionNetVolumeChangeEvent(Position obj)
        {
            UpdateCurPositionInTextBox();
        }

        private void Tab_PositionOpeningSuccesEvent(Position pos)
        {
            UpdateCurPositionInTextBox();
            UpdatePositionsOnChart();
        }

        private void UpdateCurPositionInTextBox()
        {
            if (ButtonSec1Connection.Dispatcher.CheckAccess() == false)
            {
                ButtonSec1Connection.Dispatcher.Invoke(UpdateCurPositionInTextBox);
                return;
            }

            TextBoxSec1Position.Text = _pair.Tab1.VolumeNetto.ToString();
            TextBoxSec2Position.Text = _pair.Tab2.VolumeNetto.ToString();
        }

        private void UpdatePositionsOnChart()
        {
            if (ButtonSec1Connection.Dispatcher.CheckAccess() == false)
            {
                ButtonSec1Connection.Dispatcher.Invoke(UpdatePositionsOnChart);
                return;
            }

            _chartSec1.SetPosition(_pair.Tab1.PositionsAll);
            _chartSec2.SetPosition(_pair.Tab2.PositionsAll);
        }

        // изменение значений пользователем

        private void TextBoxCointegrationDeviation_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CointegrationDeviation = TextBoxCointegrationDeviation.Text.ToDecimal();
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        private void TextBoxCointegrationLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CointegrationLookBack = Convert.ToInt32(TextBoxCointegrationLookBack.Text);
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        private void TextBoxCorrelationLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CorrelationLookBack = Convert.ToInt32(TextBoxCorrelationLookBack.Text);
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        // управление прорисовкой во время тестирования

        private void Server_TestRegimeChangeEvent(TesterRegime currentTestRegime)
        {
            if (currentTestRegime == TesterRegime.Play)
            {
                _chartSec1.BindOff();
            }
            else if (currentTestRegime == TesterRegime.Pause)
            {
                _chartSec1.BindOn();
            }
            else if (currentTestRegime == TesterRegime.PlusOne)
            {
                _chartSec1.BindOn();
            }
        }

        private void Server_TestingStartEvent()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingStartEvent));
                return;
            }

            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec2.StartPaint(null, HostSec2, null);
            HostCorrelation.Child = _chartCorrelation;
            HostCointegration.Child = _chartCointegration;
        }

        private void Server_TestingFastEvent()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingFastEvent));
                return;
            }

            if (HostCointegration.Child == null)
            { // нужно показывать
                _chartSec1.StartPaint(null, HostSec1, null);
                _chartSec2.StartPaint(null, HostSec2, null);
                HostCorrelation.Child = _chartCorrelation;
                HostCointegration.Child = _chartCointegration;
            }
            else
            { // нужно прятать
                _chartSec1.StopPaint();
                _chartSec2.StopPaint();
                HostCorrelation.Child = null;
                HostCointegration.Child = null;
            }
        }

        private void Server_TestingEndEvent()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingEndEvent));
                return;
            }

            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.BindOn();
            _chartSec2.StartPaint(null, HostSec2, null);
            HostCorrelation.Child = _chartCorrelation;
            HostCointegration.Child = _chartCointegration;
        }

        // обработка нажатий на кнопки

        PairToTrade _pair;

        private void ButtonCointegrationReload_Click(object sender, RoutedEventArgs e)
        {
            _pair.ReloadCointegrationHard();
        }

        private void ButtonCorrelationReload_Click(object sender, RoutedEventArgs e)
        {
            _pair.ReloadCorrelationHard();
        }

        // прорисовка инструментов

        ChartCandleMaster _chartSec1;

        ChartCandleMaster _chartSec2;

        private void PaintCandles()
        {
            _chartSec1 = new ChartCandleMaster(_pair.Name + "sec1", _pair.Tab1.StartProgram);
            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.SetCandles(_pair.Tab1.CandlesAll);

            for(int i = 0;i < _pair.Tab1.Indicators.Count;i++)
            {
                if (_chartSec1.IndicatorIsCreate(_pair.Tab1.Indicators[i].Name) == false)
                {
                    _chartSec1.CreateIndicator(_pair.Tab1.Indicators[i], _pair.Tab1.Indicators[i].NameArea);
                }
            }
    
            _chartSec2 = new ChartCandleMaster(_pair.Name + "sec2", _pair.Tab2.StartProgram);
            _chartSec2.StartPaint(null, HostSec2, null);
            _chartSec2.SetCandles(_pair.Tab2.CandlesAll);

            for (int i = 0; i < _pair.Tab2.Indicators.Count; i++)
            {
                if(_chartSec2.IndicatorIsCreate(_pair.Tab2.Indicators[i].Name) == false)
                {
                    _chartSec2.CreateIndicator(_pair.Tab2.Indicators[i], _pair.Tab2.Indicators[i].NameArea);
                }
            }

            _chartSec1.Bind(_chartSec2);
        }

        private void Tab1_CandleUpdateEvent(System.Collections.Generic.List<Candle> candles)
        {
            _chartSec1?.SetCandles(candles);
        }

        private void Tab2_CandleUpdateEvent(System.Collections.Generic.List<Candle> candles)
        {
            _chartSec2?.SetCandles(candles);
        }

        // прорисовка корреляции

        private Chart _chartCorrelation;

        private void _pair_CorrelationChangeEvent(List<PairIndicatorValue> obj, PairToTrade pair)
        {
            UpdateCorrelationChart();
        }

        private void CreateCorrelationChart()
        {
            _chartCorrelation = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartCorrelation.ChartAreas.Clear();
            _chartCorrelation.ChartAreas.Add(area);
            _chartCorrelation.BackColor = Color.FromArgb(21, 26, 30);
            _chartCorrelation.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartCorrelation.ChartAreas != null && i < _chartCorrelation.ChartAreas.Count; i++)
            {
                _chartCorrelation.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartCorrelation.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartCorrelation.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartCorrelation.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartCorrelation.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartCorrelation.Series.Clear();
            _chartCorrelation.Series.Add(series);

            HostCorrelation.Child = _chartCorrelation;
        }

        private void UpdateCorrelationChart()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(UpdateCorrelationChart));
                return;
            }

            List<PairIndicatorValue> values = _pair.CorrelationList;

            if (values == null
                || values.Count == 0)
            {
                return;
            }

            try
            {
                Series series = _chartCorrelation.Series[0];
                series.Points.ClearFast();

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = values[i].Value;


                    series.Points.AddXY(i + 1, val);

                    if (val > 0)
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "Time " + values[i].Time + "\n" +
                         "Value: " + val.ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }

                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

        // прорисовка коинтеграции

        private Chart _chartCointegration;

        private void _pair_CointegrationChangeEvent(List<PairIndicatorValue> obj, PairToTrade pair)
        {
            UpdateCointegrationChart();
        }

        private void CreateCointegrationChart()
        {
            _chartCointegration = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartCointegration.ChartAreas.Clear();
            _chartCointegration.ChartAreas.Add(area);
            _chartCointegration.BackColor = Color.FromArgb(21, 26, 30);
            _chartCointegration.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartCointegration.ChartAreas != null && i < _chartCointegration.ChartAreas.Count; i++)
            {
                _chartCointegration.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartCointegration.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartCointegration.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartCointegration.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartCointegration.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            // Столбцы коинтеграции
            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartCointegration.Series.Clear();
            _chartCointegration.Series.Add(series);

            // Линия вернхняя

            Series seriesUpLine = new Series();
            seriesUpLine.ChartType = SeriesChartType.Line;
            seriesUpLine.Color = Color.AliceBlue;
            _chartCointegration.Series.Add(seriesUpLine);

            // Линия нижняя

            Series seriesDownLine = new Series();
            seriesDownLine.ChartType = SeriesChartType.Line;
            seriesDownLine.Color = Color.AliceBlue;
            _chartCointegration.Series.Add(seriesDownLine);

            HostCointegration.Child = _chartCointegration;
        }

        private void UpdateCointegrationChart()
        {
            if (_chartCointegration == null)
            {
                return;
            }

            if (_chartCointegration.InvokeRequired)
            {
                _chartCointegration.Invoke(new Action(UpdateCointegrationChart));
                return;
            }

            List<PairIndicatorValue> values = _pair.Cointegration;

            if (values == null
                || values.Count == 0)
            {
                return;
            }

            try
            {
                Series series = _chartCointegration.Series[0];
                series.Points.ClearFast();

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = 0;
                    DateTime time = DateTime.MinValue;

                    try
                    {
                        val = values[i].Value;
                        time = values[i].Time;
                    }
                    catch
                    {
                        continue;
                    }

                    series.Points.AddXY(i + 1, val);

                    if (val > 0)
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "Time " + time + "\n" +
                         "Value: " + val.ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }

                }

                Series seriesUpLine = _chartCointegration.Series[1];
                seriesUpLine.Points.ClearFast();

                if (_pair.LineUpCointegration != 0)
                {
                    seriesUpLine.Points.AddXY(0, _pair.LineUpCointegration);
                    seriesUpLine.Points.AddXY(series.Points.Count, _pair.LineUpCointegration);
                }

                Series seriesDownLine = _chartCointegration.Series[2];
                seriesDownLine.Points.ClearFast();

                if (_pair.LineDownCointegration != 0)
                {
                    seriesDownLine.Points.AddXY(0, _pair.LineDownCointegration);
                    seriesDownLine.Points.AddXY(series.Points.Count, _pair.LineDownCointegration);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }


    }
}