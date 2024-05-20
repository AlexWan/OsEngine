using OsEngine.Entity;
using OsEngine.Language;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Media.Imaging;

namespace OsEngine.Charts.CandleChart.ChartAddons
{
    public class ChartRoulette : IDisposable
    {
        private Chart _chart;
        private Point posChart;
        private WinFormsChartPainter _painter;
        private int RouletteOnFaza = 0;    // 
        private int RouletteStartCandleNumber;
        private decimal RouletteStartPrice;
        private System.Drawing.Rectangle RouletteRectangle;
        private Point posChartRouletteStart;
        private int RouletteCursorCandleNumber;
        private decimal RouletteCursorPrice;
        private Image imageRouletteNoClick;
        private Image imageRouletteClick;
        private RectangleF rectangleF;
        private System.Drawing.SolidBrush myBrush;
        private int alfa = 2;// прозрачность прямоугольника

        /// <summary>
        /// Создать рулетку
        /// </summary>
        /// <param name="painter"></param>
        /// <param name="chart"></param>
        public ChartRoulette(WinFormsChartPainter painter, Chart chart)
        {

            _painter = painter;
            _chart = chart;
            _chart.MouseMove += _chart_MouseMoveRoulette;  
            _chart.Click += _chart_ClickRoulette; ;
            _chart.PostPaint += _chart_PostPaint;   
            using (var fstream = File.OpenRead(@"Images\Roulette.png"))
            {
                imageRouletteNoClick = new Bitmap(fstream);
            }
            using (var fstream = File.OpenRead(@"Images\RouletteClick.png"))
            {
                imageRouletteClick = new Bitmap(fstream);
            }

            myBrush = new SolidBrush(Color.FromArgb(alfa, Color.WhiteSmoke));
        }

        private void DrawRouletteClip(ChartPaintEventArgs e)
        {
            if (RouletteOnFaza == 0 )
            {
                rectangleF = e.Chart.Bounds;
                e.ChartGraphics.Graphics.DrawImage(imageRouletteNoClick, 2, rectangleF.Height - 30, 20, 20);
            }
            else
            {
                rectangleF = e.Chart.Bounds;
                e.ChartGraphics.Graphics.DrawImage(imageRouletteClick, 2, rectangleF.Height - 30, 20, 20);
            }
        }

        private void _chart_PostPaint(object sender, ChartPaintEventArgs e)
        {
            try
            {
                return;
                DrawRouletteClip(e);
                if (RouletteRectangle == null)
                {
                    return;
                }
                if (RouletteOnFaza == 2)
                {
                    e.ChartGraphics.Graphics.FillRectangle(myBrush, RouletteRectangle);
                }

                if (RouletteOnFaza == 3)
                {

                    e.ChartGraphics.Graphics.FillRectangle(myBrush, RouletteRectangle);
                    ChartAreaSizes areaSize = _painter.areaSizes.Find(size => size.Name == "Prime");
                    decimal CandelsRange = RouletteCursorCandleNumber - RouletteStartCandleNumber;
                    decimal pricePercent = (RouletteCursorPrice / RouletteStartPrice - 1) * 100;
                    decimal priceRange = Convert.ToDecimal(Math.Round(RouletteCursorPrice - RouletteStartPrice, areaSize.Decimals));
                    DateTime StartRouletteDateTime = _painter.myCandles[RouletteStartCandleNumber].TimeStart;
                    DateTime EndRouletteDateTime = _painter.myCandles[RouletteCursorCandleNumber].TimeStart;
                    TimeSpan RouletteRange = EndRouletteDateTime - StartRouletteDateTime;
                    string message = Convert.ToDecimal(Math.Round(RouletteStartPrice, areaSize.Decimals)) + " -> " + Convert.ToDecimal(Math.Round(RouletteCursorPrice, areaSize.Decimals)) +
                                 "\n" + priceRange.ToString() + "; ( " + pricePercent.ToString("#0.##") +
                                 $" % )\n{TextToChartCandles}: " + CandelsRange.ToString() + "; ( " + RouletteRange.ToString() + " )";
                    e.ChartGraphics.Graphics.DrawString(message, new Font("Courier New", 10),
                        new SolidBrush(Color.White), new PointF() { X = RouletteRectangle.X + RouletteRectangle.Width / 5, Y = RouletteRectangle.Y - 50 > 0 ? RouletteRectangle.Y - 50 : 0 });
                }
            }
            catch
            {
                // игнор  
            }
        }

