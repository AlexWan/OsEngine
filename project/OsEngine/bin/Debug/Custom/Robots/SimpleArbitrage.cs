/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Language;

/* Description
trading robot for osengine

Arbitration robot SimpleArbitrage

Buy security 1, Sell security 2: the spread chart closed above the upper limit of the indicator deviation.

Sell security 1 Buy security 2: the spread chart closed below the lower limit of the indicator deviation.

Exit: 
If we opened positions when the spread crossed the upper deviation limit,
then we expect the spread to become below the center line of the indicator.

If we opened positions when the spread crossed the lower deviation limit, 
then we expect the spread to become above the center line of the indicator.
 */

namespace OsEngine.Robots
{
    [Bot("SimpleArbitrage")] // We create an attribute so that we don't write anything to the BotFactory
    internal class SimpleArbitrage : BotPanel
    {
        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _deviation;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterString _tradeAssetInPortfolio;
        private StrategyParameterDecimal _volumeFirstLeg;
        private StrategyParameterDecimal _volumeSecondLeg;

        // Tabs
        private BotTabIndex _index;
        private BotTabSimple _firstLeg;
        private BotTabSimple _secondLeg;

        // Indicator
        private Aindicator _dayMiddle;

        public SimpleArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateParameters();

            // Creating Tabs
            TabCreate(BotTabType.Index);
            _index = TabsIndex.Last();

            TabCreate(BotTabType.Simple);
            _firstLeg = TabsSimple.Last();

            TabCreate(BotTabType.Simple);
            _secondLeg = TabsSimple.Last();

            // Indicator
            _dayMiddle = IndicatorsFactory.CreateIndicatorByName("LastDayMiddle", name + "LastDayMiddle", false);
            _dayMiddle.ParametersDigit[0].Value = _deviation.ValueDecimal;
            _dayMiddle = (Aindicator)_index.CreateCandleIndicator(_dayMiddle, "Prime");
            _dayMiddle.Save();

            // Subscribe to the spread change event
            _index.SpreadChangeEvent += IndexSpreadChangeEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SimpleArbitrageParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel234;
        }

        // Parameters
        private void CreateParameters()
        {
            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _deviation = CreateParameter("Deviation", 1m, 1, 10, 0.5m);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            _volumeFirstLeg = CreateParameter("First volume", 1m, 1, 50, 1);
            _volumeSecondLeg = CreateParameter("Second volume", 1m, 1, 50, 1);
        }

        private void SimpleArbitrageParametrsChangeByUser()
        {
            _dayMiddle.ParametersDigit[0].Value = _deviation.ValueDecimal;
            _dayMiddle.Save();
            _dayMiddle.Reload();
        }

        // Spread change event
        private void IndexSpreadChangeEvent(List<Candle> candles)
        {
            // If the robot is turned off, we exit the event.
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If the last value of one of the indicator series is zero,
            // then the indicator is not ready yet, exit the event
            if (_dayMiddle.DataSeries[0].Last == 0)
            {
                return;
            }

            Candle lastCandle = candles.Last();

            // Calling the method with trading logic
            TradeLogic(lastCandle);
        }

        // The method with trading logic
        private void TradeLogic(Candle lastCandle)
        {
            // If there are no positions for both instruments yet, go to the body of the conditional structure,
            // in which the logic for opening positions is executed
            if (_firstLeg.PositionsOpenAll.Count == 0 && _secondLeg.PositionsOpenAll.Count == 0)
            {
                //If the spread chart closes above the upper limit of the indicator deviation,
                //then we sell the first security and buy the second
                if (lastCandle.Close > _dayMiddle.DataSeries[1].Last)
                {
                    _firstLeg.SellAtMarket(GetVolume(_firstLeg, _volumeFirstLeg.ValueDecimal), "CrossUp");
                    _secondLeg.BuyAtMarket(GetVolume(_secondLeg, _volumeSecondLeg.ValueDecimal), "CrossUp");
                }
                //If the spread chart closes below the lower limit of the indicator deviation,
                //then we buy the first security and sell the second
                if (lastCandle.Close < _dayMiddle.DataSeries[2].Last)
                {
                    _firstLeg.BuyAtMarket(GetVolume(_firstLeg, _volumeFirstLeg.ValueDecimal), "CrossDown");
                    _secondLeg.SellAtMarket(GetVolume(_secondLeg, _volumeSecondLeg.ValueDecimal), "CrossDown");
                }
            }
            else // If there are open positions in simple tabs, we move on to the logic of closing positions
            {
                // If we opened positions when the spread crossed the upper deviation limit,
                // then we expect the spread to become below the center line of the indicator.
                // If these conditions are met, we close all positions.
                if (_firstLeg.PositionsLast.SignalTypeOpen == "CrossUp")
                {
                    if (lastCandle.Close < _dayMiddle.DataSeries[0].Last)
                    {
                        _firstLeg.CloseAllAtMarket();
                        _secondLeg.CloseAllAtMarket();
                    }
                }
                // If positions were opened after the spread broke through the lower deviation line of the indicator,
                // we expect the spread to return to the center from the lower border and,
                // if these conditions are met, we close all positions.
                if (_firstLeg.PositionsLast.SignalTypeOpen == "CrossDown")
                {
                    if (lastCandle.Close > _dayMiddle.DataSeries[0].Last)
                    {
                        _firstLeg.CloseAllAtMarket();
                        _secondLeg.CloseAllAtMarket();
                    }
                }
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SimpleArbitrage";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private decimal GetVolume(BotTabSimple tab, decimal _volume)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume / (contractPrice * tab.Security.Lot);
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

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume / 100);

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