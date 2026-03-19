/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    public partial class PositionAddingUi2 : Window
    {
        public PositionAddingUi2(BotTabSimple tab, AddPositionType addPositionType, Position position)
        {
            InitializeComponent();

            StickyBorders.Listen(this);

            Tab = tab;
            Position = position;

            SetTitle();
            SetButtonContent();

            ActivateMarketDepth();
            Tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;
            Tab.BestBidAskChangeEvent += Tab_BestBidAskChangeEvent;
            Tab.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;
            Closed += PositionAdding_Closed;

            LabelServerTypeValue.Content = Tab.Connector.ServerType;
            LabelSecurityValue.Content = Tab.Connector.SecurityName;
            LabelTabNameValue.Content = Tab.TabName;

            LabelServerType.Content = OsLocalization.Trader.Label178 + ":";
            LabelSecurity.Content = OsLocalization.Trader.Label102 + ":";
            LabelTabName.Content = OsLocalization.Trader.Label194 + ":";
            LabelOpenVolume.Content = OsLocalization.Trader.Label223 + ":";
            LabelPosState.Content = OsLocalization.Trader.Label224 + ":";
            LabelPosNumber.Content = OsLocalization.Trader.Label225 + ":";
            LabelOpenSide.Content = OsLocalization.Trader.Label228 + ":";

            LabelLimitPrice.Content = OsLocalization.Trader.Label205;
            LabelStopPrice.Content = OsLocalization.Trader.Label205;
            LabelFakePrice.Content = OsLocalization.Trader.Label205;

            LabelLimitVolume.Content = OsLocalization.Trader.Label30;
            LabelMarketVolume.Content = OsLocalization.Trader.Label30;
            LabelStopVolume.Content = OsLocalization.Trader.Label30;
            LabelFakeVolume.Content = OsLocalization.Trader.Label30;

            LabelStopActivationPrice.Content = OsLocalization.Trader.Label206;
            LabelStopActivationType.Content = OsLocalization.Trader.Label207;
            LabelStopLifeTime.Content = OsLocalization.Trader.Label208;
            LabelStopLifeTimeType.Content = OsLocalization.Trader.Label212;

            ComboBoxStopLimitType.Items.Add(StopActivateType.LowerOrEqual.ToString());
            ComboBoxStopLimitType.Items.Add(StopActivateType.HigherOrEqual.ToString());
            ComboBoxStopLimitType.SelectedItem = StopActivateType.HigherOrEqual.ToString();

            ComboBoxStopLifetimeType.Items.Add(PositionOpenerToStopLifeTimeType.CandlesCount.ToString());
            ComboBoxStopLifetimeType.Items.Add(PositionOpenerToStopLifeTimeType.NoLifeTime.ToString());
            ComboBoxStopLifetimeType.SelectedItem = PositionOpenerToStopLifeTimeType.CandlesCount.ToString();

            TextBoxStopLifeTime.Text = "1";

            TabItemLimit.Header = OsLocalization.Trader.Label200;
            TabItemMarket.Header = OsLocalization.Trader.Label201;
            TabItemStop.Header = OsLocalization.Trader.Label202;
            TabItemFake.Header = OsLocalization.Trader.Label203;

            LabelFakeOpenDate.Content = OsLocalization.Trader.Label229;
            LabelFakeOpenTime.Content = OsLocalization.Trader.Label230;
            ButtonFakeTimeOpenNow.Content = OsLocalization.Trader.Label211;

            LabelPosStateValue.Content = Position.State.ToString();
            LabelPosNumberValue.Content = Position.Number.ToString();
            LabelOpenSideValue.Content = Position.Direction.ToString();
            LabelOpenVolumeValue.Content = Position.OpenVolume.ToStringWithNoEndZero();

            if (addPositionType == AddPositionType.Limit)
            {
                TabControlTypePosition.SelectedIndex = 0;
            }
            else if (addPositionType == AddPositionType.Market)
            {
                TabControlTypePosition.SelectedIndex = 1;
            }
            else if (addPositionType == AddPositionType.Stop)
            {
                TabControlTypePosition.SelectedIndex = 2;
            }
            else if (addPositionType == AddPositionType.Fake)
            {
                TabControlTypePosition.SelectedIndex = 3;
            }

            GlobalGUILayout.Listen(this, "mD_AddPos" + Tab.TabName + Position.Number);

            RepaintMainLabels();
            RepaintCurPosStatus();

            SetNowTimeInControlsFakeOpenPos();

            Task.Run(WatcherThreadPlace);

            if (InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass == null
               || InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonPostPositionAdding.Visibility = Visibility.Visible;
            }

            StartButtonBlinkAnimation();
        }

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            PostGreenPositionAdding.Opacity = 1;
                            PostWhitePositionAdding.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            PostGreenPositionAdding.Opacity = 0;
                            PostWhitePositionAdding.Opacity = 1;
                        }
                        else
                        {
                            PostGreenPositionAdding.Opacity = 1;
                            PostWhitePositionAdding.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
            }
        }

        public BotTabSimple Tab;

        public Position Position;

        private MarketDepthPainter _marketDepthPainter;

        private bool _isDeleted;

        public void SelectTabIndx(AddPositionType addPositionType)
        {
            if (LabelOpenVolumeValue.Dispatcher.CheckAccess() == false)
            {
                LabelOpenVolumeValue.Dispatcher.Invoke(new Action<AddPositionType>(SelectTabIndx), addPositionType);
                return;
            }

            if (addPositionType == AddPositionType.Limit)
            {
                TabControlTypePosition.SelectedIndex = 0;
            }
            else if (addPositionType == AddPositionType.Market)
            {
                TabControlTypePosition.SelectedIndex = 1;
            }
            else if (addPositionType == AddPositionType.Stop)
            {
                TabControlTypePosition.SelectedIndex = 2;
            }
            else if (addPositionType == AddPositionType.Fake)
            {
                TabControlTypePosition.SelectedIndex = 3;
            }
        }

        private void SetTitle()
        {
            if (Position.Direction == Side.Buy)
            {
                Title = OsLocalization.Trader.Label674;
            }
            else if (Position.Direction == Side.Sell)
            {
                Title = OsLocalization.Trader.Label675;
            }
        }

        private void SetButtonContent()
        {
            if (Position.Direction == Side.Buy)
            {
                ButtonAddAtLimit.Content = OsLocalization.Trader.Label676;
                ButtonAddAtMarket.Content = OsLocalization.Trader.Label677;
                ButtonAddAtStop.Content = OsLocalization.Trader.Label678;
                ButtonAddAtFake.Content = OsLocalization.Trader.Label679;
            }
            else if (Position.Direction == Side.Sell)
            {
                ButtonAddAtLimit.Content = OsLocalization.Trader.Label680;
                ButtonAddAtMarket.Content = OsLocalization.Trader.Label681;
                ButtonAddAtStop.Content = OsLocalization.Trader.Label682;
                ButtonAddAtFake.Content = OsLocalization.Trader.Label683;
            }
        }

        private void PositionAdding_Closed(object sender, EventArgs e)
        {
            try
            {
                if (Tab != null)
                {
                    Tab.MarketDepthUpdateEvent -= Tab_MarketDepthUpdateEvent;
                    Tab.BestBidAskChangeEvent -= Tab_BestBidAskChangeEvent;
                    Tab.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent;
                    Tab = null;
                }

                Closed -= PositionAdding_Closed;

                if (_marketDepthPainter != null)
                {
                    _marketDepthPainter.UserClickOnMDAndSelectPriceEvent -= MarketDepthPainter_UserClickOnMDAndSelectPriceEvent;
                    _marketDepthPainter.StopPaint();
                    _marketDepthPainter.Delete();
                    _marketDepthPainter = null;
                }

                Position = null;
            }
            catch
            {
                // ignore
            }

            _isDeleted = true;
        }

        private void SetNowTimeInControlsFakeOpenPos()
        {
            if (TextBoxFakeOpenTime.Dispatcher.CheckAccess() == false)
            {
                TextBoxFakeOpenTime.Dispatcher.Invoke(SetNowTimeInControlsFakeOpenPos);
                return;
            }

            DateTime time = Tab.TimeServerCurrent;

            if (time == DateTime.MinValue)
            {
                time = DateTime.Now;
            }

            DatePickerFakeOpenDate.SelectedDate = time.AddHours(-time.Hour).AddMinutes(-time.Minute).AddSeconds(-time.Second);
            string timeStr = time.Hour.ToString() + ":" + time.Minute.ToString();
            TextBoxFakeOpenTime.Text = timeStr;
        }

        private void Connector_ConnectorStartedReconnectEvent(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, string arg5)
        {
            RepaintMainLabels();
        }

        private void RepaintMainLabels()
        {
            if (LabelServerTypeValue.Dispatcher.CheckAccess() == false)
            {
                LabelServerTypeValue.Dispatcher.Invoke(new Action(RepaintMainLabels));
                return;
            }

            LabelServerTypeValue.Content = Tab.Connector.ServerType;
            LabelSecurityValue.Content = Tab.Connector.SecurityName;
            LabelTabNameValue.Content = Tab.TabName;
        }

        private void ActivateMarketDepth()
        {
            _marketDepthPainter = new MarketDepthPainter(Tab.TabName + "AddPosGui", Tab.Connector);
            _marketDepthPainter.ProcessMarketDepth(Tab.MarketDepth);
            _marketDepthPainter.StartPaint(WinFormsHostMarketDepth, null, null);
            _marketDepthPainter.UserClickOnMDAndSelectPriceEvent += MarketDepthPainter_UserClickOnMDAndSelectPriceEvent;
        }

        private void MarketDepthPainter_UserClickOnMDAndSelectPriceEvent(decimal priceSelectedUser)
        {
            if (TextBoxLimitPrice.Dispatcher.CheckAccess() == false)
            {
                TextBoxLimitPrice.Dispatcher.Invoke(new Action<decimal>(MarketDepthPainter_UserClickOnMDAndSelectPriceEvent), priceSelectedUser);
                return;
            }

            TextBoxLimitPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxStopActivationPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxStopPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxFakePrice.Text = priceSelectedUser.ToStringWithNoEndZero();
        }

        private void Tab_BestBidAskChangeEvent(decimal bid, decimal ask)
        {
            _marketDepthPainter.ProcessBidAsk(bid, ask);
        }

        private void Tab_MarketDepthUpdateEvent(MarketDepth md)
        {
            _marketDepthPainter.ProcessMarketDepth(md);
        }

        private void ButtonFakeTimeOpenNow_Click(object sender, RoutedEventArgs e)
        {
            SetNowTimeInControlsFakeOpenPos();
        }

        private void ButtonAddAtLimit_Click(object sender, RoutedEventArgs e)
        {
            decimal price = 0;
            decimal volume = 0;

            try
            {
                price = TextBoxLimitPrice.Text.ToDecimal();
                volume = TextBoxLimitVolume.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (price == 0
                || volume == 0)
            {
                return;
            }

            if (Position.Direction == Side.Buy)
            {
                Tab.BuyAtLimitToPositionUnsafe(Position, price, volume);
            }
            else if (Position.Direction == Side.Sell)
            {
                Tab.SellAtLimitToPositionUnsafe(Position, price, volume);
            }
        }

        private void ButtonAddAtMarket_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxMarketVolume.Text))
            {
                Tab.SetNewLogMessage(OsLocalization.Trader.Label389, LogMessageType.Error);
                return;
            }

            decimal volume = 0;

            try
            {
                volume = TextBoxMarketVolume.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (volume == 0)
            {
                return;
            }

            if (Position.Direction == Side.Buy)
            {
                Tab.BuyAtMarketToPosition(Position, volume);
            }
            else if (Position.Direction == Side.Sell)
            {
                Tab.SellAtMarketToPosition(Position, volume);
            }
        }

        private void ButtonAddAtStop_Click(object sender, RoutedEventArgs e)
        {
            decimal priceOrder = 0;
            decimal priceActivation = 0;
            decimal volume = 0;
            StopActivateType stopActivateType;
            PositionOpenerToStopLifeTimeType lifeTimeType;
            int lifeTime = 0;

            try
            {
                priceActivation = TextBoxStopActivationPrice.Text.ToDecimal();
                priceOrder = TextBoxStopPrice.Text.ToDecimal();
                volume = TextBoxStopVolume.Text.ToDecimal();
                lifeTime = Convert.ToInt32(TextBoxStopLifeTime.Text);
                Enum.TryParse(ComboBoxStopLimitType.SelectedItem.ToString(), out stopActivateType);
                Enum.TryParse(ComboBoxStopLifetimeType.SelectedItem.ToString(), out lifeTimeType);
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), LogMessageType.Error);
                return;
            }

            if (priceActivation == 0
                || priceOrder == 0
                || volume == 0)
            {
                return;
            }

            if (Position.Direction == Side.Buy)
            {
                Tab.BuyAtStopToPosition(Position, volume, priceOrder, priceActivation, stopActivateType, lifeTime, lifeTimeType);
            }
            else if (Position.Direction == Side.Sell)
            {
                Tab.SellAtStopToPosition(Position, volume, priceOrder, priceActivation, stopActivateType, lifeTime, lifeTimeType);
            }
        }

        private void ButtonAddAtFake_Click(object sender, RoutedEventArgs e)
        {
            decimal price = 0;
            decimal volume = 0;
            DateTime timeOpen = DateTime.MinValue;

            try
            {
                price = TextBoxFakePrice.Text.ToDecimal();
                volume = TextBoxFakeVolume.Text.ToDecimal();

                timeOpen = DatePickerFakeOpenDate.SelectedDate.Value;
                string[] openTimeStr = TextBoxFakeOpenTime.Text.ToString().Split(':');

                timeOpen = timeOpen.AddHours(Convert.ToInt32(openTimeStr[0]));
                timeOpen = timeOpen.AddMinutes(Convert.ToInt32(openTimeStr[1]));
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (price == 0 ||
                 volume == 0 ||
                 timeOpen == DateTime.MinValue)
            {
                return;
            }

            if (Position.Direction == Side.Buy)
            {
                Tab.BuyAtFakeToPosition(Position, volume, price, timeOpen);
            }
            else if (Position.Direction == Side.Sell)
            {
                Tab.SellAtFakeToPosition(Position, volume, price, timeOpen);
            }
        }

        // поток отвечающий за просмотром статуса позиции

        private async void WatcherThreadPlace()
        {
            while (true)
            {
                if (_isDeleted)
                {
                    return;
                }

                await Task.Delay(2000);

                RepaintCurPosStatus();
            }
        }

        private void RepaintCurPosStatus()
        {
            try
            {
                if (LabelOpenVolumeValue.Dispatcher.CheckAccess() == false)
                {
                    LabelOpenVolumeValue.Dispatcher.Invoke(RepaintCurPosStatus);
                    return;
                }

                if (Position == null)
                {
                    return;
                }

                LabelOpenVolumeValue.Content = Position.OpenVolume.ToStringWithNoEndZero();
                LabelPosStateValue.Content = Position.State.ToString();

                if (Position.State == PositionStateType.Done)
                {
                    _isDeleted = true;
                    Close();
                    return;
                }
            }
            catch
            {
                // ignore
            }
        }

        #region Posts collection

        private void ButtonPostPositionAdding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.BotStationLightPosts.Link34.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }

    public enum AddPositionType
    {
        Limit,
        Market,
        Stop,
        Fake
    }
}