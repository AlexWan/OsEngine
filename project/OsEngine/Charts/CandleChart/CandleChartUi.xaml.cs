using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Entity;
using System.Drawing;

namespace OsEngine.Charts.CandleChart
{
    /// <summary>
    /// Логика взаимодействия для CandleChartUi.xaml
    /// </summary>
    public partial class CandleChartUi : Window
    {
        public CandleChartUi(string nameUniq, StartProgram startProgram)
        {
            InitializeComponent();
            _chart = new ChartCandleMaster(nameUniq, startProgram);
            _chart.StartPaint(GridChart, ChartHostPanel, RectChart);

            this.Closed += CandleChartUi_Closed;
        }

        private void CandleChartUi_Closed(object sender, EventArgs e)
        {
            _chart.StopPaint();
        }

        ChartCandleMaster _chart;

        public void ProcessCandles(List<Candle> candles)
        {
            _chart.SetCandles(candles);
        }

        public void ChangeTitle(string newTitle)
        {
            this.Title = newTitle;
        }

        public void SetColorToCandle(int indexCandle, Color newColor)
        {
             IChartPainter painter = _chart.ChartCandle;

             painter.PaintInDifColor(indexCandle, indexCandle+1, "SeriesCandle");
        }
    }
}
