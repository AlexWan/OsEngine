/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot-example from the course of lectures "C# for algotreader".
the robot is called when the candle is closed.

Buy:
When the second to last and last candle grew

Sell:
Trailing Stop by Low-value second to last candle.
 */

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson3Bot1")] 
    public class Lesson3Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную торговую вкладку 
        private BotTabSimple _tabToTrade;

        // Basic setting
        // Базовые настройки
        private StrategyParameterString _mode;
        private StrategyParameterDecimal _volume;

        public Lesson3Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);

            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel8;
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
            {   
                // Position opening
                // Открытие позиции

                // Take the last candle
                // Берём последнюю свечку
                Candle lastCandle = candles[candles.Count - 1];

                // Take the  second to last candle
                // Берём предпоследнюю свечу
                Candle prevCandle = candles[candles.Count - 2];

                if (lastCandle.IsUp == true
                    && prevCandle.IsUp == true)
                {   
                    // Buy. Two candles grow
                    // Покупаем. Две свечи растут
                    _tabToTrade.BuyAtMarket(_volume.ValueDecimal);
                }
            }
            else
            {
                // closing the position
                // закрытие позиции

                // Take the second to last candle
                // Берём предпоследнюю свечу
                Candle prevCandle = candles[candles.Count - 2];

                // Took the lowest value from this candle
                // Взяли наименьшее значение от этой свечи
                decimal lowCandle = prevCandle.Low;

                // Take position from the array
                // Берём позицию из массива
                Position position = positions[0];

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