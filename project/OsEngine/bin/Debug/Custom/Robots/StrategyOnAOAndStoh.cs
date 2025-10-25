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
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

Trend robot on Awesome Oscillator and Stochastic Oscillator

Buy:
1. Stochastic above 50 but below 80.
2. Column Awesome previous above preprevious.

Sell:
1. Stochastic below 50 but above 20.
2. Column Awesome previous below preprevious.

Exit from buy: Stop and profit.
The stop is placed behind the minimum for the period specified for the stop (StopCandles).
Profit is equal to the size of the stop * CoefProfit (CoefProfit - how many times the size of the profit is greater than the size of the stop).

Exit from sell: Stop and profit.
The stop is placed behind the maximum for the period specified for the stop (StopCandles).
Profit is equal to the size of the stop * CoefProfit (CoefProfit - how many times the size of the profit is greater than the size of the stop).
 */

namespace OsEngine.Robots
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("StrategyOnAOAndStoh")]
    public class StrategyOnAOAndStoh : BotPanel
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
        private StrategyParameterInt _fastLineLengthAO;
        private StrategyParameterInt _slowLineLengthAO;
        private StrategyParameterInt _stochasticPeriod1;
        private StrategyParameterInt _stochasticPeriod2;
        private StrategyParameterInt _stochasticPeriod3;

        // Indicator
        private Aindicator _AO;
        private Aindicator _stochastic;

        // The last value of the indicators
        private decimal _lastStochastic;

        // The prevlast value of the indicator
        private decimal _prevAO;

        // The prevprev value of the indicator
        private decimal _prevprevAO;

        // Exit settings
        private StrategyParameterDecimal _coefProfit;
        private StrategyParameterInt _stopCandles;

        public StrategyOnAOAndStoh(string name, StartProgram startProgram) : base(name, startProgram)
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
            _fastLineLengthAO = CreateParameter("Fast Line Length AO", 12, 10, 300, 10, "Indicator");
            _slowLineLengthAO = CreateParameter("Slow Line Length AO", 24, 10, 300, 10, "Indicator");
            _stochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod2 = CreateParameter("Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            _stochasticPeriod3 = CreateParameter("Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator AO
            _AO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _AO = (Aindicator)_tab.CreateCandleIndicator(_AO, "NewArea");
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            _AO.Save();

            // Create indicator Stochastic
            _stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stochastic = (Aindicator)_tab.CreateCandleIndicator(_stochastic, "NewArea0");
            ((IndicatorParameterInt)_stochastic.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stochastic.Save();

            // Exit settings
            _coefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            _stopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyOnAOAndStoh_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel261;
        }

        // Indicator Update event
        private void StrategyOnAOAndStoh_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            _AO.Save();
            _AO.Reload();
            ((IndicatorParameterInt)_stochastic.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stochastic.Save();
            _stochastic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyOnAOAndStoh";
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
            if (candles.Count < _slowLineLengthAO.ValueInt || candles.Count < _stochasticPeriod3.ValueInt)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastStochastic = _stochastic.DataSeries[0].Last;

                // The prevlast value of the indicators
                _prevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 2];

                // The prevprev value of the indicators
                _prevprevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 3];

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastStochastic > 50 && _lastStochastic < 80 && _prevAO > _prevprevAO)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastStochastic < 50 && _lastStochastic > 20 && _prevAO < _prevprevAO)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
                return;
            }
        }

        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation = 0;
            decimal price = 0;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Sell, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }

        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < _stopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // calculate the stop price at Long
                // 1 find the minimum for the time from the opening of the transaction to the current
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // look at the index of the candle after which the position was opened
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                { // look at the minimum after the opening

                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                // 1 find the maximum for the time from the opening of the transaction to the current
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // look at the index of the candle after which the position was opened
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                { // watch the maximum high

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