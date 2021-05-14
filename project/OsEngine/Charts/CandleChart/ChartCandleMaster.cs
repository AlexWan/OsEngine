/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.PrimeSettings;

namespace OsEngine.Charts.CandleChart
{
    /// <summary>
    /// Class-manager, managing the drawing of indicators, deals, candles on chart.
    /// Класс-менеджер, управляющий прорисовкой индикаторов, сделок, свечек на чарте.
    /// </summary>
    public class ChartCandleMaster
    {
        // service  сервис

        /// <summary>
        /// constructor
        /// Конструктор
        /// </summary>
        /// <param name="nameBoss">The name of robot that owns to chart/Имя робота которому принадлежит чарт</param>
        /// <param name="startProgram">program that created class/программа создавшая класс</param>
        public ChartCandleMaster(string nameBoss, StartProgram startProgram)
        {
            _name = nameBoss + "ChartMaster";
            _startProgram = startProgram;

            ChartCandle = new WinFormsChartPainter(nameBoss, startProgram);

            ChartCandle.ChartClickEvent += ChartCandle_ChartClickEvent;
            ChartCandle.LogMessageEvent += NewLogMessage;
            ChartCandle.ClickToIndexEvent += _chartCandle_ClickToIndexEvent;
            ChartCandle.SizeAxisXChangeEvent += ChartCandle_SizeAxisXChangeEvent;

            if(startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
            }
           
            _canSave = true;
        }

