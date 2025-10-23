/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Alligator Trend Average.

Buy:
1. The current price is above all lines of the Alligator indicator.
2. The Alligator lines are arranged in order: fast > middle > slow.
Sell:
1. The current price is below all lines of the Alligator indicator.
2. The Alligator lines are arranged in order: fast < middle < slow.

Standard Exit: 
Triggered by the opposite signal (i.e., when the conditions for entry are no longer valid).
Averaging Positions: 
If the price moves against the position and reaches certain conditions, additional volume is added to the position using a market order.
Pyramid:
If the price moves in the direction of the position and exceeds a certain percentage of the entry price (`_pyramidPercent`), a new part of the position ("pyramiding") is added.
 */

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("AlligatorTrendAverage")] // We create an attribute so that we don't write anything to the BotFactory
    public class AlligatorTrendAverage : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _pyramidPercent;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lengthJaw;
        private StrategyParameterInt _lengthTeeth;
        private StrategyParameterInt _lengthLips;
        private StrategyParameterInt _shiftJaw;
        private StrategyParameterInt _shiftTeeth;
        private StrategyParameterInt _shiftLips;
        
        // Indicator
        private Aindicator _alligator;

        public AlligatorTrendAverage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _pyramidPercent = CreateParameter("Pyramid percent", 0.1m, 1.0m, 50, 4);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _lengthJaw = CreateParameter("Jaw length", 13, 1, 50, 4);
            _lengthTeeth = CreateParameter("Teeth length", 8, 1, 50, 4);
            _lengthLips = CreateParameter("Lips length", 5, 1, 50, 4);
            _shiftJaw = CreateParameter("Jaw offset", 8, 1, 50, 4);
            _shiftTeeth = CreateParameter("Teeth offset", 5, 1, 50, 4);
            _shiftLips = CreateParameter("Lips offset", 3, 1, 50, 4);

            // Create indicator Alligator
            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tab.CreateCandleIndicator(_alligator, "Prime");
            _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;
            _alligator.ParametersDigit[3].Value = _shiftJaw.ValueInt;
            _alligator.ParametersDigit[4].Value = _shiftTeeth.ValueInt;
            _alligator.ParametersDigit[5].Value = _shiftLips.ValueInt;
            _alligator.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel79;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_alligator.ParametersDigit[0].Value != _lengthJaw.ValueInt ||
            _alligator.ParametersDigit[1].Value != _lengthTeeth.ValueInt ||
            _alligator.ParametersDigit[2].Value != _lengthLips.ValueInt ||
            _alligator.ParametersDigit[3].Value != _shiftJaw.ValueInt ||
            _alligator.ParametersDigit[4].Value != _shiftTeeth.ValueInt ||
            _alligator.ParametersDigit[5].Value != _shiftLips.ValueInt)
            {
                _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
                _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
                _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;
                _alligator.ParametersDigit[3].Value = _shiftJaw.ValueInt;
                _alligator.ParametersDigit[4].Value = _shiftTeeth.ValueInt;
                _alligator.ParametersDigit[5].Value = _shiftLips.ValueInt;

                _alligator.Reload();
                _alligator.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "AlligatorTrendAverage";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_alligator.DataSeries[0].Last == 0 ||
                _alligator.DataSeries[1].Last == 0 ||
                _alligator.DataSeries[2].Last == 0 ||
                _alligator.ParametersDigit[0].Value > candles.Count + 10 ||
                _alligator.ParametersDigit[1].Value > candles.Count + 10 ||
                _alligator.ParametersDigit[2].Value > candles.Count + 10)
            {
                return;
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
                LogicClosePosition(candles, openPositions[0]);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal lastSlowAlligator = _alligator.DataSeries[0].Last;

            decimal lastMiddleAlligator = _alligator.DataSeries[1].Last;

            decimal lastFastAlligator = _alligator.DataSeries[2].Last;

            if (lastPrice > lastSlowAlligator
             && lastPrice > lastMiddleAlligator
             && lastPrice > lastFastAlligator
             && lastFastAlligator > lastMiddleAlligator
             && lastMiddleAlligator > lastSlowAlligator
             && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }

            if (lastPrice < lastSlowAlligator
             && lastPrice < lastMiddleAlligator
             && lastPrice < lastFastAlligator
             && lastFastAlligator < lastMiddleAlligator
             && lastMiddleAlligator < lastSlowAlligator
             && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State == PositionStateType.Opening
                || position.SignalTypeClose == "StandardExit")
            {
                return;
            }

            if(position.Comment == null)
            {
                position.Comment = "";
            }

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastSlowAlligator = _alligator.DataSeries[0].Last;
            decimal lastMiddleAlligator = _alligator.DataSeries[1].Last;
            decimal lastFastAlligator = _alligator.DataSeries[2].Last;

            // 1 Standard Exit

            if (position.Direction == Side.Buy
                && lastPrice < lastSlowAlligator
                && lastPrice < lastMiddleAlligator
                && lastPrice < lastFastAlligator)
            {
                _tab.CloseAtMarket(position, position.OpenVolume, "StandardExit");
                return;
            }
            else if (position.Direction == Side.Sell
             && lastPrice > lastSlowAlligator
             && lastPrice > lastMiddleAlligator
             && lastPrice > lastFastAlligator)
            {
                _tab.CloseAtMarket(position, position.OpenVolume, "StandardExit");
                return;
            }

            // 2 Averaging

            if (position.Direction == Side.Buy
                && 
                (lastPrice < lastSlowAlligator
                || lastPrice < lastMiddleAlligator
                || lastPrice < lastFastAlligator))
            {
                if(position.Comment.Contains("Average") == false)
                {
                    position.Comment += "Average";
                    _tab.BuyAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }
            }
            else if (position.Direction == Side.Sell
                &&
                (lastPrice > lastSlowAlligator
                || lastPrice > lastMiddleAlligator
                || lastPrice > lastFastAlligator))
            {
                if (position.Comment.Contains("Average") == false)
                {
                    position.Comment += "Average";
                    _tab.SellAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }
            }

            // 3 Trend follow-up buying

            if (position.Direction == Side.Buy
                &&
                (position.Comment.Contains("Pyramid") == false))
            {
                decimal pyramidPrice = position.EntryPrice + position.EntryPrice * (_pyramidPercent.ValueDecimal/100);

                if(lastPrice > pyramidPrice)
                {
                    position.Comment += "Pyramid";
                    _tab.BuyAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }
            }
            else if (position.Direction == Side.Sell 
                &&
                (position.Comment.Contains("Pyramid") == false))
            {
                decimal pyramidPrice = position.EntryPrice - position.EntryPrice * (_pyramidPercent.ValueDecimal / 100);

                if (lastPrice < pyramidPrice)
                {
                    position.Comment += "Pyramid";
                    _tab.SellAtMarketToPosition(position, GetVolume(_tab));
                    return;
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