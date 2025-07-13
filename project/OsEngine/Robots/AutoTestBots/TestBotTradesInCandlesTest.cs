/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
TestBot for OsEngine.

Do not enable - a robot for testing the synchronism of the array of trades in the candle and the candles themselves.
*/

namespace OsEngine.Robots.AutoTestBots
{
    [Bot("TestBotTradesInCandlesTest")] //We create an attribute so that we don't write anything in the Boot factory
    public class TestBotTradesInCandlesTest : BotPanel
    {
        public TestBotTradesInCandlesTest(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;
            _screenerTab.CreateCandleIndicator(1, "Sma", new List<string>() { "100" }, "Prime");

            Description = OsLocalization.Description.DescriptionLabel4;
        }

        BotTabScreener _screenerTab;

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TestBotTradesInCandlesTest";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // sometimes the array of trades inside the candle is not built correctly.
            // and the last trade in the candle - its price does not correspond to the closing price.

            // we take the last candle
            Candle candle = candles[candles.Count - 1];

            // we take trades from it
            List<Trade> trades = candle.Trades;

            if(trades == null ||
                trades.Count == 0)
            { // enable saving trades in a candle
                tab.Connector.SaveTradesInCandles = true;
                return;
            }

            // we calculate OHLCV candles by trades inside
            decimal open = trades[0].Price;
            decimal high = 0;
            decimal low = decimal.MaxValue;
            decimal close = trades[trades.Count - 1].Price;
            decimal volume = 0;

            for(int i = 0;i < trades.Count;i++)
            {
                if(trades[i].Price > high)
                {
                    high = trades[i].Price;
                }
                if(trades[i].Price < low)
                {
                    low = trades[i].Price;
                }

                volume += trades[i].Volume;
            }

            if (candle.Open != open)
            {
                tab.SetNewLogMessage("Open not equal. Error in storing trades inside the candle." + tab.Security.Name, Logging.LogMessageType.Error);
            }

            if (candle.High != high)
            {
                tab.SetNewLogMessage("High not equal. Error in storing trades inside the candle." + tab.Security.Name, Logging.LogMessageType.Error);
            }

            if (candle.Low != low)
            {
                tab.SetNewLogMessage("Low not equal. Error in storing trades inside the candle." + tab.Security.Name, Logging.LogMessageType.Error);
            }

            if (candle.Close != close)
            {
                tab.SetNewLogMessage("Close not equal. Error in storing trades inside the candle." + tab.Security.Name, Logging.LogMessageType.Error);
            }

            if (candle.Volume != volume)
            {
                tab.SetNewLogMessage("Volume not equal. Error in storing trades inside the candle." + tab.Security.Name, Logging.LogMessageType.Error);
            }
        }
    }
}