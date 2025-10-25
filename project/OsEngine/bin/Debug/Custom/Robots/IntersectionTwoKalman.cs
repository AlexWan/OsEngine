/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Intersection Two Kalman.

Buy: KalmanFast above KalmanSlow.

Sell: KalmanFast below KalmanSlow.

Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred, (slides), to new price lows, also for the specified period.

Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */

namespace OsEngine.Robots
{
    [Bot("IntersectionTwoKalman")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionTwoKalman : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings 
        private StrategyParameterDecimal _sharpnessFast;
        private StrategyParameterDecimal _coefKFast;
        private StrategyParameterDecimal _sharpnessSlow;
        private StrategyParameterDecimal _coefKSlow;

        // Indicator
        private Aindicator _kalmanFast;
        private Aindicator _kalmanSlow;

        // Exit settings
        private StrategyParameterInt _trailCandlesLong;
        private StrategyParameterInt _trailCandlesShort;

        // The last value of the indicator
        private decimal _lastKalmanFast;
        private decimal _lastKalmanSlow;

        // The prev value of the indicator
        private decimal _prevKalmanFast;
        private decimal _prevKalmanSlow;

        public IntersectionTwoKalman(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _sharpnessFast = CreateParameter("Sharpness Fast", 1.0m, 1, 50, 1, "Indicator");
            _coefKFast = CreateParameter("CoefK Fast", 1.0m, 1, 50, 1, "Indicator");
            _sharpnessSlow = CreateParameter("Sharpness Slow", 2.0m, 1, 50, 1, "Indicator");
            _coefKSlow = CreateParameter("CoefK Slow", 2.0m, 1, 50, 1, "Indicator");

            // Create indicator KalmanFilter Fast
            _kalmanFast = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter Fast", false);
            _kalmanFast = (Aindicator)_tab.CreateCandleIndicator(_kalmanFast, "Prime");
            ((IndicatorParameterDecimal)_kalmanFast.Parameters[0]).ValueDecimal = _sharpnessFast.ValueDecimal;
            ((IndicatorParameterDecimal)_kalmanFast.Parameters[1]).ValueDecimal = _coefKFast.ValueDecimal;
            _kalmanFast.Save();

            // Create indicator KalmanFilter Slow
            _kalmanSlow = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter Slow", false);
            _kalmanSlow = (Aindicator)_tab.CreateCandleIndicator(_kalmanSlow, "Prime");
            ((IndicatorParameterDecimal)_kalmanSlow.Parameters[0]).ValueDecimal = _sharpnessSlow.ValueDecimal;
            ((IndicatorParameterDecimal)_kalmanSlow.Parameters[1]).ValueDecimal = _coefKSlow.ValueDecimal;
            _kalmanSlow.Save();

            // Exit settings
            _trailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            _trailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionTwoKalman_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel218;
        }

        private void IntersectionTwoKalman_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_kalmanFast.Parameters[0]).ValueDecimal = _sharpnessFast.ValueDecimal;
            ((IndicatorParameterDecimal)_kalmanFast.Parameters[1]).ValueDecimal = _coefKFast.ValueDecimal;
            _kalmanFast.Save();
            _kalmanFast.Reload();

            ((IndicatorParameterDecimal)_kalmanSlow.Parameters[0]).ValueDecimal = _sharpnessSlow.ValueDecimal;
            ((IndicatorParameterDecimal)_kalmanSlow.Parameters[1]).ValueDecimal = _coefKSlow.ValueDecimal;
            _kalmanSlow.Save();
            _kalmanSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionTwoKalman";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _coefKSlow.ValueDecimal ||
                candles.Count < _sharpnessSlow.ValueDecimal ||
                candles.Count < _coefKFast.ValueDecimal ||
                candles.Count < _sharpnessFast.ValueDecimal ||
                candles.Count < _trailCandlesLong.ValueInt ||
                candles.Count < _trailCandlesShort.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastKalmanFast = _kalmanFast.DataSeries[0].Last;
            _lastKalmanSlow = _kalmanSlow.DataSeries[0].Last;

            // The prev value of the indicator
            _prevKalmanFast = _kalmanFast.DataSeries[0].Values[_kalmanFast.DataSeries[0].Values.Count - 2];
            _prevKalmanSlow = _kalmanSlow.DataSeries[0].Values[_kalmanSlow.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevKalmanSlow > _prevKalmanFast && _lastKalmanSlow < _lastKalmanFast)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevKalmanSlow < _prevKalmanFast && _lastKalmanSlow > _lastKalmanFast)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(position, price, price + _slippage);
                }
            }
        }

        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < _trailCandlesLong.ValueInt || index < _trailCandlesShort.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - _trailCandlesLong.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - _trailCandlesShort.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}