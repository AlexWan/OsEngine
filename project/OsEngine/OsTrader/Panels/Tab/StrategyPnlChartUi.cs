using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Entity;
using static OsEngine.OsTrader.Panels.Tab.BotTabOptions;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class StrategyPnlChartUi : Form
    {
        private readonly List<OptionDataRow> _strategyLegs;
        private readonly UnderlyingAssetDataRow _uaData;

        public StrategyPnlChartUi(List<OptionDataRow> strategyLegs, UnderlyingAssetDataRow uaData)
        {
            _strategyLegs = strategyLegs;
            _uaData = uaData;
            InitializeComponent();
            CalculateAndDrawPnlProfile();
        }

        private void InitializeComponent()
        {
            this.Text = "Strategy PNL Profile";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            Chart chart = new Chart();
            chart.Dock = DockStyle.Fill;
            chart.ChartAreas.Add(new ChartArea("MainArea"));
            chart.Legends.Add(new Legend("MainLegend"));

            this.Controls.Add(chart);
        }

        private void CalculateAndDrawPnlProfile()
        {
            var chart = (Chart)this.Controls[0];
            chart.Series.Clear();
            chart.Annotations.Clear();
            chart.BackColor = Color.Black;

            var chartArea = chart.ChartAreas[0];
            chartArea.BackColor = Color.Black;
            chartArea.AxisX.LineColor = Color.White;
            chartArea.AxisY.LineColor = Color.White;
            chartArea.AxisX.LabelStyle.ForeColor = Color.White;
            chartArea.AxisY.LabelStyle.ForeColor = Color.White;
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(40, 40, 40);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(40, 40, 40);
            chartArea.AxisX.TitleForeColor = Color.White;
            chartArea.AxisY.TitleForeColor = Color.White;

            chart.Legends[0].BackColor = Color.Black;
            chart.Legends[0].ForeColor = Color.White;

            var pnlSeries = new Series("Strategy PNL") { ChartType = SeriesChartType.Line, Color = Color.MediumVioletRed, BorderWidth = 2 };

            decimal avgStrike = _strategyLegs.Average(leg => leg.Security.Strike);
            decimal currentUaPrice = _uaData.LastPrice > 0 ? _uaData.LastPrice : (_uaData.Bid + _uaData.Ask) / 2;
            if (currentUaPrice <= 0) currentUaPrice = avgStrike;

            // Dynamic Interval Logic based on average strike
            double interval;
            if (avgStrike > 50000) interval = 1000;
            else if (avgStrike > 10000) interval = 500;
            else if (avgStrike > 1000) interval = 100;
            else if (avgStrike > 100) interval = 10;
            else if (avgStrike > 10) interval = 1;
            else if (avgStrike > 1) interval = 0.5;
            else interval = 0.1;

            double minPrice = (double)(currentUaPrice * 0.7m); // Wider range for strategies
            double maxPrice = (double)(currentUaPrice * 1.3m);
            double step = (maxPrice - minPrice) / 200;

            for (double price = minPrice; price <= maxPrice; price += step)
            {
                decimal totalPnl = 0;
                foreach (var leg in _strategyLegs)
                {
                    decimal premium = leg.LastPrice > 0 ? leg.LastPrice : (leg.Bid + leg.Ask) / 2;
                    decimal intrinsicValue = (leg.Security.OptionType == OptionType.Call)
                        ? Math.Max(0, (decimal)price - leg.Security.Strike)
                        : Math.Max(0, leg.Security.Strike - (decimal)price);
                    totalPnl += (intrinsicValue - premium) * leg.Quantity;
                }
                pnlSeries.Points.AddXY(price, (double)totalPnl);
            }

            chart.Series.Add(pnlSeries);

            var zeroLine = new HorizontalLineAnnotation { AxisX = chartArea.AxisX, AxisY = chartArea.AxisY, LineColor = Color.Gray, LineWidth = 1, LineDashStyle = ChartDashStyle.Dot, Y = 0, IsInfinitive = true, ClipToChartArea = "MainArea" };
            chart.Annotations.Add(zeroLine);

            // Set explicit axis properties
            chartArea.AxisX.Minimum = Math.Floor(minPrice / interval) * interval;
            chartArea.AxisX.Maximum = Math.Ceiling(maxPrice / interval) * interval;
            chartArea.AxisX.Interval = interval;
            chartArea.AxisX.MajorGrid.Interval = interval;
            chartArea.AxisX.LabelStyle.Interval = interval;
            chartArea.AxisX.MajorTickMark.Interval = interval;

            chartArea.AxisX.Title = "Underlying Asset Price at Expiration";
            chartArea.AxisY.Title = "Total Profit / Loss";
        }
    }
}