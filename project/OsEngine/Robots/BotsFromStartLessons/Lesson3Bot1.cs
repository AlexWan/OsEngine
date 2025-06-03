/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot-example from the course of lectures "C# for algotreader".
the robot is called when the candle is closed.
Buy: When the second to last and last candle grew
Sell: Trailing Stop by Low-value second to last candle.
 */

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson3Bot1")]
    public class Lesson3Bot1 : BotPanel
    {
        private BotTabSimple _tabToTrade;

        private StrategyParameterString _mode;

        private StrategyParameterDecimal _volume;

        public Lesson3Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);

            Description = "Robot-example from the course of lectures \"C# for algotreader\"." +
                "the robot is called when the candle is closed." +
                "Buy: When the second to last and last candle grew" +
                "Sell: Trailing Stop by Low-value second to last candle.";
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {

            if (_mode.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0)
            {// position opening

                Candle lastCandle = candles[candles.Count - 1]; // take the last candle
                Candle prevCandle = candles[candles.Count - 2]; // take the  second to last candle

                if (lastCandle.IsUp == true
                    && prevCandle.IsUp == true)
                { // buy. Two candles grow
                    _tabToTrade.BuyAtMarket(_volume.ValueDecimal);
                }
            }
            else
            { // closing the position

                Candle prevCandle = candles[candles.Count - 2]; // take the  second to last candle

                decimal lowCandle = prevCandle.Low; // took the lowest value from this candle

                Position position = positions[0]; // take position from the array

                _tabToTrade.CloseAtTrailingStopMarket(position, lowCandle);
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson3Bot1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}