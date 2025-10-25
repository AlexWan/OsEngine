/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
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

The Countertrend robot on Envelopes, CCI and ER.

Buy:
1. During the CandlesCountLow period, the candle's loy was below the lower Envelopes line, then the candle closed above the lower line.
 2. During the same period there was a maximum of Er, then it began to fall.
 3. During the same period, the CCI value was above +100, then it began to fall.
Sell:
 1. During the CandlesCountHigh period, the high of the candle was above the upper line of the Envelopes, then the candle closed below the upper line.
 2. During the same period there was a maximum of Er, then it began to fall.
 3. During the same period, the CCI value was below -100, then it grows.

Exit from buy: trailing stop in % of the Low candle on which you entered.
Exit from sell: trailing stop in % of the High of the candle on which you entered.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendEnvelopesCCIandER")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendEnvelopesCCIandER : BotPanel
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
        private StrategyParameterInt _lengthCCI;
        private StrategyParameterInt _envelopLength;
        private StrategyParameterDecimal _envelopesDeviation;
        private StrategyParameterInt _lengthER;
        private StrategyParameterInt _candlesCountLow;
        private StrategyParameterInt _candlesCountHigh;

        // Indicator
        private Aindicator _CCI;
        private Aindicator _envelopes;
        private Aindicator _ER;

        // Exit Setting
        private StrategyParameterInt _trailingValue;

        // The last value of the indicator
        private decimal _lastCCI;
        private decimal _lastEnvelopUp;
        private decimal _lastEnvelopDown;
        private decimal _lastER;

        public CountertrendEnvelopesCCIandER(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lengthCCI = CreateParameter("CCI Length", 14, 7, 48, 7, "Indicator");
            _envelopLength = CreateParameter("Envelop Length", 10, 10, 300, 10, "Indicator");
            _envelopesDeviation = CreateParameter("Envelopes Deviation", 3.0m, 1, 5, 0.1m, "Indicator");
            _lengthER = CreateParameter("LengthER", 20, 10, 300, 10, "Indicator");
            _candlesCountLow = CreateParameter("Candles Count Low", 10, 10, 200, 10, "Indicator");
            _candlesCountHigh = CreateParameter("Candles Count High", 10, 10, 200, 10, "Indicator");

            // Create indicator Envelopes
            _envelopes = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelopes = (Aindicator)_tab.CreateCandleIndicator(_envelopes, "Prime");
            ((IndicatorParameterInt)_envelopes.Parameters[0]).ValueInt = _envelopLength.ValueInt;
            ((IndicatorParameterDecimal)_envelopes.Parameters[1]).ValueDecimal = _envelopesDeviation.ValueDecimal;
            _envelopes.Save();

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = _lengthCCI.ValueInt;
            _CCI.Save();

            // Create indicator EfficiencyRatio
            _ER = IndicatorsFactory.CreateIndicatorByName("EfficiencyRatio", name + "EfficiencyRatio", false);
            _ER = (Aindicator)_tab.CreateCandleIndicator(_ER, "NewArea0");
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = _lengthER.ValueInt;
            _ER.Save();

            // Exit Setting
            _trailingValue = CreateParameter("Stop Value", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendEnvelopesCCIandER_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel189;
        }

        private void CountertrendEnvelopesCCIandER_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = _lengthCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
            ((IndicatorParameterInt)_envelopes.Parameters[0]).ValueInt = _envelopLength.ValueInt;
            ((IndicatorParameterDecimal)_envelopes.Parameters[1]).ValueDecimal = _envelopesDeviation.ValueDecimal;
            _envelopes.Save();
            _envelopes.Reload();
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = _lengthER.ValueInt;
            _ER.Save();
            _ER.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendEnvelopesCCIandER";
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
            if (candles.Count < _lengthCCI.ValueInt ||
                candles.Count < _lengthER.ValueInt ||
                candles.Count < _envelopLength.ValueInt ||
                candles.Count < _candlesCountLow.ValueInt + 3||
                candles.Count < _candlesCountHigh.ValueInt + 3)
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
            _lastCCI = _CCI.DataSeries[0].Last;
            _lastEnvelopUp = _envelopes.DataSeries[0].Last;
            _lastEnvelopDown = _envelopes.DataSeries[2].Last;
            _lastER = _ER.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPriceLow = candles[candles.Count - 1].Low;
                decimal lastPriceHigh = candles[candles.Count - 1].High;

                List<decimal> VolumeER = _ER.DataSeries[0].Values;
                List<decimal> VolumeCCI = _CCI.DataSeries[0].Values;

                List<decimal> ValueUp = _envelopes.DataSeries[0].Values;
                List<decimal> ValueDown = _envelopes.DataSeries[2].Values;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (MaxValueOnPeriodInddicator(VolumeER,_candlesCountLow.ValueInt) > _lastER &&
                        MaxValueOnPeriodInddicator(VolumeCCI,_candlesCountLow.ValueInt) > 100 &&
                        _lastCCI < 100 && lastPriceLow > _lastEnvelopDown && EnterSellAndBuy(Side.Buy, candles,ValueDown) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (MaxValueOnPeriodInddicator(VolumeER,_candlesCountHigh.ValueInt) > _lastER && 
                        MinValueOnPeriodInddicator(VolumeCCI, _candlesCountHigh.ValueInt) < -100 &&
                        _lastCCI > -100 && lastPriceHigh < _lastEnvelopUp && EnterSellAndBuy(Side.Sell,candles,ValueUp) == true)
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

            decimal stopPrice;

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
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueInt / 100;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueInt / 100;
                }

                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
            }
        }

        private decimal MaxValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal max = 0;

            for (int i = 2; i <= period; i++)
            {
                if (max < Value[Value.Count - i])
                {
                    max = Value[Value.Count - i];
                }
            }

            return max;
        }

        private bool EnterSellAndBuy(Side side, List<Candle> candles, List<decimal> Value)
        {
            if(side == Side.Buy)
            {
                for (int i = 2; i <= _candlesCountLow.ValueInt + 2; i++)
                {
                    if (candles[candles.Count - i].Low > _envelopes.DataSeries[2].Values[_envelopes.DataSeries[2].Values.Count - i])
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 2; i <= _candlesCountHigh.ValueInt + 2; i++)
                {
                    if (candles[candles.Count - i].High < _envelopes.DataSeries[0].Values[_envelopes.DataSeries[0].Values.Count - i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private decimal MinValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal min = 999999;

            for (int i = 2; i <= period; i++)
            {
                if (min > Value[Value.Count - i])
                {
                    min = Value[Value.Count - i];
                }
            }

            return min;
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