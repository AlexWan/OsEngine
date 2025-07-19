using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    public class SampleCandleView : Aindicator
    {
        // Порядок High, Low, Close обязательно.
        private IndicatorDataSeries _series0; // HIGH
        private IndicatorDataSeries _series1; // Low
        private IndicatorDataSeries _series2; // Close
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series0 = CreateSeries("series0", Color.LimeGreen, IndicatorChartPaintType.Candle, true);
                _series1 = CreateSeries("series1", Color.Red, IndicatorChartPaintType.Candle, true);
                _series2 = CreateSeries("series2", Color.LightCoral, IndicatorChartPaintType.Candle, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            
            if (index <= 0 || candles.Count <= index)
                return;

            // Open–реальное (можно взять либо candles[index].Open, либо предыдущий Close)
            decimal openReal = candles[index].Open;
            decimal highReal = candles[index].High;
            decimal lowReal = candles[index].Low;
            decimal closeReal = candles[index].Close;

            // дельты относительно Open
            decimal deltaHigh = highReal - openReal;  // >0 → рисуем вверх, <0 → вниз
            decimal deltaLow = lowReal - openReal;  // >0 → рисуем выше нуля, <0 → ниже
            decimal deltaClose = closeReal - openReal;  // >0 → тело вверх, <0 → вниз

            _series0.Values[index] = deltaHigh*100;
            _series1.Values[index] = deltaLow*100;
            _series2.Values[index] = deltaClose*100;        }
    }
}