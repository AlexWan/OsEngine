using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Indicators;


/*

namespace OsEngine.Robots.GreyCardinal
{
    internal class TrendBot
    {
    }
}
*/

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("TrendBot")]
    internal class TrendBot : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterBool isOnParam;
        private StrategyParameterDecimal VolumeParam;
        private StrategyParameterInt CountCandles;
        private StrategyParameterInt CountCandlSlippagees;

        public TrendBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        }

        public override string GetNameStrategyType()
        {
            return "TrendBot";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tab_CandleFinishedEvent(List<Candle> candels)
        {

            if (candels.Count < CountCandles.ValueInt) { return; }

            List<Position> positions = _tab.PositionsOpenAll;

        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
            }
            else if (position.Direction == Side.Sell)
            {
            }
        }

    }
}


