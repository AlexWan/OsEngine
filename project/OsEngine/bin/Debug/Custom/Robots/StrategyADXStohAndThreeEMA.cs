/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on strategy ADX, Stochastic and three Ema.

Buy:
1. fast Ema is higher than the middle Ema and the middle is higher than the slow one.
2. Stochastic crosses the level 50 and is growing (from bottom to top).
3. Adx is rising and crosses level 20 upwards (growing).

Sell:
1. fast Ema is below the middle Ema and the middle is below the slow one.
2. Stochastic crosses the level 50 and falls (from top to bottom).
3. Adx is rising and crosses level 20 upwards (growing).

Exit:
From buy: fast Ema below middle Ema.
From sell: fast Ema above middle Ema.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyADXStohAndThreeEMA")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyADXStohAndThreeEMA : BotPanel
    {
        // Reference to the main trading tab
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

        // Indicators settings
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodEmaMiddle;
        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterInt _periodADX;
        private StrategyParameterInt _stochasticPeriod1;
        private StrategyParameterInt _stochasticPeriod2;
        private StrategyParameterInt _stochasticPeriod3;

        // Indicators
        private Aindicator _emaFast;
        private Aindicator _emaMiddle;
        private Aindicator _emaSlow;
        private Aindicator _ADX;
        private Aindicator _stoh;

        // The last value of the indicator
        private decimal _lastEmaFast;
        private decimal _lastEmaMiddle;
        private decimal _lastEmaSlow;
        private decimal _lastADX;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevStoh;

        public StrategyADXStohAndThreeEMA(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
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

            // Indicators settings
            _periodEmaFast = CreateParameter("Period Ema Fast", 10, 10, 100, 10, "Indicator");
            _periodEmaMiddle = CreateParameter("Period Ema Middle", 20, 10, 300, 10, "Indicator");
            _periodEmaSlow = CreateParameter("Period Ema Slow", 30, 10, 100, 10, "Indicator");
            _periodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod1 = CreateParameter("StochasticPeriod One", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod2 = CreateParameter("StochasticPeriod Two", 3, 10, 300, 10, "Indicator");
            _stochasticPeriod3 = CreateParameter("StochasticPeriod Three", 3, 10, 300, 10, "Indicator");

            // Create indicator EmaFast
            _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaFast", false);
            _emaFast = (Aindicator)_tab.CreateCandleIndicator(_emaFast, "Prime");
            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _emaFast.DataSeries[0].Color = Color.Gray;
            _emaFast.Save();

            // Create indicator EmaMiddle
            _emaMiddle = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaMiddle", false);
            _emaMiddle = (Aindicator)_tab.CreateCandleIndicator(_emaMiddle, "Prime");
            ((IndicatorParameterInt)_emaMiddle.Parameters[0]).ValueInt = _periodEmaMiddle.ValueInt;
            _emaMiddle.DataSeries[0].Color = Color.Pink;
            _emaMiddle.Save();

            // Create indicator EmaSlow
            _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaSlow", false);
            _emaSlow = (Aindicator)_tab.CreateCandleIndicator(_emaSlow, "Prime");
            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _emaSlow.DataSeries[0].Color = Color.Yellow;
            _emaSlow.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();

            // Create indicator Stoh
            _stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stoh = (Aindicator)_tab.CreateCandleIndicator(_stoh, "NewArea0");
            ((IndicatorParameterInt)_stoh.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyADXStohAndThreeEMA_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel238;
        }

        private void StrategyADXStohAndThreeEMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _emaFast.Save();
            _emaFast.Reload();

            ((IndicatorParameterInt)_emaMiddle.Parameters[0]).ValueInt = _periodEmaMiddle.ValueInt;
            _emaMiddle.Save();
            _emaMiddle.Reload();

            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _emaSlow.Save();
            _emaSlow.Reload();

            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();

            ((IndicatorParameterInt)_stoh.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stoh.Save();
            _stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyADXStohAndThreeEMA";
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
            if (candles.Count <= _periodEmaFast.ValueInt ||
                candles.Count <= _periodEmaMiddle.ValueInt ||
                candles.Count <= _periodEmaSlow.ValueInt ||
                candles.Count <= _periodADX.ValueInt ||
                candles.Count <= _stochasticPeriod1.ValueInt ||
                candles.Count <= _stochasticPeriod2.ValueInt ||
                candles.Count <= _stochasticPeriod3.ValueInt)
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
            _lastEmaFast = _emaFast.DataSeries[0].Last;
            _lastEmaMiddle = _emaMiddle.DataSeries[0].Last;
            _lastEmaSlow = _emaSlow.DataSeries[0].Last;
            _lastADX = _ADX.DataSeries[0].Last;
            _lastStoh = _stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];
            _prevStoh = _stoh.DataSeries[0].Values[_stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _ADX.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaMiddle &&
                        _lastEmaMiddle > _lastEmaSlow &&
                        _prevStoh < 50 && _lastStoh > 50 &&
                        _prevADX < 20 && _lastADX > 20)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaMiddle &&
                        _lastEmaMiddle < _lastEmaSlow &&
                        _prevStoh > 50 && _lastStoh < 50 &&
                        _prevADX < 20 && _lastADX > 20)
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

            // The last value of the indicator
            _lastEmaFast = _emaFast.DataSeries[0].Last;
            _lastEmaMiddle = _emaMiddle.DataSeries[0].Last;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastEmaFast < _lastEmaMiddle)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastEmaFast > _lastEmaMiddle)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
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