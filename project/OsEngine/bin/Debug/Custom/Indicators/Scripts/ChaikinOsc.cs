using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;


namespace CustomIndicators.Scripts
{
    public class ChaikinOsc : Aindicator
    {
        private IndicatorDataSeries _seriesLine;

        private Aindicator _seriesShort;
        private Aindicator _seriesLong;

        private IndicatorParameterInt _longPeriod;
        private IndicatorParameterInt _shortPeriod;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _shortPeriod = CreateParameterInt("Short Period", 3);

                _longPeriod = CreateParameterInt("Long Period", 10);

                _seriesLine = CreateSeries("Chaikin Oscillator", Color.Gold, IndicatorChartPaintType.Line, true);
                _seriesLine.CanReBuildHistoricalValues = false;

                _seriesShort = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Short", false);
                ((IndicatorParameterInt)_seriesShort.Parameters[0]).Bind(_shortPeriod);
                ProcessIndicator("Short Period", _seriesShort);

                _seriesLong = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Long", false);
                ((IndicatorParameterInt)_seriesLong.Parameters[0]).Bind(_longPeriod);
                ProcessIndicator("Short Period", _seriesLong);
            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {
            // CHOi = SMAi (accdist, m) – SMAi (accdist, n)

            if (index < _longPeriod.ValueInt ||
                index < _shortPeriod.ValueInt)
            {
                return;
            }

            if (index <= 0)
            {
                return;
            }


                _seriesLine.Values[index] =
                    _seriesShort.DataSeries[0].Values[index] - _seriesLong.DataSeries[0].Values[index];
            
        }
    }
}