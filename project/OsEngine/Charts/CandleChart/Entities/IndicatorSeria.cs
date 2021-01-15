using OsEngine.Indicators;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Charts.CandleChart.Entities
{
    public class IndicatorSeria
    {
        public string ChartName { get; set; }
        public string AreaName { get; set; }
        public int BotTab { get; set; }
        public IndicatorChartPaintType IndicatorType { get; set; }
        public string SeriaName { get; set; }
        public List<decimal> DataPoints { get; set; } = new List<decimal>();
        public List<DataPoint> IndicatorPoints { get; set; } = new List<DataPoint>();
        public List<DataPoint> IndicatorHistogramPoints { get; set; } = new List<DataPoint>();
        public List<ScatterPoint> IndicatorScatterPoints { get; set; } = new List<ScatterPoint>();
        public OxyColor OxyColor { get; set; }
        public int Period { get; set; }
        public bool isHide { get; set; } = false;
        public int series_counter { get; set; } = 0;
        public int Count { get; set; } = 0;
    }
}
