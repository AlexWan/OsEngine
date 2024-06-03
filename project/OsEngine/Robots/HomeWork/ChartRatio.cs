using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;

namespace OsEngine.Robots.HomeWork
{
    [Bot("ChartRatio")]
    public class ChartRatio : BotPanel
    {
        private StartProgram _startProgram;

        private BotTabSimple _tabOne;
        private BotTabSimple _tabTwo;
        private StrategyParameterString _regime;
        private StrategyParameterString _sideDeal;
        private StrategyParameterInt _timePaintChart;
        private StrategyParameterDecimal _levelSell;
        private StrategyParameterDecimal _levelBuy;
        private StrategyParameterDecimal _volumeOne;
        private StrategyParameterDecimal _volumeTwo;

        private WindowsFormsHost _host;

        private Chart _chart;
        private List<List<dynamic>> _chartRatio = new List<List<dynamic>>();
        private List<List<dynamic>> _chartDeal = new List<List<dynamic>>();

        public ChartRatio(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _startProgram = startProgram;


            TabCreate(BotTabType.Simple);
            _tabOne = TabsSimple[0];
            TabCreate(BotTabType.Simple);
            _tabTwo = TabsSimple[1];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });
            _timePaintChart = CreateParameter("Time Paint Chart", 60, 0, 1000, 1);
            _sideDeal = CreateParameter("Side Deal", "All Side", new string[] { "All Side", "Buy", "Sell" });            
            _levelSell = CreateParameter("Level Sell", 17.81m, 0, 1000, 0.0001m);
            _levelBuy = CreateParameter("Level Buy", 17.8m, 0, 1000, 0.0001m);
            _volumeOne = CreateParameter("Volume First Ticker", 10, 0.00001m, 1000000, 0.0001m);
            _volumeTwo = CreateParameter("Volume Second Ticker", 10, 0.00001m, 1000000, 0.0001m);

            this.ParamGuiSettings.Title = "Chart Ratio";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Chart Ratio");

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
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                Thread.Sleep(1000);

                if (_tabOne.Securiti != null && _tabTwo.Securiti != null)
                {
                    LoadRatioOnChart();
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

            _chart.BackColor = Color.FromArgb(17, 18, 23);            
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();

            ChartArea ratioArea = new ChartArea("ChartAreaRatio"); // создаём область на графике

            ratioArea.CursorX.IsUserSelectionEnabled = true;
            ratioArea.CursorX.IsUserEnabled = true;                // разрешаем пользователю изменять рамки представления
            ratioArea.CursorY.AxisType = AxisType.Secondary;
            ratioArea.Position.Height = 90;
            ratioArea.Position.X = 0;
            ratioArea.Position.Width = 100;
            ratioArea.Position.Y = 10;
           
            _chart.ChartAreas.Add(ratioArea); // добавляем область на чарт
                       
            // общее
            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                _chart.ChartAreas[i].CursorX.LineColor = Color.Red;
                _chart.ChartAreas[i].CursorX.LineWidth = 2;
                _chart.ChartAreas[i].BorderColor = Color.Black;
                _chart.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                _chart.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                _chart.ChartAreas[i].CursorX.LineColor = Color.Black;
                _chart.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                _chart.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                foreach (var axe in _chart.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.Gainsboro;
                }
            }

