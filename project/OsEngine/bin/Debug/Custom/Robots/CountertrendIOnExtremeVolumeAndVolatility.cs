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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The countertrend robot on  On Extreme Volume And Volatility.

Buy:
1. The volume is above the average volume for the period (the number of candles back) in the multivolume times.
2. Volatility is higher than the average volatility for the period by a factor of several.
3. A falling candle
Sell:
1. The volume is higher than the average volume for the period (the number of candles back) by a factor of several.
2. Volatility is higher than the average volatility for the period by a factor of several.
3. The candle is growing

Exit after a certain number of hours.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendIOnExtremeVolumeAndVolatility")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendIOnExtremeVolumeAndVolatility : BotPanel
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

        // Indicator Settings 
        private StrategyParameterDecimal _multVolume;
        private StrategyParameterDecimal _multVolatility;
        private StrategyParameterInt _candlesCountVolume;
        private StrategyParameterInt _candlesCountVolatility;
        private StrategyParameterInt _volatilityLength;
        private StrategyParameterDecimal _volatilityCoef;

        // Indicator
        private Aindicator _volumeIndicator;
        private Aindicator _volatility;

        // The last value of the indicator
        private decimal _lastVolume;
        private decimal _lastVolatility;

        // Exit Setting
        private StrategyParameterInt _exitCandles;

        public CountertrendIOnExtremeVolumeAndVolatility(string name, StartProgram startProgram) : base(name, startProgram)
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
            _candlesCountVolume = CreateParameter("CandlesCountVolume", 13, 10, 300, 1, "Indicator");
            _multVolume = CreateParameter("MultVolume", 10.0m, 10, 300, 10, "Indicator");
            _candlesCountVolatility = CreateParameter("CandlesCountVolatility", 13, 10, 300, 1, "Indicator");
            _multVolatility = CreateParameter("MultVolatility", 10.0m, 10, 300, 10, "Indicator");
            _volatilityLength = CreateParameter("VolatilityLength", 50, 10, 300, 1, "Indicator");
            _volatilityCoef = CreateParameter("VolatilityCoef", 0.2m, 0.1m, 1, 0.1m, "Indicator");

            // Create indicator Volatility
            _volatility = IndicatorsFactory.CreateIndicatorByName("VolatilityCandles", name + "VolatilityCandles", false);
            _volatility = (Aindicator)_tab.CreateCandleIndicator(_volatility, "NewArea0");
            ((IndicatorParameterInt)_volatility.Parameters[0]).ValueInt = _volatilityLength.ValueInt;
            ((IndicatorParameterDecimal)_volatility.Parameters[1]).ValueDecimal = _volatilityCoef.ValueDecimal;
            _volatility.Save();

            // Create indicator Volume
            _volumeIndicator = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _volumeIndicator = (Aindicator)_tab.CreateCandleIndicator(_volumeIndicator, "NewArea");
            _volumeIndicator.Save();

            // Exit Setting
            _exitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendIOnExtremeVolumeAndVolatility_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel191;
        }

        private void CountertrendIOnExtremeVolumeAndVolatility_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_volatility.Parameters[0]).ValueInt = _volatilityLength.ValueInt;
            ((IndicatorParameterDecimal)_volatility.Parameters[1]).ValueDecimal = _volatilityCoef.ValueDecimal;
            _volatility.Save();
            _volatility.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendIOnExtremeVolumeAndVolatility";
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
            if (candles.Count < _candlesCountVolume.ValueInt ||
                candles.Count < _candlesCountVolatility.ValueInt ||
                candles.Count < _volatilityLength.ValueInt)
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
            _lastVolume = _volumeIndicator.DataSeries[0].Last;
            _lastVolatility = _volatility.DataSeries[0].Last;

            // The prev value of the indicator

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPrice = candles[candles.Count - 2].Close;

                List<decimal> VolumeValues = _volumeIndicator.DataSeries[0].Values;
                List<decimal> VolatilityValues = _volatility.DataSeries[0].Values;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (GetAverage(VolatilityValues, _candlesCountVolatility.ValueInt) * _multVolatility.ValueDecimal < _lastVolatility &&
                        GetAverage(VolumeValues, _candlesCountVolume.ValueInt) * _multVolume.ValueDecimal < _lastVolume &&
                        candles[candles.Count - 1].IsDown)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (GetAverage(VolatilityValues, _candlesCountVolatility.ValueInt) * _multVolatility.ValueDecimal < _lastVolatility &&
                        GetAverage(VolumeValues, _candlesCountVolume.ValueInt) * _multVolume.ValueDecimal < _lastVolume &&
                        candles[candles.Count - 1].IsUp)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
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

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is long
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is short
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }
            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= _exitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private decimal GetAverage(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = 2; i < period; i++)
            {
                sum += Volume[Volume.Count - i];
            }

            return sum / period;
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