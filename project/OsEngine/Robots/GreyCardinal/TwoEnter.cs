using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("TwoEnter")]
    internal class TwoEnter : BotPanel
    {
        private BotTabSimple _tab;
        public TwoEnter(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += tab_PositionOpeningSuccesEvent;
        }

        
        public override string GetNameStrategyType()
        {
            return "TwoEnter";//throw new NotImplementedException();
        }

        public override void ShowIndividualSettingsDialog()
        {
            //throw new NotImplementedException();
        }
        private void tab_CandleFinishedEvent(List<Candle> candels)
        {
            if(candels.Count<5) { return; }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count >= 2) { return; }
            Candle lastCandle = candels[candels.Count - 1];
            Candle candleMinuesOne = candels[candels.Count - 2];
            Candle candleMinuesTwo = candels[candels.Count - 3];
            Candle candleMinuesThree = candels[candels.Count - 4];
            Candle candleMinuesFour = candels[candels.Count - 5];
            if ((lastCandle.IsUp && candleMinuesOne.IsUp && candleMinuesTwo.IsUp && candleMinuesThree.IsUp &&candleMinuesFour.IsUp)
            ||(lastCandle.IsUp && candleMinuesOne.IsUp && candleMinuesTwo.IsDown && candleMinuesThree.IsDown))
            {
                _tab.BuyAtLimit(1, lastCandle.Close);

            }
            if ((lastCandle.IsDown && candleMinuesOne.IsDown && candleMinuesTwo.IsDown && candleMinuesThree.IsDown && candleMinuesFour.IsDown)
            || (lastCandle.IsDown && candleMinuesOne.IsDown && candleMinuesTwo.IsUp && candleMinuesThree.IsUp))
            {
                _tab.SellAtLimit(1, lastCandle.Close);

            }
        }
        private void tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtStop(position, position.EntryPrice - 150 * _tab.Securiti.PriceStep, position.EntryPrice - 150 * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tab.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tab.Securiti.PriceStep, position.EntryPrice + 100 * _tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtStop(position, position.EntryPrice - 150 * _tab.Securiti.PriceStep, position.EntryPrice - 150 * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tab.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tab.Securiti.PriceStep, position.EntryPrice + 100 * _tab.Securiti.PriceStep);
            }
        }

    }
}
