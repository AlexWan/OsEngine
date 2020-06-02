using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.HammerBot
{
    public class RobotHammer : BotPanel
    {
        public DateTime _timeToClose;

        public decimal _stopPrice;

        public RobotHammer(string name, StartProgram startProgram) : base(
            name,
            startProgram)
        {
            TabCreate(BotTabType.Simple); //создание вкладки;

            TabsSimple[0].CandleFinishedEvent += RobotHammer_CandleFinishedEvent;
            TabsSimple[0].PositionOpeningSuccesEvent += RobotHammer_CandleOpeningSuccessEvent;

        }
        public override string GetNameStrategyType()
        {
            return "HammerBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            //пока не нужно.
        }

        private void RobotHammer_CandleFinishedEvent(List<Candle> candles)
        {
            if (TabsSimple[0].PositionsOpenAll != null &&
                TabsSimple[0].PositionsOpenAll.Any())
            {
                if (candles.Last().TimeStart >= _timeToClose)
                {
                    TabsSimple[0].CloseAllAtMarket();
                }
                return;
            }

            if (candles.Count < 21)
            {//если свечей меньше 21, то не входим.
                return;
            }

            if (candles.Last().Close <= candles.Last().Open)
            {
                //если последняя свеча не растущая
                return;
            }
            //проверяем чтобы последний лой был сваммой нижней точкой за последние 20 свечек

            decimal lastLow = candles.Last().Low;

            for (int i = candles.Count - 1; i > candles.Count - 20; i--)
            {
                if (lastLow > candles[i].Low)
                {
                    return;
                }
            }

            //проверяем чтобы тело было в три раза меньше хвоста снизу и не было больше хвоста сверху

            Candle candle = candles.Last();
            decimal body = candle.Close - candle.Open;
            decimal shadowLow = candle.Open - candle.Low;
            decimal shadowHigh = candle.High - candle.Close;

            if (body < shadowHigh)
            {
                return;
            }

            if (shadowLow / 3 < body)
            {
                return;
            }

            TabsSimple[0].BuyAtMarket(1);//открываем позицию

            _timeToClose = candle.TimeStart.AddMinutes(5);
            _stopPrice = candle.Low - TabsSimple[0].Securiti.PriceStep;
        }


        private void RobotHammer_CandleOpeningSuccessEvent(Position position)
        {
            TabsSimple[0].CloseAtStop(position,_stopPrice, _stopPrice);
        }
    }
}
