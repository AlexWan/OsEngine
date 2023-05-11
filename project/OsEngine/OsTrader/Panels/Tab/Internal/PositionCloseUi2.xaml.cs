using OkonkwoOandaV20.TradeLibrary.DataTypes.Pricing;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

            LabelLimitPrice.Content = OsLocalization.Trader.Label205;
            LabelStopPrice.Content = OsLocalization.Trader.Label205;
            LabelFakePrice.Content = OsLocalization.Trader.Label205;
            LabelProfitPrice.Content = OsLocalization.Trader.Label205;

            LabelProfitActivationPrice.Content = OsLocalization.Trader.Label206;
            LabelStopActivationPrice.Content = OsLocalization.Trader.Label206;

            TabItemLimit.Header = OsLocalization.Trader.Label200;
            TabItemMarket.Header = OsLocalization.Trader.Label201;
            TabItemStop.Header = OsLocalization.Trader.Label202;
            TabItemFake.Header = OsLocalization.Trader.Label203;
            TabItemProfit.Header = OsLocalization.Trader.Label222;

            LabelFakeOpenDate.Content = OsLocalization.Trader.Label209;
            LabelFakeOpenTime.Content = OsLocalization.Trader.Label210;
            ButtonFakeTimeOpenNow.Content = OsLocalization.Trader.Label211;

            ButtonCloseAtLimit.Content = OsLocalization.Trader.Label217;
            ButtonCloseAtMarket.Content = OsLocalization.Trader.Label218;
            ButtonCloseAtStop.Content = OsLocalization.Trader.Label219;
            ButtonCloseAtProfit.Content = OsLocalization.Trader.Label220;
            ButtonCloseAtFake.Content = OsLocalization.Trader.Label221;

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

        private void PositionOpenUi2_Closed(object sender, EventArgs e)
        {
            Tab.MarketDepthUpdateEvent -= Tab_MarketDepthUpdateEvent;
            Tab.BestBidAskChangeEvent -= Tab_BestBidAskChangeEvent;

            _marketDepthPainter.UserClickOnMDAndSelectPriceEvent -= _marketDepthPainter_UserClickOnMDAndSelectPriceEvent;
            _marketDepthPainter.StopPaint();
            _marketDepthPainter.Delete();
            _marketDepthPainter = null;

            Tab = null;
        }

        private void ActivateMarketDepth()
        {
            _marketDepthPainter = new MarketDepthPainter(Tab.TabName + "OpenPosGui");
            _marketDepthPainter.ProcessMarketDepth(Tab.MarketDepth);
            _marketDepthPainter.StartPaint(WinFormsHostMarketDepth, null);
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
                Tab.SetNewLogMessage(ex.Message.ToString(), Logging.LogMessageType.Error);
                return;
            }

            if (price <= 0)
            {
                return;
            }

            if(Position.CloseActiv == true)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label227);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }
            }

            Tab.CloseAtLimit(Position, price, Position.OpenVolume);
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

            Tab.CloseAtMarket(Position, Position.OpenVolume);
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

            Tab.CloseAtStop(Position,priceActivation, priceOrder);
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
            DateTime timeOpen = DateTime.MinValue;

            try
            {
                price = TextBoxFakePrice.Text.ToDecimal();
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
                timeOpen == DateTime.MinValue)
            {
                return;
            }

            Tab.CloseAtFake(Position,Position.OpenVolume, price, timeOpen);

        }

        // поток отвечающий за просмотром статуса позиции

        private void RepaintCurPosStatus()
        {
            LabelOpenVolumeValue.Content = Position.OpenVolume.ToStringWithNoEndZero();
            LabelPosStateValue.Content = Position.State.ToString();
            LabelPosNumberValue.Content = Position.Number.ToString();

            TextBoxProfitActivationPrice.Text = Position.ProfitOrderRedLine.ToStringWithNoEndZero();
            TextBoxProfitPrice.Text = Position.ProfitOrderPrice.ToStringWithNoEndZero();

            TextBoxStopActivationPrice.Text = Position.StopOrderRedLine.ToStringWithNoEndZero();
            TextBoxStopPrice.Text = Position.StopOrderPrice.ToStringWithNoEndZero();
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