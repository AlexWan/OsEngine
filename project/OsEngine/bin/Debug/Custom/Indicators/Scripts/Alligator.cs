using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Alligator : Aindicator
    {
        private IndicatorParameterInt _lengthTeeth;
        private IndicatorParameterInt _lengthLips;
        private IndicatorParameterInt _lengthJaw;

        private IndicatorParameterInt _shiftTeeth;
        private IndicatorParameterInt _shiftLips;
        private IndicatorParameterInt _shiftJaw;

        private IndicatorDataSeries _seriesTeeth;
        private IndicatorDataSeries _seriesLips;
        private IndicatorDataSeries _seriesJaw;

        private Aindicator _smaTeeth;
        private Aindicator _smaLips;
        private Aindicator _smaJaw;


        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthJaw = CreateParameterInt("Jaw length", 13);
                _lengthLips = CreateParameterInt("Lips length", 8);
                _lengthTeeth = CreateParameterInt("Teeth length", 5);

                _shiftJaw = CreateParameterInt("Jaw offset", 8);
                _shiftLips = CreateParameterInt("Lips offset", 3);
                _shiftTeeth = CreateParameterInt("Teeth offset", 5);

                _seriesJaw = CreateSeries("Jaw", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesTeeth = CreateSeries("Teeth", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesLips = CreateSeries("Lips", Color.LawnGreen, IndicatorChartPaintType.Line, true);

                _smaJaw = IndicatorsFactory.CreateIndicatorByName("Ssma", Name + "SsmaJaw", false);
                ((IndicatorParameterInt)_smaJaw.Parameters[0]).Bind(_lengthJaw);
                ProcessIndicator("Jaw SSMA", _smaJaw);

                _smaLips = IndicatorsFactory.CreateIndicatorByName("Ssma", Name + "SsmaLips", false);
                ((IndicatorParameterInt)_smaLips.Parameters[0]).Bind(_lengthLips);
                ProcessIndicator("Lips SSMA", _smaLips);

                _smaTeeth = IndicatorsFactory.CreateIndicatorByName("Ssma", Name + "SsmaTeeth", false);
                ((IndicatorParameterInt)_smaTeeth.Parameters[0]).Bind(_lengthTeeth);
                ProcessIndicator("Teeth SSMA", _smaTeeth);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _shiftTeeth.ValueInt >= 0)
            {
                _seriesTeeth.Values[index] = _smaTeeth.DataSeries[0].Values[index - _shiftTeeth.ValueInt];
            }
            if (index - _shiftLips.ValueInt >= 0)
            {
                _seriesLips.Values[index] = _smaLips.DataSeries[0].Values[index - _shiftLips.ValueInt];
            }
            if (index - _shiftJaw.ValueInt >= 0)
            {
                _seriesJaw.Values[index] = _smaJaw.DataSeries[0].Values[index - _shiftJaw.ValueInt];
            }
        }
    }
}