using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Entity;
using static OsEngine.OsTrader.Panels.Tab.BotTabOptions;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class OptionPnlChartUi : Form
    {
        private readonly OptionDataRow _optionData;
        private readonly UnderlyingAssetDataRow _uaData;

        public OptionPnlChartUi(OptionDataRow optionData, UnderlyingAssetDataRow uaData)
        {
            _optionData = optionData;
            _uaData = uaData;
            InitializeComponent();
            CalculateAndDrawPnlProfile();
        }

        private void InitializeComponent()
        {
            this.Text = $"PNL Profile for {_optionData.Security.Name}";
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

            var expirationPnlSeries = new Series("PNL at Expiration")
            {
                ChartType = SeriesChartType.Line,
                Color = Color.MediumVioletRed,
                BorderWidth = 2
            };

            var currentPnlSeries = new Series("Current PNL")
            {
                ChartType = SeriesChartType.Line,
                Color = Color.CornflowerBlue,
                BorderWidth = 2
            };

            decimal strike = _optionData.Security.Strike;
            double premium = _optionData.LastPrice > 0 ? _optionData.LastPrice : (_optionData.Bid + _optionData.Ask) / 2;
            if (premium <= 0) return;

            double currentUaPrice = _uaData.LastPrice > 0 ? _uaData.LastPrice : (_uaData.Bid + _uaData.Ask) / 2;
            if (currentUaPrice <= 0) currentUaPrice = (double)strike;

            // Dynamic Interval Logic
            double interval;
            if (strike > 50000) interval = 1000;
            else if (strike > 10000) interval = 500;
            else if (strike > 1000) interval = 100;
            else if (strike > 100) interval = 10;
            else if (strike > 10) interval = 1;
            else if (strike > 1) interval = 0.5;
            else interval = 0.1;

            double minPrice = currentUaPrice * 0.8;
            double maxPrice = currentUaPrice * 1.2;
            double step = (maxPrice - minPrice) / 200;

            for (double price = minPrice; price <= maxPrice; price += step)
            {
                // 1. Expiration PNL
                double expirationPnl = (_optionData.Security.OptionType == OptionType.Call)
                    ? Math.Max(0, price - (double)strike) - premium
                    : Math.Max(0, (double)strike - price) - premium;
                expirationPnlSeries.Points.AddXY(price, expirationPnl);

                // 2. Current PNL
                double timeToExpiration = (_optionData.Security.Expiration - DateTime.UtcNow).TotalDays / 365.0;
                double riskFreeRate = 0.0; // Hardcoded as per plan
                double volatility = (double)_optionData.IV/100;

                double theoreticalPrice = BlackScholes.CalculateOptionPrice(
                    _optionData.Security.OptionType,
                    price,
                    (double)strike,
                    timeToExpiration,
                    riskFreeRate,
                    volatility);

                double currentPnl = theoreticalPrice - premium;
                currentPnlSeries.Points.AddXY(price, currentPnl);
            }

            chart.Series.Add(expirationPnlSeries);
            chart.Series.Add(currentPnlSeries);

            var strikeLine = new VerticalLineAnnotation { AxisX = chartArea.AxisX, AxisY = chartArea.AxisY, LineColor = Color.Red, LineWidth = 1, LineDashStyle = ChartDashStyle.Dash, X = (double)strike, IsInfinitive = true, ClipToChartArea = "MainArea" };
            var zeroLine = new HorizontalLineAnnotation { AxisX = chartArea.AxisX, AxisY = chartArea.AxisY, LineColor = Color.Gray, LineWidth = 1, LineDashStyle = ChartDashStyle.Dot, Y = 0, IsInfinitive = true, ClipToChartArea = "MainArea" };
            chart.Annotations.Add(strikeLine);
            chart.Annotations.Add(zeroLine);
            
            // Set explicit axis properties
            chartArea.AxisX.Minimum = Math.Floor(minPrice / interval) * interval;
            chartArea.AxisX.Maximum = Math.Ceiling(maxPrice / interval) * interval;
            chartArea.AxisX.Interval = interval;
            chartArea.AxisX.MajorGrid.Interval = interval;
            chartArea.AxisX.LabelStyle.Interval = interval;
            chartArea.AxisX.MajorTickMark.Interval = interval;

            chartArea.AxisX.Title = "Underlying Asset Price";
            chartArea.AxisY.Title = "Profit / Loss";
        }
    }
}