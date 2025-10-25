/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
Trading robot for osengine.

Trend robot on the Momentum breakdown with SMA .

Buy:
1. The value of the Momentum indicator broke through the maximum
for a certain number of candles and closed higher.
2. The price is higher than Sma.

Sell: 
1. The value of the Momentum indicator broke through the minimum
for a certain number of candles and closed lower.
2. The price is lower than Sma.

Exit from buy:
Trailing stop = Lowest low (SMA period) – IvashovRange × MultIvashov

Exit from sell:
Trailing stop = Highest high (SMA period) + IvashovRange × MultIvashov
 */

namespace OsEngine.Robots
{
    [Bot("BreakMomentumWithSmaTrading")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class BreakMomentumWithSmaTrading : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _momentumLength;
        private StrategyParameterInt _lengthSma;
        private StrategyParameterInt _lengthMAIvashov;
        private StrategyParameterInt _lengthRangeIvashov;
        private StrategyParameterDecimal _multIvashov;

        // Indicators
        private Aindicator _momentum;
        private Aindicator _sma;
        private Aindicator _rangeIvashov;

        // The last value of the indicator
        private decimal _lastSma;
        private decimal _lastRangeIvashov;

        // Exit settings
        private StrategyParameterInt _trailCandlesLong;
        private StrategyParameterInt _trailCandlesShort;

        public BreakMomentumWithSmaTrading(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Indicator settings
            _momentumLength = CreateParameter("Momentum Length", 10, 5, 100, 5, "Indicator");
            _lengthMAIvashov = CreateParameter("Length MA Ivashov", 14, 7, 48, 7, "Indicator");
            _lengthRangeIvashov = CreateParameter("Length Range Ivashov", 14, 7, 48, 7, "Indicator");
            _multIvashov = CreateParameter("Mult Ivashov", 0.5m, 0.1m, 2, 0.1m, "Indicator");
            _lengthSma = CreateParameter("Length Sma", 100, 10, 300, 10, "Indicator");

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _lengthSma.ValueInt;
            _sma.Save();

            // Create indicator Momentum
            _momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum Length", false);
            _momentum = (Aindicator)_tab.CreateCandleIndicator(_momentum, "NewArea0");
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _momentumLength.ValueInt;
            _momentum.Save();

            // Create indicator Ivashov Range
            _rangeIvashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Range Ivashov", false);
            _rangeIvashov = (Aindicator)_tab.CreateCandleIndicator(_rangeIvashov, "NewArea1");
            ((IndicatorParameterInt)_rangeIvashov.Parameters[0]).ValueInt = _lengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_rangeIvashov.Parameters[1]).ValueInt = _lengthRangeIvashov.ValueInt;
            _rangeIvashov.Save();

            // Exit settings
            _trailCandlesLong = CreateParameter("Stop Value Long", 5, 10, 500, 10, "Exit");
            _trailCandlesShort = CreateParameter("Stop Value Short", 1, 15, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakMomentumWithSmaTrading_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel157;
        }

        private void BreakMomentumWithSmaTrading_ParametrsChangeByUser()
        {
            //Momentum
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _lengthMAIvashov.ValueInt;
            _momentum.Save();
            _momentum.Reload();

            //Sma
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _lengthSma.ValueInt;
            _sma.Save();
            _sma.Reload();

            //Ivashov
            ((IndicatorParameterInt)_rangeIvashov.Parameters[0]).ValueInt = _lengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_rangeIvashov.Parameters[1]).ValueInt = _lengthRangeIvashov.ValueInt;
            _rangeIvashov.Save();
            _rangeIvashov.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakMomentumWithSmaTrading";
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
            if (candles.Count < _momentumLength.ValueInt || candles.Count < _lengthRangeIvashov.ValueInt ||
                candles.Count < _lengthSma.ValueInt || candles.Count < _lengthMAIvashov.ValueInt
                || candles.Count < _multIvashov.ValueDecimal || candles.Count < _trailCandlesLong.ValueInt + 2
                || candles.Count < _trailCandlesShort.ValueInt + 2)
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

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            _lastSma = _sma.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _momentum.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLongAndShort(values, _trailCandlesLong.ValueInt) == "true" && lastPrice > _lastSma)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterLongAndShort(values, _trailCandlesShort.ValueInt) == "false" && lastPrice < _lastSma)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            _lastRangeIvashov = _rangeIvashov.DataSeries[0].Last;
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

                if (position.Direction == Side.Buy) // If the direction of the position is buy
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1) - _lastRangeIvashov * _multIvashov.ValueDecimal;

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sell
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1) + _lastRangeIvashov * _multIvashov.ValueDecimal;

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
            if (candles == null || index < _lengthSma.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - _lengthSma.ValueInt; i--)
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

                for (int i = index; i > index - _lengthSma.ValueInt; i--)
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

        private string EnterLongAndShort(List<decimal> values, int period)
        {
            if (values.Count == period)
            {
                return "false";
            }

            int l = 0;
            decimal Max = -9999999;
            decimal Min = 9999999;

            for (int i = 1; i <= period; i++)
            {
                if (values[values.Count - 1 - i] > Max)
                {
                    Max = values[values.Count - 1 - i];
                }

                if (values[values.Count - 1 - i] < Min)
                {
                    Min = values[values.Count - 1 - i];
                }

                l = i;
            }

            if (Max < values[values.Count - 1])
            {
                return "true";
            }
            else if (Min > values[values.Count - 1])
            {
                return "false";
            }

            return "nope";
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