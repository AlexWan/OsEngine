/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.AutoTestBots
{
    /// <summary>
    /// Робот созданный для тестирования синхронности массива трейдов в свече и самих свечек
    /// </summary>
    [Bot("TestBotTradesInCandlesTest")]
    public class TestBotTradesInCandlesTest : BotPanel
    {
        public TestBotTradesInCandlesTest(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;
            _screenerTab.CreateCandleIndicator(1, "Sma", new List<string>() { "100" }, "Prime");

            Description = "Do not enable - a robot for testing the synchronism" +
                " of the array of trades in the candle and the candles themselves";
        }

        BotTabScreener _screenerTab;

        public override string GetNameStrategyType()
        {
            return "TestBotTradesInCandlesTest";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

// логика проверки

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // иногда массив трейдов внутри свечи - строится не верно.
            // и последний трейд в свече - его цена, не соответствует цене закрытия. 

            // берём последнюю свечу

            Candle candle = candles[candles.Count - 1];

            // берём из неё трейды

            List<Trade> trades = candle.Trades;

            if(trades == null ||
                trades.Count == 0)
            { // включаем сохранение трейдов в свечку
                tab.Connector.SaveTradesInCandles = true;
                return;
            }

            // рассчитываем OHLCV свечи по трейдам внутри
            decimal open = trades[0].Price;
            decimal high = 0;
            decimal low = decimal.MaxValue;
            decimal close = trades[trades.Count - 1].Price;
            decimal volume = 0;

            for(int i = 0;i < trades.Count;i++)
            {
                if(trades[i].Price > high)
                {
                    high = trades[i].Price;
                }
                if(trades[i].Price < low)
                {
                    low = trades[i].Price;
                }

                volume += trades[i].Volume;
            }

            if (candle.Open != open)
            {
                tab.SetNewLogMessage("Open не равен. Ошибка в хранении трейдов внутри свечи. Бумага" + tab.Securiti.Name, Logging.LogMessageType.Error);
            }

            if (candle.High != high)
            {
                tab.SetNewLogMessage("High не равен. Ошибка в хранении трейдов внутри свечи. Бумага" + tab.Securiti.Name, Logging.LogMessageType.Error);
            }

            if (candle.Low != low)
            {
                tab.SetNewLogMessage("Low не равен. Ошибка в хранении трейдов внутри свечи. Бумага" + tab.Securiti.Name, Logging.LogMessageType.Error);
            }

            if (candle.Close != close)
            {
                tab.SetNewLogMessage("Close не равен. Ошибка в хранении трейдов внутри свечи. Бумага" + tab.Securiti.Name, Logging.LogMessageType.Error);
            }

            if (candle.Volume != volume)
            {
                tab.SetNewLogMessage("Volume не равен. Ошибка в хранении трейдов внутри свечи. Бумага" + tab.Securiti.Name, Logging.LogMessageType.Error);
            }

        }


    }
}
