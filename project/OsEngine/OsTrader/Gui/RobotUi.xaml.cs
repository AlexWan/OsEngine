/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Layout;
using OsEngine.Alerts;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsTrader.Gui
{
    public partial class RobotUi
    {
        #region Constructor

        public RobotUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard,null, StartUiToPainter.IsOsTrader,
                ComboBoxQuantityPerPageActive, BackButtonActiveList, NextButtonActiveList, null,
                null, null);

            _strategyKeeper = new OsTraderMaster(GridChart, ChartHostPanel, HostGlass, HostOpenPosition, HostClosePosition,
                                         HostBotLog, HostBotLogPrime, RectChart, HostAllert, TabControlBotsName,
                                         TabControlBotTab,TextBoxPrice,GridChartControlPanel, StartProgram.IsOsTrader, 
                                         TabControlControl, HostGrids);

            _strategyKeeper.CreateGlobalPositionController(HostAllPosition);

            Closing += RobotUi_Closing;
           
            LocationChanged += RobotUi_LocationChanged;

            CheckBoxPaintOnOff.IsChecked = true;
            CheckBoxPaintOnOff.Click += CheckBoxPaintOnOff_Click;
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;
            Local();
            TabControlControl.SelectedIndex = 3;

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "botStationUi");

            _ordersPainter = ServerMaster._ordersStorage;
            Instance = this;
        }

        private OsTraderMaster _strategyKeeper;

        private ServerMasterOrdersPainter _ordersPainter;

        public static RobotUi Instance;

        void RobotUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label48);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    e.Cancel = true;
                    return;
                }

                ServerMaster.AbortAll();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void Local()
        {
            Title = Title + " " + OsEngine.PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation;
            TabPozition.Header = OsLocalization.Trader.Label18;
            TabItemClosedPos.Header = OsLocalization.Trader.Label19;
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TextBoxPositionBord.Header = OsLocalization.Trader.Label21;
            TextBoxPositionAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogBot.Header = OsLocalization.Trader.Label23;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemMarketDepth.Header = OsLocalization.Trader.Label25;
            TabItemAlerts.Header = OsLocalization.Trader.Label26;
            TabItemControl.Header = OsLocalization.Trader.Label27;
            ButtonBuyFast.Content = OsLocalization.Trader.Label28;
            ButtonSellFast.Content = OsLocalization.Trader.Label29;
            TextBoxVolumeInterText.Text = OsLocalization.Trader.Label30;
            TextBoxPriceText.Text = OsLocalization.Trader.Label31;
            ButtonBuyLimit.Content = OsLocalization.Trader.Label32;
            ButtonSellLimit.Content = OsLocalization.Trader.Label33;
            ButtonCloseLimit.Content = OsLocalization.Trader.Label34;
            LabelGeneralSettings.Content = OsLocalization.Trader.Label35;
            LabelBotControl.Content = OsLocalization.Trader.Label36;
            ButtonServer.Content = OsLocalization.Trader.Label37;
            ButtonNewBot.Content = OsLocalization.Trader.Label38;
            ButtonDeleteBot.Content = OsLocalization.Trader.Label39;
            ButtonJournalCommunity.Content = OsLocalization.Trader.Label40;
            ButtonRiskManagerCommunity.Content = OsLocalization.Trader.Label41;
            CheckBoxPaintOnOff.Content = OsLocalization.Trader.Label42;
            ButtonStrategSettingsIndividual.Content = OsLocalization.Trader.Label43;
            ButtonRedactTab.Content = OsLocalization.Trader.Label44;
            ButtonStrategParametr.Content = OsLocalization.Trader.Label45;
            ButtonRiskManager.Content = OsLocalization.Trader.Label46;
            ButtonStrategSettings.Content = OsLocalization.Trader.Label47;
            ButtonAddVisualAlert.Content = OsLocalization.Trader.Label440;
            ButtonAddPriceAlert.Content = OsLocalization.Trader.Label441;
            TabItemGrids.Header = OsLocalization.Trader.Label437;
            LabelPageActive.Content = OsLocalization.Trader.Label576;
            LabelFromActive.Content = OsLocalization.Trader.Label577;
            LabelCountActive.Content = OsLocalization.Trader.Label578;

        }

        #endregion

        #region Location

        void TabControlBotsName_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                double up = TabControlBotsName.ActualHeight - 28;

                if (up < 0)
                {
                    up = 0;
                }

                GreedChartPanel.Margin = new Thickness(5, up, 315, 10);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RobotUi_LocationChanged(object sender, EventArgs e)
        {
            try
            {
                WindowCoordinate.X = Convert.ToDecimal(Left);
                WindowCoordinate.Y = Convert.ToDecimal(Top);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region  Main menu

        void CheckBoxPaintOnOff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CheckBoxPaintOnOff.IsChecked.HasValue &&
                    CheckBoxPaintOnOff.IsChecked.Value)
                {
                    _strategyKeeper.StartPaint();
                }
                else
                {
                    _strategyKeeper.StopPaint();
                }
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerMaster.ShowDialog(false);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonNewBot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.CreateNewBot();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonDeleteBot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                _strategyKeeper.DeleteRobotActive();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region  Bot managment

        private void ButtonStrategManualSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.BotManualSettingsDialog();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonJournalCommunity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.ShowCommunityJournal(1, Top + ButtonJournalCommunity.ActualHeight, Left + ButtonJournalCommunity.ActualHeight);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRedactTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.BotTabConnectorDialog();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRiskManagerCommunity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.ShowRiskManagerDialog();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.BotShowRiskManager();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonStrategParametr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.BotShowParametersDialog();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Trading

        private void ButtonStrategIndividualSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.BotIndividualSettings();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal volume;
                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }
                _strategyKeeper.BotBuyMarket(volume);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void buttonSellFast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal volume;
                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }
                _strategyKeeper.BotSellMarket(volume);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal volume;
                try
                {
                    volume = Decimal.Parse(TextBoxVolumeFast.Text.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }

                decimal price;

                try
                {
                    price = TextBoxPrice.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                if (price == 0)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                _strategyKeeper.BotBuyLimit(volume, price);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal volume;
                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }

                decimal price;

                try
                {
                    price = TextBoxPrice.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                if (price == 0)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                _strategyKeeper.BotSellLimit(volume, price);
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonCloseLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _strategyKeeper.CancelLimits();
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Alert

        private void ButtonAddVisualAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BotPanel _panel = _strategyKeeper._activePanel;

                if (_panel == null)
                {
                    return;
                }

                if (_panel.ActiveTab == null)
                {
                    _panel.SendNewLogMessage(OsLocalization.Trader.Label438, LogMessageType.Error);
                    return;
                }

                if (_panel.ActiveTab is BotTabSimple)
                {
                    BotTabSimple tab = (BotTabSimple)_panel.ActiveTab;

                    if (tab.IsConnected == false)
                    {
                        _panel.SendNewLogMessage(OsLocalization.Trader.Label442, LogMessageType.Error);
                        return;
                    }

                    tab._alerts.ShowAlertNewDialog(AlertType.ChartAlert);
                }
                else
                {
                    _panel.SendNewLogMessage(OsLocalization.Trader.Label439, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAddPriceAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BotPanel _panel = _strategyKeeper._activePanel;

                if (_panel == null)
                {
                    return;
                }

                if (_panel.ActiveTab == null)
                {
                    _panel.SendNewLogMessage(OsLocalization.Trader.Label438, LogMessageType.Error);
                    return;
                }

                if (_panel.ActiveTab is BotTabSimple)
                {
                    BotTabSimple tab = (BotTabSimple)_panel.ActiveTab;

                    if (tab.IsConnected == false)
                    {
                        _panel.SendNewLogMessage(OsLocalization.Trader.Label442, LogMessageType.Error);
                        return;
                    }

                    tab._alerts.ShowAlertNewDialog(AlertType.PriceAlert);
                }
                else
                {
                    _panel.SendNewLogMessage(OsLocalization.Trader.Label439, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _strategyKeeper.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion
    }
}