using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
/* Description
Robot-example from the course of lectures "C# for algotreader".
the robot is called when the candle is closed.
Buy: SmaFast > SmaSlow. Buy At Market.
Sell: SmaFast < SmaSlow. Close At Market.
 */
namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson3Bot3")]
    public class Lesson3Bot3 : BotPanel
    {
        private BotTabSimple _tabToTrade;

        // Basic setting
        private StrategyParameterString _Mode;
        private StrategyParameterDecimal _volume;

        // Indicator setting
        private StrategyParameterInt _smaLenFast;
        private StrategyParameterInt _smaLenSlow;

        // Indicator
        private Aindicator _smaFast;
        private Aindicator _smaSlow;

        public Lesson3Bot3(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            // Basic setting
            _Mode = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);

            // Indicator setting
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

            Description = "Robot-example from the course of lectures \"C# for algotreader\"." +
                "the robot is called when the candle is closed." +
                "Buy: SmaFast > SmaSlow. Buy At Market." +
                "Sell: SmaFast < SmaSlow. Close At Market.";
            
            ParametrsChangeByUser += Lesson3Bot3_ParametrsChangeByUser;
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
            // called on each new candle

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
            {// position opening
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
            { // closing the position
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
                    Position position = positions[0]; // take position from the array

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