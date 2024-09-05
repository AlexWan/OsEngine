using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;


namespace OsEngine.Robots.TechSamples
{
    [Bot("CandlesLoggingSample")]
    public class CandlesLoggingSample : BotPanel
    {
        BotTabSimple _tab;

        public CandlesLoggingSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            this.Description = "An example of a robot for programmers, where you can see how logging works";
        }

        public override string GetNameStrategyType()
        {
            return "CandlesLoggingSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            string message = 
                "Candle finished. Last candle time: " + candles[candles.Count - 1].TimeStart;

            // 1 only in log window
            SendNewLogMessage(message, Logging.LogMessageType.User);

            // 2 for error log. LogMessageType - Error

            SendNewLogMessage(message, Logging.LogMessageType.Error);

        }
    }
}