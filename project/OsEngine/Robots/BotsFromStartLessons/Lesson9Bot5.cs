/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot example from the lecture course "C# for algotreader".
Stores examples of different methods for manage position.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson9Bot5")]
    public class Lesson9Bot5 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        BotTabSimple _tabToTrade;

        public Lesson9Bot5(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // CloseAllAtMarket
            _closeAllOrderToPositionButton = CreateParameterButton("Close orders to position", "Close to position");
            _closeAllOrderToPositionButton.UserClickOnButtonEvent += _closeAllOrderToPositionButton_UserClickOnButtonEvent;

            _closeAllOrderToPositionSignal = CreateParameter("Close orders to position have signal", false, "Close to position");

            // CloseAllOrderInSystem
            _closeAllOrderInSystemButton = CreateParameterButton("Close orders in system", "Close all orders");
            _closeAllOrderInSystemButton.UserClickOnButtonEvent += _closeAllOrderInSystemButton_UserClickOnButtonEvent;

            _closeAllOrderInSystemSignal = CreateParameter("Close orders to in system", false, "Close all orders");

            // CloseOrder
            _closeCloseOrderButton = CreateParameterButton("Close order", "Close order");
            _closeCloseOrderButton.UserClickOnButtonEvent += _closeCloseOrderButton_UserClickOnButtonEvent;

            // ChangeOrderPrice
            _changeOrderPriceButton = CreateParameterButton("Change order price", "ChangeOrderPrice");
            _changeOrderPriceButton.UserClickOnButtonEvent += _changeOrderPriceButton_UserClickOnButtonEvent;

            Description = OsLocalization.Description.DescriptionLabel22;
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
            return "Lesson9Bot5";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}