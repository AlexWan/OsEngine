/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Threading;

namespace OsEngine.Robots.CurrencyArbitrage
{
    [Bot("CurrencyMoveExplorer")]
    public class CurrencyMoveExplorer : BotPanel
    {
        public CurrencyMoveExplorer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Polygon);
            _tabPolygon = this.TabsPolygon[0];
            _tabPolygon.ProfitGreaterThanSignalValueEvent += _tabPolygon_ProfitGreaterThanSignalValueEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            Thread worker = new Thread(ThreadWorkerArea);
            worker.Start();

            Description = "Robot for research. Saves slices of the situation on the bundle of instruments within 3 seconds after receiving a signal that there is profit on the sequence.";
        }

        public override string GetNameStrategyType()
        {
            return "CurrencyMoveExplorer";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabPolygon _tabPolygon;

        public StrategyParameterString Regime;

        private void _tabPolygon_ProfitGreaterThanSignalValueEvent(decimal profit, PolygonToTrade sequence)
        {
            if (Regime.ValueString == "Off")
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