using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class OxyAreaSettings
    {
        public OxyColor TicklineColor = OxyColors.AliceBlue;
        public OxyColor MajorGridlineColor = OxyColor.FromArgb(25, 238, 239, 255);
        public LineStyle MajorGridlineStyle = LineStyle.DashDot;
        public double MajorGridlineThickness = 1.2;
        public OxyColor MinorGridlineColor = OxyColor.FromArgb(10, 238, 239, 255);
        public LineStyle MinorGridlineStyle = LineStyle.DashDot;
        public double MinorGridlineThickness = 1;
        public EdgeRenderingMode EdgeRenderingMode = EdgeRenderingMode.PreferSharpness;
        public double AbsoluteMinimum = 0;
        public LineStyle AxislineStyle = LineStyle.None;
        public OxyColor AxislineColor = OxyColor.FromArgb(10, 238, 239, 255);

        public OxyColor ScrollBarColor = OxyColor.FromArgb(100, 192, 197, 201);

        public bool cursor_X_is_active = false;
        public bool cursor_Y_is_active = false;
        public bool ann_price_is_active = false;
        public bool ann_date_time_is_active = false;

        public bool X_Axies_is_visible = false;
        public bool Y_Axies_is_visible = false;

        public OxyColor CursorColor = OxyColor.Parse("#FF5500");


        System.Windows.Media.BrushConverter converter = new System.Windows.Media.BrushConverter();
        
        public string brush_background = "";
        public System.Windows.Media.Brush Brush_background { get
            {
                if (brush_background == "")
                    return (System.Windows.Media.Brush)converter.ConvertFromString("#111217");
                else
                    return (System.Windows.Media.Brush)converter.ConvertFromString(brush_background);
            }
        }

        public string brush_border = "";
        public System.Windows.Media.Brush Brush_border
        {
            get
            {
                if (brush_border == "")
                    return (System.Windows.Media.Brush)converter.ConvertFromString("#151A1E");
                else
                    return (System.Windows.Media.Brush)converter.ConvertFromString(brush_border);
            }
        }

        public string brush_scroll_bacground = "";
        public System.Windows.Media.Brush Brush_scroll_bacground
        {
            get
            {
                if (brush_scroll_bacground == "")
                    return (System.Windows.Media.Brush)converter.ConvertFromString("#151A1E");
                else
                    return (System.Windows.Media.Brush)converter.ConvertFromString(brush_scroll_bacground);
            }
        }

        public string brush_scroll_freeze_bacground = "";
        public System.Windows.Media.Brush Brush_scroll_freeze_bacground
        {
            get
            {
                if (brush_scroll_freeze_bacground == "")
                    return (System.Windows.Media.Brush)converter.ConvertFromString("#25FF5500");
                else
                    return (System.Windows.Media.Brush)converter.ConvertFromString(brush_scroll_freeze_bacground);
            }
        }

        public object Tag = new object();

        public int candles_in_run = 300;
        public int empty_gap = 50;
    }
}
