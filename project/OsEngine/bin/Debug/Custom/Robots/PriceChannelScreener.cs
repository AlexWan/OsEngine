using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;

/* Description
trading robot for osengine

Screener on the Price Channel indicator

Buy: the price is above the upper Price Channel.

Sell: the price is below the lower Price Channel.

Exit from purchase: set a stop at the lower border of the Price Channel.

Exit from sale: set trailing stop.

 */

namespace OsEngine.Robots.Screeners
{
    [Bot("PriceChannelScreener")] // We create an attribute so that we don't write anything to the BotFactory
    internal class PriceChannelScreener : BotPanel
    {
        // Tab
        private BotTabScreener _screenerTab;

        // Settings
        public StrategyParameterString _regime;
        public StrategyParameterInt _upLine;
        public StrategyParameterInt _downLine;
        public StrategyParameterDecimal _stopForShort;
        public StrategyParameterDecimal _volume;
        public StrategyParameterDecimal _slippage;

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

            Description = "Screener on the Price Channel indicator. " +
                "Buy: the price is above the upper Price Channel. " +
                "Sell: the price is below the lower Price Channel. " +
                "Exit from purchase: set a stop at the lower border of the Price Channel" +
                "Exit from sale: set trailing stop.";
        }

        private void CreateParameters()
        {
            _upLine = CreateParameter("Up line", 500, 10, 100, 10);

            _downLine = CreateParameter("Down line", 300, 10, 100, 10);

            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off" });

            _stopForShort = CreateParameter("Stop for short", 0.5m, 0.1m, 1, 0.1m);

            _volume = CreateParameter("Volume", 10m, 10, 100, 10);

            _slippage = CreateParameter("Slippage", 0.1m, 0.1m, 1, 0.1m);
        }

        private void ScreenerNewTabCreateEvent(BotTabSimple tabSimple)
        {
            tabSimple.Connector.SecuritySubscribeEvent += TabSimpleSecuritySubscribeEvent;
        }

        private void TabSimpleSecuritySubscribeEvent(Security security)
        {
            SendNewLogMessage($"Security {security.Name} connected", Logging.LogMessageType.NoName);
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
            simpleTab.BuyAtStop(_volume.ValueDecimal, activatePrice, orderPrice, StopActivateType.HigherOrEqual);

            // Conditional sell order activation price.
            activatePrice = indicator.DataSeries[1].Values.Last();
            // Order price including slippage.
            orderPrice = CalcPriceSlippageDown(activatePrice);
            // Conditional order to sell.
            simpleTab.SellAtStop(_volume.ValueDecimal, activatePrice, orderPrice, StopActivateType.LowerOrEqyal);
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

       
        //private void PanelParametrsChangeByUser2()
        //{
        //    _screenerTab.Tabs.ForEach(tab =>
        //    {
        //        var indicator = (Aindicator)tab.Indicators[0];

        //        indicator.ParametersDigit[0].Value = _upLine.ValueInt;
        //        indicator.ParametersDigit[1].Value = _downLine.ValueInt;

        //        indicator.Reload();
        //    });
        //}

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
