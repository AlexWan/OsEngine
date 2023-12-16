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
        private IndicatorDataSeries _seriesUp, _seriesDown;
        public override void OnProcess(List<Candle> candels, int index)
        {
            int maxBack = 1000;
            decimal upLine = 0,downLine=0 ;
            if (index - maxBack > 0)
            {
                upLine = candels[1].High;
                for (int i = index; i >3 && i> index- maxBack.ValueInt; i--)
                {
                    if (upLine < candels[i].High)
                    {
                        upLine = candels[i].High;
                    }
                    else
                    { 
                        break;
                    }

                }
            }
            if (index - _LenghtDownLine.ValueInt > 0)
            {
                downLine = candels[1].Low;
                for (int i = index; i > -1 && i > index - maxBack; i--)
                {
                    if (downLine > candels[i].Low)
                    {
                        downLine = candels[i].Low;
                    }
                    else
                    {
                        break
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
                _seriesUp = CreateSeries("Series up",Color.Aqua,IndicatorChartPaintType.Line,true);
                _seriesDown = CreateSeries("Series down", Color.BlueViolet, IndicatorChartPaintType.Line, true);

            }
            else if(state== IndicatorState.Dispose)
            {
                // Clear temp data
            }
        }
    }
}
