/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot example from the lecture course "C# for algotreader".
Stores examples of different methods for entering in position.
When you click on the button in robot parameters, an order of the selected type is created.
You can close the positions with button in the settings of the robot.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson9Bot1")]
    public class Lesson9Bot1 : BotPanel
    {
        // Reference to the main trading tab
        BotTabSimple _tabToTrade;

        public Lesson9Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Close positions
            _closeAllMarketButton = CreateParameterButton("Close all positions", "Close");
            _closeAllMarketButton.UserClickOnButtonEvent += _closeAllMarketButton_UserClickOnButtonEvent;

            // BuyAtMarket / SellAtMarket
            _buyMarketButton = CreateParameterButton("Market Buy", "Market");
            _buyMarketButton.UserClickOnButtonEvent += MarketBuy_UserClickOnButtonEvent;

            _sellMarketButton = CreateParameterButton("Market Sell", "Market");
            _sellMarketButton.UserClickOnButtonEvent += MarketSell_UserClickOnButtonEvent;

            _marketSignal = CreateParameter("Market with signal type", false, "Market");

            // BuyAtLimit / SellAtLimit
            _buyLimitButton = CreateParameterButton("Limit Buy", "Limit");
            _buyLimitButton.UserClickOnButtonEvent += _buyLimitButton_UserClickOnButtonEvent;

            _sellLimitButton = CreateParameterButton("Limit Sell", "Limit");
            _sellLimitButton.UserClickOnButtonEvent += _sellLimitButton_UserClickOnButtonEvent;

            _limitSignal = CreateParameter("Limit with signal type", false, "Limit");

            // BuyAtIceberg / SellAtIceberg
            _buyIcebergButton = CreateParameterButton("Iceberg Buy", "Iceberg");
            _buyIcebergButton.UserClickOnButtonEvent += _buyIcebergButton_UserClickOnButtonEvent;

            _sellIcebergButton = CreateParameterButton("Iceberg Sell", "Iceberg");
            _sellIcebergButton.UserClickOnButtonEvent += _sellIcebergButton_UserClickOnButtonEvent;

            _icebergCount = CreateParameter("Iceberg orders count", 2, 1, 10, 1, "Iceberg");
            _icebergSignal = CreateParameter("Iceberg with signal type", false, "Iceberg");
            _icebergVolume = CreateParameter("Iceberg volume", 10m, 1, 10, 1, "Iceberg");
            _icebergMarket = CreateParameter("Iceberg market", false, "Iceberg");
            _icebergMarketMinMillisecondsDistance = CreateParameter("Iceberg market min milliseconds distance", 500, 1, 10, 1, "Iceberg");

            // BuyAtFake / SellAtFake
            _buyFakeButton = CreateParameterButton("Fake Buy", "Fake");
            _buyFakeButton.UserClickOnButtonEvent += _buyFakeButton_UserClickOnButtonEvent;

            _sellFakeButton = CreateParameterButton("Fake Sell", "Fake");
            _sellFakeButton.UserClickOnButtonEvent += _sellFakeButton_UserClickOnButtonEvent;

            _fakeSignal = CreateParameter("Fake with signal type", false, "Fake");

            // BuyAtStop / SellAtStop
            _buyAtStopButton = CreateParameterButton("Buy at Stop", "Entry at Stop");
            _buyAtStopButton.UserClickOnButtonEvent += _buyAtStopButton_UserClickOnButtonEvent;

            _sellAtStopButton = CreateParameterButton("Sell at Stop", "Entry at Stop");
            _sellAtStopButton.UserClickOnButtonEvent += _sellAtStopButton_UserClickOnButtonEvent;

            _openAtStopExpiresBars = CreateParameter("Open at stop expires bars", 0, 0, 10, 1, "Entry at Stop");
            _openAtStopIsNoLifeTimeOrder = CreateParameter("Use no lifetime order", false, "Entry at Stop");
            _openAtStopIsMarketOrder = CreateParameter("Is market order", false, "Entry at Stop");

            Description = OsLocalization.Description.DescriptionLabel18;
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

            for (int i = 0;i <  openPositions.Count;i++)
            {
                Position position = openPositions[i];

                _tabToTrade.CloseAllOrderToPosition(position);

                if(position.OpenVolume > 0)
                {
                    _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                }
            }
        }

        #endregion

        #region BuyAtMarket / SellAtMarket

        private StrategyParameterButton _buyMarketButton;

        private StrategyParameterButton _sellMarketButton;

        private StrategyParameterBool _marketSignal;

        private void MarketBuy_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            if (_marketSignal.ValueBool == false)
            {
                _tabToTrade.BuyAtMarket(volume);
            }
            else if (_marketSignal.ValueBool == true)
            {
                _tabToTrade.BuyAtMarket(volume, "User click button buy market");
            }
        }

        private void MarketSell_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            if (_marketSignal.ValueBool == false)
            {
                _tabToTrade.SellAtMarket(volume);
            }
            else if (_marketSignal.ValueBool == true)
            {
                _tabToTrade.SellAtMarket(volume, "User click button sell market");
            }
        }

        #endregion

        #region BuyAtLimit / SellAtLimit

        private StrategyParameterButton _buyLimitButton;

        private StrategyParameterButton _sellLimitButton;

        private StrategyParameterBool _limitSignal;

        private void _buyLimitButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestAsk;

            if(price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            if (_limitSignal.ValueBool == false)
            {
                _tabToTrade.BuyAtLimit(volume, price);
            }
            else if (_limitSignal.ValueBool == true)
            {
                _tabToTrade.BuyAtLimit(volume, price, "User click button buy limit");
            }
        }

        private void _sellLimitButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestBid;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            if (_limitSignal.ValueBool == false)
            {
                _tabToTrade.SellAtLimit(volume, price);
            }
            else if (_limitSignal.ValueBool == true)
            {
                _tabToTrade.SellAtLimit(volume, price, "User click button sell limit");
            }
        }

        #endregion

        #region BuyAtIceberg / SellAtIceberg

        private StrategyParameterButton _buyIcebergButton;

        private StrategyParameterButton _sellIcebergButton;

        private StrategyParameterBool _icebergSignal;

        private StrategyParameterBool _icebergMarket;

        private StrategyParameterInt _icebergMarketMinMillisecondsDistance;

        private StrategyParameterInt _icebergCount;

        private StrategyParameterDecimal _icebergVolume;

        private void _buyIcebergButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = _icebergVolume.ValueDecimal;

            decimal price = _tabToTrade.PriceBestAsk;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            int ordersCount = _icebergCount.ValueInt;

            if (_icebergMarket.ValueBool == true)
            { // Market iceberg

                if (_icebergSignal.ValueBool == false)
                {
                    _tabToTrade.BuyAtIcebergMarket(volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt);
                }
                else if (_icebergSignal.ValueBool == true)
                {
                    _tabToTrade.BuyAtIcebergMarket(volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt, "User click button buy iceberg Market");
                }
            }

            else if (_icebergMarket.ValueBool == false)
            { // Limit iceberg

                if (_icebergSignal.ValueBool == false)
                {
                    _tabToTrade.BuyAtIceberg(volume, price, ordersCount);
                }
                else if (_icebergSignal.ValueBool == true)
                {
                    _tabToTrade.BuyAtIceberg(volume, price, ordersCount, "User click button buy iceberg Limit");
                }
            }
        }

        private void _sellIcebergButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = _icebergVolume.ValueDecimal;

            decimal price = _tabToTrade.PriceBestBid;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            int ordersCount = _icebergCount.ValueInt;

            if (_icebergMarket.ValueBool == true)
            { // Market iceberg

                if (_icebergSignal.ValueBool == false)
                {
                    _tabToTrade.SellAtIcebergMarket(volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt);
                }
                else if (_icebergSignal.ValueBool == true)
                {
                    _tabToTrade.SellAtIcebergMarket(volume, ordersCount, _icebergMarketMinMillisecondsDistance.ValueInt, "User click button sell iceberg Market");
                }
            }

            else if (_icebergMarket.ValueBool == false)
            { // Limit iceberg

                if (_icebergSignal.ValueBool == false)
                {
                    _tabToTrade.SellAtIceberg(volume, price, ordersCount);
                }
                else if (_icebergSignal.ValueBool == true)
                {
                    _tabToTrade.SellAtIceberg(volume, price, ordersCount, "User click button sell iceberg Limit");
                }
            }
        }

        #endregion

        #region BuyAtFake / SellAtFake

        private StrategyParameterButton _buyFakeButton;

        private StrategyParameterButton _sellFakeButton;

        private StrategyParameterBool _fakeSignal;

        private void _buyFakeButton_UserClickOnButtonEvent()
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

            DateTime time = _tabToTrade.TimeServerCurrent;

            if (_fakeSignal.ValueBool == false)
            {
                _tabToTrade.BuyAtFake(volume, price, time);
            }
            else if (_fakeSignal.ValueBool == true)
            {
                _tabToTrade.BuyAtFake(volume, price, time, "User click button buy fake");
            }
        }

        private void _sellFakeButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            decimal price = _tabToTrade.PriceBestBid;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            DateTime time = _tabToTrade.TimeServerCurrent;

            if (_fakeSignal.ValueBool == false)
            {
                _tabToTrade.SellAtFake(volume, price, time);
            }
            else if (_fakeSignal.ValueBool == true)
            {
                _tabToTrade.SellAtFake(volume, price, time, "User click button sell fake");
            }
        }

        #endregion

        #region BuyAtStop / SellAtStop

        private StrategyParameterButton _buyAtStopButton;

        private StrategyParameterButton _sellAtStopButton;

        private StrategyParameterInt _openAtStopExpiresBars;

        private StrategyParameterBool _openAtStopIsNoLifeTimeOrder;

        private StrategyParameterBool _openAtStopIsMarketOrder;

        private void _buyAtStopButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal price = candles[candles.Count - 1].High;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.BuyAtStopCancel();

            if (_openAtStopIsMarketOrder.ValueBool == true)
            {
                _tabToTrade.BuyAtStopMarket(volume, price, price,
                    StopActivateType.HigherOrEqual, 1, "Buy at stop market", PositionOpenerToStopLifeTimeType.NoLifeTime);

                return;
            }

            if (_openAtStopIsNoLifeTimeOrder.ValueBool == true)
            {
                _tabToTrade.BuyAtStop(volume, price, price,
                    StopActivateType.HigherOrEqual, 1, "No life time buy at stop", PositionOpenerToStopLifeTimeType.NoLifeTime);

                return;
            }

            if (_openAtStopExpiresBars.ValueInt == 0)
            {// lifetime of the order - one candle
                if (_limitSignal.ValueBool == false)
                {
                    _tabToTrade.BuyAtStop(volume, price, price, StopActivateType.HigherOrEqual);
                }
                else if (_limitSignal.ValueBool == true)
                {
                    _tabToTrade.BuyAtStop(volume, price, price, StopActivateType.HigherOrEqual, "User click button buy at stop");
                }
            }
            else
            {// lifetime order is clearly indicated in the number of candles. Can be more than 1
                int expireBars = _openAtStopExpiresBars.ValueInt;

                if (_limitSignal.ValueBool == false)
                {
                    _tabToTrade.BuyAtStop(volume, price, price, StopActivateType.HigherOrEqual, expireBars);
                }
                else if (_limitSignal.ValueBool == true)
                {
                    _tabToTrade.BuyAtStop(volume, price, price, StopActivateType.HigherOrEqual, expireBars, "User click button buy at stop");
                }
            }
        }

        private void _sellAtStopButton_UserClickOnButtonEvent()
        {
            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal volume = 1;

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            decimal price = candles[candles.Count - 1].Low;

            if (price == 0)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade. No price", Logging.LogMessageType.Error);
                return;
            }

            _tabToTrade.SellAtStopCancel();

            if (_openAtStopIsMarketOrder.ValueBool == true)
            {
                _tabToTrade.SellAtStopMarket(volume, price, price,
                    StopActivateType.HigherOrEqual, 1, "Sell at stop market", PositionOpenerToStopLifeTimeType.NoLifeTime);

                return;
            }

            if (_openAtStopIsNoLifeTimeOrder.ValueBool == true)
            {
                _tabToTrade.SellAtStop(volume, price, price,
                    StopActivateType.LowerOrEqual, 1, "No life time sell at stop", PositionOpenerToStopLifeTimeType.NoLifeTime);

                return;
            }

            if (_openAtStopExpiresBars.ValueInt == 0)
            {// lifetime of the order - one candle
                if (_limitSignal.ValueBool == false)
                {
                    _tabToTrade.SellAtStop(volume, price, price, StopActivateType.LowerOrEqual);
                }
                else if (_limitSignal.ValueBool == true)
                {
                    _tabToTrade.SellAtStop(volume, price, price, StopActivateType.LowerOrEqual, "User click button sell at stop");
                }
            }
            else
            {// lifetime order is clearly indicated in the number of candles. Can be more than 1
                int expireBars = _openAtStopExpiresBars.ValueInt;

                if (_limitSignal.ValueBool == false)
                {
                    _tabToTrade.SellAtStop(volume, price, price, StopActivateType.LowerOrEqual, expireBars);
                }
                else if (_limitSignal.ValueBool == true)
                {
                    _tabToTrade.SellAtStop(volume, price, price, StopActivateType.LowerOrEqual, expireBars, "User click button sell at stop");
                }
            }
        }

        #endregion

        public override string GetNameStrategyType()
        {
            return "Lesson9Bot1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}