using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Indicators;
using OsEngine.Entity;
using System.Drawing;

namespace OsEngine.Indicators.GreyCardinal
{
    [IndicatorAttribute("PriceChannel")]

    internal class PriceChannel : Aindicator
    {
       
        private IndicatorParameterInt _LenghtUpLine;
        private IndicatorParameterInt _LenghtDownLine;
        private IndicatorDataSeries _seriesUp, _seriesDown;
        public override void OnProcess(List<Candle> candels, int index)
        {
            decimal upLine = 0,downLine=0 ;
            if (index - _LenghtUpLine.ValueInt > 0)
            {
                for (int i = index; i >-1 && i> index-_LenghtUpLine.ValueInt; i--)
                {
                    if (upLine < candels[i].High)
                    {
                        upLine = candels[i].High;
                    }

                }
            }
            if (index - _LenghtDownLine.ValueInt > 0)
            {
                downLine = decimal.MaxValue;
                for (int i = index; i > -1 && i > index - _LenghtDownLine.ValueInt; i--)
                {
                    if (downLine > candels[i].Low)
                    {
                        downLine = candels[i].Low;
                    }

                }
            }
            _seriesUp.Values[index] = upLine;
            _seriesDown.Values[index] = downLine;
        }

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            {
                _LenghtUpLine = CreateParameterInt("Upper line leght", 30);
                _LenghtDownLine = CreateParameterInt("Down line leght", 30);
                _seriesUp = CreateSeries("Series up",Color.Aqua,IndicatorChartPaintType.Line,true);
                _seriesDown = CreateSeries("Series down", Color.BlueViolet, IndicatorChartPaintType.Line, true);

            }
            else if(state== IndicatorState.Dispose)
            {
                // Clear temp data
            }
            //throw new NotImplementedException();
        }
    }
}