        /// <summary>
        /// upload settings from file
        /// Загрузить настройки из файла
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
                    while (!reader.EndOfStream)
                    {
                        string readerStr = reader.ReadLine();

                        if (readerStr == null)
                        {
                            continue;
                        }

                        string[] indicator = readerStr.Split('@');

                        if (indicator[indicator.Length - 1] == "IsScript")
                        {
                            IIndicator ind = IndicatorsFactory.CreateIndicatorByName(indicator[0], indicator[1], Convert.ToBoolean(indicator[3]));

                            if (ind == null)
                            {
                                LogMessageEvent("Indicator class " + indicator[0] + "do not exist in IndicatorFactory.cs", LogMessageType.Error);
                                continue;
                            }

                            CreateIndicator(ind, indicator[2]);
                            continue;
                        }

                        if (indicator[0] == "TickVolume")
                        {
                            CreateIndicator(new TickVolume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "StochRsi")
                        {
                            CreateIndicator(new StochRsi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "UltimateOscillator")
                        {
                            CreateIndicator(new UltimateOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "Vwap")
                        {
                            CreateIndicator(new Vwap(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
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
                            ChartCandle.CreateTickArea();
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
                        if (indicator[0] == "LinearRegressionCurve")
                        {
                            CreateIndicator(new LinearRegressionCurve(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        if (indicator[0] == "SimpleVWAP")
                        {
                            CreateIndicator(new SimpleVWAP(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }

                        if (indicator[0] == "DTD")
                        {
                            CreateIndicator(new DynamicTrendDetector(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }

                        if (indicator[0] == "AtrChannel")
                        {
                            CreateIndicator(new AtrChannel(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
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
        /// When loading data, robot tries to save it immediately, as indicators are created
        /// this variable protects the Save method from being saved incorrectly at this time
        /// во время загрузки данных, робот пытается тутже сохранять их,т.к. создаются индикаторы
        /// эта переменная защищает метод Save от ошибочного сохранения в это время
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// object-creating program
        /// программа создавшая объект
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// save settings to file
        /// Сохранить настройки в файл
        /// </summary>
        private void Save()
        {
            if (_canSave == false)
            {
                return;
            }

            if(_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {

                    if (_indicators != null)
                    {
                        for (int i = 0; i < _indicators.Count; i++)
                        {
                            if (_indicators[i].ValuesToChart != null &&
                                _indicators[i].ValuesToChart.Count != 0)
                            {
                                writer.WriteLine(_indicators[i].GetType().Name + "@" +
                                                 _indicators[i].Name + "@" + _indicators[i].NameArea +
                                                 "@" + _indicators[i].CanDelete);
                            }
                            else
                            {
                                writer.WriteLine(_indicators[i].GetType().Name + "@" +
                                                 _indicators[i].Name + "@" + _indicators[i].NameArea +
                                                 "@" + _indicators[i].CanDelete + "@IsScript");
                            }
                        }
                    }
                    if (ChartCandle.AreaIsCreate("TradeArea") == true)
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
        /// delete file with settings
        /// Удалить файл настроек
        /// </summary>
        public void Delete()
        {
            try
            {
                for (int i = 0; _indicators != null && i < _indicators.Count; i++)
                {
                    _indicators[i].Delete();
                }

                if (File.Exists(@"Engine\" + Name + @".txt"))
                {
                    File.Delete(@"Engine\" + Name + @".txt");
                }
                if (ChartCandle != null)
                {
                    ChartCandle.Delete();
                }

                _myCandles = null;
                _chartElements = null;
                _alertArray = null;
                _indicators = null;
                _myPosition = null;
                
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        private string _name;

        /// <summary>
        /// The unique name of the chart maker
        /// Уникальное имя мастера чартов
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// chart
        /// чарт
        /// </summary>
        public IChartPainter ChartCandle;

        // context menu контекстное меню

        /// <summary>
        /// user clicked on chart
        /// Пользователь кликнул по чарту
        /// </summary>
        private void ChartCandle_ChartClickEvent(ChartClickType buttonType)
        {
            try
            {
                ChartClickEvent?.Invoke(buttonType);

                if (buttonType != ChartClickType.RightButton)
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

        public event Action<ChartClickType> ChartClickEvent;

        /// <summary>
        /// Reassemble the context menu for chart
        /// Пересобрать контекстное меню для чарта
        /// </summary>
        private void ReloadContext()
        {
            try
            {
                List<MenuItem> menuRedact = null;

                List<MenuItem> menuDelete = null;

                if (_indicators != null)
                {
                    menuRedact = new List<MenuItem>();
                    menuDelete = new List<MenuItem>();
                    for (int i = 0; i < _indicators.Count; i++)
                    {
                        menuRedact.Add(new MenuItem(_indicators[i].GetType().Name));
                        menuRedact[menuRedact.Count - 1].Click += RedactContextMenu_Click;
                        if (_indicators[i].CanDelete)
                        {
                            menuDelete.Add(new MenuItem(_indicators[i].GetType().Name));
                            menuDelete[menuDelete.Count - 1].Click += DeleteContextMenu_Click;
                        }
                    }
                }

                if (ChartCandle.AreaIsCreate("TradeArea") == true)
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
                items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem1,
                    new MenuItem[]
                {new MenuItem(OsLocalization.Charts.ChartMenuItem2,
                        new MenuItem[]{new MenuItem(OsLocalization.Charts.ChartMenuItem3),
                            new MenuItem(OsLocalization.Charts.ChartMenuItem4)}),

                new MenuItem(OsLocalization.Charts.ChartMenuItem5,
                    new MenuItem[]{
                        new MenuItem(OsLocalization.Charts.ChartMenuItem6),
                        new MenuItem(OsLocalization.Charts.ChartMenuItem7),
                        new MenuItem(OsLocalization.Charts.ChartMenuItem8),
                        new MenuItem(OsLocalization.Charts.ChartMenuItem9)})}

                ));

                items[items.Count - 1].MenuItems[0].MenuItems[0].Click += ChartBlackColor_Click;
                items[items.Count - 1].MenuItems[0].MenuItems[1].Click += ChartWhiteColor_Click;

                items[items.Count - 1].MenuItems[1].MenuItems[0].Click += ChartCrossToPosition_Click;
                items[items.Count - 1].MenuItems[1].MenuItems[1].Click += ChartRombToPosition_Click;
                items[items.Count - 1].MenuItems[1].MenuItems[2].Click += ChartCircleToPosition_Click;
                items[items.Count - 1].MenuItems[1].MenuItems[3].Click += ChartTriangleToPosition_Click;

                items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem10));
                items[items.Count - 1].Click += ChartHideIndicators_Click;

                items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem11));
                items[items.Count - 1].Click += ChartShowIndicators_Click;

                if (menuRedact != null)
                {
                    items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem12, menuRedact.ToArray()));
                    items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem13, menuDelete.ToArray()));
                }

                items.Add(new MenuItem(OsLocalization.Charts.ChartMenuItem14));
                items[items.Count - 1].Click += CreateIndicators_Click;

                ContextMenu menu = new ContextMenu(items.ToArray());

                ChartCandle.ShowContextMenu(menu);
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// user has selected the crosshair in context menu to draw trades on chart
        /// Пользователь выбрал в контекстном меню перекрестие для прорисовки сделок на чарте
        /// </summary>
        private void ChartCrossToPosition_Click(object sender, EventArgs e)
        {
            ChartCandle.SetPointType(PointType.Cross);
            ChartCandle.ProcessPositions(_myPosition);
        }

        /// <summary>
        /// user has chosen the diamond for drawing deals on chart in context menu
        /// Пользователь выбрал в контекстном меню ромб для прорисовки сделок на чарте
        /// </summary>
        private void ChartRombToPosition_Click(object sender, EventArgs e)
        {
            ChartCandle.SetPointType(PointType.Romb);
            ChartCandle.ProcessPositions(_myPosition);
        }

        /// <summary>
        /// user has chosen the circle for drawing deals on chart in context menu
        /// Пользователь выбрал в контекстном меню кружок для прорисовки сделок на чарте
        /// </summary>
        private void ChartCircleToPosition_Click(object sender, EventArgs e)
        {
            ChartCandle.SetPointType(PointType.Circle);
            ChartCandle.ProcessPositions(_myPosition);
        }

        /// <summary>
        /// user has chosen a triangle in context menu to draw deals on chart
        /// Пользователь выбрал в контекстном меню треугольник для прорисовки сделок на чарте
        /// </summary>
        private void ChartTriangleToPosition_Click(object sender, EventArgs e)
        {
            ChartCandle.SetPointType(PointType.TriAngle);
            ChartCandle.ProcessPositions(_myPosition);
        }

        /// <summary>
        /// user has selected a dark color setting in context menu
        /// Пользователь выбрал в контекстном меню темную настройку цветов
        /// </summary>
        private void ChartBlackColor_Click(object sender, EventArgs e)
        {
            ChartCandle.SetBlackScheme();
        }

        /// <summary>
        /// user has selected a light color setting in context menu
        /// Пользователь выбрал в контекстном меню светлую настройку цветов
        /// </summary>
        private void ChartWhiteColor_Click(object sender, EventArgs e)
        {
            ChartCandle.SetWhiteScheme();
        }

        /// <summary>
        /// user has chosen to hide areas in context menu
        /// Пользователь выбрал в контекстном меню спрятать области
        /// </summary>
        private void ChartHideIndicators_Click(object sender, EventArgs e)
        {
            ChartCandle.HideAreaOnChart();
        }

        /// <summary>
        /// user has chosen to show all areas in context menu
        /// Пользователь выбрал в контекстном меню показать все области
        /// </summary>
        private void ChartShowIndicators_Click(object sender, EventArgs e)
        {
            ChartCandle.ShowAreaOnChart();
        }

        /// <summary>
        /// user has chosen to edit indicator in context menu
        /// Пользователь выбрал в контекстном меню редактировать индикатор
        /// </summary>
        private void RedactContextMenu_Click(object sender, EventArgs e)
        {
            try
            {
                MenuItem item = (MenuItem)sender;
                _indicators[item.Index].ShowDialog();
                _indicators[item.Index].Save();
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// user has chosen to delete indicator in context menu
        /// Пользователь выбрал в контекстном меню удалить индикатор
        /// </summary>
        private void DeleteContextMenu_Click(object sender, EventArgs e)
        {
            try
            {
                if (((MenuItem)sender).Text == @"Trades")
                {
                    ChartCandle.DeleteTickArea();
                    Save();
                    return;
                }
                int number = ((MenuItem)sender).Index;

                if ((_indicators == null || _indicators.Count <= number))
                {
                    return;
                }

                List<IIndicator> indicators = _indicators.FindAll(candle => candle.CanDelete == true);
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
        /// user has chosen to create an indicator in context menu
        /// Пользователь выбрал в контекстном меню создать индикатор
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

        // work on changing trade points depending on the size of representation on the X-axis
        // работа по изменению точек сделок в зависимости от размера представления на оси Х

        private void ChartCandle_SizeAxisXChangeEvent(int newSizeX)
        {
            //  return;

            if (_myPosition == null ||
                _myPosition.Count == 0)
            {
                return;
            }

            if (_lastAbsoluteSizeX == newSizeX)
            {
                return;
            }

            if (_lastTipeSizeX != ChartPositionTradeSize.Size4 && newSizeX < 200)
            {
                _lastTipeSizeX = ChartPositionTradeSize.Size4;
                ChartCandle.SetPointSize(ChartPositionTradeSize.Size4);
                ChartCandle.ProcessPositions(_myPosition);
            }

            else if (_lastTipeSizeX != ChartPositionTradeSize.Size3 && newSizeX > 200 && newSizeX < 500)
            {
                _lastTipeSizeX = ChartPositionTradeSize.Size3;
                ChartCandle.SetPointSize(ChartPositionTradeSize.Size3);
                ChartCandle.ProcessPositions(_myPosition);
            }
            else if (_lastTipeSizeX != ChartPositionTradeSize.Size2 && newSizeX > 500 && newSizeX < 1300)
            {
                _lastTipeSizeX = ChartPositionTradeSize.Size2;
                ChartCandle.SetPointSize(ChartPositionTradeSize.Size2);
                ChartCandle.ProcessPositions(_myPosition);
            }
            else if (_lastTipeSizeX != ChartPositionTradeSize.Size1 && newSizeX > 1300)
            {
                _lastTipeSizeX = ChartPositionTradeSize.Size1;
                ChartCandle.SetPointSize(ChartPositionTradeSize.Size1);
                ChartCandle.ProcessPositions(_myPosition);
            }

            _lastAbsoluteSizeX = newSizeX;
        }

        private int _lastAbsoluteSizeX;

        private ChartPositionTradeSize _lastTipeSizeX;
        
        // indicator management управление индикаторами

        /// <summary>
        /// Indicators
        /// Индикаторы
        /// </summary>
        public List<IIndicator> Indicators
        {
            get { return _indicators; }
        }
        private List<IIndicator> _indicators;

        /// <summary>
        /// to create an area for drawing ticks on chart
        /// создать на чарте область для прорисовки тиков
        /// </summary>
        public void CreateTickChart()
        {
            ChartCandle.CreateTickArea();
            Save();
        }

        /// <summary>
        /// to create a new indicator. If there is already one with this name, the existing one returned
        /// создать новый индикатор. Если уже есть с таким именем, возвращается имеющийся
        /// </summary>
        /// <param name="indicator">an indicator to be integrated into chart/индикатор, который нужно интегрировать в чарт</param>
        /// <param name="nameArea">ame of area where indicator should be drawn/имя области, на которой следует прорисовать индикатор</param>
        /// <returns></returns>
        public IIndicator CreateIndicator(IIndicator indicator, string nameArea)
        {
            try
            {
                indicator.NameArea = nameArea;

                if (_indicators != null)
                {
                    // check if there is such indicator in the collection
                    // проверяем, есть ли такой индикатор в коллекции
                    for (int i = 0; i < _indicators.Count; i++)
                    {
                        if (_indicators[i].Name == indicator.Name)
                        {
                            return _indicators[i];
                        }
                    }
                }

                bool inNewArea = true;

                if (ChartCandle.AreaIsCreate(nameArea))
                {
                    inNewArea = false;
                }

                List<List<decimal>> values = indicator.ValuesToChart;

                if (values != null)
                { // прогружаем классические индикаторы
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (inNewArea == false)
                        {
                            indicator.NameSeries = ChartCandle.CreateSeries(nameArea,
                                indicator.TypeIndicator, indicator.Name + i);
                        }
                        else
                        {
                            string area = ChartCandle.CreateArea(nameArea, 15);
                            indicator.NameSeries = ChartCandle.CreateSeries(area,
                                indicator.TypeIndicator, indicator.Name + i);
                        }
                    }
                }
                else
                {
                    Aindicator ind = (Aindicator)indicator;

                    List<IndicatorDataSeries> series = ind.DataSeries;

                    if (series == null ||
                        series.Count == 0)
                    {
                        NewLogMessage("Indicator " + ind.Name + " don`t have a value series.", LogMessageType.Error);
                        return null;
                    }

                    for (int i = 0; i < series.Count; i++)
                    {

                        if (inNewArea == false)
                        {
                            series[i].NameSeries = ChartCandle.CreateSeries(nameArea,
                                series[i].ChartPaintType, indicator.Name + i);
                        }
                        else
                        {
                            string area = ChartCandle.CreateArea(nameArea, 15);

                            series[i].NameSeries = ChartCandle.CreateSeries(area,
                                series[i].ChartPaintType, indicator.Name + i);
                        }
                    }
                }



                if (_indicators == null)
                {
                    _indicators = new List<IIndicator>();
                    _indicators.Add(indicator);
                }
                else
                {
                    _indicators.Add(indicator);
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
        /// indicator has changed. It need to redraw
        /// Индикатор изменился. Надо перерисовать
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        private void indicator_NeadToReloadEvent(IIndicator indicator)
        {
            try
            {

                List<Candle> candles = _myCandles;
                if (candles != null)
                {
                    indicator.Process(candles);
                }

                ChartCandle.RePaintIndicator(indicator);
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// Delete indicator
        /// Удалить индикатор
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        public void DeleteIndicator(IIndicator indicator)
        {
            try
            {
                ChartCandle.DeleteIndicator(indicator);

                indicator.Delete();

                if (_indicators.Count == 1)
                {
                    _indicators = null;
                }
                else
                {
                    _indicators.Remove(indicator);
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
        /// is there an indicator created
        /// создан ли индикатор
        /// </summary>
        /// <param name="name">indicator name/имя индикатора</param>
        /// <returns>true-created//true - создан//false-no//false - нет</returns>
        public bool IndicatorIsCreate(string name)
        {
            return ChartCandle.IndicatorIsCreate(name);
        }

        /// <summary>
        /// if area on chart is created
        /// создана ли область на графике
        /// </summary>
        /// <param name="name">area name/имя области</param>
        /// <returns></returns>
        public bool AreaIsCreate(string name)
        {
            return ChartCandle.AreaIsCreate(name);
        }
        // chart element management
        // управление элементами чарта

        /// <summary>
        /// collection of custom items created on chart
        /// коллекция пользовательских элементов созданная на чарте
        /// </summary>
        private List<IChartElement> _chartElements;

        /// <summary>
        /// add a custom element to chart
        /// добавить на график пользовательский элемент
        /// </summary>
        /// <param name="element">element/элемент</param>
        public void SetChartElement(IChartElement element)
        {
            try
            {
                if (_chartElements == null)
                {
                    _chartElements = new List<IChartElement>();
                }
                // 1  If such an element has already been added, replace it
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
                // 2 sending it over for a drawing.
                // 2 отправляем на прорисовку

                ChartCandle.ProcessElem(myElement);
            }
            catch (Exception error)
            {
                NewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Remove custom item from chart
        /// удалить с графика пользовательский элемент
        /// </summary>
        /// <param name="element">element/элемент</param>
        public void DeleteChartElement(IChartElement element)
        {
            ChartCandle.ProcessClearElem(element);

            try
            {
                // if there is such an element in the collection of elements - delete
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
        /// remove all custom elements from chart
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
        /// Incoming event: item should be deleted
        /// входящее событие: элемент нужно удалить
        /// </summary>
        /// <param name="element">element/элемент</param>
        void myElement_DeleteEvent(IChartElement element)
        {
            DeleteChartElement(element);
        }

        /// <summary>
        /// Incoming event: element must be redrawn
        /// входящее событие: элемент нужно перерисовать
        /// </summary>
        /// <param name="element">element/элемент</param>
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
            ChartCandle.ProcessElem(element);
        }
      
        // Alert management управление Алертов

        /// <summary>
        /// array with alerts
        /// массив с алертами
        /// </summary>
        private List<IIAlert> _alertArray;

        /// <summary>
        /// draw alerts
        /// порисовать алерты
        /// </summary>
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
                        ChartCandle.PaintAlert((AlertToChart)alertArray[i]);
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
        /// to clear chart of Alert
        /// очистить чарт от Алертов
        /// </summary>
        private void EraseAlertFromChart(List<IIAlert> alertArray)
        {
            ChartCandle.ClearAlerts(alertArray);
        }

        // candle drawing прорисовка свечей

        /// <summary>
        /// candles available on chart
        /// свечи доступные на чарте
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// last price
        /// последняя цена
        /// </summary>
        private decimal _lastPrice;

        /// <summary>
        /// last number of candles in the array
        /// последнее кол-во свечей в массиве
        /// </summary>
        private int _lastCount;

        /// <summary>
        /// Last time the candles entered
        /// время последнего вхождения свечей
        /// </summary>
        private DateTime _lastCandleIncome = DateTime.MinValue;

        /// <summary>
        /// upgrade candles
        /// обновить свечи
        /// </summary>
        /// <param name="candles">candles/свечи</param>
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
                    && _startProgram != StartProgram.IsTester &&
                    _startProgram != StartProgram.IsOsData &&
                    _lastPrice == candles[candles.Count - 1].Close)
                {
                    // only update candles when they've really been updated.
                    // обновляем свечи только когда они действительно обновились
                    return;
                }

                bool canReload = _startProgram != StartProgram.IsOsOptimizer;

                _lastCount = candles.Count;
                _lastPrice = candles[candles.Count - 1].Close;
                _myCandles = candles;

                if (ChartCandle != null)
                {
                    if (canReload)
                    {
                        _lastCandleIncome = DateTime.Now;
                        ChartCandle.ProcessCandles(candles);
                        ChartCandle.ProcessPositions(_myPosition);
                    }

                    if (_indicators != null)
                    {
                        for (int i = 0; i < _indicators.Count; i++)
                        {
                            _indicators[i].Process(candles);

                            if (canReload)
                            {
                                ChartCandle.ProcessIndicator(_indicators[i]);
                            }
                        }
                    }
                    if (canReload && _alertArray != null && _alertArray.Count != 0)
                    {
                        ChartCandle.ClearAlerts(_alertArray);

                        for (int i = 0; _alertArray != null && i < _alertArray.Count; i++)
                        {
                            if (_alertArray[i].TypeAlert == AlertType.ChartAlert)
                            {
                                ChartCandle.PaintAlert((AlertToChart)_alertArray[i]);
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

        // drawing ticks прорисовка тиков

        /// <summary>
        /// ticks in connector have been updated
        /// в коннекторе обновились тики
        /// </summary>
        /// <param name="trades">ticks/тики</param>
        public void SetTick(List<Trade> trades)
        {
            if(_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                ChartCandle.ProcessTrades(trades);
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        //position drawing прорисовка позиций

        /// <summary>
        /// array with positions
        /// массив с позициями
        /// </summary>
        private List<Position> _myPosition;

        /// <summary>
        /// in connector changed the deal
        /// в коннекторе изменилась сделка
        /// </summary>
        /// <param name="position">deal/сделка</param>
        public void SetPosition(List<Position> position)
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            _myPosition = position;
            ChartCandle.ProcessPositions(position);
        }

        // management управление

        /// <summary>
        /// to start drawing this chart on window
        /// начать прорисовывать данный чарт на окне
        /// </summary>
        public void StartPaint(System.Windows.Controls.Grid gridChart, WindowsFormsHost host, Rectangle rectangle)
        {
            try
            {
                ChartCandle.StartPaintPrimeChart(gridChart, host, rectangle);
                ChartCandle.ProcessCandles(_myCandles);
                ChartCandle.ProcessPositions(_myPosition);

                for (int i = 0; _indicators != null && i < _indicators.Count; i++)
                {
                    ChartCandle.ProcessIndicator(_indicators[i]);
                }

                for (int i = 0; _chartElements != null && i < _chartElements.Count; i++)
                {
                    ChartCandle.ProcessElem(_chartElements[i]);
                }

                ChartCandle.ClearAlerts(_alertArray);

                for (int i = 0; _alertArray != null && i < _alertArray.Count; i++)
                {
                    if (_alertArray[i].TypeAlert == AlertType.ChartAlert)
                    {
                        ChartCandle.PaintAlert((AlertToChart)_alertArray[i]);
                    }
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// stop drawing this chart on window
        /// прекратить прорисовывать данный чарт на окне
        /// </summary>
        public void StopPaint()
        {
            ChartCandle.StopPaint();

            if (_grid != null)
            {
                _grid.Children.Clear();
                _grid = null;
            }
        }

        /// <summary>
        /// clear chart
        /// очистить чарт
        /// </summary>
        public void Clear()
        {
            _myCandles = null;
            ChartCandle.ClearDataPointsAndSizeValue();
            _myPosition = null;

            for (int i = 0; _indicators != null && i < _indicators.Count; i++)
            {
                _indicators[i].Clear();
            }
        }

        public void ClearTimePoints()
        {
            ChartCandle.ClearDataPointsAndSizeValue();
        }

        public int GetSelectCandleNumber()
        {
            return ChartCandle.GetCursorSelectCandleNumber();
        }

        public decimal GetCursorSelectPrice()
        {
            return ChartCandle.GetCursorSelectPrice();
        }

        public void RemoveCursor()
        {
            ChartCandle.RemoveCursor();
        }

        /// <summary>
        /// move the chart view to time
        /// переместить представление чарта к времени
        /// </summary>
        /// <param name="time">time/время</param>
        public void GoChartToTime(DateTime time)
        {
            ChartCandle.GoChartToTime(time);
        }

        public void GoChartToIndex(int index)
        {
            if (index < 0)
            {
                return;
            }
            ChartCandle.GoChartToIndex(index);
        }

        /// <summary>
        /// to take a list of chart areas
        /// взять список областей чарта
        /// </summary>
        /// <returns>an array of areas. If not - null/массив областей. Если нет - null</returns>
        public List<string> GetChartAreas()
        {
            return ChartCandle.GetAreasNames();
        }
        // tool display window
        // окно отображения инструмента

        public void StartPaintChartControlPanel(System.Windows.Controls.Grid grid)
        {
            if (_label == null)
            {
                _label = new System.Windows.Controls.Label();

                _label.Margin = new Thickness(0, 0, 25, 0);
                _label.VerticalAlignment = VerticalAlignment.Top;
                _label.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }

            _grid = grid;
            PaintLabelOnSlavePanel();
        }

        /// <summary>
        /// to load a new tool into chart
        /// подгрузить в чарт новый инструмент
        /// </summary>
        /// <param name="security">security/бумага</param>
        /// <param name="timeFrameBuilder">an object that stores candles construction settings/объект хранящий в себе настройки построения свечей</param>
        /// <param name="portfolioName">portfolio/портфель</param>
        /// <param name="serverType">server type/тип сервера</param>
        public void SetNewSecurity(string security, TimeFrameBuilder timeFrameBuilder, string portfolioName, ServerType serverType)
        {
            if (_securityOnThisChart == security &&
                _timeFrameSecurity == timeFrameBuilder.TimeFrame &&
                serverType == _serverType &&
                _candleCreateMethodTypeOnThisChart == timeFrameBuilder.CandleCreateMethodType)
            {
                return;
            }

            if (ChartCandle != null)
            {
                ChartCandle.ClearDataPointsAndSizeValue();
                if (timeFrameBuilder.CandleCreateMethodType != CandleCreateMethodType.Simple)
                {
                    ChartCandle.SetNewTimeFrame(TimeSpan.FromSeconds(1), timeFrameBuilder.TimeFrame);
                }
                else
                {
                    ChartCandle.SetNewTimeFrame(timeFrameBuilder.TimeFrameTimeSpan, timeFrameBuilder.TimeFrame);
                }
            }

            string lastSecurity = _securityOnThisChart;
            List<Position> positions = _myPosition;
            _timeFrameBuilder = timeFrameBuilder;
            _securityOnThisChart = security;
            _timeFrameSecurity = timeFrameBuilder.TimeFrame;
            _serverType = serverType;
            _candleCreateMethodTypeOnThisChart = timeFrameBuilder.CandleCreateMethodType;

            Clear();
            PaintLabelOnSlavePanel();

            if (lastSecurity == security && positions != null)
            {
                SetPosition(positions);
            }
        }

        private TimeFrameBuilder _timeFrameBuilder;

        private ServerType _serverType;

        /// <summary>
        /// security drawing on chart
        /// бумага отображаемая на чарте
        /// </summary>
        private string _securityOnThisChart;

        /// <summary>
        /// timeframe of this chart's paper
        /// таймфрейм бумаги этого чарта
        /// </summary>
        private TimeFrame _timeFrameSecurity;

        /// <summary>
        /// candles built method
        /// метод построения свечей на чарте
        /// </summary>
        private CandleCreateMethodType _candleCreateMethodTypeOnThisChart;

        private System.Windows.Controls.Label _label;

        private System.Windows.Controls.Grid _grid;

        /// <summary>
        /// get chart information
        /// получить информацию о чарте
        /// </summary>
        public string GetChartLabel()
        {
            string security = _securityOnThisChart;

            if (string.IsNullOrEmpty(security))
            {
                security = "Unknown";
            }

            string label = _serverType.ToString();

            if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                label += " / " + security + " / " + _timeFrameSecurity;
            }
            else
            {
                label += " / " + security + " / " + _timeFrameBuilder.CandleCreateMethodType;
            }

            return label;
        }

        private void PaintLabelOnSlavePanel()
        {
            if (_grid == null)
            {
                return;
            }

            if (!_label.Dispatcher.CheckAccess())
            {
                _label.Dispatcher.Invoke(PaintLabelOnSlavePanel);
                return;
            }

            string security = _securityOnThisChart;

            if (string.IsNullOrEmpty(security))
            {
                security = "Unknown";
            }

            _label.Content = _serverType;

            if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                _label.Content += " / " + security + " / " + _timeFrameSecurity;
            }
            else
            {
                _label.Content += " / " + security + " / " + _timeFrameBuilder.CandleCreateMethodType;
            }

            _grid.Children.Clear();
            _grid.Children.Add(_label);
        }
        // pattern management
        // работа с паттернами

        void _chartCandle_ClickToIndexEvent(int index)
        {
            if (ClickToIndexEvent != null)
            {
                ClickToIndexEvent(index);
            }
        }

        public event Action<int> ClickToIndexEvent;

        public void PaintInDifColor(int indexStart, int indexEnd, string seriesName)
        {
            ChartCandle.PaintInDifColor(indexStart, indexEnd, seriesName);
        }

        public void RefreshChartColor()
        {

            ChartCandle.RefreshChartColor();
        }

        // logging логирование

        /// <summary>
        /// send an error message upstairs
        /// выслать наверх сообщение об ошибке
        /// </summary>
        private void SendErrorMessage(Exception error)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(error.ToString(), LogMessageType.Error);
            }
            else
            {
                // if no one's subscribed to us and there's a mistake
                // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// an incoming event from class where chart drawn
        /// входящее событие из класса в котором прорисовывается чарт
        /// </summary>
        void NewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                // if no one's subscribed to us and there's a mistake
                // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
