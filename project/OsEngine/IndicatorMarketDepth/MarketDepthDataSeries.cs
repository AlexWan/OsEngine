/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth
{
    public class MarketDepthDataSeries
    {
        public MarketDepthDataSeries(Color color, string name, IndicatorChartPaintType paintType, bool isPaint)
        {
            Name = name;
            Color = color;
            ChartPaintType = paintType;
            IsPaint = isPaint;
        }

        public string Name;

        public string NameSeries;

        public string GetSaveStr()
        {
            string result = "";

            result += Name + "&";

            result += Color.ToArgb() + "&";

            result += ChartPaintType + "&";

            result += IsPaint + "&";

            result += LineWidth + "&";

            result += ZeroIsGap + "&";

            return result;
        }

        public void LoadFromStr(string[] array)
        {
            Name = array[0];

            Color = Color.FromArgb(Convert.ToInt32(array[1]));

            Enum.TryParse(array[2], out ChartPaintType);

            IsPaint = Convert.ToBoolean(array[3]);

            if (array.Length > 4)
            {
                LineWidth = Convert.ToInt32(array[4]);
            }
            else
            {
                LineWidth = 1;
            }

            if (array.Length > 5)
            {
                ZeroIsGap = Convert.ToBoolean(array[5]);
            }
            else
            {
                ZeroIsGap = false;
            }
        }

        public void Clear()
        {
            Values.Clear();
            Times.Clear();
        }

        public void Delete()
        {
            Values.Clear();
            Values = null;
            Times.Clear();
            Times = null;
        }

        public Color Color;

        public IndicatorChartPaintType ChartPaintType;

        public bool IsPaint;

        public bool CanReBuildHistoricalValues;

        public int LineWidth = 1;

        public bool ZeroIsGap;

        public List<decimal> Values = new List<decimal>();

        public List<DateTime> Times = new List<DateTime>();

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
    }
}
