using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.HomeWork
{
    [Bot("HomeWork1")]
    public class HomeWork1 : BotPanel
    {        
        private BotTabSimple _tab;

        public HomeWork1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);

            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;           
        }

        public override string GetNameStrategyType()
        {
            return "HomeWork1";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count < 5)
            {
                return;
            }

            Candle lastCandle = candles[candles.Count - 1];
            Candle candleMinusOne = candles[candles.Count - 2];
            Candle candleMinusTwo = candles[candles.Count - 3];
            Candle candleMinusThree = candles[candles.Count - 4];
            Candle candleMinusFour = candles[candles.Count - 5];

            List<Position> position = _tab.PositionsOpenAll;

            if (position.Count > 0)
            {
                if (position[0].TimeOpen.AddMinutes(30) <= lastCandle.TimeStart + (lastCandle.TimeStart - candleMinusOne.TimeStart))
                {
                    _tab.CloseAtMarket(position[0], 1);
                }
                return;
            }

            if (lastCandle.IsUp &&
                candleMinusOne.IsUp &&
                candleMinusTwo.IsUp &&
                candleMinusThree.IsUp &&
                candleMinusFour.IsUp)
            {
                _tab.BuyAtMarket(1);
            }
            else if (lastCandle.IsUp &&
                candleMinusOne.IsUp &&
                candleMinusTwo.IsDown &&
                candleMinusThree.IsDown)
            {
                _tab.BuyAtMarket(1);
            }
        }
    }
}