        private string TextToChartCandles
        {
            get { return OsLocalization.Charts.LabelRoulette1; }
        }

        private void _chart_ClickRoulette(object sender, EventArgs e)
        {
            try
            {
                return;
                ChartArea area = null;
                for (int i = 0; i < _chart.ChartAreas.Count; i++)
                {
                    if (!double.IsNaN(_chart.ChartAreas[i].CursorX.Position))
                    {
                        area = _chart.ChartAreas[i];
                        break;
                    }
                }
                posChartRouletteStart = new Point(((MouseEventArgs)e).X, ((MouseEventArgs)e).Y); //Position of the mouse respect to the chart
                if (RouletteOnFaza == 0)
                {
                    if (posChartRouletteStart.X > 0
                        && posChartRouletteStart.X < 20
                        && posChartRouletteStart.Y > rectangleF.Height - 30
                        )
                    {
                        RouletteOnFaza = 1;
                        return;
                    }
                }
                if (RouletteOnFaza == 1 && area != null && area.Name == "Prime")
                {
                    RouletteRectangle = new System.Drawing.Rectangle();
                    RouletteStartCandleNumber = _painter.GetCursorSelectCandleNumber();
                    RouletteStartPrice = _painter.GetCursorSelectPrice();
                    RouletteRectangle.Location = new Point() { X = posChartRouletteStart.X, Y = posChartRouletteStart.Y };
                    RouletteRectangle.Width = 50;
                    RouletteRectangle.Height = 50;
                    RouletteOnFaza = 2;
                }
                else if (RouletteOnFaza == 2 && area != null && area.Name == "Prime")
                {
                    RouletteCursorCandleNumber = _painter.GetCursorSelectCandleNumber();
                    RouletteCursorPrice = _painter.GetCursorSelectPrice();
                    decimal pricePercent = (RouletteStartPrice / RouletteCursorPrice - 1) * -1;
                    decimal CandelsRange = RouletteCursorCandleNumber - RouletteStartCandleNumber;
                    if (RouletteStartCandleNumber >= _painter.myCandles.Count)
                    {
                        RouletteStartCandleNumber = _painter.myCandles.Count - 1;
                    }
                    DateTime StartRouletteDateTime = _painter.myCandles[RouletteStartCandleNumber].TimeStart;
                    DateTime EndRouletteDateTime = _painter.myCandles[RouletteCursorCandleNumber].TimeStart;
                    TimeSpan RouletteRange = EndRouletteDateTime - StartRouletteDateTime;
                    RouletteOnFaza = 3;
                }
                else if (RouletteOnFaza == 3)
                {
                    RouletteOnFaza = 0;
                }
            }
            catch
            {
                RouletteOnFaza = 0;
                //игнор
            }
        }

        private void _chart_MouseMoveRoulette(object sender, MouseEventArgs e)
        {
            return;
            if (RouletteRectangle == null)
            {
                return;
            }
            posChart = new Point(e.X, e.Y); //Position of the mouse respect to the chart
            ChartArea area = GetChartArea("Prime");
            int x;
            int y;
            if ((RouletteOnFaza == 2) && area != null && area.Name == "Prime")
            {

                if (posChart.X - posChartRouletteStart.X <= 0)
                {
                    x = posChart.X;
                }
                else
                {
                    x = posChartRouletteStart.X;
                }
                if (posChart.Y - posChartRouletteStart.Y <= 0)
                {
                    y = posChart.Y;
                }
                else
                {
                    y = posChartRouletteStart.Y;
                }
                RouletteRectangle.Location = new Point() { X = x, Y = y };

                RouletteRectangle.Width = Math.Abs(posChart.X - posChartRouletteStart.X);
                RouletteRectangle.Height = Math.Abs(posChart.Y - posChartRouletteStart.Y);
                _chart.Invalidate();
            }
            if (RouletteOnFaza == 3 && area != null && area.Name == "Prime")
            {
                _chart.Invalidate();
            }

        }

        private ChartArea GetChartArea(string name)
        {
            return _chart.ChartAreas.FindByName(name);
        }

        public void RulerClick()
        {
            RouletteOnFaza = 1;
        }


        public void Dispose()
        {
            _chart.MouseMove -= _chart_MouseMoveRoulette;
            _chart.Click -= _chart_ClickRoulette;
            _chart.PostPaint -= _chart_PostPaint;
        }



    }
}
