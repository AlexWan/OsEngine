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

The trend robot on Strategy Rsi,Two Ema And Stohastic.

Buy:
1. Fast Ema is higher than slow Ema.
2. The Rsi is above 50 and below 70, rising.
3. Stochastic is growing and is above 20 and below 80.
Sell:
1. Fast Ema is lower than slow Ema.
2. The Rsi is below 50 and above 20, falling.
3. Stochastic is falling and is above 20 and below 80.

Exit: the opposite signal of the Ema.
 */

namespace OsEngine.Robots
{
    [Bot("StrategyRsiTwoEmaAndStohastic")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyRsiTwoEmaAndStohastic : BotPanel
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
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterInt _periodRSI;
        private StrategyParameterInt _stochPeriod1;
        private StrategyParameterInt _stochPeriod2;
        private StrategyParameterInt _stochPeriod3;

        // Indicator
        private Aindicator _rsi;
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _stoh;

        // The last value of the indicator
        private decimal _lastRSI;
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevRSI;
        private decimal _prevStoh;

        public StrategyRsiTwoEmaAndStohastic(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEmaFast = CreateParameter("fast EMA1 period", 250, 50, 500, 50, "Indicator");
            _periodEmaSlow = CreateParameter("slow EMA2 period", 1000, 500, 1500, 100, "Indicator");
            _periodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");
            _stochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1, "Indicator");
            _stochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1, "Indicator");
            _stochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1, "Indicator");

            // Create indicator RSI
            _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "NewArea");
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _periodRSI.ValueInt;
            _rsi.Save();

            // Creating indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating indicator EmaSlow
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema2.DataSeries[0].Color = Color.Green;
            _ema2.Save();

            // Create indicator Stoh
            _stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stoh", false);
            _stoh = (Aindicator)_tab.CreateCandleIndicator(_stoh, "NewArea0");
            ((IndicatorParameterInt)_stoh.Parameters[0]).ValueInt = _stochPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[1]).ValueInt = _stochPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[2]).ValueInt = _stochPeriod1.ValueInt;
            _stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanAndVwma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel273;
        }

        private void IntersectionKalmanAndVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _periodRSI.ValueInt;
            _rsi.Save();
            _rsi.Reload();
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema2.Save();
            _ema2.Reload();
            ((IndicatorParameterInt)_stoh.Parameters[0]).ValueInt = _stochPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[1]).ValueInt = _stochPeriod1.ValueInt;
            ((IndicatorParameterInt)_stoh.Parameters[2]).ValueInt = _stochPeriod1.ValueInt;
            _stoh.Save();
            _stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyRsiTwoEmaAndStohastic";
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
            if (candles.Count < _periodRSI.ValueInt ||
                candles.Count < _periodEmaFast.ValueInt ||
                candles.Count < _periodEmaSlow.ValueInt ||
                candles.Count < _stochPeriod1.ValueInt ||
                candles.Count < _stochPeriod2.ValueInt ||
                candles.Count < _stochPeriod3.ValueInt)
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
            _lastRSI = _rsi.DataSeries[0].Last;
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaSlow = _ema2.DataSeries[0].Last;
            _lastStoh = _stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevRSI = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];
            _prevStoh = _stoh.DataSeries[0].Values[_stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaSlow &&
                        _lastRSI > 50 && _lastRSI < 70 && _prevRSI < _lastRSI &&
                        _lastStoh > 20 && _lastStoh < 80 && _prevStoh > _lastStoh)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaSlow &&
                        _lastRSI < 50 && _lastRSI > 20 && _prevRSI > _lastRSI &&
                        _lastStoh > 20 && _lastStoh < 80 && _prevStoh > _lastStoh)
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

            // The last value of the indicator
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaSlow = _ema2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastEmaFast < _lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastEmaFast > _lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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