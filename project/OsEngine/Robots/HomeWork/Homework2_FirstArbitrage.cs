using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.HomeWork
{
    [Bot("Homework2_FirstArbitrage")]
    public class Homework2_FirstArbitrage : BotPanel
    {
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;

        public Homework2_FirstArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab2 = TabsSimple[1];

            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;
        }

        public override string GetNameStrategyType()
        {
            return "Homework2_FirstArbitrage";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count == 0)
            {
                return;
            }

            List<Position> position = _tab1.PositionsOpenAll;

            if (position.Count != 0)
            {
                if (position[0].TimeOpen.AddMinutes(30) <= candles[candles.Count-1].TimeStart)
                {
                    _tab1.CloseAtMarket(position[0], position[0].OpenVolume);
                }
                return;
            }

            if (candles[candles.Count - 1].TimeStart == _tab2.CandlesFinishedOnly[_tab2.CandlesFinishedOnly.Count - 1].TimeStart)
            {
                TradeLogic();
            }
        }

        private void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count == 0)
            {
                return;
            }

            List<Position> position = _tab2.PositionsOpenAll;

            if (position.Count != 0)
            {
                if (position[0].TimeOpen.AddMinutes(30) <= candles[candles.Count - 1].TimeStart)
                {
                    _tab2.CloseAtMarket(position[0], position[0].OpenVolume);
                }
                return;
            }

            if (candles[candles.Count - 1].TimeStart == _tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].TimeStart)
            {
                TradeLogic();
            }
        }

        private void TradeLogic()
        {
            if (_tab1.PositionsOpenAll.Count > 0 || _tab2.PositionsOpenAll.Count > 0)
            {
                return;
            }

            List<Candle> candlesOne = _tab1.CandlesFinishedOnly;
            List<Candle> candlesTwo = _tab2.CandlesFinishedOnly;

            bool candleUpTabOne = candlesOne[candlesOne.Count - 1].IsUp;
            decimal bodyTabOne = candlesOne[candlesOne.Count - 1].Body;
            decimal shadowUpTabOne = candlesOne[candlesOne.Count - 1].ShadowTop;
            decimal shadowDownTabOne = candlesOne[candlesOne.Count - 1].ShadowBottom;

            bool candleUpTabTwo = candlesTwo[candlesTwo.Count - 1].IsUp;
            decimal bodyTabTwo = candlesTwo[candlesTwo.Count - 1].Body;
            decimal shadowUpTabTwo = candlesTwo[candlesTwo.Count - 1].ShadowTop;
            decimal shadowDownTabTwo = candlesTwo[candlesTwo.Count - 1].ShadowBottom;


            if (candleUpTabOne && bodyTabOne < shadowDownTabOne / 2 && bodyTabOne > shadowUpTabOne &&
                !candleUpTabTwo && bodyTabTwo < shadowUpTabTwo / 2 && bodyTabTwo > shadowDownTabTwo)
            {
                _tab1.BuyAtLimit(1, candlesOne[candlesOne.Count - 1].Close);
                _tab2.SellAtLimit(1, candlesTwo[candlesTwo.Count - 1].Close);
            }

            if (!candleUpTabOne && bodyTabOne < shadowUpTabOne / 2 && bodyTabOne > shadowDownTabOne &&
                candleUpTabTwo && bodyTabTwo < shadowDownTabTwo / 2 && bodyTabTwo > shadowUpTabTwo)
            {
                _tab2.BuyAtLimit(1, candlesTwo[candlesTwo.Count - 1].Close);
                _tab1.SellAtLimit(1, candlesOne[candlesOne.Count - 1].Close);
            }
        }

    }
}
