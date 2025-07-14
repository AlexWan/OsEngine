/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Threading;

/* Description
Arbitrage robot for OsEngine.

Robot for research. Saves slices of the situation on the bundle of instruments 
within 3 seconds after receiving a signal that there is profit on the sequence.
*/

namespace OsEngine.Robots.CurrencyArbitrage
{
    [Bot("CurrencyMoveExplorer")] // We create an attribute so that we don't write anything to the BotFactory
    public class CurrencyMoveExplorer : BotPanel
    {
        public CurrencyMoveExplorer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Polygon);
            _tabPolygon = this.TabsPolygon[0];
            _tabPolygon.ProfitGreaterThanSignalValueEvent += _tabPolygon_ProfitGreaterThanSignalValueEvent;

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // Create worker Area
            Thread worker = new Thread(ThreadWorkerArea);
            worker.Start();

            Description = OsLocalization.Description.DescriptionLabel27;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CurrencyMoveExplorer";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Tabs
        private BotTabPolygon _tabPolygon;

        // Basic setting
        private StrategyParameterString _regime;

        // Trade logic
        private void _tabPolygon_ProfitGreaterThanSignalValueEvent(decimal profit, PolygonToTrade sequence)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if(_polygonToWatch != null)
            {
                return;
            }

            _lastTimePolygonStartWatch = DateTime.Now;

            _polygonToWatch = sequence;

            DateTime time = DateTime.Now;

            string message = "Start watch " + _polygonToWatch.SecuritiesInSequence + "\n";
            message += "Start profit  " + profit.ToString();

            SendNewLogMessage(message, Logging.LogMessageType.System);
        }

        PolygonToTrade _polygonToWatch;

        DateTime _lastTimePolygonStartWatch;

        // Worker area
        private void ThreadWorkerArea()
        {
            while(true)
            {
                Thread.Sleep(200);

                if(_polygonToWatch == null)
                {
                    continue;
                }

                if(_lastTimePolygonStartWatch.AddSeconds(3) < DateTime.Now)
                {
                    SendNewLogMessage("End watch " + _polygonToWatch.SecuritiesInSequence, Logging.LogMessageType.System);
                    _polygonToWatch = null;
                    continue;
                }

                DateTime time = DateTime.Now;

                string message = time.ToLongTimeString() + "." + time.Millisecond;
                message += "Profit: " + _polygonToWatch.ProfitToDealPercent.ToString();

                SendNewLogMessage(message, Logging.LogMessageType.System);

            }
        }
    }
}