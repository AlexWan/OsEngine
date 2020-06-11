using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace OsEngine.Robots.Soldier
{
    public class SoldierBot : BotPanel
    {
        private DateTime _timeToClose;

        private decimal _stopPrice;
        public SoldierBot(string name, StartProgram startProgram) : base(
            name,
            startProgram)
        {
            TabCreate(BotTabType.Simple);

            TabsSimple[0].CandleFinishedEvent += SoldierBot_CandleFinishedEvent;
            TabsSimple[0].PositionOpeningSuccesEvent += SoldierBot_PositionOpeningSuccessEvent;
        }

        public override string GetNameStrategyType()
        {
            return "SoldierBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private void SoldierBot_CandleFinishedEvent(List<Candle> candles)
        {
            if (TabsSimple[0].PositionsOpenAll != null &&
                TabsSimple[0].PositionsOpenAll.Any())
            {
                for (int i = candles.Count - 1; i > candles.Count - 4; i--)
                {
                    if (candles[i].Close > candles[i].Open)
                    {
                        return;
                    }
                }
                if (candles.Last().TimeStart >= _timeToClose)
                {
                    TabsSimple[0].CloseAllAtMarket();
                    return;
                }
                var position = TabsSimple[0].PositionsOpenAll.Last();
                TabsSimple[0].CloseAtMarket(position, position.OpenVolume);
                return;
            }

            if (candles.Count < 21)
            {
                return;
            }

            var lastIndex = candles.Count - 1;
            for(int i = lastIndex; i > candles.Count - 4; i--)
            {
                if(candles[i].Close <= candles[i].Open)
                {
                    return;
                }
            }

            var lastClose = candles[lastIndex].Close;
            for(int i = lastIndex; i> candles.Count - 20; i--)
            {
                if(lastClose < candles[i].Close)
                {
                    return;
                }
            }

            TabsSimple[0].BuyAtMarket(1);//открываем позицию

            //_timeToClose = candles[lastIndex].TimeStart.AddMinutes(5);
            //_stopPrice = candles[lastIndex].Low - TabsSimple[0].Securiti.PriceStep;
        }

        private void SoldierBot_PositionOpeningSuccessEvent(Position position)
        {
            //TabsSimple[0].CloseAtStop(position, _stopPrice, _stopPrice);
        }
    }
}
