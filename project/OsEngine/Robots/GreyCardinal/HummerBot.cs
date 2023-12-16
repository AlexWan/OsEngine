using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Data.SqlTypes;


namespace OsEngine.Robots.GreyCardinal
{
    [Bot("HummerBot")]

    internal class HummerBot : BotPanel
    {
        private BotTabSimple _tab;
        public HummerBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandeleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.CloseAtStop(position, position.EntryPrice - 30 * _tab.Securiti.PriceStep,position.EntryPrice - 30 * _tab.Securiti.PriceStep);// StopLost
        }

        private void _tab_CandeleFinishedEvent(List<Candle> candels)
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count > 0) {
                if (positions[0].TimeOpen.AddMinutes(75) < candels[candels.Count-1].TimeStart)
                {
                    _tab.CloseAtMarket(positions[0], positions[0].OpenVolume);
                }
                return; 
            }
            bool candelUp = candels[candels.Count - 1].IsUp;
            decimal body = candels[candels.Count - 1].Body;
            decimal upShadow = candels[candels.Count - 1].ShadowTop;
            decimal downShadow = candels[candels.Count - 1].ShadowBottom;
            if(candelUp && body<downShadow/2 && body > upShadow)
            {
                _tab.BuyAtMarket(1);
            }
        }

        public override string GetNameStrategyType()
        {
            return "HummerBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            //throw new NotImplementedException();
        }
    }
}
