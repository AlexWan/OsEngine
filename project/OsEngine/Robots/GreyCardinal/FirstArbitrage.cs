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
    [Bot("FirstArbitrage")]
    internal class FirstArbitrage : BotPanel
    {
        private BotTabSimple _tabOne,_tabTwo;
        public FirstArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);

            _tabOne = TabsSimple[0];
            _tabOne.CandleFinishedEvent += _tabOne_CandleFinishedEvent;
            _tabOne.PositionOpeningSuccesEvent += _tabOne_PositionOpeningSuccesEvent;
            TabCreate(BotTabType.Simple);
            _tabTwo = TabsSimple[1];
            _tabTwo.CandleFinishedEvent += _tabTwo_CandleFinishedEvent;
            _tabTwo.PositionOpeningSuccesEvent += _tabTwo_PositionOpeningSuccesEvent;
        }


        public override string GetNameStrategyType()
        {
            return "FirstArbitrage";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
        private void _tabOne_CandleFinishedEvent(List<Candle> candels)
        {
            if (candels.Count < 5) { return; }
            if (candels[candels.Count - 1].TimeStart == _tabTwo.CandlesFinishedOnly[_tabTwo.CandlesFinishedOnly.Count - 1].TimeStart)
            {
                tradeLogic();
            }
        }
        private void _tabOne_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tabOne.CloseAtStop(position, position.EntryPrice - 150 * _tabOne.Securiti.PriceStep, position.EntryPrice - 150 * _tabOne.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tabOne.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tabOne.Securiti.PriceStep, position.EntryPrice + 100 * _tabOne.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tabOne.CloseAtStop(position, position.EntryPrice - 150 * _tabOne.Securiti.PriceStep, position.EntryPrice - 150 * _tabOne.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tabOne.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tabOne.Securiti.PriceStep, position.EntryPrice + 100 * _tabOne.Securiti.PriceStep);
            }
        }

        private void _tabTwo_CandleFinishedEvent(List<Candle> candels)
        {
            if (candels.Count < 5) { return; }
            if (candels[candels.Count-1].TimeStart == _tabOne.CandlesFinishedOnly[_tabOne.CandlesFinishedOnly.Count-1].TimeStart)
            {
                tradeLogic();
            }
        }
        private void _tabTwo_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tabTwo.CloseAtStop(position, position.EntryPrice - 150 * _tabTwo.Securiti.PriceStep, position.EntryPrice - 150 * _tabTwo.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tabTwo.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tabTwo.Securiti.PriceStep, position.EntryPrice + 100 * _tabTwo.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tabTwo.CloseAtStop(position, position.EntryPrice - 150 * _tabTwo.Securiti.PriceStep, position.EntryPrice - 150 * _tabTwo.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tabTwo.CloseAtTrailingStop(position, position.EntryPrice + 150 * _tabTwo.Securiti.PriceStep, position.EntryPrice + 100 * _tabTwo.Securiti.PriceStep);
            }
        }
        private void tradeLogic()
        {
            if(_tabOne.PositionsOpenAll.Count>0 || _tabTwo.PositionsOpenAll.Count > 0)
            {
                return;
            }
            List<Candle> candlesOne = _tabOne.CandlesFinishedOnly, candlesTwo = _tabTwo.CandlesFinishedOnly;
            if (candlesOne[candlesOne.Count-1].IsUp && candlesOne[candlesOne.Count - 2].IsUp && candlesOne[candlesOne.Count - 3].IsUp
                && candlesTwo[candlesTwo.Count - 1].IsDown && candlesTwo[candlesTwo.Count - 2].IsDown && candlesTwo[candlesTwo.Count - 3].IsDown)
            {
                _tabOne.SellAtLimit(1, _tabOne.PriceBestBid);
                _tabTwo.BuyAtLimit(1, _tabTwo.PriceBestAsk);
            }
            else if (candlesOne[candlesOne.Count - 1].IsDown && candlesOne[candlesOne.Count - 2].IsDown && candlesOne[candlesOne.Count - 3].IsDown
                && candlesTwo[candlesTwo.Count - 1].IsUp && candlesTwo[candlesTwo.Count - 2].IsUp && candlesTwo[candlesTwo.Count - 3].IsUp)
            {
                _tabOne.BuyAtLimit(1, _tabOne.PriceBestBid);
                _tabTwo.SellAtLimit(1, _tabTwo.PriceBestAsk);
            }

        }
    }
}

