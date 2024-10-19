using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson3Bot3")]
    public class Lesson3Bot3 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;
        StrategyParameterDecimal _volume;
        StrategyParameterInt _smaLenFast;
        StrategyParameterInt _smaLenSlow;

        Aindicator _smaFast;
        Aindicator _smaSlow;

        public Lesson3Bot3(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);

            _smaLenFast = CreateParameter("Sma fast len", 15, 1, 10, 1);
            _smaLenSlow = CreateParameter("Sma slow len", 100, 1, 10, 1);

            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaFast, "Prime");
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            _smaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _smaSlow = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaSlow, "Prime");
            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;

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
            // вызывается на каждой новой свече

            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0)
            {// открытие позиции
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
            { // закрытие позиции
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
                    Position position = positions[0]; // берём позицию из массива

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