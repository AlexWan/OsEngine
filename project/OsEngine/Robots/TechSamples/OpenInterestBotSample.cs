/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Collections.Generic;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System;
using OsEngine.Language;

/* Description
An example of a robot requesting open interest in its logic.

Enter Long when OI falls to the specified value.

Exit by stop and profit orders.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("OpenInterestBotSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class OpenInterestBotSample : BotPanel
    {
        // Simple tab
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _oiDownsizeToEntry;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Exit settings
        private StrategyParameterDecimal _profitPercent;
        private StrategyParameterDecimal _stopPercent;

        public OpenInterestBotSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _oiDownsizeToEntry = CreateParameter("OI downsizing for entry", 50, 10m, 300, 10);
            
            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume on one line", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            // Exit settings
            _profitPercent = CreateParameter("Profit percent", 0.1m, 10m, 300, 10);
            _stopPercent = CreateParameter("Stop percent", 0.1m, 10m, 300, 10);

            Description = OsLocalization.Description.DescriptionLabel107;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "OpenInterestBotSample";
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

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            if(positions.Count == 0)
            {
                LogicEntry(candles);
            }
            else
            {
                LogicClosePosition(candles, positions[0]);
            }
        }

        // Opening position logic
        private void LogicEntry(List<Candle> candles)
        {
            Candle currentCandle = candles[^1];
            Candle prevCandle = candles[^2];

            if(currentCandle.OpenInterest == 0 
                || prevCandle.OpenInterest == 0)
            {
                return;
            }

            decimal currentOi = currentCandle.OpenInterest;
            decimal prevOi = prevCandle.OpenInterest;

            if(currentOi >= prevOi)
            { // if OI grows, do nothing
                return;
            }

            // we calculate by how many contracts OI has decreased
            decimal oiDownSize = prevOi - currentOi;

            if(oiDownSize > _oiDownsizeToEntry.ValueDecimal)
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if(position.StopOrderRedLine != 0)
            {
                return;
            }

            decimal profit = position.EntryPrice + position.EntryPrice * (_profitPercent.ValueDecimal / 100);
            decimal stop = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);

            _tab.CloseAtStopMarket(position, stop);
            _tab.CloseAtProfitMarket(position, profit);
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