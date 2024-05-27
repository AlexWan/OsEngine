using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Globalization;
using System.Threading;


namespace OsEngine.Robots.HomeWork
{
    [Bot("ChartBot")]
    public class ChartBot: BotPanel
    {
        private BotTabSimple _tab;        
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volume;
        private StrategyParameterDecimal _stopLoss;
        private StrategyParameterDecimal _takeProfit;

        private WindowsFormsHost _host;

        private Chart _chart;
        private Candle[] _candleArray;

        public DateTime Time;

        public ChartBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });
            _volume = CreateParameter("Volume of trade", 1, 0.1m, 10, 0.1m);
            _stopLoss = CreateParameter("Stop Loss, points of price step", 1m, 1, 10, 1);
            _takeProfit = CreateParameter("Take Profit, points of price ste", 1m, 1, 10, 1);

            this.ParamGuiSettings.Title = "TableBot Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Table Parameters");

            CreateChart();

            customTab.AddChildren(_host);

            StartThread();
        }

        private void StartThread()
        {           
            Thread worker = new Thread(StartPaintChart) { IsBackground = true };
            worker.Start();
        }

        private void StartPaintChart()
        {            
            long countCandles = 0;

            while (true)
            {
                Thread.Sleep(1000);

                if (_tab.Securiti != null)
                {
                    if (_tab.CandlesFinishedOnly != null)
                    {
                        if (countCandles != _tab.CandlesFinishedOnly.Count)
                        {
                            countCandles = _tab.CandlesFinishedOnly.Count;
                            SetCandle();
                        }                                       
                    }
                    
                }                                
            }
        }

        private void CreateChart()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateChart));
                return;
            }

            _host = new WindowsFormsHost();
            _chart = new Chart();
            _host.Child = _chart;            
            _host.Child.Show();

            _chart.Series.Clear();
            _chart.ChartAreas.Clear();

            ChartArea candleArea = new ChartArea("ChartAreaCandleOnTable"); // создаём область на графике
            candleArea.CursorX.IsUserSelectionEnabled = true;
            candleArea.CursorX.IsUserEnabled = true;                // разрешаем пользователю изменять рамки представления
            candleArea.CursorY.AxisType = AxisType.Secondary;
            candleArea.Position.Height = 70;
            candleArea.Position.X = 0;
            candleArea.Position.Width = 100;
            candleArea.Position.Y = 0; //чертa
            candleArea.AxisY.Maximum = 5000; // Установка максимума в NaN для автоматического масштабирования
            candleArea.AxisY.Minimum = 2000; // Установка минимума в NaN для автоматического масштабирования

            // Пересчет масштаба осей
            //candleArea.RecalculateAxesScale();

            _chart.ChartAreas.Add(candleArea); // добавляем область на чарт

            Series candleSeries = new Series("SeriesCandleOnTable") // создаём для нашей области коллекцию значений
            {
                ChartType = SeriesChartType.Candlestick, // назначаем этой коллекции тип "Свечи"
                YAxisType = AxisType.Secondary,// назначаем ей правую линейку по шкале Y (просто для красоты) Везде ж так
                ChartArea = "ChartAreaCandleOnTable", // помещаем нашу коллекцию на ранее созданную область
                ShadowOffset = 2 // наводим тень
            };

            //candleSeries.Points.AddXY(10, 10);
            //candleSeries.Points.AddXY(100, 100);

            _chart.Series.Add(candleSeries); // добавляем коллекцию на чарт

            // объём
            /*ChartArea volumeArea = new ChartArea("ChartAreaVolume") // создаём область для объёма
            {
                CursorX = { IsUserEnabled = true }, //чертa
                CursorY = { AxisType = AxisType.Secondary }, // ось У правая
                AlignWithChartArea = "ChartAreaCandle",// выравниваем по верхней диаграмме
                Position = { Height = 30, X = 0, Width = 100, Y = 70 },
                AxisX = { Enabled = AxisEnabled.False }// отменяем прорисовку оси Х
            };

            _chart.ChartAreas.Add(volumeArea);

            Series volumeSeries = new Series("SeriesVolume") // создаём для нашей области коллекцию значений
            {
                ChartType = SeriesChartType.Column, // назначаем этой коллекции тип "столбцы"
                YAxisType = AxisType.Secondary, // назначаем ей правую линейку по шкале Y (просто для красоты)
                ChartArea = "ChartAreaVolume", // помещаем нашу коллекцию на ранее созданную область
                ShadowOffset = 2 // наводим тень на плетень
            };

            _chart.Series.Add(volumeSeries);*/

            // общее
            foreach (ChartArea area in _chart.ChartAreas)
            {
                // Делаем курсор по Y красным и толстым
                area.CursorX.LineColor = Color.Red;
                area.CursorX.LineWidth = 2;
            }

            //SetCandle();

        }

        private void SetCandle()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(SetCandle));
                return;
            }

            _candleArray = _tab.CandlesFinishedOnly.ToArray();

            Series candleSeries = new Series("SeriesCandleOnTable")
            {
                ChartType = SeriesChartType.Candlestick,// назначаем этой коллекции тип "Свечи"
                YAxisType = AxisType.Secondary,// назначаем ей правую линейку по шкале Y (просто для красоты)
                ChartArea = "ChartAreaCandleOnTable",// помещаем нашу коллекцию на ранее созданную область
                ShadowOffset = 2,  // наводим тень
                YValuesPerPoint = 4 // насильно устанавливаем число У точек для серии
            };

            for (int i = 0; i < _candleArray.Length; i++)
            {
                // забиваем новую свечку
                candleSeries.Points.AddXY(i, _candleArray[i].Low, _candleArray[i].High, _candleArray[i].Open,
                    _candleArray[i].Close);

                // подписываем время
               /* candleSeries.Points[candleSeries.Points.Count - 1].AxisLabel =
                    _candleArray[i].Time.ToString(CultureInfo.InvariantCulture);*/

                // разукрышиваем в привычные цвета
                if (_candleArray[i].Close > _candleArray[i].Open)
                {
                    candleSeries.Points[candleSeries.Points.Count - 1].Color = Color.Green;
                }
                else
                {
                    candleSeries.Points[candleSeries.Points.Count - 1].Color = Color.Red;
                }
            }
            _chart.Series.Clear();
            _chart.Series.Add(candleSeries);
            _chart.Refresh();
        }

        public override string GetNameStrategyType()
        {
            return "ChartBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
           
        }
    }
}
