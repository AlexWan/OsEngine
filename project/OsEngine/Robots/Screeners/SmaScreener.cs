/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Sma Screener.

Buy: If there is no position. Open long if the last N candles we were above the moving average

Exit: by trailing stop.
*/

namespace OsEngine.Robots.Screeners
{
    [Bot("SmaScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class SmaScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _candlesLookBack;
        private StrategyParameterInt _maxPoses;
        private StrategyParameterInt _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator setting
        private StrategyParameterInt _smaLength;

        // Exit setting
        private StrategyParameterDecimal _trailStop;

        public SmaScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            // Subscribe to the candle finished event
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;
            
            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _maxPoses = CreateParameter("Max poses", 1, 1, 20, 1);
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            _candlesLookBack = CreateParameter("Candles Look Back count", 10, 5, 100, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator setting
            _smaLength = CreateParameter("Sma length", 100, 5, 300, 1);
            
            // Exit setting
            _trailStop = CreateParameter("Trail Stop", 0.7m, 0.5m, 5, 0.1m);

            // Create indicator Sma
            _screenerTab.CreateCandleIndicator(1, "Sma", new List<string>() { _smaLength.ValueInt.ToString(), "Close" }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel94;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() { _smaLength.ValueInt.ToString(), "Close"};

            _screenerTab.UpdateIndicatorsParameters();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SmaScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // 1 If there is a position, then we close the trailing stop

            // 2 No position. Open long if we were above the moving average for the last N candles
            
            if(_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            if(candles.Count - 1 - _candlesLookBack.ValueInt - 1 <= 0)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if(positions.Count == 0)
            { // opening logic

                int allPosesInAllTabs = this.PositionsCount;

                if (allPosesInAllTabs >= _maxPoses.ValueInt)
                {
                    return;
                }

                Aindicator sma = (Aindicator)tab.Indicators[0];

                for(int i = candles.Count-1; i >= 0 && i > candles.Count -1 - _candlesLookBack.ValueInt;i--)
                {
                    decimal curSma = sma.DataSeries[0].Values[i];

                    if(curSma == 0)
                    {
                        return;
                    }

                    if (candles[i].Close < curSma)
                    {
                        return;
                    }
                }

                if(candles[candles.Count - 1 - _candlesLookBack.ValueInt - 1].Close > sma.DataSeries[0].Values[candles.Count - 1 - _candlesLookBack.ValueInt - 1])
                {
                    return;
                }

                tab.BuyAtLimit(GetVolume(tab), tab.PriceBestAsk + tab.Security.PriceStep * _slippage.ValueInt);
            }
            else // Logic close position
            {
                Position pos = positions[0];

                if(pos.State != PositionStateType.Open)
                {
                    return;
                }

                decimal close = candles[candles.Count - 1].Low;
                decimal priceActivation = close - close * _trailStop.ValueDecimal/100;
                decimal priceOrder = priceActivation - tab.Security.PriceStep * _slippage.ValueInt;

                tab.CloseAtTrailingStop(pos, priceActivation, priceOrder);
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