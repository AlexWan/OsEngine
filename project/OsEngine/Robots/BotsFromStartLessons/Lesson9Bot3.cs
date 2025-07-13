/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using OsEngine.Language;

/* Description
Robot example from the lecture course "C# for algotreader".
Stores examples of different methods for close position.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson9Bot3")]
    public class Lesson9Bot3 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        BotTabSimple _tabToTrade;

        public Lesson9Bot3(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // BuyAtMarket / SellAtMarket
            _buyMarketButton = CreateParameterButton("Market Buy", "Open");
            _buyMarketButton.UserClickOnButtonEvent += MarketBuy_UserClickOnButtonEvent;

            _sellMarketButton = CreateParameterButton("Market Sell", "Open");
            _sellMarketButton.UserClickOnButtonEvent += MarketSell_UserClickOnButtonEvent;

            _volumeOpenPosition = CreateParameter("Volume open position", 10m, 1, 20, 1, "Open");

            // CloseAllAtMarket
            _closeAllMarketButton = CreateParameterButton("Close all at market", "Market all");
            _closeAllMarketButton.UserClickOnButtonEvent += _closeAllMarketButton_UserClickOnButtonEvent;

            _closeAllAtMarketSignal = CreateParameter("Close all at market have signal", false, "Market all");

            // CloseAtFake
            _closeAtFakeButton = CreateParameterButton("Close at fake", "Fake");
            _closeAtFakeButton.UserClickOnButtonEvent += _closeAtFakeButton_UserClickOnButtonEvent;

            // CloseAtMarket
            _closeAtMarketButton = CreateParameterButton("Close at market", "Market one");
            _closeAtMarketButton.UserClickOnButtonEvent += _closeAtMarketButton_UserClickOnButtonEvent;

            _closeAtMarketSignal = CreateParameter("Close at market have signal", false, "Market one");

            // CloseAtLimit
            _closeAtLimitButton = CreateParameterButton("Close at Limit", "Limit");
            _closeAtLimitButton.UserClickOnButtonEvent += _closeAtLimitButton_UserClickOnButtonEvent;

            _closeAtLimitSignal = CreateParameter("Close at Limit have signal", false, "Limit");

            // CloseAtLimitUnsafe
            _closeAtLimitUnsafeButton = CreateParameterButton("Close at Limit Unsafe", "LimitUnsafe");
            _closeAtLimitUnsafeButton.UserClickOnButtonEvent += _closeAtLimitUnsafeButton_UserClickOnButtonEvent;

            // CloseAtIceberg
            _closeAtIcebergButton = CreateParameterButton("Close at Iceberg", "Iceberg");
            _closeAtIcebergButton.UserClickOnButtonEvent += _closeAtIcebergButton_UserClickOnButtonEvent;

            _closeAtIcebergSignal = CreateParameter("Close at Iceberg have signal", false, "Iceberg");
            _icebergCount = CreateParameter("Iceberg orders count", 2, 1, 10, 1, "Iceberg");
            _icebergMarket = CreateParameter("Iceberg market", false, "Iceberg");
            _icebergMarketMinMillisecondsDistance = CreateParameter("Iceberg market min milliseconds distance", 500, 1, 10, 1, "Iceberg");

            Description = OsLocalization.Description.DescriptionLabel20;
        }

        #region BuyAtMarket / SellAtMarket

        private StrategyParameterButton _buyMarketButton;

        private StrategyParameterButton _sellMarketButton;

        private StrategyParameterDecimal _volumeOpenPosition;

        private void MarketBuy_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = _volumeOpenPosition.ValueDecimal;

            _tabToTrade.BuyAtMarket(volume);
        }

        private void MarketSell_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = _volumeOpenPosition.ValueDecimal;

            _tabToTrade.SellAtMarket(volume);
        }

        #endregion

        #region CloseAllAtMarket

        private StrategyParameterButton _closeAllMarketButton;

        private StrategyParameterBool _closeAllAtMarketSignal;

        private void _closeAllMarketButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            if (_closeAllAtMarketSignal.ValueBool == false)
            {
                _tabToTrade.CloseAllAtMarket();
            }
            else if (_closeAllAtMarketSignal.ValueBool == false)
            {
                _tabToTrade.CloseAllAtMarket("User click close ALL at market button");
            }
        }

        #endregion

        #region CloseAtFake

        private StrategyParameterButton _closeAtFakeButton;

        private void _closeAtFakeButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if(openPositions.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No positions", Logging.LogMessageType.Error);
                return;
            }

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            Position position = openPositions[0];
            _tabToTrade.CloseAllOrderToPosition(position);

            DateTime time = _tabToTrade.TimeServerCurrent;

            if(position.Direction == Side.Buy)
            {
                decimal price = _tabToTrade.PriceBestAsk;

                _tabToTrade.CloseAtFake(position, position.OpenVolume, price, time);
            }
            else if(position.Direction == Side.Sell)
            {
                decimal price = _tabToTrade.PriceBestBid;

                _tabToTrade.CloseAtFake(position, position.OpenVolume, price, time);
            }
        }

        #endregion

        #region CloseAtMarket

        private StrategyParameterButton _closeAtMarketButton;

        private StrategyParameterBool _closeAtMarketSignal;

        private void _closeAtMarketButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No positions", Logging.LogMessageType.Error);
                return;
            }

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            Position position = openPositions[0];
            _tabToTrade.CloseAllOrderToPosition(position);

            if(_closeAtMarketSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtMarket(position, position.OpenVolume);
            }
            else if (_closeAtMarketSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtMarket(position, position.OpenVolume, "User click close at market button");
            }
        }

        #endregion

        #region CloseAtLimit

        private StrategyParameterButton _closeAtLimitButton;

        private StrategyParameterBool _closeAtLimitSignal;

        private void _closeAtLimitButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No positions", Logging.LogMessageType.Error);
                return;
            }

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            Position position = openPositions[0];
            _tabToTrade.CloseAllOrderToPosition(position);

            decimal price = 0;

            if(position.Direction == Side.Buy)
            {
                price = _tabToTrade.PriceBestAsk;
            }
            else if(position.Direction == Side.Sell)
            {
                price = _tabToTrade.PriceBestBid;
            }

            if (_closeAtLimitSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtLimit(position, price, position.OpenVolume);
            }
            else if (_closeAtLimitSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtLimit(position, price, position.OpenVolume, "User click close at Limit button");
            }
        }

        #endregion

        #region CloseAtLimitUnsafe

        private StrategyParameterButton _closeAtLimitUnsafeButton;

        private void _closeAtLimitUnsafeButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No positions", Logging.LogMessageType.Error);
                return;
            }

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            Position position = openPositions[0];

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = _tabToTrade.PriceBestAsk;
            }
            else if (position.Direction == Side.Sell)
            {
                price = _tabToTrade.PriceBestBid;
            }

            _tabToTrade.CloseAtLimitUnsafe(position, price, position.OpenVolume);
        }

        #endregion

        #region CloseAtIceberg

        private StrategyParameterButton _closeAtIcebergButton;

        private StrategyParameterInt _icebergCount;

        private StrategyParameterBool _closeAtIcebergSignal;

        private StrategyParameterBool _icebergMarket;

        private StrategyParameterInt _icebergMarketMinMillisecondsDistance;

        private void _closeAtIcebergButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No positions", Logging.LogMessageType.Error);
                return;
            }

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            Position position = openPositions[0];
            _tabToTrade.CloseAllOrderToPosition(position);

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = _tabToTrade.PriceBestAsk;
            }
            else if (position.Direction == Side.Sell)
            {
                price = _tabToTrade.PriceBestBid;
            }

            int ordersCount = _icebergCount.ValueInt;

            if(_icebergMarket.ValueBool == false)
            { // Limit

                if (_closeAtIcebergSignal.ValueBool == false)
                {
                    _tabToTrade.CloseAtIceberg(position, price, position.OpenVolume, ordersCount);
                }
                else if (_closeAtIcebergSignal.ValueBool == true)
                {
                    _tabToTrade.CloseAtIceberg(position, price, position.OpenVolume, ordersCount, "User click close at Iceberg Limit");
                }
            }
            else if(_icebergMarket.ValueBool == true)
            { // Market

                if (_closeAtIcebergSignal.ValueBool == false)
                {
                    _tabToTrade.CloseAtIcebergMarket(position, position.OpenVolume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt);
                }
                else if (_closeAtIcebergSignal.ValueBool == true)
                {
                    _tabToTrade.CloseAtIcebergMarket(position, position.OpenVolume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt, "User click close at Iceberg Market");
                }
            }
        }

        #endregion

        public override string GetNameStrategyType()
        {
            return "Lesson9Bot3";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}