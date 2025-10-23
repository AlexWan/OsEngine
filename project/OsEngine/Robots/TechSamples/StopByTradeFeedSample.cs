/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

Buy: price is above the upper line of the PriceChannel.  
Sell: price is below the lower line of the PriceChannel.  
Exit: by trailing stop.

An example of a robot that pulls up the stop for a position based on changes in the deals feed. IMPORTANT! 
Tests of this robot should be conducted on the deals feed.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("StopByTradeFeedSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class StopByTradeFeedSample : BotPanel
    {
        // Simple tabs
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator setting
        private StrategyParameterInt _indLength;
        
        // Indicator
        private Aindicator _pc;

        // Exit setting
        private StrategyParameterDecimal _trailStopPercent;

        public StopByTradeFeedSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage in price step", 0, 0, 20, 1);
            _indLength = CreateParameter("Price channel length", 10, 10, 80, 3);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit setting
            _trailStopPercent = CreateParameter("Trail stop percent", 0.2m, 0.5m, 5, 4);

            // Create indicator PriceChannel
            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = _indLength.ValueInt;
            _pc.ParametersDigit[1].Value = _indLength.ValueInt;
            _pc.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the new tick event
            _tab.NewTickEvent += _tab_NewTickEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel108;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_indLength.ValueInt != _pc.ParametersDigit[0].Value ||
                _indLength.ValueInt != _pc.ParametersDigit[1].Value)
            {
                _pc.ParametersDigit[0].Value = _indLength.ValueInt;
                _pc.ParametersDigit[1].Value = _indLength.ValueInt;
                _pc.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StopByTradeFeedSample";
        }

        // Show setting GUI
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

            if (_pc.DataSeries[0].Values == null 
                || _pc.DataSeries[1].Values == null)
            {
                return;
            }

            if (_pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 2 
                || _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 2)
            {
                return;
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {// no positions
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
                decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

                // long
                if (_regime.ValueString != "OnlyShort")
                {
                    if (lastPrice > lastPcUp)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong")
                {
                    if (lastPrice < lastPcDown)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }
            }
        }

        // Close position logic
        private void _tab_NewTickEvent(Trade trade)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null
                || openPositions.Count == 0)
            {
                return;
            }

            Position myPos = openPositions[0];

            if(myPos.State != PositionStateType.Open)
            {
                return;
            }

            decimal stopPrice = 0;
            decimal orderPrice = 0;

            if (myPos.Direction == Side.Buy)
            {
                stopPrice = trade.Price - (trade.Price * (_trailStopPercent.ValueDecimal/100));
                orderPrice = stopPrice - _slippage.ValueInt * _tab.Security.PriceStep;
            }
            else if(myPos.Direction == Side.Sell)
            {
                stopPrice = trade.Price + (trade.Price * (_trailStopPercent.ValueDecimal / 100));
                orderPrice = stopPrice + _slippage.ValueInt * _tab.Security.PriceStep;
            }

            _tab.CloseAtTrailingStop(myPos,stopPrice,orderPrice);
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
                        tab.Security .Lot != 0 &&
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