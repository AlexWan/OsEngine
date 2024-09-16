using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// Interaction logic for PositionCloseUi2.xaml
    /// </summary>
    public partial class PositionCloseUi2 : Window
    {
        public PositionCloseUi2(BotTabSimple tab, ClosePositionType closePositionType, Position position)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);

            Title = OsLocalization.Trader.Label226;

            Tab = tab;
            Position = position;
            ActivateMarketDepth();
            Tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;
            Tab.BestBidAskChangeEvent += Tab_BestBidAskChangeEvent;
            Tab.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;
            Closed += PositionOpenUi2_Closed;

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
            LabelProfitPrice.Content = OsLocalization.Trader.Label205;

            LabelLimitVolumeToClose.Content = OsLocalization.Trader.Label30;
            LabelMarketVolumeToClose.Content = OsLocalization.Trader.Label30;
            LabelFakeVolume.Content = OsLocalization.Trader.Label30;

            LabelProfitActivationPrice.Content = OsLocalization.Trader.Label206;
            LabelStopActivationPrice.Content = OsLocalization.Trader.Label206;

            TabItemLimit.Header = OsLocalization.Trader.Label200;
            TabItemMarket.Header = OsLocalization.Trader.Label201;
            TabItemStop.Header = OsLocalization.Trader.Label202;
            TabItemFake.Header = OsLocalization.Trader.Label203;
            TabItemProfit.Header = OsLocalization.Trader.Label222;

            LabelFakeOpenDate.Content = OsLocalization.Trader.Label229;
            LabelFakeOpenTime.Content = OsLocalization.Trader.Label230;
            ButtonFakeTimeOpenNow.Content = OsLocalization.Trader.Label211;

            ButtonCloseAtLimit.Content = OsLocalization.Trader.Label217;
            ButtonCloseAtMarket.Content = OsLocalization.Trader.Label218;
            ButtonCloseAtStop.Content = OsLocalization.Trader.Label219;
            ButtonCloseAtProfit.Content = OsLocalization.Trader.Label220;
            ButtonCloseAtFake.Content = OsLocalization.Trader.Label221;

            ButtonRevokeLimit.Content = OsLocalization.Trader.Label419;
            ButtonRevokeProfit.Content = OsLocalization.Trader.Label419;
            ButtonRevokeStop.Content = OsLocalization.Trader.Label419;

            GlobalGUILayout.Listen(this, "mD_ClosePos" + Tab.TabName + Position.Number);

            SetNowTimeInControlsFakeOpenPos();

            RepaintMainLabels();
            RepaintCurPosStatus();

            if (closePositionType == ClosePositionType.Limit)
            {
                TabControlTypePosition.SelectedIndex = 0;
            }
            else if (closePositionType == ClosePositionType.Market)
            {
                TabControlTypePosition.SelectedIndex = 1;
            }
            else if (closePositionType == ClosePositionType.Stop)
            {
                TabControlTypePosition.SelectedIndex = 2;
            }
            else if (closePositionType == ClosePositionType.Profit)
            {
                TabControlTypePosition.SelectedIndex = 3;
            }
            else if (closePositionType == ClosePositionType.Fake)
            {
                TabControlTypePosition.SelectedIndex = 4;
            }

            LabelOpenVolumeValue.Content = Position.OpenVolume.ToStringWithNoEndZero();
            TextBoxLimitVolumeToClose.Text = Position.OpenVolume.ToStringWithNoEndZero();
            TextBoxMarketVolumeToClose.Text = Position.OpenVolume.ToStringWithNoEndZero();
            TextBoxFakeVolume.Text = Position.OpenVolume.ToStringWithNoEndZero();

            LabelLimitAllOpenVolumeSend.Content = OsLocalization.Trader.Label387 + Position.OpenVolume.ToStringWithNoEndZero();
            LabelLimitAllOpenVolumeSend.MouseLeftButtonDown += LabelLimitAllOpenVolumeSend_MouseLeftButtonDown;
            LabelLimitAllOpenVolumeSend.MouseEnter += LabelLimitAllOpenVolumeSend_MouseEnter;
            LabelLimitAllOpenVolumeSend.MouseLeave += LabelLimitAllOpenVolumeSend_MouseLeave;

            LabelMarketAllOpenVolumeSend.Content = OsLocalization.Trader.Label387 + Position.OpenVolume.ToStringWithNoEndZero();
            LabelMarketAllOpenVolumeSend.MouseLeftButtonDown += LabelMarketAllOpenVolumeSend_MouseLeftButtonDown;
            LabelMarketAllOpenVolumeSend.MouseEnter += LabelMarketAllOpenVolumeSend_MouseEnter;
            LabelMarketAllOpenVolumeSend.MouseLeave += LabelMarketAllOpenVolumeSend_MouseLeave;

            LabelFakeAllOpenVolume.Content = OsLocalization.Trader.Label387 + Position.OpenVolume.ToStringWithNoEndZero();
            LabelFakeAllOpenVolume.MouseLeftButtonDown += LabelFakeAllOpenVolume_MouseLeftButtonDown;
            LabelFakeAllOpenVolume.MouseEnter += LabelFakeAllOpenVolume_MouseEnter;
            LabelFakeAllOpenVolume.MouseLeave += LabelFakeAllOpenVolume_MouseLeave;

            LabelPosStateValue.Content = Position.State.ToString();
            LabelPosNumberValue.Content = Position.Number.ToString();
            LabelOpenSideValue.Content = Position.Direction.ToString();

            TextBoxProfitActivationPrice.Text = Position.ProfitOrderRedLine.ToStringWithNoEndZero();
            TextBoxProfitPrice.Text = Position.ProfitOrderPrice.ToStringWithNoEndZero();

            TextBoxStopActivationPrice.Text = Position.StopOrderRedLine.ToStringWithNoEndZero();
            TextBoxStopPrice.Text = Position.StopOrderPrice.ToStringWithNoEndZero();

            Task.Run(WatcherThreadPlace);

        }

        public void SelectTabIndx(ClosePositionType closePositionType)
        {
            if (LabelOpenVolumeValue.Dispatcher.CheckAccess() == false)
            {
                LabelOpenVolumeValue.Dispatcher.Invoke(new Action<ClosePositionType>(SelectTabIndx), closePositionType);
                return;
            }

            if (closePositionType == ClosePositionType.Limit)
            {
                TabControlTypePosition.SelectedIndex = 0;
            }
            else if (closePositionType == ClosePositionType.Market)
            {
                TabControlTypePosition.SelectedIndex = 1;
            }
            else if (closePositionType == ClosePositionType.Stop)
            {
                TabControlTypePosition.SelectedIndex = 2;
            }
            else if (closePositionType == ClosePositionType.Profit)
            {
                TabControlTypePosition.SelectedIndex = 3;
            }
            else if (closePositionType == ClosePositionType.Fake)
            {
                TabControlTypePosition.SelectedIndex = 4;
            }
        }

        private void PositionOpenUi2_Closed(object sender, EventArgs e)
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

                Closed -= PositionOpenUi2_Closed;

                if (_marketDepthPainter != null)
                {
                    _marketDepthPainter.UserClickOnMDAndSelectPriceEvent -= _marketDepthPainter_UserClickOnMDAndSelectPriceEvent;
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

        private bool _isDeleted;

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

        private void Connector_ConnectorStartedReconnectEvent(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, ServerType arg5)
        {
            RepaintMainLabels();
        }

        private void RepaintMainLabels()
        {
            if (TextBoxLimitPrice.Dispatcher.CheckAccess() == false)
            {
                TextBoxLimitPrice.Dispatcher.Invoke(new Action(RepaintMainLabels));
                return;
            }

            LabelServerTypeValue.Content = Tab.Connector.ServerType;
            LabelSecurityValue.Content = Tab.Connector.SecurityName;
            LabelTabNameValue.Content = Tab.TabName;
        }

        MarketDepthPainter _marketDepthPainter;

        public BotTabSimple Tab;

        public Position Position;

        private void ActivateMarketDepth()
        {
            _marketDepthPainter = new MarketDepthPainter(Tab.TabName + "OpenPosGui");
            _marketDepthPainter.ProcessMarketDepth(Tab.MarketDepth);
            _marketDepthPainter.StartPaint(WinFormsHostMarketDepth, null,null);
            _marketDepthPainter.UserClickOnMDAndSelectPriceEvent += _marketDepthPainter_UserClickOnMDAndSelectPriceEvent;
        }

        private void _marketDepthPainter_UserClickOnMDAndSelectPriceEvent(decimal priceSelectedUser)
        {
            if (TextBoxLimitPrice.Dispatcher.CheckAccess() == false)
            {
                TextBoxLimitPrice.Dispatcher.Invoke(new Action<decimal>(_marketDepthPainter_UserClickOnMDAndSelectPriceEvent), priceSelectedUser);
                return;
            }

            TextBoxLimitPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxStopActivationPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxStopPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxProfitActivationPrice.Text = priceSelectedUser.ToStringWithNoEndZero();
            TextBoxProfitPrice.Text = priceSelectedUser.ToStringWithNoEndZero();

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

        private void ButtonCloseAtLimit_Click(object sender, RoutedEventArgs e)
        {
            decimal price = 0;

            try
            {
                price = TextBoxLimitPrice.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                    OsLocalization.Trader.Label390 + "\n" + ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (price <= 0)
            {
                Tab.SetNewLogMessage(
                 OsLocalization.Trader.Label390, Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 0;

            try
            {
                volume = TextBoxLimitVolumeToClose.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                    OsLocalization.Trader.Label389 + "\n" + ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (volume <= 0)
            {
                Tab.SetNewLogMessage(
                 OsLocalization.Trader.Label389, Logging.LogMessageType.Error);
                return;
            }

            if (Position.CloseActiv == true)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label227);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }
            }

            Tab.CloseAtLimit(Position, price, volume);
        }

        private void ButtonCloseAtMarket_Click(object sender, RoutedEventArgs e)
        {
            if (Position.CloseActiv == true)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label227);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }
            }

            if(string.IsNullOrEmpty(TextBoxMarketVolumeToClose.Text))
            {
                Tab.SetNewLogMessage(OsLocalization.Trader.Label389, Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 0;
            
            try
            {
                volume = TextBoxMarketVolumeToClose.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                    OsLocalization.Trader.Label389 + "\n" + ex.ToString(), 
                    Logging.LogMessageType.Error);
                return;
            }

            if (volume <= 0)
            {
                Tab.SetNewLogMessage(OsLocalization.Trader.Label389, Logging.LogMessageType.Error);
                return;
            }

            Tab.CloseAtMarket(Position, volume);
        }

        private void ButtonCloseAtStop_Click(object sender, RoutedEventArgs e)
        {
            decimal priceOrder = 0;
            decimal priceActivation = 0;

            try
            {
                priceActivation = TextBoxStopActivationPrice.Text.ToDecimal();
                priceOrder = TextBoxStopPrice.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (priceActivation <= 0
                || priceOrder <= 0)
            {
                return;
            }

            Tab.CloseAtStop(Position, priceActivation, priceOrder);
        }

        private void ButtonCloseAtProfit_Click(object sender, RoutedEventArgs e)
        {
            decimal priceOrder = 0;
            decimal priceActivation = 0;

            try
            {
                priceActivation = TextBoxProfitActivationPrice.Text.ToDecimal();
                priceOrder = TextBoxProfitPrice.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (priceActivation <= 0
                || priceOrder <= 0)
            {
                return;
            }

            Tab.CloseAtProfit(Position, priceActivation, priceOrder);
        }

        private void ButtonCloseAtFake_Click(object sender, RoutedEventArgs e)
        {
            decimal price = 0;

            try
            {
                price = TextBoxFakePrice.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                    OsLocalization.Trader.Label390 + "\n" + ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (price <= 0)
            {
                Tab.SetNewLogMessage(
                 OsLocalization.Trader.Label390, Logging.LogMessageType.Error);
                return;
            }

            DateTime timeOpen = DateTime.MinValue;

            try
            {
                timeOpen = DatePickerFakeOpenDate.SelectedDate.Value;
                string[] openTimeStr = TextBoxFakeOpenTime.Text.ToString().Split(':');

                timeOpen = timeOpen.AddHours(Convert.ToInt32(openTimeStr[0]));
                timeOpen = timeOpen.AddMinutes(Convert.ToInt32(openTimeStr[1]));

            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                OsLocalization.Trader.Label388, Logging.LogMessageType.Error);

                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (timeOpen == DateTime.MinValue)
            {
                Tab.SetNewLogMessage(
                 OsLocalization.Trader.Label388, Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 0;

            try
            {
                volume = TextBoxFakeVolume.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(
                    OsLocalization.Trader.Label389 + "\n" + ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (volume <= 0)
            {
                Tab.SetNewLogMessage(
                 OsLocalization.Trader.Label389, Logging.LogMessageType.Error);
                return;
            }

            Tab.CloseAtFake(Position, volume, price, timeOpen);

        }

        // работа с кнопками актуального объёма

        private void LabelLimitAllOpenVolumeSend_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextBoxLimitVolumeToClose.Text = Position.OpenVolume.ToStringWithNoEndZero();
        }

        private void LabelLimitAllOpenVolumeSend_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelLimitAllOpenVolumeSend.Foreground = Brushes.YellowGreen;
        }

        private void LabelLimitAllOpenVolumeSend_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelLimitAllOpenVolumeSend.Foreground = LabelOpenVolumeValue.Foreground;
        }

        private void LabelMarketAllOpenVolumeSend_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextBoxMarketVolumeToClose.Text = Position.OpenVolume.ToStringWithNoEndZero();
        }

        private void LabelMarketAllOpenVolumeSend_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelMarketAllOpenVolumeSend.Foreground = Brushes.YellowGreen;
        }

        private void LabelMarketAllOpenVolumeSend_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelMarketAllOpenVolumeSend.Foreground = LabelOpenVolumeValue.Foreground;
        }

        private void LabelFakeAllOpenVolume_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextBoxFakeVolume.Text = Position.OpenVolume.ToStringWithNoEndZero();
        }

        private void LabelFakeAllOpenVolume_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelFakeAllOpenVolume.Foreground = Brushes.YellowGreen;
        }

        private void LabelFakeAllOpenVolume_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LabelFakeAllOpenVolume.Foreground = LabelOpenVolumeValue.Foreground;
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

                string curPosAll = OsLocalization.Trader.Label387 + Position.OpenVolume.ToStringWithNoEndZero();

                if (curPosAll != LabelLimitAllOpenVolumeSend.Content.ToString())
                {
                    LabelLimitAllOpenVolumeSend.Content = curPosAll;
                    LabelMarketAllOpenVolumeSend.Content = curPosAll;
                    LabelFakeAllOpenVolume.Content = curPosAll;
                }
            }
            catch
            {
                // ignore
            }

        }

        // отзыв ордеров по позиции

        private void ButtonRevokeProfit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Position.ProfitOrderIsActiv = false;
                Position.ProfitOrderPrice = 0;
                Position.ProfitOrderRedLine = 0;
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRevokeStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Position.StopOrderIsActiv = false;
                Position.StopOrderPrice = 0;
                Position.StopOrderRedLine = 0;
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRevokeLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for(int i = 0; Position.CloseOrders != null && i < Position.CloseOrders.Count;i++)
                {
                    Order order = Position.CloseOrders[i];

                    if(order.State == OrderStateType.Activ)
                    {
                        Tab.CloseOrder(order);
                    }
                }
            }
            catch (Exception ex)
            {
                Tab.SetNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }
    }

    public enum ClosePositionType
    {
        Limit,
        Market,
        Stop,
        Profit,
        Fake
    }
}