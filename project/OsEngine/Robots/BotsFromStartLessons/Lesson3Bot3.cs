using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
/* Description
Robot-example from the course of lectures "C# for algotreader".
the robot is called when the candle is closed.

Buy:
SmaFast > SmaSlow. Buy At Market.

Sell:
SmaFast < SmaSlow. Close At Market.
 */
namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson3Bot3")]
    public class Lesson3Bot3 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку 
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _Mode;
        private StrategyParameterDecimal _volume;

        // Indicator settings
        // Настройки индикаторов
        private StrategyParameterInt _smaLenFast;
        private StrategyParameterInt _smaLenSlow;

        // Indicator
        private Aindicator _smaFast;
        private Aindicator _smaSlow;

        public Lesson3Bot3(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _Mode = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);

            // Indicator settings
            // Настройки индикаторов
            _smaLenFast = CreateParameter("Sma fast len", 15, 1, 10, 1);
            _smaLenSlow = CreateParameter("Sma slow len", 100, 1, 10, 1);

            // Indicator SmaFast
            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaFast, "Prime");
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            // Indicator SmaSlow
            _smaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _smaSlow = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaSlow, "Prime");
            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;

            ParametrsChangeByUser += Lesson3Bot3_ParametrsChangeByUser;
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel10;
        }

        private void Lesson3Bot3_ParametrsChangeByUser()
        {
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;
            _smaFast.Reload();
            _smaFast.Save();

            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;
            _smaSlow.Reload();
            _smaSlow.Save();
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // Сalled on each new candle
            // вызывается перед каждой новой свечой

            if (_Mode.ValueString == "Off")
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
                // opening the position 
                // открытие позиции

                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast > smaSlowLast)
                {
                    _tabToTrade.BuyAtMarket(_volume.ValueDecimal);
                }
            }
            else
            {   
                // closing the position
                // закрытие позиции

                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast < smaSlowLast)
                {
                    // take position from the array
                    // Берём позицию из массива
                    Position position = positions[0]; 

                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson3Bot3";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}