/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

An example of a robot for programmers, where you can see how logging works.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("CandlesLoggingSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class CandlesLoggingSample : BotPanel
    {
        // Simple tab
        BotTabSimple _tab;

        public CandlesLoggingSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create simple tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            this.Description = OsLocalization.Description.DescriptionLabel99;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CandlesLoggingSample";
        }

        // Show settings GUI
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