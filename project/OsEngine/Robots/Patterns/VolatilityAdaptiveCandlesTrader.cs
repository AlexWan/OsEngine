/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Volatility Adaptive Candles Trader.

Buy:
1. If the difference between the opening and closing price of the current candle relative
to its price is less than the specified threshold (HeightSignalCandle), then no entry is made.  
2. The current candle is bullish (the closing price is higher than the opening price).  

Sell: 
1. If the difference between the opening and closing price of the current candle relative 
to its price is less than the specified threshold (HeightSignalCandle), then no entry is made.  
2. The current candle is bearish (the closing price is lower than the opening price).  

Exit: by trailing stop.
 */

namespace OsEngine.Robots.Patterns
{
    [Bot("VolatilityAdaptiveCandlesTrader")] // We create an attribute so that we don't write anything to the BotFactory
    public class VolatilityAdaptiveCandlesTrader : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _heightSignalCandle;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Volatility settings
        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightSignalCandleVolaPercent;
        private StrategyParameterDecimal _trailingStopVolaPercent;

        // Exit setting
        private StrategyParameterDecimal _trailingStopPercent;

        public VolatilityAdaptiveCandlesTrader(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);
            _heightSignalCandle = CreateParameter("Height signal candle %", 1, 0, 20, 1m);

            // Volatility settings
            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 1, 0, 20, 1);
            _trailingStopVolaPercent = CreateParameter("Height trail stop volatility percent", 10, 0, 20, 1m);
            _heightSignalCandleVolaPercent = CreateParameter("Height signal candle volatility percent", 20, 0, 20, 1m);
            
            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit setting
            _trailingStopPercent = CreateParameter("Trail stop %", 20m, 0, 20, 1m);

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel78;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "VolatilityAdaptiveCandlesTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Volatility adaptation
        private void AdaptSignalCandleHeight(List<Candle> candles)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightSignalCandleVolaPercent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 we calculate the movement from high to low within N days

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);

                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= _daysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }

                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2 we average this movement. We need average volatility percentage

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 we calculate the size of the parameters taking this volatility into account

            decimal signalCandleHeight = volaPercentSma * (_heightSignalCandleVolaPercent.ValueDecimal / 100);
            _heightSignalCandle.ValueDecimal = signalCandleHeight;

            decimal trailStopHeight = volaPercentSma * (_trailingStopVolaPercent.ValueDecimal / 100);
            _trailingStopPercent.ValueDecimal = trailStopHeight;
        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            if (candles.Count > 20 &&
                candles[candles.Count - 1].TimeStart.Date != candles[candles.Count - 2].TimeStart.Date)
            {
                AdaptSignalCandleHeight(candles);
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }

                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < _heightSignalCandle.ValueDecimal)
            {
                return;
            }

            //  long
            if (_regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _lastPrice * (_slippage.ValueDecimal / 100));
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _lastPrice * (_slippage.ValueDecimal / 100));
                }
            }

            return;
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal priceStop = _lastPrice - (_lastPrice * _trailingStopPercent.ValueDecimal) / 100;
                    _tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop - priceStop * (_slippage.ValueDecimal / 100));
                }
                else //if (openPositions[i].Direction == Side.Sell)
                {
                    decimal priceStop = _lastPrice + (_lastPrice * _trailingStopPercent.ValueDecimal) / 100;
                    _tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop + priceStop * (_slippage.ValueDecimal / 100));
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