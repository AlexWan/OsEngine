/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot-example from the course of lectures "C# for algotreader".
the robot is called when the candle is closed.
Buy: if low-value from Last Candle < Sma and close-value from Last Candle > Sma. Buy At Limit.
Sell: position is open and close-value from Last Candle < sma. Close At Market.
 */

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson3Bot2")]
    public class Lesson3Bot2 : BotPanel
    {
        private BotTabSimple _tabToTrade;

        // Basic setting
        private StrategyParameterString _mode;
        private StrategyParameterDecimal _volume;
        private StrategyParameterDecimal _slippage;

        // Indicator
        private Aindicator _sma;

        public Lesson3Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic setting
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);
            _slippage = CreateParameter("Slippage percent", 0.1m, 0, 10, 1);

            // Indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tabToTrade.CreateCandleIndicator(_sma, "Prime");

            Description = "Robot-example from the course of lectures \"C# for algotreader\"." +
                "the robot is called when the candle is closed." +
                "Buy: if low-value from Last Candle < Sma and close-value from Last Candle > Sma. Buy At Limit." +
                "Sell: position is open and close-value from Last Candle < sma. Close At Market.";
            
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // called on each new candle
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
                decimal valueSma = _sma.DataSeries[0].Last;

                if (valueSma == 0)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];
                decimal lowLastCandle = lastCandle.Low;
                decimal closeLastCandle = lastCandle.Close;

                if (lowLastCandle < valueSma
                    && closeLastCandle > valueSma)
                {
                    decimal entryPrice = closeLastCandle + closeLastCandle * (_slippage.ValueDecimal / 100);
                    _tabToTrade.BuyAtLimit(_volume.ValueDecimal, entryPrice);
                }
            }
            else
            { // closing the position
                decimal valueSma = _sma.DataSeries[0].Last;
                if (valueSma == 0)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];
                decimal closeLastCandle = lastCandle.Close;
                Position position = positions[0];

                if (closeLastCandle < valueSma)
                {
                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson3Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}