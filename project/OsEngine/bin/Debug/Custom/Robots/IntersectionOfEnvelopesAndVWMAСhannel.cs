/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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

The trend robot on Envelopes and VWMA channel.

Buy: 
The lower line of Envelopes is above the upper line of the Vwma channel.

Sell:
The upper line Envelopes below the lower line of the Vwma channel.

Exit from the buy: 
The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred
(slides), over the new price minimums, also for the specified period - IvashovRange*MuItIvashov.

Exit from the sell:
The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides), 
to the new maximum of the price, also for the specified period + IvashovRange*MuItIvashov.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("IntersectionOfEnvelopesAndVWMAСhannel")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfEnvelopesAndVWMAСhannel : BotPanel
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
        private StrategyParameterInt _envelopesLength;
        private StrategyParameterDecimal _envelopesDeviation;
        private StrategyParameterInt _lengthVwmaChannel;
        private StrategyParameterInt _lengthMAIvashov;
        private StrategyParameterInt _lengthRangeIvashov;
        private StrategyParameterDecimal _multIvashov;

        // Indicator
        private Aindicator _vwmaHigh;
        private Aindicator _vwmaLow;
        private Aindicator _envelop;
        private Aindicator _rangeIvashov;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastVwmaHigh;
        private decimal _lastVwmaLow;
        private decimal _lastRangeIvashov;

        // Exit Settings
        private StrategyParameterInt _trailCandlesLong;
        private StrategyParameterInt _trailCandlesShort;

        public IntersectionOfEnvelopesAndVWMAСhannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            _envelopesLength = CreateParameter("Envelopes Length", 21, 7, 48, 7, "Indicator");
            _envelopesDeviation = CreateParameter("Envelopes Deviation", 0.5m, 0.1m, 2, 0.1m, "Indicator");
            _lengthVwmaChannel = CreateParameter("Period Vwma", 21, 7, 48, 7, "Indicator");
            _lengthMAIvashov = CreateParameter("Length MA Ivashov", 14, 7, 48, 7, "Indicator");
            _lengthRangeIvashov = CreateParameter("Length Range Ivashov", 14, 7, 48, 7, "Indicator");
            _multIvashov = CreateParameter("Mult Ivashov", 0.5m, 0.1m, 2, 0.1m, "Indicator");

            // Create indicator Envelop
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopesLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopesDeviation.ValueDecimal;
            _envelop.Save();

            // Create indicator VwmaHigh
            _vwmaHigh = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma High", false);
            _vwmaHigh = (Aindicator)_tab.CreateCandleIndicator(_vwmaHigh, "Prime");
            ((IndicatorParameterInt)_vwmaHigh.Parameters[0]).ValueInt = _lengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_vwmaHigh.Parameters[1]).ValueString = "High";
            _vwmaHigh.Save();

            // Create indicator VwmaLow
            _vwmaLow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma Low", false);
            _vwmaLow = (Aindicator)_tab.CreateCandleIndicator(_vwmaLow, "Prime");
            ((IndicatorParameterInt)_vwmaLow.Parameters[0]).ValueInt = _lengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_vwmaLow.Parameters[1]).ValueString = "Low";
            _vwmaLow.Save();

            // Create indicator Ivashov
            _rangeIvashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Range Ivashov", false);
            _rangeIvashov = (Aindicator)_tab.CreateCandleIndicator(_rangeIvashov, "NewArea");
            ((IndicatorParameterInt)_rangeIvashov.Parameters[0]).ValueInt = _lengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_rangeIvashov.Parameters[1]).ValueInt = _lengthRangeIvashov.ValueInt;
            _rangeIvashov.Save();

            // Exit Settings
            _trailCandlesLong = CreateParameter("Stop Value Long", 5, 10, 500, 10, "Exit");
            _trailCandlesShort = CreateParameter("Stop Value Short", 1, 15, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfEnvelopesAndVWMAСhannel_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel201;
        }

        private void IntersectionOfEnvelopesAndVWMAСhannel_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopesLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopesDeviation.ValueDecimal;
            _envelop.Save();
            _envelop.Reload();
           
            ((IndicatorParameterInt)_vwmaHigh.Parameters[0]).ValueInt = _lengthVwmaChannel.ValueInt;
            _vwmaHigh.Save();
            _vwmaHigh.Reload();
           
            ((IndicatorParameterInt)_vwmaLow.Parameters[0]).ValueInt = _lengthVwmaChannel.ValueInt;
            _vwmaLow.Save();
            _vwmaLow.Reload();

            ((IndicatorParameterInt)_rangeIvashov.Parameters[0]).ValueInt = _lengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_rangeIvashov.Parameters[1]).ValueInt = _lengthRangeIvashov.ValueInt;
            _rangeIvashov.Save();
            _rangeIvashov.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfEnvelopesAndVWMAСhannel";
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
            if (candles.Count < _lengthRangeIvashov.ValueInt ||
                candles.Count < _lengthVwmaChannel.ValueInt ||
                candles.Count < _envelopesLength.ValueInt)
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
            _lastVwmaHigh = _vwmaHigh.DataSeries[0].Last;
            _lastVwmaLow = _vwmaLow.DataSeries[0].Last;
            _lastUpLine = _envelop.DataSeries[0].Last;
            _lastDownLine = _envelop.DataSeries[2].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastDownLine > _lastVwmaHigh )
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUpLine < _lastVwmaLow)
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

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1) - _lastRangeIvashov * _multIvashov.ValueDecimal;

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sale
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