            // подписываемся на события изменения масштабов
            _chart.AxisScrollBarClicked += chart_AxisScrollBarClicked; // событие передвижения курсора
            _chart.AxisViewChanged += chart_AxisViewChanged; // событие изменения масштаба
            _chart.CursorPositionChanged += chart_CursorPositionChanged;// событие выделения диаграммы
        }

        private void LoadRatioOnChart() // формирует серии данных
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(LoadRatioOnChart));
                return;
            }

            if (_tabOne.CandlesAll == null ||
                _tabTwo.CandlesAll == null)
            {
                return;
            }

            if (_tabOne.CandlesAll.Count == 0 ||
                _tabTwo.CandlesAll.Count == 0)
            {
                return;
            }

            Series ratioSeries = new Series("SeriesRatio");
            ratioSeries.ChartType = SeriesChartType.Line;
            ratioSeries.YAxisType = AxisType.Secondary;// назначаем ей правую линейку по шкале Y (просто для красоты)
            ratioSeries.ChartArea = "ChartAreaRatio";// помещаем нашу коллекцию на ранее созданную область            
            ratioSeries.Color = Color.DeepSkyBlue;
            ratioSeries.BorderWidth = 2;

            Series dealSeries = new Series("SeriesDeal");
            dealSeries.ChartType = SeriesChartType.Point;
            dealSeries.YAxisType = AxisType.Secondary;// назначаем ей правую линейку по шкале Y (просто для красоты)
            dealSeries.ChartArea = "ChartAreaRatio";// помещаем нашу коллекцию на ранее созданную область            
            dealSeries.MarkerSize = 7;

            AddPointRatio(); // добавляем новую точку Ratio и точку сделки в массив и убираем из массива лишние данные

            for (int i = 0; i < _chartRatio.Count; i++)
            {
                ratioSeries.Points.AddXY(_chartRatio[i][0], _chartRatio[i][1]);

                if (_chartRatio[i][2] == 0)
                {
                    dealSeries.Points.AddXY(_chartRatio[i][0], double.NaN);
                }
                else
                {
                    dealSeries.Points.AddXY(_chartRatio[i][0], _chartRatio[i][1]);

                    if (_chartRatio[i][2] == Side.Buy)
                    {
                        dealSeries.Points[dealSeries.Points.Count - 1].Color = Color.Green;
                    }
                    else if (_chartRatio[i][2] == Side.Sell)
                    {
                        dealSeries.Points[dealSeries.Points.Count - 1].Color = Color.Red;
                    }
                }
            }
           
            double lastPoint = ratioSeries.Points[ratioSeries.Points.Count - 1].YValues[0];

            TradeLogic(lastPoint);

            _chart.Series.Clear();
            _chart.Series.Add(ratioSeries);
            _chart.Series.Add(dealSeries);
                        
            // выводим заголовок с данными Ratio
            _chart.Titles.Clear();
            Title mainTitle = new Title($"{_tabOne.Securiti.Name} / {_tabTwo.Securiti.Name} (Ratio = {ratioSeries.Points[ratioSeries.Points.Count - 1].YValues[0]})");
            mainTitle.Font = new Font("", 14);
            mainTitle.BackColor = Color.FromArgb(17, 18, 23);
            mainTitle.ForeColor = Color.Gainsboro;
            _chart.Titles.Add(mainTitle);

            ChartArea candleArea = _chart.ChartAreas.FindByName("ChartAreaRatio");
            if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible)
            
            {
                candleArea.AxisX.ScaleView.Scroll(_chart.ChartAreas[0].AxisX.Maximum);
            }

            ChartResize();
            _chart.Refresh();
        }

        // события
        private void chart_CursorPositionChanged(object sender, CursorEventArgs e)
        // событие изменение отображения диаграммы
        {
            ChartResize();
        }

        private void chart_AxisViewChanged(object sender, ViewEventArgs e)
        // событие изменение отображения диаграммы 
        {
            ChartResize();
        }

        private void chart_AxisScrollBarClicked(object sender, ScrollBarEventArgs e)
        // событие изменение отображения диаграммы
        {
            ChartResize();
        }

        private void ChartResize()
        {
            try
            {
                if (_chartRatio == null)
                {
                    return;
                }

                Series candleSeries = _chart.Series.FindByName("SeriesRatio");
                ChartArea candleArea = _chart.ChartAreas.FindByName("ChartAreaRatio");

                if (candleArea == null ||
                    candleSeries == null)
                {
                    return;
                }

                int startPozition = 0; 
                int endPozition = candleSeries.Points.Count; 

                if (_chart.ChartAreas[0].AxisX.ScrollBar.IsVisible)
                {
                    // если уже выбран какой-то диапазон, назначаем первую и последнюю исходя из этого диапазона

                    startPozition = Convert.ToInt32(candleArea.AxisX.ScaleView.Position);
                    endPozition = Convert.ToInt32(candleArea.AxisX.ScaleView.Position) +
                                  Convert.ToInt32(candleArea.AxisX.ScaleView.Size);
                }

                candleArea.AxisY2.Maximum = GetMaxValueOnChart(_chartRatio, startPozition, endPozition);
                candleArea.AxisY2.Minimum = GetMinValueOnChart(_chartRatio, startPozition, endPozition);

                _chart.Refresh();
            }
            catch (Exception error)
            {
                MessageBox.Show("Обибка при изменении ширины представления. Ошибка: " + error);
            }
        }

        private double GetMinValueOnChart(List<List<dynamic>> chartRatio, int start, int end)        
        {
            double result = double.MaxValue;

            for (int i = start; i < end && i < chartRatio.Count; i++)
            {
                if ((double)chartRatio[i][1] < result)
                {
                    result = (double)chartRatio[i][1];
                }
            }

            return result - ((double)_chartRatio[_chartRatio.Count - 1][1] * 0.0005);
        }

        private double GetMaxValueOnChart(List<List<dynamic>> chartRatio, int start, int end)
        {
            double result = double.MinValue;

            for (int i = start; i < end && i < chartRatio.Count; i++)
            {
                if ((double)chartRatio[i][1] > result)
                {
                    result = (double)chartRatio[i][1];
                }
            }

            return result + ((double)_chartRatio[_chartRatio.Count - 1][1] * 0.0005);
        }

        private void AddPointRatio()
        {
            List<dynamic> points = new List<dynamic>();

            points.Add(DateTime.Now.ToString("HH:mm:ss"));
            points.Add(_tabOne.CandlesAll[_tabOne.CandlesAll.Count - 1].Close / _tabTwo.CandlesAll[_tabTwo.CandlesAll.Count - 1].Close);
            points.Add(0);

            _chartRatio.Add(points);

            if (_chartRatio.Count > _timePaintChart.ValueInt)
            {                
                _chartRatio.RemoveAt(0);

                while (_chartRatio.Count != _timePaintChart.ValueInt)
                {
                    _chartRatio.RemoveAt(0);
                }
            }  
        }

        private void TradeLogic(double lastPoint)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            List<Position> positionsTabOne = _tabOne.PositionsOpenAll;
            List<Position> positionsTabTwo = _tabTwo.PositionsOpenAll;

            if (lastPoint <= (double)_levelBuy.ValueDecimal)
            {
                if (positionsTabOne != null && positionsTabOne.Count != 0)
                {
                    if (positionsTabOne[0].Direction == Side.Buy)
                    {
                        return;
                    }

                    if (_sideDeal.ValueString == "Sell")
                    {
                        _tabOne.CloseAtMarket(positionsTabOne[0], _volumeOne.ValueDecimal);
                        _tabTwo.CloseAtMarket(positionsTabTwo[0], _volumeTwo.ValueDecimal);
                        _chartRatio[_chartRatio.Count - 1][2] = Side.Buy;
                    }
                    if (_sideDeal.ValueString == "All Side")
                    {
                        _tabOne.CloseAtMarket(positionsTabOne[0], _volumeOne.ValueDecimal);
                        _tabTwo.CloseAtMarket(positionsTabTwo[0], _volumeTwo.ValueDecimal);
                        _tabOne.BuyAtMarket(_volumeOne.ValueDecimal);
                        _tabTwo.SellAtMarket(_volumeTwo.ValueDecimal);
                        _chartRatio[_chartRatio.Count - 1][2] = Side.Buy;
                    }                        
                }
                else if (_sideDeal.ValueString == "Buy" || _sideDeal.ValueString == "All Side")
                {
                    _tabOne.BuyAtMarket(_volumeOne.ValueDecimal);
                    _tabTwo.SellAtMarket(_volumeTwo.ValueDecimal);
                    _chartRatio[_chartRatio.Count - 1][2] = Side.Buy;
                }                
            }

            if (lastPoint >= (double)_levelSell.ValueDecimal)
            {
                if (positionsTabOne != null && positionsTabOne.Count != 0)
                {
                    if (positionsTabOne[0].Direction == Side.Sell)
                    {
                        return;
                    }

                    if (_sideDeal.ValueString == "Buy")
                    {
                        _tabOne.CloseAtMarket(positionsTabOne[0], _volumeOne.ValueDecimal);
                        _tabTwo.CloseAtMarket(positionsTabTwo[0], _volumeTwo.ValueDecimal);
                        _chartRatio[_chartRatio.Count - 1][2] = Side.Sell;
                    }

                    if (_sideDeal.ValueString == "All Side")
                    {
                        _tabOne.CloseAtMarket(positionsTabOne[0], _volumeOne.ValueDecimal);
                        _tabTwo.CloseAtMarket(positionsTabTwo[0], _volumeTwo.ValueDecimal);
                        _tabOne.SellAtMarket(_volumeOne.ValueDecimal);
                        _tabTwo.BuyAtMarket(_volumeTwo.ValueDecimal);
                        _chartRatio[_chartRatio.Count - 1][2] = Side.Sell;
                    }
                }
                else if (_sideDeal.ValueString == "Sell" || _sideDeal.ValueString == "All Side")
                {
                    _tabOne.SellAtMarket(_volumeOne.ValueDecimal);
                    _tabTwo.BuyAtMarket(_volumeTwo.ValueDecimal);
                    _chartRatio[_chartRatio.Count - 1][2] = Side.Sell;
                }                
            }
        }
               
        public override string GetNameStrategyType()
        {
            return "ChartRatio";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
