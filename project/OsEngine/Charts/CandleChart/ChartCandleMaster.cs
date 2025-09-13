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
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.Charts.CandleChart
{
    /// <summary>
    /// Class-manager, managing the drawing of indicators, deals, candles on chart.
    /// Класс-менеджер, управляющий прорисовкой индикаторов, сделок, свечек на чарте.
    /// </summary>
    public class ChartCandleMaster
    {
        // service сервис

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

            if (_startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
                _canSave = true;
            }
        }

        private void UpDateChartPainter()
        {
            if (ChartCandle != null)
            {
                ChartCandle.Delete();
                ChartCandle.ChartClickEvent -= ChartCandle_ChartClickEvent;
                ChartCandle.LogMessageEvent -= NewLogMessage;
                ChartCandle.ClickToIndexEvent -= _chartCandle_ClickToIndexEvent;
                ChartCandle.SizeAxisXChangeEvent -= ChartCandle_SizeAxisXChangeEvent;
                ChartCandle.LastXIndexChangeEvent -= ChartCandle_LastXIndexChangeEvent;
            }
            ChartCandle = new WinFormsChartPainter(_name, _startProgram);
            ChartCandle.ChartClickEvent += ChartCandle_ChartClickEvent;
            ChartCandle.LogMessageEvent += NewLogMessage;
            ChartCandle.ClickToIndexEvent += _chartCandle_ClickToIndexEvent;
            ChartCandle.SizeAxisXChangeEvent += ChartCandle_SizeAxisXChangeEvent;
            ChartCandle.LastXIndexChangeEvent += ChartCandle_LastXIndexChangeEvent;
            SetNewTimeFrameToChart(_timeFrameBuilder);  //AVP добавил, чтоб ChartCandle знал, с каким он таймфреймом. (Это важно для скринера, с отложенным созданием Чарта)  

            if (_indicators != null)
            {
                for (int i = 0; i < _indicators.Count; i++)
                {
                    LoadIndicatorOnChart(_indicators[i]);
                }
            }

        }

        /// <summary>
        /// Установить TimeFrame на чарт    
        /// Set TimeFrame to Chart
        /// </summary>
        /// <param name="timeFrameBuilder"></param>
        private void SetNewTimeFrameToChart(TimeFrameBuilder timeFrameBuilder)      //AVP
        {
            if (timeFrameBuilder == null || ChartCandle == null)
            {
                return;
            }
            if (timeFrameBuilder.CandleCreateMethodType != "Simple")
            {
                ChartCandle.SetNewTimeFrame(TimeSpan.FromSeconds(1), timeFrameBuilder.TimeFrame);
            }
            else
            {
                ChartCandle.SetNewTimeFrame(timeFrameBuilder.TimeFrameTimeSpan, timeFrameBuilder.TimeFrame);
            }
        }

        /// <summary>
        /// upload settings from file
        /// Загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
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

                        if (indicator[0] == "DonchianChannel")
                        {
                            CreateIndicator(new DonchianChannel(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "TickVolume")
                        {
                            CreateIndicator(new TickVolume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "StochRsi")
                        {
                            CreateIndicator(new StochRsi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "UltimateOscillator")
                        {
                            CreateIndicator(new UltimateOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Vwap")
                        {
                            CreateIndicator(new Vwap(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "KalmanFilter")
                        {
                            CreateIndicator(new KalmanFilter(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "PivotPoints")
                        {
                            CreateIndicator(new PivotPoints(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Pivot")
                        {
                            CreateIndicator(new Pivot(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "VolumeOscillator")
                        {
                            CreateIndicator(new VolumeOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "ParabolicSaR")
                        {
                            CreateIndicator(new ParabolicSaR(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "BfMfi")
                        {
                            CreateIndicator(new BfMfi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "BullsPower")
                        {
                            CreateIndicator(new BullsPower(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "BearsPower")
                        {
                            CreateIndicator(new BearsPower(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Cmo")
                        {
                            CreateIndicator(new Cmo(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Cci")
                        {
                            CreateIndicator(new Cci(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "StandardDeviation")
                        {
                            CreateIndicator(new StandardDeviation(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "MovingAverage")
                        {
                            CreateIndicator(new MovingAverage(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Bollinger")
                        {
                            CreateIndicator(new Bollinger(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Fractal")
                        {
                            CreateIndicator(new Fractal(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "ForceIndex")
                        {
                            CreateIndicator(new ForceIndex(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "OnBalanceVolume")
                        {
                            CreateIndicator(new OnBalanceVolume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "StochasticOscillator")
                        {
                            CreateIndicator(new StochasticOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Rsi")
                        {
                            CreateIndicator(new Rsi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Roc")
                        {
                            CreateIndicator(new Roc(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Rvi")
                        {
                            CreateIndicator(new Rvi(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Volume")
                        {
                            CreateIndicator(new Volume(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "AwesomeOscillator")
                        {
                            CreateIndicator(new AwesomeOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "AccumulationDistribution")
                        {
                            CreateIndicator(new AccumulationDistribution(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Adx")
                        {
                            CreateIndicator(new Adx(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Atr")
                        {
                            CreateIndicator(new Atr(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Alligator")
                        {
                            CreateIndicator(new Alligator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "PriceChannel")
                        {
                            CreateIndicator(new PriceChannel(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "PriceOscillator")
                        {
                            CreateIndicator(new PriceOscillator(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "MacdHistogram")
                        {
                            CreateIndicator(new MacdHistogram(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "MacdLine")
                        {
                            CreateIndicator(new MacdLine(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Momentum")
                        {
                            CreateIndicator(new Momentum(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "MoneyFlowIndex")
                        {
                            CreateIndicator(new MoneyFlowIndex(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Envelops")
                        {
                            CreateIndicator(new Envelops(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "EfficiencyRatio")
                        {
                            CreateIndicator(new EfficiencyRatio(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Line")
                        {
                            CreateIndicator(new CandleChart.Indicators.Line(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "AdaptiveLookBack")
                        {
                            CreateIndicator(new AdaptiveLookBack(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "IvashovRange")
                        {
                            CreateIndicator(new IvashovRange(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Ac")
                        {
                            CreateIndicator(new Ac(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "VerticalHorizontalFilter")
                        {
                            CreateIndicator(new VerticalHorizontalFilter(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "WilliamsRange")
                        {
                            CreateIndicator(new WilliamsRange(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Trix")
                        {
                            CreateIndicator(new Trix(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "Ichimoku")
                        {
                            CreateIndicator(new Ichimoku(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "TradeThread")
                        {
                            CreateIndicator(new TradeThread(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "LinearRegressionCurve")
                        {
                            CreateIndicator(new LinearRegressionCurve(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "SimpleVWAP")
                        {
                            CreateIndicator(new SimpleVWAP(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "DTD")
                        {
                            CreateIndicator(new DynamicTrendDetector(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else if (indicator[0] == "AtrChannel")
                        {
                            CreateIndicator(new AtrChannel(indicator[1], Convert.ToBoolean(indicator[3])), indicator[2]);
                        }
                        else
                        {
                            NewLogMessage("Chart can`t load indicator with name: " + indicator[0], LogMessageType.Error);
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

            if (_startProgram == StartProgram.IsOsOptimizer)
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
                    if (ChartCandle != null &&
                        ChartCandle.AreaIsCreate("TradeArea") == true)
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
                _bindChart = null;

                if (_indicators != null)
                {
                    for (int i = 0; _indicators != null && i < _indicators.Count; i++)
                    {
                        _indicators[i].NeedToReloadEvent -= indicator_NeedToReloadEvent;
                        _indicators[i].Clear();
                        _indicators[i].Delete();
                    }

                    _indicators.Clear();
                    _indicators = null;
                }

                if (_startProgram != StartProgram.IsOsOptimizer)
                {
                    if (File.Exists(@"Engine\" + Name + @".txt"))
                    {
                        File.Delete(@"Engine\" + Name + @".txt");
                    }
                }

                if (ChartCandle != null)
                {
                    ChartCandle.ChartClickEvent -= ChartCandle_ChartClickEvent;
                    ChartCandle.ClickToIndexEvent -= _chartCandle_ClickToIndexEvent;
                    ChartCandle.SizeAxisXChangeEvent -= ChartCandle_SizeAxisXChangeEvent;
                    ChartCandle.ClearDataPointsAndSizeValue();
                    ChartCandle.Delete();
                    ChartCandle.LogMessageEvent -= NewLogMessage;
                    ChartCandle = null;
                }

                _myCandles = null;

                if (_chartElements != null)
                {
                    for (int i = 0; i < _chartElements.Count; i++)
                    {
                        _chartElements[i].UpdateEvent -= myElement_UpdeteEvent;
                        _chartElements[i].DeleteEvent -= myElement_DeleteEvent;
                        _chartElements[i].Delete();
                    }

                    _chartElements.Clear();
                    _chartElements = null;
                }
                if (_alertArray != null)
                {
                    _alertArray.Clear();
                    _alertArray = null;
                }

                if (_myPosition != null)
                {
                    _myPosition = null;
                }

                if (_myStopLimit != null)
                {
                    _myStopLimit.Clear();
                    _myStopLimit = null;
                }
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

        /// <summary>
        /// On / Off events regime
        /// </summary>
        public bool EventIsOn = true;

        // bind 

        public void Bind(ChartCandleMaster chart)
        {
            _bindChart = chart;
            _bindIsOn = true;
        }

        public void BindOff()
        {
            _bindIsOn = false;
        }

        public void BindOn()
        {
            _bindIsOn = true;
        }

        private bool _bindIsOn = false;

        private ChartCandleMaster _bindChart;

        private void ChartCandle_LastXIndexChangeEvent(int curXFromRight)
        {

            if (_bindChart == null &&
                ChartCandle != null)
            {
                ChartCandle.LastXIndexChangeEvent -= ChartCandle_LastXIndexChangeEvent;
                return;
            }

            if (_bindIsOn == false)
            {
                return;
            }

            if (ChartCandle == null)
            {
                return;
            }

            _bindChart.SetAxisXPositionFromRight(curXFromRight);
        }

        private void CheckBindAreaSize(int size)
        {
            if (_bindChart == null)
            {
                return;
            }

            if (_bindIsOn == false)
            {
                return;
            }

            if (size <= 0)
            {
                return;
            }

            if (ChartCandle == null)
            {
                return;
            }

            _bindChart.SetAxisXSize(size);

        }

        public void SetAxisXSize(int size)
        {
            if (ChartCandle == null)
            {
                return;
            }

            ChartCandle.SetAxisXSize(size);
        }

        public void SetAxisXPositionFromRight(int xPosition)
        {
            if (ChartCandle == null)
            {
                return;
            }

            ChartCandle.SetAxisXPositionFromRight(xPosition);
        }

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
            ContextMenuStrip menu = GetContextMenu();
            ChartCandle.ShowContextMenu(menu);
        }

        /// <summary>
        /// взять контекстное меню настройки отображения чарта и индикаторов
        /// </summary>
        /// <returns></returns>
        public ContextMenuStrip GetContextMenu()
        {
            try
            {
                List<ToolStripMenuItem> menuRedact = null;

                List<ToolStripMenuItem> menuDelete = null;

                if (_indicators != null)
                {
                    menuRedact = new List<ToolStripMenuItem>();

                    menuDelete = new List<ToolStripMenuItem>();
                    for (int i = 0; i < _indicators.Count; i++)
                    {
                        string indicatorName = _indicators[i].GetType().Name;

                        menuRedact.Add(new ToolStripMenuItem(indicatorName));
                        menuRedact[menuRedact.Count - 1].ToolTipText = indicatorName + "*" + i;
                        menuRedact[menuRedact.Count - 1].Click += RedactContextMenu_Click;

                        if (_indicators[i].CanDelete)
                        {
                            menuDelete.Add(new ToolStripMenuItem(indicatorName));
                            menuDelete[menuDelete.Count - 1].ToolTipText = indicatorName + "*" + i;
                            menuDelete[menuDelete.Count - 1].Click += DeleteContextMenu_Click;
                        }
                    }
                }

                if (ChartCandle == null)
                {
                    UpDateChartPainter();
                }


                if (ChartCandle.AreaIsCreate("TradeArea") == true)
                {
                    if (menuRedact == null)
                    {
                        menuRedact = new List<ToolStripMenuItem>();

                        menuDelete = new List<ToolStripMenuItem>();
                    }

                    menuDelete.Add(new ToolStripMenuItem("Trades"));
                    menuDelete[menuDelete.Count - 1].Click += DeleteContextMenu_Click;
                }

                List<ToolStripMenuItem> items;

                items = new List<ToolStripMenuItem>();


                ToolStripMenuItem item1 = new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem1);


                ToolStripMenuItem item2 = new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem2);

                item2.DropDownItems.AddRange(new ToolStripMenuItem[]{
                            new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem3),
                            new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem4)});

                ToolStripMenuItem item5 = new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem5);
                item5.DropDownItems.AddRange(
                    new ToolStripMenuItem[]{
                        new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem15),
                        new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem6),
                        new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem7),
                        new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem8),
                        new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem9)});

                item1.DropDownItems.Add(item2);
                item1.DropDownItems.Add(item5);

                items.Add(item1);

                ((ToolStripMenuItem)items[0].DropDownItems[0]).DropDownItems[0].Click += ChartBlackColor_Click;
                ((ToolStripMenuItem)items[0].DropDownItems[0]).DropDownItems[1].Click += ChartWhiteColor_Click;

                ((ToolStripMenuItem)items[0].DropDownItems[1]).DropDownItems[0].Click += ChartAutoToPosition_Click;
                ((ToolStripMenuItem)items[0].DropDownItems[1]).DropDownItems[1].Click += ChartCrossToPosition_Click;
                ((ToolStripMenuItem)items[0].DropDownItems[1]).DropDownItems[2].Click += ChartRombToPosition_Click;
                ((ToolStripMenuItem)items[0].DropDownItems[1]).DropDownItems[3].Click += ChartCircleToPosition_Click;
                ((ToolStripMenuItem)items[0].DropDownItems[1]).DropDownItems[4].Click += ChartTriangleToPosition_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem10));
                items[items.Count - 1].Click += ChartHideIndicators_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem11));
                items[items.Count - 1].Click += ChartShowIndicators_Click;

                if (menuRedact != null)
                {
                    var itemEdit = new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem12);
                    itemEdit.DropDownItems.AddRange(menuRedact.ToArray());
                    items.Add(itemEdit);

                    var itemDel = new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem13);
                    itemDel.DropDownItems.AddRange(menuDelete.ToArray());
                    items.Add(itemDel);
                }

               items.Add(new ToolStripMenuItem(OsLocalization.Charts.ChartMenuItem14));
                items[items.Count - 1].Click += CreateIndicators_Click;

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.AddRange(items.ToArray());

                return menu;
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
            return null;
        }
                
        /// <summary>
        /// user has selected the crosshair in context menu to draw trades on chart
        /// Пользователь выбрал в контекстном меню перекрестие для прорисовки сделок на чарте
        /// </summary>
        private void ChartAutoToPosition_Click(object sender, EventArgs e)
        {
            ChartCandle.SetPointType(PointType.Auto);
            ChartCandle.ProcessPositions(_myPosition);
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
                ToolStripMenuItem item = (ToolStripMenuItem)sender;

                int num = Convert.ToInt32(item.ToolTipText.Split('*')[1]);

                if(_indicators == null 
                    || num >= _indicators.Count)
                {
                    return;
                }

                _indicators[num].ShowDialog();
                _indicators[num].Save();

                if (IndicatorUpdateEvent != null)
                {
                    IndicatorUpdateEvent();
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        public event Action IndicatorUpdateEvent;

        public event Action<IIndicator> IndicatorManuallyCreateEvent;

        public event Action<IIndicator> IndicatorManuallyDeleteEvent;

        /// <summary>
        /// user has chosen to delete indicator in context menu
        /// Пользователь выбрал в контекстном меню удалить индикатор
        /// </summary>
        private void DeleteContextMenu_Click(object sender, EventArgs e)
        {
            try
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;

                int number = Convert.ToInt32(item.ToolTipText.Split('*')[1]);


                if ((_indicators == null || number >= _indicators.Count))
                {
                    return;
                }

                IIndicator indicator = _indicators[number];

                DeleteIndicator(indicator);

                if (IndicatorManuallyDeleteEvent != null)
                {
                    IndicatorManuallyDeleteEvent(indicator);
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }

            if (IndicatorUpdateEvent != null)
            {
                IndicatorUpdateEvent();
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
                int indicatorsOld = 0;

                if (Indicators != null)
                {
                    indicatorsOld = Indicators.Count;
                }

                IndicatorCreateUi ui = new IndicatorCreateUi(this);
                ui.ShowDialog();

                if (IndicatorUpdateEvent != null)
                {
                    IndicatorUpdateEvent();
                }

                int indicatorsNow = 0;

                if (Indicators != null)
                {
                    indicatorsNow = Indicators.Count;
                }

                if (indicatorsOld < indicatorsNow &&
                    IndicatorManuallyCreateEvent != null)
                {
                    IndicatorManuallyCreateEvent(Indicators[Indicators.Count - 1]);
                }
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

            CheckBindAreaSize(newSizeX);

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
        private List<IIndicator> _indicators = new List<IIndicator>();

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

                if (indicator.GetType().BaseType.Name == "Aindicator")
                {
                    ((Aindicator)indicator).StartProgram = _startProgram;
                }

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

                if (_indicators == null)
                {
                    _indicators = new List<IIndicator>();
                    _indicators.Add(indicator);
                }
                else
                {
                    _indicators.Add(indicator);
                }

                LoadIndicatorOnChart(indicator);

                indicator.NeedToReloadEvent += indicator_NeedToReloadEvent;
                indicator_NeedToReloadEvent(indicator);

                return indicator;
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
                return null;
            }
        }

        private void LoadIndicatorOnChart(IIndicator indicator)
        {
            if (ChartCandle == null)
            {
                return;
            }
            bool inNewArea = true;
            string nameArea = indicator.NameArea;

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
                    return;
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

            Save();
            ReloadContext();
        }

        /// <summary>
        /// indicator has changed. It need to redraw
        /// Индикатор изменился. Надо перерисовать
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        private void indicator_NeedToReloadEvent(IIndicator indicator)
        {
            try
            {
                List<Candle> candles = _myCandles;
                if (candles != null)
                {
                    indicator.Process(candles);
                }

                if (ChartCandle != null)
                {
                    ChartCandle.RePaintIndicator(indicator);
                }
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
                if (ChartCandle != null)
                {
                    ChartCandle.DeleteIndicator(indicator);
                }

                indicator.Delete();

                for (int i = 0; i < _indicators.Count; i++)
                {
                    if(_indicators[i].Name == indicator.Name)
                    {
                        _indicators.RemoveAt(i);
                        break;
                    }
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
            if (ChartCandle == null)
            {
                return false;
            }

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
            if (ChartCandle != null)
            {
                return ChartCandle.AreaIsCreate(name);
            }
            else
            {
                return true;
            }
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

                if (_startProgram == StartProgram.IsOsOptimizer)
                {
                    return;
                }

                // 1  If such an element has already been added, replace it
                // 1 если такой элемент уже добавлен, заменяем его
                IChartElement myElement = _chartElements.Find(chartElement => chartElement.UniqName == element.UniqName);
                if (myElement != null)
                {
                    _chartElements.Remove(myElement);
                    myElement.UpdateEvent -= myElement_UpdeteEvent;
                    myElement.DeleteEvent -= myElement_DeleteEvent;
                }

                myElement = element;

                _chartElements.Add(myElement);

                myElement.UpdateEvent += myElement_UpdeteEvent;
                myElement.DeleteEvent += myElement_DeleteEvent;
                // 2 sending it over for a drawing.
                // 2 отправляем на прорисовку

                if (ChartCandle != null && _startProgram
                    != StartProgram.IsOsOptimizer)
                {
                    ChartCandle.ProcessElem(myElement);
                }
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
            if (ChartCandle != null)
            {
                ChartCandle.ProcessClearElem(element);
            }

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

            List<IChartElement> listToDelete = new List<IChartElement>();

            for (int i = 0; i < _chartElements.Count; i++)
            {
                listToDelete.Add(_chartElements[i]);
            }

            for (int i = 0; listToDelete != null && i < listToDelete.Count; i++)
            {
                DeleteChartElement(listToDelete[i]);
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
            if (ChartCandle != null)
            {
                ChartCandle.ProcessElem(element);
            }
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
        public void PaintAlerts(List<IIAlert> alertArray, bool needToWait)
        {
            try
            {
                _alertArray = alertArray;

                if (_alertArray == null || _myCandles == null)
                {
                    return;
                }

                for (int i = 0; i < _alertArray.Count; i++)
                {
                    if (_alertArray[i].TypeAlert == AlertType.ChartAlert)
                    {
                        if (ChartCandle != null)
                        {
                            AlertToChart alertCur = (AlertToChart)_alertArray[i];

                            if (alertCur.Lines == null)
                            {
                                continue;
                            }

                            if (ChartCandle.HaveAlertOnChart(alertCur) == false)
                            {
                                ChartCandle.RemoveAlert(alertCur);
                                ChartCandle.ProcessAlert(alertCur, needToWait);
                            }
                        }
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

        public void DeleteAlert(IIAlert alert)
        {
            if (ChartCandle == null)
            {
                return;
            }

            if (alert.TypeAlert == AlertType.ChartAlert)
            {
                ChartCandle.RemoveAlert((AlertToChart)alert);
            }
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
        public void SetCandles(List<Candle> candles)
        {
            try
            {
                if (candles == null
                    || candles.Count == 0)
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

                bool isFirstTime = false;

                if (_myCandles == null
                    || _myCandles.Count - candles.Count < -5
                    || _myCandles.Count - candles.Count > 5)
                {
                    isFirstTime = true;
                }

                _myCandles = candles;

                if (ChartCandle != null)
                {
                    if (canReload)
                    {
                        if (_startProgram == StartProgram.IsOsTrader)
                        {
                            ChartCandle?.ProcessCandles(candles);

                            if (_lastCandleIncome.AddSeconds(1) < DateTime.Now)
                            {
                                _lastCandleIncome = DateTime.Now;
                                ChartCandle?.ProcessPositions(_myPosition);
                                ChartCandle?.ProcessStopLimits(_myStopLimit);
                            }
                        }
                        else
                        {
                            _lastCandleIncome = DateTime.Now;
                            ChartCandle?.ProcessCandles(candles);
                            ChartCandle?.ProcessPositions(_myPosition);
                            ChartCandle?.ProcessStopLimits(_myStopLimit);
                        }
                    }

                    if (_indicators != null)
                    {

                        for (int i = 0; i < _indicators.Count; i++)
                        {
                            _indicators[i].Process(candles);

                            if (EventIsOn == false) 
                            {
                                Type indType = _indicators[i].GetType();
                                if (indType.BaseType.Name == "Aindicator")
                                {
                                    ((Aindicator)_indicators[i]).Reload();
                                }
                            }    

                            if (canReload)
                            {
                                ChartCandle?.ProcessIndicator(_indicators[i]);
                            }
                        }

                        if (EventIsOn == false)
                        {
                            EventIsOn = true;
                        }
                    }
                    if (canReload && _alertArray != null && _alertArray.Count != 0)
                    {
                        if (isFirstTime)
                        {
                            PaintAlerts(_alertArray, true);
                        }
                        else
                        {
                            PaintAlerts(_alertArray, false);
                        }
                    }
                }
                else
                {
                    if (_indicators != null)
                    {
                        for (int i = 0; i < _indicators.Count; i++)
                        {
                            _indicators[i].Process(candles);
                        }
                    }
                }
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

            if (ChartCandle != null)
            {
                ChartCandle.ProcessPositions(position);
            }
        }

        // stop Limits drawing

        private List<PositionOpenerToStopLimit> _myStopLimit;

        public void SetStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            _myStopLimit = stopLimits;

            if (ChartCandle != null)
            {
                ChartCandle.ProcessStopLimits(stopLimits);
            }
        }

        // management управление

        /// <summary>
        /// to start drawing this chart on window
        /// начать прорисовывать данный чарт на окне
        /// </summary>
        public void StartPaint(System.Windows.Controls.Grid gridChart, WindowsFormsHost host, System.Windows.Shapes.Rectangle rectangle)
        {
            try
            {
                if (ChartCandle == null)
                {
                    UpDateChartPainter();
                }
                ChartCandle.StartPaintPrimeChart(gridChart, host, rectangle);
                ChartCandle.ProcessCandles(_myCandles);
                ChartCandle.ProcessPositions(_myPosition);
                ChartCandle.ProcessStopLimits(_myStopLimit);

                for (int i = 0; _indicators != null && i < _indicators.Count; i++)
                {
                    ChartCandle.ProcessIndicator(_indicators[i]);
                }

                for (int i = 0; _chartElements != null && i < _chartElements.Count; i++)
                {
                    ChartCandle.ProcessElem(_chartElements[i]);
                }

                PaintAlerts(_alertArray, true);

                if (_lastStopChartScale > 10)
                {
                    ChartCandle.OpenChartScale = _lastStopChartScale;
                    ChartCandle.MoveChartToTheRight(_lastStopChartScale);
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
            if (ChartCandle != null)
            {
                IChartPainter painter = ChartCandle;

                ChartCandle = null;

                if (painter.OpenChartScale != 0)
                {
                    _lastStopChartScale = painter.OpenChartScale;
                }

                painter.StopPaint();
                painter.Delete();

            }

            if (_grid != null)
            {
                _grid.Children.Clear();
                _grid = null;
            }

            //UpDateChartPainter();
        }

        private int _lastStopChartScale = 0;

        /// <summary>
        /// clear chart
        /// очистить чарт
        /// </summary>
        public void Clear()
        {
            _myCandles = null;

            if (ChartCandle != null)
            {
                ChartCandle.ClearDataPointsAndSizeValue();
            }

            _myPosition = null;
            _myStopLimit = null;

            for (int i = 0; _indicators != null && i < _indicators.Count; i++)
            {
                _indicators[i].Clear();
            }

            if (_alertArray != null)
            {
                _alertArray = null;
            }
        }

        public void ClearTimePoints()
        {
            if (ChartCandle != null)
            {
                ChartCandle.ClearDataPointsAndSizeValue();
            }

            _lastPrice = 0;
        }

        /// <summary>
        /// сдвинуть представление чарта вправо до конца
        /// </summary>
        public void MoveChartToTheRight()
        {
            if (ChartCandle != null)
            {
                ChartCandle.MoveChartToTheRight(0);
            }
        }

        public int GetSelectCandleNumber()
        {
            if (ChartCandle == null)
            {
                return 0;
            }

            return ChartCandle.GetCursorSelectCandleNumber();
        }

        public decimal GetCursorSelectPrice()
        {
            if (ChartCandle == null)
            {
                return 0;
            }
            return ChartCandle.GetCursorSelectPrice();
        }

        public void RemoveCursor()
        {
            if (ChartCandle == null)
            {
                return;
            }
            ChartCandle.RemoveCursor();
        }

        /// <summary>
        /// move the chart view to time
        /// переместить представление чарта к времени
        /// </summary>
        /// <param name="time">time/время</param>
        public void GoChartToTime(DateTime time)
        {
            if (ChartCandle == null)
            {
                return;
            }
            ChartCandle.GoChartToTime(time);
        }

        public void GoChartToIndex(int index)
        {
            if (ChartCandle == null)
            {
                return;
            }
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
            if (ChartCandle == null)
            {
                return null;
            }
            return ChartCandle.GetAreasNames();
        }
        // tool display window
        // окно отображения инструмента

        public void StartPaintChartControlPanel(System.Windows.Controls.Grid grid)
        {
            if (ChartCandle == null)
            {
                return;
            }
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

        bool _isFirstTimeSetSecurity = true;

        /// <summary>
        /// to load a new tool into chart
        /// подгрузить в чарт новый инструмент
        /// </summary>
        /// <param name="security">security/бумага</param>
        /// <param name="timeFrameBuilder">an object that stores candles construction settings/объект хранящий в себе настройки построения свечей</param>
        /// <param name="portfolioName">portfolio/портфель</param>
        /// <param name="serverType">server type/тип сервера</param>
        public void SetNewSecurity(string security, TimeFrameBuilder timeFrameBuilder, string portfolioName, string serverType)
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (serverType == null)
            {
                return;
            }

            serverType = serverType.Replace("_", "-");

            if (_securityOnThisChart == security &&
                _timeFrameSecurity == timeFrameBuilder.TimeFrame &&
                serverType == _serverType &&
                _candleCreateMethodTypeOnThisChart == timeFrameBuilder.CandleCreateMethodType)
            {
                return;
            }

            if (string.IsNullOrEmpty(_serverType)
                || _serverType == ServerType.None.ToString())
            {
                _isFirstTimeSetSecurity = true;
            }

            if (ChartCandle != null)
            {
                ChartCandle.ClearDataPointsAndSizeValue();
                SetNewTimeFrameToChart(timeFrameBuilder);   //AVP  рефакторинг, чтоб нижний код два раза не повторялся.
            }

            string lastSecurity = _securityOnThisChart;
            List<Position> positions = _myPosition;
            List<PositionOpenerToStopLimit> limits = _myStopLimit;
            _timeFrameBuilder = timeFrameBuilder;
            _securityOnThisChart = security;
            _timeFrameSecurity = timeFrameBuilder.TimeFrame;
            _serverType = serverType;
            _candleCreateMethodTypeOnThisChart = timeFrameBuilder.CandleCreateMethodType;

            if (_isFirstTimeSetSecurity)
            {
                _isFirstTimeSetSecurity = false;
                return;
            }

            Clear();
            PaintLabelOnSlavePanel();

            if (lastSecurity == security)
            {
                if (positions != null)
                {
                    SetPosition(positions);
                }
                if (limits != null)
                {
                    SetStopLimits(limits);
                }
            }
        }

        private TimeFrameBuilder _timeFrameBuilder;

        private string _serverType;

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
        private string _candleCreateMethodTypeOnThisChart;

        private System.Windows.Controls.Label _label;

        private System.Windows.Controls.Grid _grid;

        /// <summary>
        /// get chart information
        /// получить информацию о чарте
        /// </summary>
        public string GetChartLabel()
        {
            if (ChartCandle == null)
            {
                return "";
            }
            string security = _securityOnThisChart;

            if (string.IsNullOrEmpty(security))
            {
                security = "Unknown";
            }

            string label = _serverType.ToString();

            if (_timeFrameBuilder.CandleCreateMethodType == "Simple")
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

            if (ChartCandle == null)
            {
                return;
            }

            if (_timeFrameBuilder == null)
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

            if (_timeFrameBuilder != null
                && _timeFrameBuilder.CandleCreateMethodType == "Simple")
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
            if (ChartCandle == null)
            {
                return;
            }
            ChartCandle.PaintInDifColor(indexStart, indexEnd, seriesName);
        }

        public void RefreshChartColor()
        {
            if (ChartCandle == null)
            {
                return;
            }
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