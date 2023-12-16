using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/*

namespace OsEngine.Robots.GreyCardinal
{
    internal class MarketDephtBot
    {
    }
}
*/

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("MarketDephtBot")]
    internal class MarketDephtBot : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterBool isOnParam;
        private StrategyParameterDecimal VolumeParam;
        private StrategyParameterInt CountCandles;

        public MarketDephtBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if(!_tab.IsConnected)
            {
                return;
            }
            if (marketDepth.AskSummVolume * 5 < marketDepth.BidSummVolume)
            {
                _tab.BuyAtLimit(1, _tab.PriceBestAsk);
            }
        }

        public override string GetNameStrategyType()
        {
            return "MarketDephtBot";
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


