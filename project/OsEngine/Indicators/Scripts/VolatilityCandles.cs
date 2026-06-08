using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("VolatilityCandles")]
    public class VolatilityCandles : Aindicator
    {
        private IndicatorParameterInt _length;         
        
        private IndicatorParameterDecimal _koeff;      
        
        private IndicatorDataSeries _series;                

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)         
            {
                _length = CreateParameterInt("Length", 50);                                                            
                _koeff = CreateParameterDecimal("WeightCoeff", 0.2m);    // весовой коэффициент, от 0 до 1 выбирается: чем ближе к 1, тем больший вес имеют последние данные и сильнее шумит (около 0 на низкой длине тоже шумит)
                _series = CreateSeries("VolatilityCandles", Color.Aqua, IndicatorChartPaintType.Line, true);                     
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt + 1 > index)   
            {
                return;
            }

            decimal high = candles[index - 1].GetPoint("High");
            decimal median = candles[index - 1].GetPoint("Median");

            decimal volatility = (high - median) / median * 100m;       
            
            for (int i = index - _length.ValueInt + 1; i < index + 1; i++)    
            {
                decimal Ihigh = candles[i].GetPoint("High");
                decimal Imedian = candles[i].GetPoint("Median");
                decimal Ivolatility = (Ihigh - Imedian) / Imedian * 100m;
                volatility = _koeff.ValueDecimal * Ivolatility + (1 - _koeff.ValueDecimal) * volatility;
            }
    
            _series.Values[index] = Math.Round(volatility, 3);         
        }
    }
}