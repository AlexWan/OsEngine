using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson10Bot2")]
    public class Lesson10Bot2 : BotPanel
    {
        BotTabSimple _tabToTrade;

        public Lesson10Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // CloseAllAtMarket

            _closeAllOrderToPositionButton = CreateParameterButton("Close orders to position", "Close to position");
            _closeAllOrderToPositionButton.UserClickOnButtonEvent += _closeAllOrderToPositionButton_UserClickOnButtonEvent;
            _closeAllOrderToPositionSignal = CreateParameter("Close orders to position have signal", false, "Close to position");

            // CloseAllInSystem

            _closeAllOrderInSystemButton = CreateParameterButton("Close orders in system", "Close all orders");
            _closeAllOrderInSystemButton.UserClickOnButtonEvent += _closeAllOrderInSystemButton_UserClickOnButtonEvent;
            _closeAllOrderInSystemSignal = CreateParameter("Close orders to in system", false, "Close all orders");

            // CloseOrder

            _closeCloseOrderButton = CreateParameterButton("Close order", "Close order");
            _closeCloseOrderButton.UserClickOnButtonEvent += _closeCloseOrderButton_UserClickOnButtonEvent;

            // ChangeOrderPrice

            _changeOrderPriceButton = CreateParameterButton("Change order price", "ChangeOrderPrice");
            _changeOrderPriceButton.UserClickOnButtonEvent += _changeOrderPriceButton_UserClickOnButtonEvent;
        }

        #region CloseAllOrderToPosition

        private StrategyParameterButton _closeAllOrderToPositionButton;

        private StrategyParameterBool _closeAllOrderToPositionSignal;

        private void _closeAllOrderToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            price = price - price * 0.01m;

            Position newPosition = _tabToTrade.BuyAtLimit(volume, price);

            Thread.Sleep(5000);

            if(_closeAllOrderToPositionSignal.ValueBool == false)
            {
                _tabToTrade.CloseAllOrderToPosition(newPosition);
            }
            else if (_closeAllOrderToPositionSignal.ValueBool == true)
            {
                _tabToTrade.CloseAllOrderToPosition(newPosition,"User click close orders to position");
            }
        }

        #endregion

        #region CloseAllOrderInSystem

        private StrategyParameterButton _closeAllOrderInSystemButton;

        private StrategyParameterBool _closeAllOrderInSystemSignal;

        private void _closeAllOrderInSystemButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            price = price - price * 0.01m;

            Position newPosition = _tabToTrade.BuyAtLimit(volume, price);
            Position newPosition2 = _tabToTrade.BuyAtLimit(volume, price);

            Thread.Sleep(5000);

            if (_closeAllOrderInSystemSignal.ValueBool == false)
            {
                _tabToTrade.CloseAllOrderInSystem();
            }
            else if (_closeAllOrderInSystemSignal.ValueBool == true)
            {
                _tabToTrade.CloseAllOrderInSystem("User click close all orders in system");
            }
        }

        #endregion

        #region CloseOrder

        private StrategyParameterButton _closeCloseOrderButton;

        private void _closeCloseOrderButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            price = price - price * 0.01m;

            Position newPosition = _tabToTrade.BuyAtLimit(volume, price);

            Thread.Sleep(5000);

            Order order = newPosition.OpenOrders[0];

            _tabToTrade.CloseOrder(order);
        }

        #endregion

        #region ChangeOrderPrice

        private StrategyParameterButton _changeOrderPriceButton;

        private void _changeOrderPriceButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            price = price - price * 0.01m;

            decimal newPrice = price - price * 0.02m;

            Position newPosition = _tabToTrade.BuyAtLimit(volume, price);

            Thread.Sleep(5000);

            Order order = newPosition.OpenOrders[0];

            _tabToTrade.ChangeOrderPrice(order, newPrice);
        }

        #endregion

        public override string GetNameStrategyType()
        {
            return "Lesson10Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}