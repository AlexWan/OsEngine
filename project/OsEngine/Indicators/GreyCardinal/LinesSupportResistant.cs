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
    [IndicatorAttribute("LinesSupportResistant")]

    internal class LinesSupportResistant : Aindicator
    {
        private IndicatorParameterInt _lenghtHistory;
 
        private IndicatorDataSeries _seriesUp, _seriesDown;

        public override void OnProcess(List<Candle> candels, int index)
        {
            decimal upLine = 0, downLine = 0;
            if (index - _lenghtHistory.ValueInt > 0)
            {
                upLine = candels[index].High; 
                downLine = candels[index].Low; 
 

                for (int i = index; i > 3 && i > index - _lenghtHistory.ValueInt; i--)
                {
                    if (upLine < candels[i-1].High)
                    {
                        upLine = candels[i - 1].High;
                    }
                    else if(upLine> candels[index].High)
                    {
                        break;
                    }

                }
                for (int i = index; i > 3 && i > index - _lenghtHistory.ValueInt; i--)
                {
                    if (downLine > candels[i-1].Low)
                    {
                        downLine = candels[i - 1].Low;
                    }
                    else if(downLine< candels[index].Low)
                    {
                        break;
                    }

                }
                _seriesUp.Values[index ] = upLine;
                _seriesDown.Values[index] = downLine;
            }
        }

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            {
                _lenghtHistory = CreateParameterInt("History length", 60);
                _seriesUp = CreateSeries("Resistant line",Color.Red,IndicatorChartPaintType.Line,true);
                _seriesDown = CreateSeries("Support line", Color.GreenYellow, IndicatorChartPaintType.Line, true);

            }
            else if(state== IndicatorState.Dispose)
            {
                // Clear temp data
            }
        }
    }
}
