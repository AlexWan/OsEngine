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

            Panel buttonPanel = new Panel();
            buttonPanel.Height = 40;
            buttonPanel.Dock = DockStyle.Bottom;

            Button buyButton = new Button();
            buyButton.Text = "Buy Market";
            buyButton.Location = new Point(10, 5);
            buyButton.Click += BuyButton_Click;

            buttonPanel.Controls.Add(buyButton);

            this.Controls.Add(chart);
            this.Controls.Add(buttonPanel);
            chart.BringToFront();
        }

        private void BuyButton_Click(object sender, EventArgs e)
        {
            // Handle underlying asset
            if (_uaData != null && _uaData.Quantity != 0 && _uaData.SimpleTab != null)
            {
                if (_uaData.Quantity > 0)
                {
                    _uaData.SimpleTab.BuyAtMarket(_uaData.Quantity);
                }
                else if (_uaData.Quantity < 0)
                {
                    _uaData.SimpleTab.SellAtMarket(Math.Abs(_uaData.Quantity));
                }
            }

            // Handle option legs
            foreach (var leg in _strategyLegs)
            {
                if (leg.Quantity > 0)
                {
                    leg.SimpleTab.BuyAtMarket(leg.Quantity);
                }
                else if (leg.Quantity < 0)
                {
                    leg.SimpleTab.SellAtMarket(Math.Abs(leg.Quantity));
                }
            }
            MessageBox.Show("Market orders sent for strategy.");
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

            var expirationPnlSeries = new Series("PNL at Expiration") { ChartType = SeriesChartType.Line, Color = Color.MediumVioletRed, BorderWidth = 2 };
            var currentPnlSeries = new Series("Current PNL") { ChartType = SeriesChartType.Line, Color = Color.CornflowerBlue, BorderWidth = 2 };

            double avgStrike = (double)_strategyLegs.Average(leg => leg.Security.Strike);
            double currentUaPrice = _uaData.LastPrice > 0 ? _uaData.LastPrice : (_uaData.Bid + _uaData.Ask) / 2;
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

            double minPrice = currentUaPrice * 0.7; // Wider range for strategies
            double maxPrice = currentUaPrice * 1.3;
            double step = (maxPrice - minPrice) / 200;

            for (double price = minPrice; price <= maxPrice; price += step)
            {
                double totalExpirationPnl = 0;
                double totalCurrentPnl = 0;

                // Add PNL from the underlying asset if it's part of the strategy
                if (_uaData != null && _uaData.Quantity != 0)
                {
                    double uaPnl = _uaData.Quantity * (price - currentUaPrice);
                    totalExpirationPnl += uaPnl;
                    totalCurrentPnl += uaPnl;
                }

                foreach (var leg in _strategyLegs)
                {
                    double premium = leg.LastPrice > 0 ? leg.LastPrice : (leg.Bid + leg.Ask) / 2;
                    if (premium == 0)
                    {
                        premium = (double)leg.Security.PriceStep;
                    }
                    // 1. Expiration PNL
                    double intrinsicValue = (leg.Security.OptionType == OptionType.Call)
                        ? Math.Max(0, price - (double)leg.Security.Strike)
                        : Math.Max(0, (double)leg.Security.Strike - price);
                    totalExpirationPnl += (intrinsicValue - premium) * leg.Quantity;

                    // 2. Current PNL
                    double timeToExpiration = (leg.Security.Expiration - DateTime.UtcNow).TotalDays / 365.0;
                    double riskFreeRate = 0.0; // Hardcoded as per plan
                    double volatility = (double)leg.IV/100;

                    double theoreticalPrice = BlackScholes.CalculateOptionPrice(
                        leg.Security.OptionType,
                        price,
                        (double)leg.Security.Strike,
                        timeToExpiration,
                        riskFreeRate,
                        volatility);

                    totalCurrentPnl += (theoreticalPrice - premium) * leg.Quantity;
                }
                expirationPnlSeries.Points.AddXY(price, totalExpirationPnl);
                currentPnlSeries.Points.AddXY(price, totalCurrentPnl);
            }

            chart.Series.Add(expirationPnlSeries);
            chart.Series.Add(currentPnlSeries);

            var zeroLine = new HorizontalLineAnnotation { AxisX = chartArea.AxisX, AxisY = chartArea.AxisY, LineColor = Color.Gray, LineWidth = 1, LineDashStyle = ChartDashStyle.Dot, Y = 0, IsInfinitive = true, ClipToChartArea = "MainArea" };
            chart.Annotations.Add(zeroLine);

            // Set explicit axis properties
            chartArea.AxisX.Minimum = Math.Floor(minPrice / interval) * interval;
            chartArea.AxisX.Maximum = Math.Ceiling(maxPrice / interval) * interval;
            chartArea.AxisX.Interval = interval;
            chartArea.AxisX.MajorGrid.Interval = interval;
            chartArea.AxisX.LabelStyle.Interval = interval;
            chartArea.AxisX.MajorTickMark.Interval = interval;

            chartArea.AxisX.Title = "Underlying Asset Price";
            chartArea.AxisY.Title = "Total Profit / Loss";
        }
    }
}