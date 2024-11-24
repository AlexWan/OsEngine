using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson9Bot4")]
    public class Lesson9Bot4 : BotPanel
    {
        BotTabSimple _tabToTrade;

        public Lesson9Bot4(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // BuyAtMarket / SellAtMarket

            _buyMarketButton = CreateParameterButton("Market Buy", "Open");
            _buyMarketButton.UserClickOnButtonEvent += MarketBuy_UserClickOnButtonEvent;
            _sellMarketButton = CreateParameterButton("Market Sell", "Open");
            _sellMarketButton.UserClickOnButtonEvent += MarketSell_UserClickOnButtonEvent;

            // CloseAllAtMarket

            _closeAllMarketButton = CreateParameterButton("Close all at market", "Market all");
            _closeAllMarketButton.UserClickOnButtonEvent += _closeAllMarketButton_UserClickOnButtonEvent;
            _closeAllAtMarketSignal = CreateParameter("Close all at market have signal", false, "Market all");

            // CloseAtStop

            _closeAtStopButton = CreateParameterButton("Close at Stop", "Stop");
            _closeAtStopButton.UserClickOnButtonEvent += _closeAtStopButton_UserClickOnButtonEvent;
            _closeAtStopCancelButton = CreateParameterButton("Cancel Stop order", "Stop");
            _closeAtStopCancelButton.UserClickOnButtonEvent += _closeAtStopCancelButton_UserClickOnButtonEvent;
            _closeAtStopSignal = CreateParameter("Close at Stop have signal", false, "Stop");
            _closeAtStopIsMarket = CreateParameter("Close at Stop is market", false, "Stop");

            // CloseAtTrailingStop

            _closeAtTrailingStopButton = CreateParameterButton("Close at Trailing Stop", "Trailing Stop");
            _closeAtTrailingStopButton.UserClickOnButtonEvent += _closeAtTrailingStopButton_UserClickOnButtonEvent;
            _closeAtTrailingStopCancelButton.UserClickOnButtonEvent += _closeAtTrailingStopCancelButton_UserClickOnButtonEvent;
           _closeAtTrailingStopSignal = CreateParameter("Close at Trailing Stop have signal", false, "Trailing Stop");
            _closeAtTrailingStopIsMarket = CreateParameter("Close at Trailing Stop is market", false, "Trailing Stop");

            // CloseAtProfit

            _closeAtProfitButton = CreateParameterButton("Close at Profit", "Profit");
            _closeAtProfitButton.UserClickOnButtonEvent += _closeAtProfitButton_UserClickOnButtonEvent;
            _closeAtProfitCancelButton.UserClickOnButtonEvent += _closeAtProfitCancelButton_UserClickOnButtonEvent;
            _closeAtProfitSignal = CreateParameter("Close at Profit have signal", false, "Profit");
            _closeAtProfitIsMarket = CreateParameter("Close at Profit is market", false, "Profit");
        }

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

        #region CloseAllAtMarket

        private StrategyParameterButton _closeAllMarketButton;

        private StrategyParameterBool _closeAllAtMarketSignal;

        private void _closeAllMarketButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (_tabToTrade.IsReadyToTrade == false)
            {
                _tabToTrade.SetNewLogMessage("Connection not ready to trade", Logging.LogMessageType.Error);
                return;
            }

            for (int i = 0; i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                _tabToTrade.CloseAllOrderToPosition(position);

                if (position.OpenVolume == 0)
                {
                    continue;
                }

                if (_closeAllAtMarketSignal.ValueBool == false)
                {
                    _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                }
                else if (_closeAllAtMarketSignal.ValueBool == false)
                {
                    _tabToTrade.CloseAtMarket(position, position.OpenVolume, "User click close ALL at market button");
                }
            }
        }

        #endregion

        #region CloseAtStop

        private StrategyParameterButton _closeAtStopButton;

        private StrategyParameterButton _closeAtStopCancelButton;

        private StrategyParameterBool _closeAtStopSignal;

        private StrategyParameterBool _closeAtStopIsMarket;

        private void _closeAtStopButton_UserClickOnButtonEvent()
        {
            if(_closeAtStopIsMarket.ValueBool == false)
            {
                CloseAtStopLimitMethod();
            }
            else if(_closeAtStopIsMarket.ValueBool == true)
            {
                CloseAtStopMarketMethod();
            }
        }

        private void _closeAtStopCancelButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            for (int i = 0; i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                position.StopOrderIsActiv = false;
            }
        }

        private void CloseAtStopLimitMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].Low;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].High;
            }

            if (_closeAtStopSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtStop(position, price, price);
            }
            else if (_closeAtStopSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtStop(position, price, price, "User click close at Stop button");
            }
        }

        private void CloseAtStopMarketMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].Low;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].High;
            }

            if (_closeAtStopSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtStopMarket(position, price);
            }
            else if (_closeAtStopSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtStopMarket(position, price, "User click close at Stop button");
            }
        }

        #endregion

        #region CloseAtTrailingStop

        private StrategyParameterButton _closeAtTrailingStopButton;

        private StrategyParameterButton _closeAtTrailingStopCancelButton;

        private StrategyParameterBool _closeAtTrailingStopSignal;

        private StrategyParameterBool _closeAtTrailingStopIsMarket;

        private void _closeAtTrailingStopButton_UserClickOnButtonEvent()
        {
            if (_closeAtTrailingStopIsMarket.ValueBool == false)
            {
                CloseAtTrailingStopLimitMethod();
            }
            else if (_closeAtTrailingStopIsMarket.ValueBool == true)
            {
                CloseAtTrailingStopMarketMethod();
            }
        }

        private void _closeAtTrailingStopCancelButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            for (int i = 0; i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                position.StopOrderIsActiv = false;
            }
        }

        private void CloseAtTrailingStopLimitMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].Low;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].High;
            }

            if (_closeAtTrailingStopSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtTrailingStop(position, price, price);
            }
            else if (_closeAtTrailingStopSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtTrailingStop(position, price, price, "User click close at Trailing Stop button");
            }
        }

        private void CloseAtTrailingStopMarketMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].Low;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].High;
            }

            if (_closeAtTrailingStopSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtTrailingStopMarket(position, price);
            }
            else if (_closeAtTrailingStopSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtTrailingStopMarket(position, price, "User click close at Trailing Stop button");
            }
        }

        #endregion

        #region CloseAtProfit

        private StrategyParameterButton _closeAtProfitButton;

        private StrategyParameterButton _closeAtProfitCancelButton;

        private StrategyParameterBool _closeAtProfitSignal;

        private StrategyParameterBool _closeAtProfitIsMarket;

        private void _closeAtProfitButton_UserClickOnButtonEvent()
        {
            if (_closeAtProfitIsMarket.ValueBool == false)
            {
                CloseAtProfitLimitMethod();
            }
            else if (_closeAtProfitIsMarket.ValueBool == true)
            {
                CloseAtProfitMarketMethod();
            }
        }

        private void _closeAtProfitCancelButton_UserClickOnButtonEvent()
        {
            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            for (int i = 0; i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                position.ProfitOrderIsActiv = false;
            }
        }

        private void CloseAtProfitLimitMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].High;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].Low;
            }

            if (_closeAtProfitSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtProfit(position, price, price);
            }
            else if (_closeAtProfitSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtProfit(position, price, price, "User click close at Profit button");
            }
        }

        private void CloseAtProfitMarketMethod()
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

            List<Candle> candles = _tabToTrade.CandlesFinishedOnly;

            if (candles.Count == 0)
            {
                _tabToTrade.SetNewLogMessage("No candles", Logging.LogMessageType.Error);
                return;
            }

            decimal price = 0;

            if (position.Direction == Side.Buy)
            {
                price = candles[candles.Count - 1].High;
            }
            else if (position.Direction == Side.Sell)
            {
                price = candles[candles.Count - 1].Low;
            }

            if (_closeAtProfitSignal.ValueBool == false)
            {
                _tabToTrade.CloseAtProfitMarket(position, price);
            }
            else if (_closeAtProfitSignal.ValueBool == true)
            {
                _tabToTrade.CloseAtProfitMarket(position, price, "User click close at Profit button");
            }
        }

        #endregion

        public override string GetNameStrategyType()
        {
            return "Lesson9Bot4";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}