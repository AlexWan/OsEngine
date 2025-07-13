/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot example from the lecture course "C# for algotreader".
Stores examples of different methods for modification a position.
The buttons are used to create more orders for the position.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson9Bot2")]
    public class Lesson9Bot2 : BotPanel
    {
        // Reference to the main trading tab
        BotTabSimple _tabToTrade;

        public Lesson9Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Reference to the main trading tab
            // Ссылка на главную вкладку
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Close positions
            _closeAllMarketButton = CreateParameterButton("Close all positions", "Close");
            _closeAllMarketButton.UserClickOnButtonEvent += _closeAllMarketButton_UserClickOnButtonEvent;

            // BuyAtMarket / SellAtMarket
            _buyMarketButton = CreateParameterButton("Market Buy", "Open");
            _buyMarketButton.UserClickOnButtonEvent += MarketBuy_UserClickOnButtonEvent;

            _sellMarketButton = CreateParameterButton("Market Sell", "Open");
            _sellMarketButton.UserClickOnButtonEvent += MarketSell_UserClickOnButtonEvent;

            // BuyAtLimitToPosition / SellAtLimitToPosition
            _buyLimitToPositionButton = CreateParameterButton("Buy at limit to position", "Limit");
            _buyLimitToPositionButton.UserClickOnButtonEvent += _buyLimitToPositionButton_UserClickOnButtonEvent;

            _sellLimitToPositionButton = CreateParameterButton("Sell at limit to position", "Limit");
            _sellLimitToPositionButton.UserClickOnButtonEvent += _sellLimitToPositionButton_UserClickOnButtonEvent;

            // BuyAtLimitToPositionUnsafe / SellAtLimitToPositionUnsafe
            _buyLimitToPositionUnsafeButton = CreateParameterButton("Buy at limit to position Unsafe", "Unsafe");
            _buyLimitToPositionUnsafeButton.UserClickOnButtonEvent += _buyLimitToPositionUnsafeButton_UserClickOnButtonEvent;

            _sellLimitToPositionUnsafeButton = CreateParameterButton("Sell at limit to position Unsafe", "Unsafe");
            _sellLimitToPositionUnsafeButton.UserClickOnButtonEvent += _sellLimitToPositionUnsafeButton_UserClickOnButtonEvent;

            // BuyAtMarketToPosition / SellAtMarketToPosition
            _buyMarketToPositionButton = CreateParameterButton("Buy at market to position", "Market");
            _buyMarketToPositionButton.UserClickOnButtonEvent += _buyMarketToPositionButton_UserClickOnButtonEvent;

            _sellMarketToPositionButton = CreateParameterButton("Sell at market to position", "Market");
            _sellMarketToPositionButton.UserClickOnButtonEvent += _sellMarketToPositionButton_UserClickOnButtonEvent;

            _addOrderToPositionAtMarketSignal = CreateParameter("With signal type", false, "Market");

            // BuyAtIcebergToPosition / SellAtIcebergToPosition
            _buyIcebergToPositionButton = CreateParameterButton("Buy at Iceberg to position", "Iceberg");
            _buyIcebergToPositionButton.UserClickOnButtonEvent += _buyIcebergToPositionButton_UserClickOnButtonEvent;

            _sellIcebergToPositionButton = CreateParameterButton("Sell at Iceberg to position", "Iceberg");
            _sellIcebergToPositionButton.UserClickOnButtonEvent += _sellIcebergToPositionButton_UserClickOnButtonEvent;

            _icebergToPositionOrdersCount = CreateParameter("Iceberg orders count to position", 2, 1, 10, 1, "Iceberg");
            _icebergVolume = CreateParameter("Iceberg volume", 10m, 1, 10, 1, "Iceberg");
            _icebergMarket = CreateParameter("Iceberg market", false, "Iceberg");
            _icebergMarketMinMillisecondsDistance = CreateParameter("Iceberg market min milliseconds distance", 500, 1, 10, 1, "Iceberg");

            Description = OsLocalization.Description.DescriptionLabel19;
        }

        #region Close positions

        private StrategyParameterButton _closeAllMarketButton;

        private void _closeAllMarketButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.BuyAtStopCancel();
            _tabToTrade.SellAtStopCancel();

            for (int i = 0; i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                _tabToTrade.CloseAllOrderToPosition(position);

                if (position.OpenVolume > 0)
                {
                    _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                }
            }
        }

        #endregion

        #region BuyAtMarket / SellAtMarket

        private StrategyParameterButton _buyMarketButton;

        private StrategyParameterButton _sellMarketButton;

        private void MarketBuy_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            _tabToTrade.BuyAtMarket(volume);
        }

        private void MarketSell_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            _tabToTrade.SellAtMarket(volume);
        }

        #endregion

        #region BuyAtLimitToPosition / SellAtLimitToPosition

        private StrategyParameterButton _buyLimitToPositionButton;

        private StrategyParameterButton _sellLimitToPositionButton;

        private void _buyLimitToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.BuyAtLimitToPosition(pos, price, volume);
        }

        private void _sellLimitToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.SellAtLimitToPosition(pos, price, volume);
        }

        #endregion

        #region BuyAtLimitToPositionUnsafe / SellAtLimitToPositionUnsafe

        private StrategyParameterButton _buyLimitToPositionUnsafeButton;

        private StrategyParameterButton _sellLimitToPositionUnsafeButton;

        private void _buyLimitToPositionUnsafeButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.BuyAtLimitToPositionUnsafe(pos, price, volume);
        }

        private void _sellLimitToPositionUnsafeButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.SellAtLimitToPositionUnsafe(pos, price, volume);
        }

        #endregion

        #region BuyAtMarketToPosition / SellAtMarketToPosition

        private StrategyParameterButton _buyMarketToPositionButton;

        private StrategyParameterButton _sellMarketToPositionButton;

        private StrategyParameterBool _addOrderToPositionAtMarketSignal;

        private void _buyMarketToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            if (_addOrderToPositionAtMarketSignal.ValueBool == false)
            {
                _tabToTrade.BuyAtMarketToPosition(pos, volume);
            }
            else if (_addOrderToPositionAtMarketSignal.ValueBool == true)
            {
                _tabToTrade.BuyAtMarketToPosition(pos, volume, "User click button BuyAtMarketToPosition");
            }
        }

        private void _sellMarketToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = 1;

            if (_addOrderToPositionAtMarketSignal.ValueBool == false)
            {
                _tabToTrade.SellAtMarketToPosition(pos, volume);
            }
            else if (_addOrderToPositionAtMarketSignal.ValueBool == true)
            {
                _tabToTrade.SellAtMarketToPosition(pos, volume, "User click button SellAtMarketToPosition");
            }
        }

        #endregion

        #region BuyAtIcebergToPosition / SellAtIcebergToPosition

        private StrategyParameterButton _buyIcebergToPositionButton;

        private StrategyParameterButton _sellIcebergToPositionButton;

        private StrategyParameterInt _icebergToPositionOrdersCount;

        private StrategyParameterDecimal _icebergVolume;

        private StrategyParameterBool _icebergMarket;

        private StrategyParameterInt _icebergMarketMinMillisecondsDistance;

        private void _buyIcebergToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = _icebergVolume.ValueDecimal;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            int ordersCount = _icebergToPositionOrdersCount.ValueInt;

            if(_icebergMarket.ValueBool == true)
            { // Market
                _tabToTrade.BuyAtIcebergToPositionMarket(pos, volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt);
            }
            else if(_icebergMarket.ValueBool == false)
            { // Limit
                _tabToTrade.BuyAtIcebergToPosition(pos, price, volume, ordersCount);
            }
        }

        private void _sellIcebergToPositionButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            List<Position> posesAll = _tabToTrade.PositionsOpenAll;

            if (posesAll.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No position", Logging.LogMessageType.Error);
                return;
            }

            Position pos = posesAll[0];

            decimal volume = _icebergVolume.ValueDecimal;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            int ordersCount = _icebergToPositionOrdersCount.ValueInt;

            if (_icebergMarket.ValueBool == true)
            { // Market
                _tabToTrade.SellAtIcebergToPositionMarket(pos, volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt);
            }
            else if (_icebergMarket.ValueBool == false)
            { // Limit
                _tabToTrade.SellAtIcebergToPosition(pos, price, volume, ordersCount);
            }
        }

        #endregion

        public override string GetNameStrategyType()
        {
            return "Lesson9Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}