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

The trend robot on Three Soldier Volatility Adaptive.

Logic:
For the last three candles, it is checked that the absolute value of the difference between the opening and closing prices relative to the closing price exceeds a specified threshold (Height Soldiers for the most recent candle and min Height One Soldier for the previous ones).

Buy: All three of the most recent candles are bullish (rising).

Sell: All three of the most recent candles are bearish (falling).

Exit: Based on stop-loss and take-profit levels.
 */

namespace OsEngine.Robots.Patterns
{
    [Bot("ThreeSoldierVolatilityAdaptive")] // We create an attribute so that we don't write anything to the BotFactory
    public class ThreeSoldierVolatilityAdaptive : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _heightSoldiers;
        private StrategyParameterDecimal _minHeightOneSoldier;
        private StrategyParameterDecimal _procHeightTake;
        private StrategyParameterDecimal _procHeightStop;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Volatility settings
        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightSoldiersVolaPecrent;
        private StrategyParameterDecimal _minHeightOneSoldiersVolaPecrent;

        public ThreeSoldierVolatilityAdaptive(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);
            _heightSoldiers = CreateParameter("Height soldiers %", 1, 0, 20, 1m);
            _minHeightOneSoldier = CreateParameter("Min height one soldier %", 0.2m, 0, 20, 1m);
            _procHeightTake = CreateParameter("Profit % from height of pattern", 50m, 0, 20, 1m);
            _procHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Volatility settings
            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 1, 0, 20, 1);
            _heightSoldiersVolaPecrent = CreateParameter("Height soldiers volatility percent", 5, 0, 20, 1m);
            _minHeightOneSoldiersVolaPecrent = CreateParameter("Min height one soldier volatility percent", 1, 0, 20, 1m);

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel77;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ThreeSoldierVolatilityAdaptive";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Volatility adaptation
        private void AdaptSoldiersHeight(List<Candle> candles)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightSoldiersVolaPecrent.ValueDecimal <= 0
                || _minHeightOneSoldiersVolaPecrent.ValueDecimal <= 0)
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

            // 2 we average this movement.We need average volatility percentage

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 we calculate the size of the candles taking this volatility into account

            decimal allSoldiersHeight = volaPercentSma * (_heightSoldiersVolaPecrent.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (_minHeightOneSoldiersVolaPecrent.ValueDecimal / 100);

            _heightSoldiers.ValueDecimal = allSoldiersHeight;
            _minHeightOneSoldier.ValueDecimal = oneSoldiersHeight;
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

            if(candles.Count > 20 &&
                candles[candles.Count-1].TimeStart.Date != candles[candles.Count-2].TimeStart.Date) 
            {
                AdaptSoldiersHeight(candles);
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

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < _heightSoldiers.ValueDecimal)
            {
                return;
            }

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close)
                / (candles[candles.Count - 3].Close / 100) < _minHeightOneSoldier.ValueDecimal)
            {
                return;
            }

            if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close)
                / (candles[candles.Count - 2].Close / 100) < _minHeightOneSoldier.ValueDecimal)
            {
                return;
            }

            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < _minHeightOneSoldier.ValueDecimal)
            {
                return;
            }

            //  long
            if (_regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _lastPrice * (_slippage.ValueDecimal / 100));
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
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

                if (openPositions[i].StopOrderPrice != 0)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 4].Open - _tab.CandlesAll[_tab.CandlesAll.Count - 2].Close);
                    decimal priceStop = _lastPrice - (heightPattern * _procHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * _procHeightTake.ValueDecimal) / 100;

                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop - priceStop * (_slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake - priceStop * (_slippage.ValueDecimal / 100));
                }
                else
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 2].Close - _tab.CandlesAll[_tab.CandlesAll.Count - 4].Open);
                    decimal priceStop = _lastPrice + (heightPattern * _procHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * _procHeightTake.ValueDecimal) / 100;

                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop + priceStop * (_slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake + priceStop * (_slippage.ValueDecimal / 100));
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