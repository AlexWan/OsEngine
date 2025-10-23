/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.OnScriptIndicators
{
    [Bot("TimeOfDayBot")] // We create an attribute so that we don't write anything to the BotFactory
    public class TimeOfDayBot : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterTimeOfDay _timeToInter;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Exit settings
        private StrategyParameterDecimal _stop;
        private StrategyParameterDecimal _profit;

        public TimeOfDayBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] {"Off", "Buy", "Sell"});
            _slippage = CreateParameter("Slippage", 0, 0, 20m, 0.1m);
            _timeToInter = CreateParameterTimeOfDay("Time to Inter", 10, 0, 1, 0);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit settings
            _stop = CreateParameter("Stop", 1, 1.0m, 10, 0.1m);
            _profit = CreateParameter("Profit", 1, 1.0m, 10, 0.1m);

            // Subscribe to the tab on new tick event
            _tab.NewTickEvent += TabOnNewTickEvent;

            // Subscribe to the position opening succes event
            _tab.PositionOpeningSuccesEvent += TabOnPositionOpeningSuccesEvent;

            Description = OsLocalization.Description.DescriptionLabel68;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TimeOfDayBot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void TabOnNewTickEvent(Trade trade)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                return;
            }

            if (_timeToInter.Value < trade.Time)
            {
                LogicOpenPosition();
            }
        }

        // Open position logic
        private void LogicOpenPosition()
        {
            if (_regime.ValueString == "Buy")
            {
                _tab.BuyAtLimit(GetVolume(_tab),
                    _tab.PriceBestAsk + _tab.PriceBestAsk * (_slippage.ValueDecimal / 100));
            }

            if (_regime.ValueString == "Sell")
            {
                _tab.SellAtLimit(GetVolume(_tab),
                    _tab.PriceBestBid - _tab.PriceBestBid * (_slippage.ValueDecimal / 100));
            }

            _regime.ValueString = "Off";
        }

        // Close position logic
        private void TabOnPositionOpeningSuccesEvent(Position position)
        {
            decimal stopPrice = 0;
            decimal stopActivationPrice = 0;
            decimal profitPrice = 0;
            decimal profitActivationPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopActivationPrice  = position.EntryPrice - position.EntryPrice * (_stop.ValueDecimal / 100);
                stopPrice = stopPrice - stopPrice * (_slippage.ValueDecimal / 100);

                profitActivationPrice = position.EntryPrice + position.EntryPrice * (_profit.ValueDecimal / 100);
                profitPrice = profitPrice - stopPrice * (_slippage.ValueDecimal / 100);
            }
            if (position.Direction == Side.Sell)
            {
                stopActivationPrice  = position.EntryPrice + position.EntryPrice * (_stop.ValueDecimal / 100);
                stopPrice = stopPrice + stopPrice * (_slippage.ValueDecimal / 100);

                profitActivationPrice  = position.EntryPrice - position.EntryPrice * (_profit.ValueDecimal / 100);
                profitPrice = profitPrice + stopPrice * (_slippage.ValueDecimal / 100);
            }

            _tab.CloseAtStop(position, stopActivationPrice, stopPrice);
            _tab.CloseAtProfit(position, profitActivationPrice, profitPrice);
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