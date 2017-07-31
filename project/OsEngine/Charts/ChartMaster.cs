/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;

namespace OsEngine.Charts
{

    /// <summary>
    /// класс-менеджер. Управляющий прорисовкой на чарте: индикаторов, сделок, свечек.
    /// </summary>
    public class ChartMaster
    {

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="nameBoss">имя робота которому принадлежит чарт</param>
        public ChartMaster(string nameBoss)
        {
            _name = nameBoss + "ChartMaster";

            _chartCandle = new ChartPainter(nameBoss);
            _chartCandle.GetChart().Click += ChartMasterOneSecurity_Click;
            _chartCandle.LogMessageEvent += NewLogMessage;
            _reloadState = ChartReloadState.OneSecond;

            Load();
            _canSave = true;
        }

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @".txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @".txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out _reloadState);
                    while (!reader.EndOfStream)
                    {
                        string readerStr = reader.ReadLine();

                        if (readerStr == null)
                        {
                            continue;
                        }

                        string[] indicator = readerStr.Split('@');

                        if (indicator[0] == "KalmanFilter")
                        {
                            CreateIndicator(new KalmanFilter(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "PivotPoints")
                        {
                            CreateIndicator(new PivotPoints(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Pivot")
                        {
                            CreateIndicator(new Pivot(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "VolumeOscillator")
                        {
                            CreateIndicator(new VolumeOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "ParabolicSAR")
                        {
                            CreateIndicator(new ParabolicSaR(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "BfMfi")
                        {
                            CreateIndicator(new BfMfi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "BullsPower")
                        {
                            CreateIndicator(new BullsPower(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "BearsPower")
                        {
                            CreateIndicator(new BearsPower(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Cmo")
                        {
                            CreateIndicator(new Cmo(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Cci")
                        {
                            CreateIndicator(new Cci(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "StandardDeviation")
                        {
                            CreateIndicator(new StandardDeviation(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "MovingAverage")
                        {
                            CreateIndicator(new MovingAverage(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Bollinger")
                        {
                            CreateIndicator(new Bollinger(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Fractal")
                        {
                            CreateIndicator(new Fractal(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "ForceIndex")
                        {
                            CreateIndicator(new ForceIndex(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "OnBalanceVolume")
                        {
                            CreateIndicator(new OnBalanceVolume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "StochasticOscillator")
                        {
                            CreateIndicator(new StochasticOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Rsi")
                        {
                            CreateIndicator(new Rsi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Roc")
                        {
                            CreateIndicator(new Roc(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Rvi")
                        {
                            CreateIndicator(new Rvi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Volume")
                        {
                            CreateIndicator(new Volume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "AwesomeOscillator")
                        {
                            CreateIndicator(new AwesomeOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "AccumulationDistribution")
                        {
                            CreateIndicator(new AccumulationDistribution(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Adx")
                        {
                            CreateIndicator(new Adx(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Atr")
                        {
                            CreateIndicator(new Atr(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Alligator")
                        {
                            CreateIndicator(new Alligator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Trades")
                        {
                            _chartCandle.CreateTickArea();
                        }
                        if (indicator[0] == "PriceChannel")
                        {
                            CreateIndicator(new PriceChannel(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "PriceOscillator")
                        {
                            CreateIndicator(new PriceOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "MacdHistogram")
                        {
                            CreateIndicator(new MacdHistogram(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "MacdLine")
                        {
                            CreateIndicator(new MacdLine(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Momentum")
                        {
                            CreateIndicator(new Momentum(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "MoneyFlowIndex")
                        {
                            CreateIndicator(new MoneyFlowIndex(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Envelops")
                        {
                            CreateIndicator(new Envelops(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "EfficiencyRatio")
                        {
                            CreateIndicator(new EfficiencyRatio(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Line")
                        {
                            CreateIndicator(new CandleChart.Indicators.Line(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "AdaptiveLookBack")
                        {
                            CreateIndicator(new AdaptiveLookBack(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "IvashovRange")
                        {
                            CreateIndicator(new IvashovRange(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Ac")
                        {
                            CreateIndicator(new Ac(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "VerticalHorizontalFilter")
                        {
                            CreateIndicator(new VerticalHorizontalFilter(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "WilliamsRange")
                        {
                            CreateIndicator(new WilliamsRange(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Trix")
                        {
                            CreateIndicator(new Trix(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Ichimoku")
                        {
                            CreateIndicator(new Ichimoku(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "TradeThread")
                        {
                            CreateIndicator(new TradeThread(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// можно ли сохранять индикаторы.
        /// Это нужно в момент их создания
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// частота обновления чарта
        /// </summary>
        private ChartReloadState _reloadState;

        /// <summary>
        /// сохранить настройки в файл
        /// </summary>
        private void Save()
        {
            try
            {
                if (_canSave == false)
                {
                    return;
                }
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(_reloadState);

                    if (_indicatorsCandles != null)
                    {
                        for (int i = 0; i < _indicatorsCandles.Count; i++)
                        {
                            string s = _indicatorsCandles[i].GetType().Name;
                            writer.WriteLine(_indicatorsCandles[i].GetType().Name + "@" +
                                             _indicatorsCandles[i].Name + "@" + _indicatorsCandles[i].NameArea +
                                             "@" + _indicatorsCandles[i].CanDelete);
                        }
                    }
                    if (_chartCandle.GetChartArea("TradeArea") != null)
                    {
                        writer.WriteLine("Trades");
                    }
                   
                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        /// <summary>
        /// удалить файл настроек
        /// </summary>
        public void Delete()
        {
            try
            {
                while (_indicatorsCandles != null)
                {
                    DeleteIndicator(_indicatorsCandles[0]);
                }

                if (File.Exists(@"Engine\" + Name + @".txt"))
                {
                    File.Delete(@"Engine\" + Name + @".txt");
                }
                if (_chartCandle != null)
                {
                    _chartCandle.Delete();
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        private string _name;

        /// <summary>
        /// уникальное имя мастера чартов
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// чарт
        /// </summary>
        private ChartPainter _chartCandle;

// контекстное меню

        /// <summary>
        /// пользователь кликнул по чарту
        /// </summary>
        private void ChartMasterOneSecurity_Click(object sender, EventArgs e)
        {
            try
            {
                if (((MouseEventArgs)e).Button != MouseButtons.Right)
                {
                    return;
                }
                ReloadContext();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        /// <summary>
        /// пересобрать контекстное меню для чарта
        /// </summary>
        private void ReloadContext()
        {
            try
            {
                List<MenuItem> menuRedact = null;

                List<MenuItem> menuDelete = null;

                if (_indicatorsCandles != null)
                {
                    menuRedact = new List<MenuItem>();
                    menuDelete = new List<MenuItem>();
                    for (int i = 0; i < _indicatorsCandles.Count; i++)
                    {
                        menuRedact.Add(new MenuItem(_indicatorsCandles[i].GetType().Name));
                        menuRedact[menuRedact.Count - 1].Click += RedactContextMenu_Click;
                        if (_indicatorsCandles[i].CanDelete)
                        {
                            menuDelete.Add(new MenuItem(_indicatorsCandles[i].GetType().Name));
                            menuDelete[menuDelete.Count - 1].Click += DeleteContextMenu_Click;
                        }
                    }
                }

                if (_chartCandle.GetChartArea("TradeArea") != null)
                {
                    if (menuRedact == null)
                    {
                        menuRedact = new List<MenuItem>();
                        menuDelete = new List<MenuItem>();
                    }
                    menuDelete.Add(new MenuItem("Trades"));
                    menuDelete[menuDelete.Count - 1].Click += DeleteContextMenu_Click;
                }

                List<MenuItem> items;

                items = new List<MenuItem>();
                items.Add(new MenuItem("Отрисовка чарта", 
                    new MenuItem[] 
                {new MenuItem("Цветовая схема", new MenuItem[]{new MenuItem("Тёмная"),new MenuItem("Светлая")}),
                new MenuItem("Фигура сделки", new MenuItem[]{new MenuItem("Ромб"),new MenuItem("Кружок"),new MenuItem("Треугольник(тормозит при дебаггинге)")}),
                new MenuItem("Размер фигуры", new MenuItem[]{new MenuItem("6"),new MenuItem("10"),new MenuItem("14")}),
                new MenuItem("Частота обновления", new MenuItem[]{new MenuItem("Максимально часто"),new MenuItem("Раз в секунду(оптимально)"),new MenuItem("Раз в пять секунд")})}
                
                ));

                items[items.Count - 1].MenuItems[0].MenuItems[0].Click += ChartBlackColor_Click;
                items[items.Count - 1].MenuItems[0].MenuItems[1].Click += ChartWhiteColor_Click;

                items[items.Count - 1].MenuItems[1].MenuItems[0].Click += ChartRombToPosition_Click;
                items[items.Count - 1].MenuItems[1].MenuItems[1].Click += ChartCircleToPosition_Click;
                items[items.Count - 1].MenuItems[1].MenuItems[2].Click += ChartTriangleToPosition_Click;

                items[items.Count - 1].MenuItems[2].MenuItems[0].Click += ChartMinPointSize_Click;
                items[items.Count - 1].MenuItems[2].MenuItems[1].Click += ChartMiddlePointSize_Click;
                items[items.Count - 1].MenuItems[2].MenuItems[2].Click += ChartMaxPointSize_Click;

                items[items.Count - 1].MenuItems[3].MenuItems[0].Click += ChartReloadMaxFast_Click;
                items[items.Count - 1].MenuItems[3].MenuItems[1].Click += ChartReloadOneSecond_Click;
                items[items.Count - 1].MenuItems[3].MenuItems[2].Click += ChartReloadFiveSeconds_Click;

                items.Add(new MenuItem("Скрыть области"));
                items[items.Count - 1].Click += ChartHideIndicators_Click;

                items.Add(new MenuItem("Показать области"));
                items[items.Count - 1].Click += ChartShowIndicators_Click;

                if (menuRedact != null)
                {
                    items.Add(new MenuItem("Редактировать индикаторы", menuRedact.ToArray()));
                    items.Add(new MenuItem("Удалить индикатор", menuDelete.ToArray()));
                }

                items.Add(new MenuItem("Добавить индикатор"));
                items[items.Count - 1].Click += CreateIndicators_Click;

                ContextMenu menu = new ContextMenu(items.ToArray());

                _chartCandle.GetChart().ContextMenu = menu;
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: размер для точки отображающей позиции на графике: минимальный
        /// </summary>
        private void ChartMinPointSize_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointSize(6);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: размер для точки отображающей позиции на графике: средний
        /// </summary>
        private void ChartMiddlePointSize_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointSize(10);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: размер для точки отображающей позиции на графике: максимальный
        /// </summary>
        private void ChartMaxPointSize_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointSize(14);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: ромб для прорисовки сделок на чарте
        /// </summary>
        private void ChartRombToPosition_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointType(PointType.Romb);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: кружок для прорисовки сделок на чарте
        /// </summary>
        private void ChartCircleToPosition_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointType(PointType.Circle);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: треугольник для прорисовки сделок на чарте
        /// </summary>
        private void ChartTriangleToPosition_Click(object sender, EventArgs e)
        {
            _chartCandle.SetPointType(PointType.TriAngle);
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню максимальную скорость прорисовку чарта
        /// </summary>
        private void ChartReloadMaxFast_Click(object sender, EventArgs e)
        {
            _reloadState = ChartReloadState.Maximum;
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню среднюю скорость прорисовку чарта
        /// </summary>
        private void ChartReloadOneSecond_Click(object sender, EventArgs e)
        {
            _reloadState = ChartReloadState.OneSecond;
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню медленную скорость прорисовку чарта
        /// </summary>
        private void ChartReloadFiveSeconds_Click(object sender, EventArgs e)
        {
            _reloadState = ChartReloadState.FiveSecond;
        }


        /// <summary>
        /// пользователь выбрал в контекстном меню: настройку цветов
        /// </summary>
        private void ChartBlackColor_Click(object sender, EventArgs e)
        {
            _chartCandle.SetBlackScheme();
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: настройку цветов
        /// </summary>
        private void ChartWhiteColor_Click(object sender, EventArgs e)
        {
            _chartCandle.SetWhiteScheme();
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: спрятать области
        /// </summary>
        private void ChartHideIndicators_Click(object sender, EventArgs e)
        {
            _chartCandle.HideAreaOnChart();
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: показать все области
        /// </summary>
        private void ChartShowIndicators_Click(object sender, EventArgs e)
        {
            _chartCandle.ShowAreaOnChart();
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: редактировать индикатор
        /// </summary>
        private void RedactContextMenu_Click(object sender, EventArgs e)
        {
            try
            {
                MenuItem item = (MenuItem)sender;
                _indicatorsCandles[item.Index].ShowDialog();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: удалить индикатор
        /// </summary>
        private void DeleteContextMenu_Click(object sender, EventArgs e)
        {
            try
            {
                if (((MenuItem)sender).Text == @"Trades")
                {
                    _chartCandle.DeleteTickArea();
                    Save();
                    return;
                }
                int number = ((MenuItem)sender).Index;

                if ((_indicatorsCandles == null || _indicatorsCandles.Count <= number))
                {
                    return;
                }

                List<IIndicatorCandle> indicators = _indicatorsCandles.FindAll(candle => candle.CanDelete == true);
                if (number < indicators.Count)
                {
                    DeleteIndicator(indicators[number]);
                }
                
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        /// <summary>
        /// пользователь выбрал в контекстном меню: создать индикатор
        /// </summary>
        private void CreateIndicators_Click(object sender, EventArgs e)
        {
            try
            {
                IndicarotCreateUi ui = new IndicarotCreateUi(this);
                ui.Show();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

// управление индикаторами

        /// <summary>
        /// индикаторы
        /// </summary>
        private List<IIndicatorCandle> _indicatorsCandles;

        /// <summary>
        /// создать на чарте область для прорисовки тиков
        /// </summary>
        public void CreateTickChart()
        {
            _chartCandle.CreateTickArea();
            Save();
        }

        /// <summary>
        /// создать новый индикатор. Если уже есть с таким именем, то не создаётся а возвращается имеющийся
        /// </summary>
        /// <param name="indicator">индикатор, который нужно интегрировать в чарт</param>
        /// <param name="nameArea">имя области, на которой следует прорисовать индикатор</param>
        /// <returns></returns>
        public IIndicatorCandle CreateIndicator(IIndicatorCandle indicator, string nameArea)
        {
            try
            {
                indicator.NameArea = nameArea;

                if (_indicatorsCandles != null)
                {
                    // проверяем, есть ли такой индикатор в коллекции
                    for (int i = 0; i < _indicatorsCandles.Count; i++)
                    {
                        if (_indicatorsCandles[i].Name == indicator.Name)
                        {
                            return _indicatorsCandles[i];
                        }
                    }
                }

                bool inNewArea = true;

                if (_chartCandle.GetChartArea(nameArea) != null)
                {
                    inNewArea = false;
                }

                List<List<decimal>> values = indicator.ValuesToChart;

                for (int i = 0; i < values.Count; i++)
                {
                    if (inNewArea == false)
                    {
                        indicator.NameSeries = _chartCandle.CreateSeries(_chartCandle.GetChartArea(nameArea),
                            indicator.TypeIndicator, indicator.Name + i);
                    }
                    else
                    {
                        ChartArea area = _chartCandle.CreateArea(nameArea, 15);
                        indicator.NameSeries = _chartCandle.CreateSeries(area,
                            indicator.TypeIndicator, indicator.Name + i);
                    }
                }

                if (_indicatorsCandles == null)
                {
                    _indicatorsCandles = new List<IIndicatorCandle>();
                    _indicatorsCandles.Add(indicator);
                }
                else
                {
                    _indicatorsCandles.Add(indicator);
                }

                Save();
                ReloadContext();
                indicator.NeadToReloadEvent += indicator_NeadToReloadEvent;
                indicator_NeadToReloadEvent(indicator);
                return indicator;
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
                return null;
            }  
        }

        /// <summary>
        /// индикатор изменился. Надо перерисовать
        /// </summary>
        /// <param name="indicator">индикатор</param>
        private void indicator_NeadToReloadEvent(IIndicatorCandle indicator)
        {
            try
            {

                List<Candle> candles = _myCandles;
                if (candles != null)
                {
                    indicator.Process(candles);
                }

                _chartCandle.RePaintIndicator(indicator);
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// удалить индикатор
        /// </summary>
        /// <param name="indicator">индикатор</param>
        public void DeleteIndicator(IIndicatorCandle indicator)
        {
            try
            {
                _chartCandle.DeleteIndicator(indicator);

                indicator.Delete();

                if (_indicatorsCandles.Count == 1)
                {
                    _indicatorsCandles = null;
                }
                else
                {
                    _indicatorsCandles.Remove(indicator);
                }
                Save();
                ReloadContext();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }  
        }

        /// <summary>
        /// создан ли индикатор
        /// </summary>
        /// <param name="name">имя индикатора</param>
        /// <returns>true - создан false - нет</returns>
        public bool IndicatorIsCreate(string name)
        {
            return _chartCandle.IndicatorIsCreate(name);
        }

        /// <summary>
        /// создана ли область на графике
        /// </summary>
        /// <param name="name">имя области</param>
        /// <returns></returns>
        public bool AreaIsCreate(string name)
        {
            return _chartCandle.AreaIsCreate(name);
        }

// управление элементами чарта

        /// <summary>
        /// коллекция пользовательских элементов созданная на чарте
        /// </summary>
        private List<IChartElement> _chartElements;

        /// <summary>
        /// добавить на график пользовательский элемент
        /// </summary>
        /// <param name="element">элемент</param>
        public void SetChartElement(IChartElement element)
        {
            try
            {
                if (_chartElements == null)
                {
                    _chartElements = new List<IChartElement>();
                }
                // 1 если такой элемент уже добавлен, заменяем его
                IChartElement myElement = _chartElements.Find(chartElement => chartElement.UniqName == element.UniqName);
                if (myElement != null)
                {
                    _chartElements.Remove(myElement);
                    myElement.UpdeteEvent -= myElement_UpdeteEvent;
                    myElement.DeleteEvent -= myElement_DeleteEvent;
                }

                myElement = element;

                _chartElements.Add(myElement);

                myElement.UpdeteEvent += myElement_UpdeteEvent;
                myElement.DeleteEvent += myElement_DeleteEvent;

                // 2 отправляем на прорисовку

                _chartCandle.PaintElem(myElement);
            }
            catch (Exception error)
            {
                NewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить с графика пользовательский элемент
        /// </summary>
        /// <param name="element">элемент</param>
        public void DeleteChartElement(IChartElement element)
        {
            _chartCandle.ClearElem(element);

            try
            {
                // если такой элемент есть в коллекции элементов - удаляем
                if (_chartElements != null && _chartElements.Count != 0)
                {
                    IChartElement elem = _chartElements.Find(chartElement => chartElement.UniqName == element.UniqName);

                    if (elem != null)
                    {
                        _chartElements.Remove(elem);
                    }
                }
            }
            catch (Exception error)
            {
                NewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить все пользовательские элементы с графика
        /// </summary>
        public void DeleteAllChartElement()
        {
            if (_chartElements == null || _chartElements.Count <= 0)
            {
                return;
            }

            for (int i = 0; _chartElements != null && i < _chartElements.Count; i++)
            {
                DeleteChartElement(_chartElements[i]);
            }
        }

        /// <summary>
        /// входящее событие: элемент нужно удалить
        /// </summary>
        /// <param name="element">элемент</param>
        void myElement_DeleteEvent(IChartElement element)
        {
            DeleteChartElement(element);
        }

        /// <summary>
        /// входящее событие: элемент нужно перерисовать
        /// </summary>
        /// <param name="element">элемент</param>
        void myElement_UpdeteEvent(IChartElement element)
        {
            if (_chartElements == null)
            {
                _chartElements = new List<IChartElement>();
            }
            if (_chartElements.Find(el => el.UniqName == element.UniqName) == null)
            {
                _chartElements.Add(element);
            }
            _chartCandle.PaintElem(element);
        }

// управление Алертов

        private List<IIAlert> _alertArray;

        public void PaintAlerts(List<IIAlert> alertArray)
        {
            try
            {
                _alertArray = alertArray;

                EraseAlertFromChart(alertArray);

                if (alertArray == null || _myCandles == null)
                {
                    return;
                }

                for (int i = 0; i < alertArray.Count; i++)
                {
                    if (alertArray[i].TypeAlert == AlertType.ChartAlert)
                    {
                        _chartCandle.PaintAlert((AlertToChart)alertArray[i]);
                    }
                }
            }
            catch (Exception error)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// очистить чарт от Алертов
        /// </summary>
        private void EraseAlertFromChart(List<IIAlert> alertArray)
        {
            _chartCandle.ClearAlerts(alertArray);
        }

// входящие события

        /// <summary>
        /// в коннекторе обновились тики
        /// </summary>
        /// <param name="trades">тики</param>
        public void SetTick(List<Trade> trades)
        {
            try
            {
                _chartCandle.PaintTrades(trades);
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        private List<Candle> _myCandles;

        private decimal _lastPrice;

        private int _lastCount;

        /// <summary>
        /// время последнего вхождения свечей
        /// </summary>
        private DateTime _lastCandleIncome = DateTime.MinValue;

        /// <summary>
        /// обновить свечи
        /// </summary>
        /// <param name="candles">свечи</param>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void SetCandles(List<Candle> candles)
        {
            try
            {
                if (candles == null)
                {
                    return;
                }

                if (_myCandles != null && _lastCount == candles.Count
                    && !ServerMaster.IsTester &&
                    _lastPrice == candles[candles.Count - 1].Close)
                {
                    // обновляем свечи только когда они действительно обновились
                    return;
                }

                bool canReload = true;

                if (!ServerMaster.IsTester &&
                    _reloadState == ChartReloadState.OneSecond 
                    && _lastCandleIncome.AddSeconds(1) > DateTime.Now)
                {
                    canReload = false;
                }

                if (!ServerMaster.IsTester && 
                    _reloadState == ChartReloadState.FiveSecond 
                    && _lastCandleIncome.AddSeconds(5) > DateTime.Now)
                {
                    canReload = false;
                }

                
                _lastCount = candles.Count;
                _lastPrice = candles[candles.Count - 1].Close;

                _myCandles = candles;

                if (_chartCandle != null)
                {
                    if (canReload)
                    {
                        _lastCandleIncome = DateTime.Now;
                        _chartCandle.PaintCandles(candles);
                        _chartCandle.PaintPositions(_myPosition);
                        
                    }

                    if (_indicatorsCandles != null)
                    {
                        for (int i = 0; i < _indicatorsCandles.Count; i++)
                        {
                            _indicatorsCandles[i].Process(candles);

                            if (canReload)
                            {
                                _chartCandle.PaintIndicator(_indicatorsCandles[i]);
                            }
                        }
                    }
                    if (canReload)
                    {
                        _chartCandle.ClearAlerts(_alertArray);

                        for (int i = 0; _alertArray != null && i < _alertArray.Count; i++)
                        {
                            if (_alertArray[i].TypeAlert == AlertType.ChartAlert)
                            {
                                _chartCandle.PaintAlert((AlertToChart)_alertArray[i]);
                            }
                        }
                    }
                }

            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        private List<Position> _myPosition;

        /// <summary>
        /// в коннекторе изменилась сделка
        /// </summary>
        /// <param name="position">сделка</param>
        public void SetPosition(List<Position> position)
        {
            _myPosition = position;
            _chartCandle.PaintPositions(position);
        }

// управление

        /// <summary>
        /// начать прорисовывать данный чарт на окне
        /// </summary>
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            try
            {
                _chartCandle.StartPaint(host, rectangle);
                _chartCandle.PaintPositions(_myPosition);

                _chartCandle.PaintCandles(_myCandles);

                for (int i = 0; _indicatorsCandles != null && i < _indicatorsCandles.Count; i++)
                {
                    _chartCandle.PaintIndicator(_indicatorsCandles[i]);
                }
                
                for (int i = 0; _chartElements != null && i < _chartElements.Count; i++)
                {
                    _chartCandle.PaintElem(_chartElements[i]);
                }

                _chartCandle.ClearAlerts(_alertArray);

                for (int i = 0; _alertArray != null && i < _alertArray.Count; i++)
                {
                    if (_alertArray[i].TypeAlert == AlertType.ChartAlert)
                    {
                        _chartCandle.PaintAlert((AlertToChart)_alertArray[i]);
                    }
                }

            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// прекратить прорисовывать данный чарт на окне
        /// </summary>
        public void StopPaint()
        {
            _chartCandle.StopPaint();
        }

        /// <summary>
        /// очистить чарт
        /// </summary>
        public void Clear()
        {
            _myCandles = null;
            _chartCandle.Clear();
            _myPosition = null;

            for (int i = 0; _indicatorsCandles != null && i < _indicatorsCandles.Count; i++)
            {
                _indicatorsCandles[i].Clear();
            }
        }

        /// <summary>
        /// взять чарт
        /// </summary>
        public Chart GetChart()
        {
            return _chartCandle.GetChart();
        }

        /// <summary>
        /// взять область по имени
        /// </summary>
        /// <param name="name">имя области</param>
        public ChartArea GetChartArea(string name)
        {
            return _chartCandle.GetChartArea(name);
        }

        /// <summary>
        /// переместить представление чарта к времени
        /// </summary>
        /// <param name="time">время</param>
        public void GoChartToTime(DateTime time)
        {
            _chartCandle.GoChartToTime(time);
        }

        /// <summary>
        /// взять список областей чарта
        /// </summary>
        /// <returns>массив областей. Если нет - null</returns>
        public string[] GetChartAreas()
        {
            try
            {
                List<string> areas = new List<string>();

                Chart chart;

                chart = _chartCandle.GetChart();


                for (int i = 0; i < chart.ChartAreas.Count; i++)
                {
                    areas.Add(chart.ChartAreas[i].Name);
                }


                return areas.ToArray();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
                return null;
            }
        }

// события

        /// <summary>
        /// выслать наверх сообщение об ошибке
        /// </summary>
        private void SendErrorMessage(Exception error)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(error.ToString(), LogMessageType.Error);
            }
            else
            { // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// входящее событие из класса в котором прорисовывается чарт
        /// </summary>
        void NewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string,LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// частота обновления чарта
    /// </summary>
    public enum ChartReloadState
    {
        /// <summary>
        /// максимум
        /// </summary>
        Maximum,
        /// <summary>
        /// раз в секунду
        /// </summary>
        OneSecond,

        /// <summary>
        /// раз в пять секунд
        /// </summary>
        FiveSecond
    }
}
