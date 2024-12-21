using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{

    public class IndicatorDataSeries
    {
        #region Service

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="color">color of the data series on the chart</param>
        /// <param name="name">unique data series name</param>
        /// <param name="paintType">graph type for data series. Line, column, etc...</param>
        /// <param name="isPaint">whether this series of data needs to be plotted on a chart</param>
        public IndicatorDataSeries(Color color, string name, IndicatorChartPaintType paintType, bool isPaint)
        {
            Name = name;
            Color = color;
            ChartPaintType = paintType;
            IsPaint = isPaint;
        }

        /// <summary>
        /// unique data series name
        /// </summary>
        public string Name;

        /// <summary>
        /// name of data series on the chart
        /// </summary>
        public string NameSeries;

        /// <summary>
        /// object string for saving settings
        /// </summary>
        public string GetSaveStr()
        {
            string result = "";

            result += Name + "&";

            result += Color.ToArgb() + "&";

            result += ChartPaintType + "&";

            result += IsPaint + "&";

            return result;
        }

        /// <summary>
        /// load an object from a string
        /// </summary>
        public void LoadFromStr(string[] array)
        {
            Name = array[0];

            Color = Color.FromArgb(Convert.ToInt32(array[1]));

            Enum.TryParse(array[2], out ChartPaintType);

            IsPaint = Convert.ToBoolean(array[3]);
        }

        /// <summary>
        /// clear the data in the series
        /// </summary>
        public void Clear()
        {
            Values.Clear();
        }

        /// <summary>
        /// is called when an object is deleted to clear it
        /// </summary>
        public void Delete()
        {
            Values.Clear();
            Values = null;
        }

        #endregion

        #region Settings and data

        /// <summary>
        /// color of the data series on the chart
        /// </summary>
        public Color Color;

        /// <summary>
        /// graph type for data series. Line, column, etc...
        /// </summary>
        public IndicatorChartPaintType ChartPaintType;

        /// <summary>
        /// whether this series of data needs to be plotted on a chart
        /// </summary>
        public bool IsPaint;

        /// <summary>
        /// do you have to redraw the series on the chart every time from start to finish.
        /// </summary>
        public bool CanReBuildHistoricalValues;

        /// <summary>
        /// series data points
        /// </summary>
        public List<decimal> Values = new List<decimal>();

        /// <summary>
        /// the last value of the series
        /// </summary>
        public decimal Last
        {
            get
            {
                if (Values.Count == 0)
                {
                    return 0;
                }

                return Values[Values.Count - 1];
            }
        }

        #endregion

    }
}