/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

Screener on the Price Channel indicator

Buy: the price is above the upper Price Channel.

Sell: the price is below the lower Price Channel.

Exit from purchase: set a stop at the lower border of the Price Channel.

Exit from sale: set trailing stop.
 */

namespace OsEngine.Robots
{
    [Bot("PriceChannelScreener")] // We create an attribute so that we don't write anything to the BotFactory
    internal class PriceChannelScreener : BotPanel
    {
        // Tab
        private BotTabScreener _screenerTab;

        // Basic Settings
        public StrategyParameterString _regime;
        public StrategyParameterInt _upLine;
        public StrategyParameterInt _downLine;
        public StrategyParameterDecimal _stopForShort;
        public StrategyParameterDecimal _slippage;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public PriceChannelScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateParameters();

            // Creating Tab
            TabCreate(BotTabType.Screener);

            _screenerTab = TabsScreener[0];

            // Subscribe to the event of adding a simple tab.
            _screenerTab.NewTabCreateEvent += ScreenerNewTabCreateEvent;

            // Subscribe to the event of the end of the next candle.
            _screenerTab.CandleFinishedEvent += ScreenerTabCandleFinishedEvent;

            // Subscribe to the event of successful opening of a position.
            _screenerTab.PositionOpeningSuccesEvent += ScreenerPositionOpeningSuccesEvent;

            // We create a list of parameters for the indicator.
            List<string> indicatorParams = new List<string>()
            { _upLine.ValueInt.ToString(), _downLine.ValueInt.ToString() };

            // Create an indicator for the screener.
            _screenerTab.CreateCandleIndicator(1, "PriceChannel", indicatorParams, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += PanelParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel232;
        }

        private void CreateParameters()
        {
            // Basic Settings
            _upLine = CreateParameter("Up line", 500, 10, 100, 10);
            _downLine = CreateParameter("Down line", 300, 10, 100, 10);
            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off" });
            _stopForShort = CreateParameter("Stop for short", 0.5m, 0.1m, 1, 0.1m);
            _slippage = CreateParameter("Slippage", 0.1m, 0.1m, 1, 0.1m);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
        }

        private void ScreenerNewTabCreateEvent(BotTabSimple tabSimple)
        {
            tabSimple.Connector.SecuritySubscribeEvent += TabSimpleSecuritySubscribeEvent;
        }

        private void TabSimpleSecuritySubscribeEvent(Security security)
        {

            SendNewLogMessage("Security connected " + security.Name, Logging.LogMessageType.NoName);
        }

        private void ScreenerTabCandleFinishedEvent(List<Candle> candles, BotTabSimple simpleTab)
        {
            // If the robot is turned off, we exit the event.
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // We check whether there are a sufficient number of candles to form the indicator.
            if (candles.Count <= Math.Max(_upLine.ValueInt, _downLine.ValueInt) + 1)
            {
                return;
            }

            // We get access to the indicator.
            Aindicator indicator = (Aindicator)simpleTab.Indicators[0];

            // If there are no open positions and no conditional orders in the tab being processed,
            // the position opening logic is executed.
            if (simpleTab.PositionsOpenAll.Count == 0)
            {
                if (simpleTab.PositionOpenerToStop.Count == 0)
                {
                    EnterLogic(simpleTab, indicator);
                }
            }
            else // If there are open positions, the logic of closing positions is executed.
            {
                ExitLogic(simpleTab, indicator);
            }
        }

        private void EnterLogic(BotTabSimple simpleTab, Aindicator indicator)
        {
            // Conditional buy order activation price,
            decimal activatePrice = indicator.DataSeries[0].Values.Last();

            // Order price including slippage.
            decimal orderPrice = CalcPriceSlippageUp(activatePrice);

            // Conditional order to buy.
            simpleTab.BuyAtStop(GetVolume(simpleTab), activatePrice, orderPrice, StopActivateType.HigherOrEqual);

            // Conditional sell order activation price.
            activatePrice = indicator.DataSeries[1].Values.Last();
            // Order price including slippage.
            orderPrice = CalcPriceSlippageDown(activatePrice);
            // Conditional order to sell.
            simpleTab.SellAtStop(GetVolume(simpleTab), activatePrice, orderPrice, StopActivateType.LowerOrEqyal);
        }

        private void ExitLogic(BotTabSimple simpleTab, Aindicator indicator)
        {
            Position lastPosition = simpleTab.PositionsLast;

            // If we are in a long position
            if (lastPosition.Direction == Side.Buy)
            {
                decimal lastDownValue = indicator.DataSeries[1].Values.Last();
                // Exit by stop order
                simpleTab.CloseAtStop(lastPosition, lastDownValue, CalcPriceSlippageUp(lastDownValue));
            }
            else // If we are in a short position
            {
                var lastCandle = simpleTab.CandlesAll.Last();

                decimal activatePrice = CalcStopForShort(lastCandle.Close);
                // Exit by trailing
                simpleTab.CloseAtTrailingStop(lastPosition, activatePrice, CalcPriceSlippageUp(activatePrice));
            }
        }

        private void ScreenerPositionOpeningSuccesEvent(Position position, BotTabSimple simpleTab)
        {
            var indicator = (Aindicator)simpleTab.Indicators[0];

            // If we are in a long position
            if (position.Direction == Side.Buy)
            {
                decimal activatePrice = indicator.DataSeries[1].Values.Last();
                // Exit by stop order
                simpleTab.CloseAtStop(position, activatePrice, CalcPriceSlippageDown(activatePrice));
            }
            else // If we are in a short position
            {
                decimal activatePrice = CalcStopForShort(position.EntryPrice);
                // Exit by trailing
                simpleTab.CloseAtTrailingStop(position, activatePrice, CalcPriceSlippageUp(activatePrice));
            }
        }

        // The method calculates and returns the buy order price taking into account slippage.
        private decimal CalcPriceSlippageUp(decimal price)
        {
            decimal orderPrice = price + price / 100 * _slippage.ValueDecimal;

            return orderPrice;
        }

        // The method calculates and returns the sell order price taking into account slippage.
        private decimal CalcPriceSlippageDown(decimal price)
        {
            decimal orderPrice = price - price / 100 * _slippage.ValueDecimal;

            return orderPrice;
        }

        // The method calculates and returns the stop loss order activation price for a short position.
        private decimal CalcStopForShort(decimal price)
        {
            decimal stopPrice = price + price / 100 * _stopForShort.ValueDecimal;

            return stopPrice;
        }

        // ParametrsChangeByUser event handler. 
        private void PanelParametrsChangeByUser()
        {
            foreach (BotTabSimple tab in _screenerTab.Tabs)
            {
                var indicator = (Aindicator)tab.Indicators[0];

                indicator.ParametersDigit[0].Value = _upLine.ValueInt;
                indicator.ParametersDigit[1].Value = _downLine.ValueInt;

                indicator.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

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
