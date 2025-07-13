﻿/*
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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The Countertrend robot on Bollinger And Volumes.

Buy:
1. During the CandlesCountLow period, the candle's loy was below the lower Bollinger line, then the candle closed above the lower line.
2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were minimal.
That is, when the price returned to the channel (closed above the lower Bollinger line), the Volume indicator should be below its highs, and
Eom and OBV should be above their lows for the CandlesCountLow period.

Sell:
1. During the CandlesCountHigh period, the high of the candle was above the upper Bollinger line, then the candle closed below the upper line.
2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were maximum.
That is, when the price returned to the channel (closed below the upper Bollinger line), the Volume, Eom and OBV indicators should be below 
their highs for the CandlesCountHigh period.

Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred, (slides), to new price lows, also for the specified period.

Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendBollingerAndVolumes")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendBollingerAndVolumes : BotPanel
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
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;
        private StrategyParameterInt _lengthEom;
        private StrategyParameterInt _candlesCountLow;
        private StrategyParameterInt _candlesCountHigh;
        private StrategyParameterInt _period;

        // Indicator
        private Aindicator _bollinger;
        private Aindicator _EOM;
        private Aindicator _OBV;
        private Aindicator _volumeIndicator;

        // Exit Settings
        private StrategyParameterInt _trailCandlesLong;
        private StrategyParameterInt _trailCandlesShort;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastEOM;
        private decimal _lastOBV;
        private decimal _lastVolume;

        public CountertrendBollingerAndVolumes(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _bollingerLength = CreateParameter("Bollinger Length", 21, 7, 48, 7, "Indicator");
            _bollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            _lengthEom = CreateParameter("Eom Length", 14, 7, 48, 7, "Indicator");
            _candlesCountLow = CreateParameter("Candles Count Low", 10, 10, 200, 10, "Indicator");
            _candlesCountHigh = CreateParameter("Candles Count High", 10, 10, 200, 10, "Indicator");
            _period = CreateParameter("Period", 10, 10, 200, 10, "Indicator");

            // Create indicator Bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();

            // Create indicator EOM
            _EOM = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement", name + "EaseOfMovement", false);
            _EOM = (Aindicator)_tab.CreateCandleIndicator(_EOM, "NewArea");
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = _lengthEom.ValueInt;
            _EOM.Save();

            // Create indicator OBV
            _OBV = IndicatorsFactory.CreateIndicatorByName("OBV", name + "OBV", false);
            _OBV = (Aindicator)_tab.CreateCandleIndicator(_OBV, "NewArea0");
            _OBV.Save();

            // Create indicator Volume
            _volumeIndicator = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _volumeIndicator = (Aindicator)_tab.CreateCandleIndicator(_volumeIndicator, "NewArea1");
            _volumeIndicator.Save();

            // Exit Settings
            _trailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            _trailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelVwmaATR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel186;
        }

        private void BreakChannelVwmaATR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();
            _bollinger.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendBollingerAndVolumes";
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
            if (candles.Count < _bollingerDeviation.ValueDecimal ||
                candles.Count < _bollingerLength.ValueInt ||
                candles.Count < _lengthEom.ValueInt ||
                candles.Count < _period.ValueInt + _period.ValueInt ||
                candles.Count < _candlesCountHigh.ValueInt ||
                candles.Count < _candlesCountLow.ValueInt)
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
            _lastUpLine = _bollinger.DataSeries[0].Last;
            _lastDownLine = _bollinger.DataSeries[1].Last;
            _lastEOM = _EOM.DataSeries[0].Last;
            _lastOBV = _OBV.DataSeries[0].Last;
            _lastVolume = _volumeIndicator.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> Volume = _volumeIndicator.DataSeries[0].Values;
                List<decimal> VolumeEOM = _EOM.DataSeries[0].Values;
                List<decimal> VolumeOBV = _OBV.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (AverageVolumeValue(Volume, _period.ValueInt) > AverageVolumeValueTwo(Volume, _period.ValueInt) &&
                        MaxValueOnPeriodInddicator(Volume, _period.ValueInt) > _lastVolume &&
                        MinValueOnPeriodInddicator(VolumeEOM, _period.ValueInt) < _lastEOM &&
                        MinValueOnPeriodInddicator(VolumeOBV, _period.ValueInt) < _lastOBV && 
                        lastPrice > _lastDownLine)
                    {
                        for(int i = 2; i <= _candlesCountLow.ValueInt; i++)
                        {
                            if (candles[candles.Count - i].Low > _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - i])
                            {
                                return;
                            }
                        }

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (AverageVolumeValue(Volume, _period.ValueInt) > AverageVolumeValueTwo(Volume, _period.ValueInt) &&
                        MaxValueOnPeriodInddicator(Volume, _period.ValueInt) > _lastVolume &&
                        MaxValueOnPeriodInddicator(VolumeEOM, _period.ValueInt) > _lastEOM &&
                        MaxValueOnPeriodInddicator(VolumeOBV, _period.ValueInt) > _lastOBV &&
                        lastPrice < _lastUpLine)
                    {
                        for (int i = 2; i <= _candlesCountHigh.ValueInt; i++)
                        {
                            if (candles[candles.Count - i].High < _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - i])
                            {
                                return;
                            }
                        }

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

        private decimal MaxValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal max = 0;

            for (int i = 2; i <= period; i++)
            {
                if(max < Value[Value.Count - i])
                {
                    max = Value[Value.Count - i];
                }
            }

            return max;
        }

        private decimal MinValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal min = 99999;

            for (int i = 2; i <= period; i++)
            {
                if (min > Value[Value.Count - i])
                {
                    min = Value[Value.Count - i];
                }
            }

            return min;
        }

        private decimal AverageVolumeValue(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = 1; i <= period; i++)
            {
                sum += Volume[Volume.Count - i];
            }

            if (sum > 0)
            {
                return sum / period;
            }

            return 0;
        }

        private decimal AverageVolumeValueTwo(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = period; i <= period * 2; i++)
            {
                sum += Volume[Volume.Count - i];
            }

            if (sum > 0)
            {
                return sum / period;
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