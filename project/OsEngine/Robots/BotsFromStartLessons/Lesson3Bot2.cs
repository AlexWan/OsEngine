using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson3Bot2")]
    public class Lesson3Bot2 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;

        StrategyParameterDecimal _volume;

        StrategyParameterDecimal _slippage;

        Aindicator _sma;

        public Lesson3Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volume = CreateParameter("Volume", 10m, 1, 10, 1);
            _slippage = CreateParameter("Slippage percent", 0.1m, 0, 10, 1);

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tabToTrade.CreateCandleIndicator(_sma, "Prime");
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
                {// событие открытия позиции произошло
                    decimal entryPrice = closeLastCandle + closeLastCandle * (_slippage.ValueDecimal / 100);
                    _tabToTrade.BuyAtLimit(_volume.ValueDecimal, entryPrice);
                }
            }
            else
            { // закрытие позиции
                decimal valueSma = _sma.DataSeries[0].Last;
                if (valueSma == 0)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];
                decimal closeLastCandle = lastCandle.Close;
                Position position = positions[0]; // берём позицию из массива

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