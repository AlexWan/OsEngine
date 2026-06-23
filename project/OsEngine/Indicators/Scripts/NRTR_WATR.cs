using OsEngine.Entity;
using OsEngine.Language;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;


namespace OsEngine.Indicators
{
    [Indicator("NRTR_WATR")]
    public class NRTR_WATR : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "NRTR_WATR adapts trailing-stop levels using ATR, stepping back from local extremes by an amount proportional to volatility, making the stop tighter in calm markets and wider in volatile ones. " +
                             "Traders use it for dynamic exits and trend reversal detection.";

                string ru = "NRTR_WATR адаптирует уровни trailing-stop с помощью ATR, отступая от локальных экстремумов на величину, кратную волатильности, что делает стоп более чувствительным в спокойных рынках и широким в волатильных. " +
                            "Трейдеры используют его для динамического выхода из позиции и определения разворота тренда.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _period;

        private IndicatorParameterDecimal _mult;

        private IndicatorDataSeries _seriesData;

        private int _trend = 0;

        private decimal hp = decimal.MinValue;
		
        private decimal lp = decimal.MaxValue;
		
        private decimal nrtr = 0;

        private Aindicator _atr;
		
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Length", 21);
                _mult = CreateParameterDecimal("ATR multiplicator", 2.5m);

                _seriesData = CreateSeries("NRTR_WATR", Color.Red, IndicatorChartPaintType.Line, true);

                _atr = IndicatorsFactory.CreateIndicatorByName("ATR", Name + "ATR", false);
                _atr.Parameters[0].Bind(_period);
                ProcessIndicator("ATR", _atr);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return;
            }

            if (_trend >= 0)
            {
                if (candles[index].Close > hp)
                {
                    hp = candles[index].Close;
                }

                nrtr = hp - _atr.DataSeries[0].Values[index] * _mult.ValueDecimal;

                if (candles[index].Close <= nrtr)
                {
                    _trend = -1;
                    lp = candles[index].Close;
                    nrtr = lp + _atr.DataSeries[0].Values[index] * _mult.ValueDecimal;
                }
            }
            else if (candles[index].Close < lp)
            {
                lp = lp = candles[index].Close;
                nrtr = lp + _atr.DataSeries[0].Values[index] * _mult.ValueDecimal;
            }
            else if (candles[index].Close > nrtr)
            {
                _trend = 1;
                hp = candles[index].Close;
                nrtr = hp - _atr.DataSeries[0].Values[index] * _mult.ValueDecimal;
            }

            _seriesData.Values[index] = nrtr;
        }
    }
}
