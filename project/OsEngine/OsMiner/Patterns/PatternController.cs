/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// менеджер сборных паттернов
    /// </summary>
    public class PatternController
    {
        public PatternController()
        {
            ExitFromSomeCandlesIsOn = true;
            ExitFromSomeCandlesValue = 10;

            WeigthToInter = 1;
            WeigthToExit = 1;
            WeigthToTempPattern = 1;

            ExpandToTempPattern = 99.8m;

            MiningMo = 0.01m;
            MiningDealsCount = 50;
            MiningProfit = 10;
            LotsCount = LotsCountType.All;
            SideInter= Side.Buy;
           
        }

        /// <summary>
        /// сервер данных
        /// </summary>
        public OsMinerServer DataServer;

        /// <summary>
        /// имя сборного паттерна
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;

                DataServer = new OsMinerServer(_name);
                DataServer.CandleSeriesChangeEvent += _dataServer_CandleSeriesChangeEvent;

                _chart = new ChartMaster(_name,StartProgram.IsOsMiner);
                _chart.ClickToIndexEvent += _chart_ClickToIndexEvent;

                _chartTempPattern = new ChartPainter(_name + "TempPattern",StartProgram.IsOsMiner);
                _chartTempPattern.IsPatternChart = true;
                _chartSingleOpenPattern = new ChartPainter(_name + "OpenSinglePattern", StartProgram.IsOsMiner);
                _chartSingleOpenPattern.IsPatternChart = true;
                _chartSingleClosePattern = new ChartPainter(_name + "CloseSinglePattern", StartProgram.IsOsMiner);
                _chartSingleClosePattern.IsPatternChart = true;
            }
        }
        private string _name;

        /// <summary>
        /// серии данных доступных для майнинга
        /// </summary>
        public List<MinerCandleSeries> CandleSeries { get; set; }

        /// <summary>
        /// гуй 
        /// </summary>
        private PatternControllerUi _ui;

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new PatternControllerUi(this);
                _ui.Show();
            }
            else
            {
                _ui.Focus();
            }
        }

        /// <summary>
        /// загрузить из строки сохранения
        /// </summary>
        public void Load(string saveStr)
        {
            string[] array = saveStr.Split('?');

            _name = array[0];

            // разбираем паттерны на вход
            string[] patternsToEnter = array[1].Split('%');
            for (int i = 0; i < patternsToEnter.Length-1; i += 1)
            {
                LoadPattern(patternsToEnter[i], PatternsToOpen);
            }

            // разбираем паттерны на выход
            string[] patternsToExit = array[2].Split('%');
            for (int i = 0; i < patternsToExit.Length-1; i += 1)
            {
                LoadPattern(patternsToExit[i], PatternsToClose);
            }

            IsOn = Convert.ToBoolean(array[3]);
            Enum.TryParse(array[4],out SideInter);
            StopOrderIsOn = Convert.ToBoolean(array[5]);
            ProfitOrderIsOn = Convert.ToBoolean(array[6]);
            ExitFromSomeCandlesIsOn = Convert.ToBoolean(array[7]);
            TrailingStopIsOn = Convert.ToBoolean(array[8]);
            StopOrderValue = Convert.ToDecimal(array[9]);
            ProfitOrderValue = Convert.ToDecimal(array[10]);
            TreilingStopValue = Convert.ToDecimal(array[11]);
            ExitFromSomeCandlesValue = Convert.ToInt32(array[12]);
            SecurityToInter = array[13];
            WeigthToInter = Convert.ToDecimal(array[14]);
            WeigthToExit = Convert.ToDecimal(array[15]);
            WeigthToTempPattern = Convert.ToDecimal(array[16]);
            Enum.TryParse(array[17], out PlaceToUsePattern);
            ExpandToTempPattern = Convert.ToDecimal(array[18]);
            MiningMo = Convert.ToDecimal(array[19]);
            MiningDealsCount = Convert.ToInt32(array[20]);
            MiningProfit = Convert.ToDecimal(array[21]);

            DataServer = new OsMinerServer(Name);
            DataServer.CandleSeriesChangeEvent += _dataServer_CandleSeriesChangeEvent;
            
            _chart = new ChartMaster(_name,StartProgram.IsOsMiner);
            _chart.ClickToIndexEvent += _chart_ClickToIndexEvent;

            _chartTempPattern = new ChartPainter(_name + "TempPattern", StartProgram.IsOsMiner);
            _chartTempPattern.IsPatternChart = true;
            _chartSingleOpenPattern = new ChartPainter(_name + "OpenSinglePattern", StartProgram.IsOsMiner);
            _chartSingleOpenPattern.IsPatternChart = true;
            _chartSingleClosePattern = new ChartPainter(_name + "CloseSinglePattern", StartProgram.IsOsMiner);
            _chartSingleClosePattern.IsPatternChart = true;

            if (PatternsToOpen.Count != 0)
            {
                PaintClosePattern(0);
            }

            if (PatternsToClose.Count != 0)
            {
                PaintOpenPattern(0);
            }
            
        }

        /// <summary>
        /// загрузить отдельный одиночный паттерн из строки
        /// </summary>
        private void LoadPattern(string pat, List<IPattern> array)
        {
            string[] patternInString = pat.Split('^');

            PatternType type;
            Enum.TryParse(patternInString[0], out type);

            if (type == PatternType.Candle)
            {
                PatternCandle pattern = new PatternCandle();
                pattern.Load(pat);
                array.Add(pattern);
            }
            if (type == PatternType.Volume)
            {
                PatternVolume pattern = new PatternVolume();
                pattern.Load(pat);
                array.Add(pattern);
            }
            if (type == PatternType.Indicators)
            {
                PatternIndicators pattern = new PatternIndicators();
                pattern.Load(pat);
                array.Add(pattern);
            }
            if (type == PatternType.Time)
            {
                PatternTime pattern = new PatternTime();
                pattern.Load(pat);
                array.Add(pattern);
            }
        }

        /// <summary>
        /// сохранить 
        /// </summary>
        public void Save()
        {
            if (NeadToSaveEvent != null)
            {
                NeadToSaveEvent();
            }
        }

        /// <summary>
        /// взять строку сохранения
        /// </summary>
        /// <returns></returns>
        public string GetSaveString()
        {
            // разделители на предыдущих уровнях: # * ?

            string saveStr = "";

            saveStr += Name + "?";

            for (int i = 0; PatternsToOpen != null && i < PatternsToOpen.Count; i++)
            {
                saveStr += PatternsToOpen[i].GetSaveString() + "%";
            }

            saveStr += "?";

            for (int i = 0; PatternsToClose != null && i < PatternsToClose.Count; i++)
            {
                saveStr += PatternsToClose[i].GetSaveString() + "%";
            }

            saveStr += "?";

            saveStr += IsOn + "?";
            saveStr += SideInter + "?";
            saveStr += StopOrderIsOn + "?";
            saveStr += ProfitOrderIsOn + "?";
            saveStr += ExitFromSomeCandlesIsOn + "?";
            saveStr += TrailingStopIsOn + "?";
            saveStr += StopOrderValue + "?";
            saveStr += ProfitOrderValue + "?";
            saveStr += TreilingStopValue + "?";
            saveStr += ExitFromSomeCandlesValue + "?";
            saveStr += SecurityToInter + "?";
            saveStr += WeigthToInter + "?";
            saveStr += WeigthToExit + "?";
            saveStr += WeigthToTempPattern + "?";
            saveStr += PlaceToUsePattern + "?";
            saveStr += ExpandToTempPattern + "?";

            saveStr += MiningMo + "?";
            saveStr += MiningDealsCount + "?";
            saveStr += MiningProfit + "?";

            return saveStr;
        }

        /// <summary>
        /// удалить всю информацию по сету из файловой системы
        /// </summary>
        public void Delete()
        {
            _chart.Delete();
        }

        public event Action NeadToSaveEvent;

        /// <summary>
        /// изменился состав  бумаг  доступных для тестов
        /// </summary>
        void _dataServer_CandleSeriesChangeEvent(List<MinerCandleSeries> series)
        {
            if (series == null ||
                series.Count == 0)
            {
                return;
            }

            TimeFrame firstFrame = series[0].TimeFrame;

            for (int i = 0; i < series.Count; i++)
            {
                if (firstFrame != series[i].TimeFrame)
                {
                    SendNewLogMessage("Сет данных не принят, т.к. содержит данные с разным таймфреймом",LogMessageType.Error);
                    return;
                }
            }

            CandleSeries = series;
        }

        /// <summary>
        /// прекратить прорисовывать паттерн
        /// </summary>
        public void StopPaint()
        {
            if (_ui != null)
            {
                _ui.Close();
                _ui = null;
            }
        }

        /// <summary>
        /// прорисовать сборный паттерн
        /// </summary>>
        public void Paint(WindowsFormsHost chartHost,Rectangle rectChart)
        {
            _chartHost = chartHost;
            _rectChart = rectChart;
            PaintPrimeChart();
        }

        /// <summary>
        /// подгрузка контролов из ГУИ
        /// </summary>
        public void PaintController(WindowsFormsHost hostTempPattern, WindowsFormsHost hostSinglePatternToOpen, WindowsFormsHost hostSinglePatternToClose)
        {
            _chartSingleOpenPattern.StartPaintPrimeChart(hostSinglePatternToOpen, new Rectangle());
            _chartSingleClosePattern.StartPaintPrimeChart(hostSinglePatternToClose, new Rectangle());
            _chartTempPattern.StartPaintPrimeChart(hostTempPattern, new Rectangle());

            PaintTempPattern();
        }

        /// <summary>
        /// тип лотов
        /// </summary>
        public LotsCountType LotsCount;

// Настройки и Паттерны для входа

        /// <summary>
        /// включен ли 
        /// </summary>
        public bool IsOn;

        /// <summary>
        /// сторона входа для паттерна
        /// </summary>
        public Side SideInter;

        /// <summary>
        /// инструмент для входа. Используется в Майнере
        /// </summary>
        public string SecurityToInter
        {
            get { return _securityToInter; }
            set
            {
                _securityToInter = value;
                PaintPrimeChart();
            }
        }

        /// <summary>
        /// бумага для входа во время тестов
        /// </summary>
        private string _securityToInter;

        /// <summary>
        /// общий вес одиночных паттернов для входа
        /// </summary>
        public decimal WeigthToInter;

        /// <summary>
        /// одиночные паттерны для входа в позицию
        /// </summary>
        public List<IPattern> PatternsToOpen = new List<IPattern>();

        /// <summary>
        /// удалить одиночный паттерн для входа по индексу
        /// </summary>
        public void RemovePatternToInter(int num)
        {
            if (num >= PatternsToOpen.Count)
            {
                return;
            }
            PatternsToOpen.RemoveAt(num);
            Save();
        }

// Настройки и Паттерны для выхода

        /// <summary>
        /// общий вес одиночных паттернов для выхода из позиции
        /// </summary>
        public decimal WeigthToExit;

        /// <summary>
        /// включен ли стоп ордер для выхода
        /// </summary>
        public bool StopOrderIsOn;

        /// <summary>
        /// включен ли профит ордер для выхода
        /// </summary>
        public bool ProfitOrderIsOn;

        /// <summary>
        /// включен ли выход через N свечек
        /// </summary>
        public bool ExitFromSomeCandlesIsOn;

        /// <summary>
        /// включен ли трейлинг стоп для выхода
        /// </summary>
        public bool TrailingStopIsOn;

        /// <summary>
        /// величина стопОрдера
        /// </summary>
        public decimal StopOrderValue;

        /// <summary>
        /// величина профит ордера
        /// </summary>
        public decimal ProfitOrderValue;

        /// <summary>
        /// величина трейлинг стопа
        /// </summary>
        public decimal TreilingStopValue;

        /// <summary>
        /// количество свечек после которого сработает выход через N свечек
        /// </summary>
        public int ExitFromSomeCandlesValue;

        /// <summary>
        /// одиночные паттерны на выход из позиции
        /// </summary>
        public List<IPattern> PatternsToClose = new List<IPattern>();

        /// <summary>
        /// удалить одиночный паттерн на выход по индексу
        /// </summary>
        public void RemovePatternToExit(int num)
        {
            if (num >= PatternsToClose.Count)
            {
                return;
            }
            PatternsToClose.RemoveAt(num);
            Save();
        }

// логирование

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

// бектестинг 

        /// <summary>
        /// открыть журнал сделок
        /// </summary>
        public void ShowJournal()
        {
            Journal.Journal journal = new Journal.Journal("",StartProgram.IsOsMiner);
            for (int i = 0; i < PositionsInTrades.Count; i++)
            {
                journal.SetNewDeal(PositionsInTrades[i]);
            }

            BotPanelJournal botPanelJournal = new BotPanelJournal();

            BotTabJournal botTabJournal = new BotTabJournal();
            botTabJournal.Journal = journal;

            botPanelJournal._Tabs= new List<BotTabJournal>();
            botPanelJournal._Tabs.Add(botTabJournal);
            botPanelJournal.BotName = "";

            List<BotPanelJournal> list = new List<BotPanelJournal>();
            list.Add(botPanelJournal);

            JournalUi ui = new JournalUi(list,StartProgram.IsOsMiner);
            ui.ShowDialog();
        }

        /// <summary>
        /// позиции за последний бекТест
        /// </summary>
        public List<Position> PositionsInTrades = new List<Position>();

        /// <summary>
        /// паттерны за последний бекТест
        /// </summary>
        public List<ChartColorSeries>  ColorSeries = new List<ChartColorSeries>();
        
        /// <summary>
        /// начать тестирование текущих готовых одиночных паттернов без учёта паттерна из вкладки поиска
        /// </summary>
        public void TestCurrentPatterns()
        {
            try
            {
                if (PatternsToOpen == null || PatternsToOpen.Count == 0)
                {
                    return;
                }

                Test(PatternsToOpen, PatternsToClose);
                PaintPatternsColorSeries();

                if (PositionsInTrades.Count != 0)
                {
                    _chart.SetPosition(PositionsInTrades);
                }

                if (BackTestEndEvent != null)
                {
                    BackTestEndEvent(GetShortReport());
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// оттестировать  паттерны
        /// </summary>
        private void Test(List<IPattern> patternsToInter, List<IPattern> patternsToExit)
        {
            if (string.IsNullOrEmpty(SecurityToInter))
            {
                return;
            }
            MinerCandleSeries candlesToTrade = GetCandleSeries(SecurityToInter);

            ColorSeries = new List<ChartColorSeries>();

            PositionsInTrades = new List<Position>();

            _numPatternToWatch = 0;

            if (candlesToTrade == null)
            {
                SendNewLogMessage("Выбранная серия для торговли отсутствует.", LogMessageType.Error);
                return;
            }

            if (StopOrderIsOn == false &&
                ProfitOrderIsOn == false &&
                TrailingStopIsOn == false &&
                ExitFromSomeCandlesIsOn == false &&
                PatternsToClose.Count == 0)
            {
                SendNewLogMessage("Не выбран не один способ закрытия позиции. Тестирование не возможно", LogMessageType.Error);
                return;
            }

            List<List<Candle>> candlesParallelPatternsToInter = new List<List<Candle>>();

            for (int i = 0; i < patternsToInter.Count; i++)
            {
                MinerCandleSeries series = CandleSeries.Find(ser => ser.Security.Name == _securityToInter);

                if (series != null)
                {
                    candlesParallelPatternsToInter.Add(series.Candles);
                }
            }

            if (patternsToInter.Count != candlesParallelPatternsToInter.Count)
            {
                SendNewLogMessage("К паттрнам на ОТКРЫТИЕ не удалось найти всех нужных серий данных. Перейдите в соответствующую вкладку и назначте всем паттернам серию данных.", LogMessageType.Error);
                return;
            }

            List<List<Candle>> candlesParallelPatternsToExit = new List<List<Candle>>();
            for (int i = 0; i < patternsToExit.Count; i++)
            {
                MinerCandleSeries series = CandleSeries.Find(ser => ser.Security.Name == _securityToInter);

                if (series != null)
                {
                    candlesParallelPatternsToExit.Add(series.Candles);
                }
            }

            if (patternsToExit.Count != candlesParallelPatternsToExit.Count)
            {
                SendNewLogMessage("К паттрнам на ЗАКРЫТИЕ не удалось найти всех нужных серий данных. Перейдите в соответствующую вкладку и назначте всем паттернам серию данных.", LogMessageType.Error);
                return;
            }

            int indexWithPosition = 0;

            for (int i = 0; i < candlesToTrade.Candles.Count - 1; i++)
            {// первый цикл ищет входы

                if (LotsCount == LotsCountType.One && indexWithPosition > i)
                {
                    continue;
                }

                if (CheckInter(PatternsToOpen, candlesParallelPatternsToInter, i, candlesToTrade.Candles[i].TimeStart, WeigthToInter) == false)
                {
                    continue;
                }

                Position position = CreatePosition(candlesToTrade.Candles[i + 1].Open, candlesToTrade.Security, i,
                    candlesToTrade.Candles[i + 1].TimeStart);

                if (StopOrderIsOn)
                {
                    if (position.Direction == Side.Buy)
                    {
                        position.StopOrderPrice = position.EntryPrice - position.EntryPrice * (StopOrderValue / 100);
                    }
                    else
                    {
                        position.StopOrderPrice = position.EntryPrice + position.EntryPrice * (StopOrderValue / 100);
                    }
                }

                if (ProfitOrderIsOn)
                {
                    if (position.Direction == Side.Buy)
                    {
                        position.ProfitOrderPrice = position.EntryPrice + position.EntryPrice * (ProfitOrderValue / 100);
                    }
                    else
                    {
                        position.ProfitOrderPrice = position.EntryPrice - position.EntryPrice * (ProfitOrderValue / 100);
                    }
                }

                if (TrailingStopIsOn)
                {
                    if (position.Direction == Side.Buy)
                    {
                        position.StopOrderRedLine = position.EntryPrice - position.EntryPrice * (TreilingStopValue / 100);
                    }
                    else
                    {
                        position.StopOrderRedLine = position.EntryPrice + position.EntryPrice * (TreilingStopValue / 100);
                    }
                }

                for (int i2 = i; i2 < candlesToTrade.Candles.Count - 1; i2++)
                {// второй  цикл ищет выходы

                    if (TrailingStopIsOn)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            decimal price = candlesToTrade.Candles[i2].Close - candlesToTrade.Candles[i2].Close * TreilingStopValue / 100;
                            if (price > position.StopOrderRedLine)
                            {
                                position.StopOrderRedLine = price;
                            }
                        }
                        else
                        {
                            decimal price = candlesToTrade.Candles[i2].Close + candlesToTrade.Candles[i2].Close * TreilingStopValue / 100;
                            if (price < position.StopOrderRedLine)
                            {
                                position.StopOrderRedLine = price;
                            }
                        }
                    }

                    if (CheckExit(position, patternsToExit, candlesParallelPatternsToExit, i2, candlesToTrade.Candles[i2].TimeStart, candlesToTrade.Candles[i2].Close, candlesToTrade) == false)
                    {
                        continue;
                    }

                    indexWithPosition = i2;
                    ClosePosition(position, i2, candlesToTrade.Candles[i2 + 1].TimeStart, candlesToTrade.Candles[i2 + 1].Open);

                    PositionsInTrades.Add(position);
                    break;
                }
            }
        }

        /// <summary>
        /// проверить паттерны на вход в позицию
        /// </summary>
        private bool CheckInter(List<IPattern> patterns, List<List<Candle>> series, int index, DateTime time,decimal weigthToInterOrExit)
        {
            if (patterns == null ||
                patterns.Count == 0)
            {
                return false;
            }
            decimal weigth = 0;
            List<ChartColorSeries> seriesNew = new List<ChartColorSeries>();

            for (int i = 0; i < patterns.Count && i< series.Count; i++)
            {
                int startIndex = index;
                while (startIndex > 0 && series[i][startIndex].TimeStart > time)
                {
                    startIndex -= 10;
                }
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                for (int i2 = startIndex; i2 < series[i].Count; i2++)
                {
                    if (time == series[i][i2].TimeStart)
                    {
                        if (patterns[i].ThisIsIt(series[i], _chart.Indicators, i2))
                        {
                            weigth += patterns[i].Weigth;
                            if (patterns[i].Type == PatternType.Candle)
                            {
                                ChartColorSeries newColorSeries = new ChartColorSeries();
                                newColorSeries.NameSeries = "SeriesCandle";
                                newColorSeries.IndexStart = i2 - ((PatternCandle)patterns[i]).Length+1;
                                newColorSeries.IndexEnd = i2+1;
                                seriesNew.Add(newColorSeries);
                            }
                            if (patterns[i].Type == PatternType.Volume)
                            {
                                ChartColorSeries newColorSeries = new ChartColorSeries();
                                newColorSeries.NameSeries = "";
                                newColorSeries.IndexStart = i2 - ((PatternVolume)patterns[i]).Length + 1;
                                newColorSeries.IndexEnd = i2 + 1;
                                seriesNew.Add(newColorSeries);
                            }
                        }

                        break;
                    }

                    else if (time > series[i][i2].TimeStart)
                    {
                        break;
                    }
                }
            }

            if (weigth >= weigthToInterOrExit)
            {
                ColorSeries.AddRange(seriesNew);
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить выход из позиции
        /// </summary>
        private bool CheckExit(Position position, List<IPattern> patterns, List<List<Candle>> series, int index, DateTime time, decimal price,MinerCandleSeries tradeSeries)
        {
            if (CheckInter(patterns, series, index, time,WeigthToExit))
            { // если выходим по паттернам
                return true;
            }

            // проверить стоп

            if (StopOrderIsOn)
            {
                if (position.Direction == Side.Buy &&
                    price < position.StopOrderPrice)
                {
                    return true;
                }
                else if (position.Direction == Side.Sell &&
                         price > position.StopOrderPrice)
                {
                    return true;
                }
            }

            if (TrailingStopIsOn)
            {
                if (position.Direction == Side.Buy &&
                    price < position.StopOrderRedLine)
                {
                    return true;
                }
                else if (position.Direction == Side.Sell &&
                         price > position.StopOrderRedLine)
                {
                    return true;
                }

                if (position.Direction == Side.Buy)
                {
                    decimal newTrail =  position.EntryPrice - tradeSeries.Security.PriceStep * TreilingStopValue;

                    if (newTrail > position.StopOrderRedLine)
                    {
                        position.StopOrderRedLine = newTrail;
                    }
                }
                else
                {
                    decimal newTrail = position.EntryPrice + tradeSeries.Security.PriceStep * TreilingStopValue;

                    if (newTrail < position.StopOrderRedLine)
                    {
                        position.StopOrderRedLine = newTrail;
                    }
                }
            }

            // проверить профит

            if (ProfitOrderIsOn)
            {
                if (position.Direction == Side.Buy &&
                    price > position.ProfitOrderPrice)
                {
                    return true;
                }
                else if (position.Direction == Side.Sell &&
                        price < position.ProfitOrderPrice)
                {
                    return true;
                }
            }

            // проверить выход по времени

            if (ExitFromSomeCandlesIsOn)
            {
                if (position.OpenOrders[0].NumberUser + ExitFromSomeCandlesValue <= index)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// взять серию данных по имени
        /// </summary>
        private MinerCandleSeries GetCandleSeries(string name)
        {
            if(CandleSeries == null)
            {
                return null;
            }
            MinerCandleSeries candles = null;

            for (int i = 0; i < CandleSeries.Count; i++)
            {
                if (CandleSeries[i].Security.Name == name)
                {
                    candles = CandleSeries[i];
                }
            }
            return candles;
        }

        /// <summary>
        /// создать новую позицию по  индексу
        /// </summary>
        private Position CreatePosition(decimal price, Security security, int index, DateTime time)
        {
            Order newOrder = new Order();
            newOrder.SecurityNameCode = security.Name;
            newOrder.Price = price;
            newOrder.NumberUser = index;
            newOrder.Volume = 1;
            newOrder.NumberMarket = index.ToString();
            newOrder.Side = SideInter;
            newOrder.TimeCallBack = time;
            newOrder.TimeCreate = time;

            Position newPosition = new Position();
            newPosition.Direction = SideInter;
            newPosition.PriceStep = security.PriceStep;
            newPosition.PriceStepCost = security.PriceStep;

            newPosition.AddNewOpenOrder(newOrder);

            MyTrade trade = new MyTrade();
            trade.Volume = 1;
            trade.Side = newOrder.Side;
            trade.NumberOrderParent = index.ToString();
            trade.Price = price;
            trade.NumberTrade = index + 1.ToString();
            trade.Time = time;
            newPosition.SetTrade(trade);

            return newPosition;
        }

        /// <summary>
        /// закрыть позицию
        /// </summary>
        private void ClosePosition(Position position, int index, DateTime time, decimal price)
        {
            Order newOrder = new Order();
            newOrder.SecurityNameCode = position.SecurityName;
            newOrder.Price = price;
            newOrder.NumberUser = index;
            newOrder.Volume = 1;
            newOrder.NumberMarket = index.ToString();

            if (SideInter == Side.Buy)
            {
                newOrder.Side = Side.Sell;
            }
            else
            {
                newOrder.Side = Side.Buy;
            }

            newOrder.TimeCallBack = time;
            newOrder.TimeCreate = time;

            position.AddNewCloseOrder(newOrder);

            MyTrade trade = new MyTrade();
            trade.Volume = 1;
            trade.Side = newOrder.Side;
            trade.NumberOrderParent = index.ToString();
            trade.Price = price;
            trade.NumberTrade = index + 1.ToString();
            trade.Time = time;

            position.SetTrade(trade);

        }

        /// <summary>
        /// взять короткий отчёт о последнем тесте
        /// </summary>
        /// <returns></returns>
        private string GetShortReport()
        {
            string result = "";

            decimal profit = 0;
            _lastCount = 0;
            _lastProfit = 0;
            _lastMo = 0;

            for (int i = 0; i < PositionsInTrades.Count; i++)
            {
                profit += PositionsInTrades[i].ProfitOperationPunkt;
            }

            if(PositionsInTrades.Count == 0)
            {
                return "По паттерну небыло сделок";
            }

            _lastProfit = profit;

            decimal mO = profit / PositionsInTrades.Count;
            _lastCount = PositionsInTrades.Count;
            _lastMo = mO;
            result += "Общий профит: " + profit + "\r\n";
            result += "Прибыль со сделки: " + mO + "\r\n";
            result += "Количество входов: " + PositionsInTrades.Count + "\r\n";

            return result;
        }

        private decimal _lastProfit;

        private int _lastCount;

        private decimal _lastMo;

// перемещение графика по паттернам

        /// <summary>
        /// номер паттерна на который мы в последний раз смотрели
        /// </summary>
        private int _numPatternToWatch;

        /// <summary>
        /// перейти к следующему одиночному паттерну вправо
        /// </summary>
        public void GoRight()
        {
            if (_numPatternToWatch >= ColorSeries.Count
                )
            {
                return;
            }

            if (_numPatternToWatch < 0)
            {
                _numPatternToWatch = 0;
            }

            int num = ColorSeries[_numPatternToWatch].IndexStart;


            _chart.GoChartToIndex(num-5);
            _numPatternToWatch++;
        }


        /// <summary>
        /// перейти к следующему одиночному паттерну влево
        /// </summary>
        public void GoLeft()
        {
            if (_numPatternToWatch >= ColorSeries.Count)
            {
                return;
            }

            if (_numPatternToWatch < 0)
            {
                _numPatternToWatch = 0;
            }

            int num = ColorSeries[_numPatternToWatch].IndexStart;


            _chart.GoChartToIndex(num-5);
            _numPatternToWatch--;
        }

// автомайнинг паттернов

        /// <summary>
        /// среднее матОжидание за последний тест
        /// </summary>
        public decimal MiningMo;

        /// <summary>
        /// количество  сделок в последнем  тесте
        /// </summary>
        public int MiningDealsCount;

        /// <summary>
        /// общий профит в пунктах за последний тест
        /// </summary>
        public decimal MiningProfit;

        /// <summary>
        /// начать майнинг
        /// </summary>
        public void StartMining()
        {
            if (_worker != null)
            {
                StopMiningProcces();
                return;
            }
            _worker = new Thread(MiningThreadPlace);
            _worker.IsBackground = true;
            _worker.Start();
        }

        /// <summary>
        /// остановить майнинг
        /// </summary>
        public void StopMining()
        {
            _neadToStopMining = true;
        }

        /// <summary>
        /// поток занимающийся поиском паттернов
        /// </summary>
        private Thread _worker;

        /// <summary>
        /// флаг. Нужно ли остановить майнинг
        /// </summary>
        private bool _neadToStopMining;

        /// <summary>
        /// место работы потока майнинга
        /// </summary>
        private void MiningThreadPlace()
        {
            try
            {
                MinerCandleSeries mySeries = CandleSeries.Find(ser => ser.Security.Name == SecurityToInter);
                _testResults = new List<TestResult>();
                int curNum = 0;

                if (_curTempPatternType == PatternType.Candle)
                {
                    curNum += ((PatternCandle)GetTempPattern(_curTempPatternType)).Length + 3;
                }
                if (_curTempPatternType == PatternType.Indicators)
                {
                    curNum += ((PatternIndicators)GetTempPattern(_curTempPatternType)).Length + 3;
                }
                if (_curTempPatternType == PatternType.Volume)
                {
                    curNum += ((PatternVolume)GetTempPattern(_curTempPatternType)).Length + 3;
                }

                while (true)
                {
                    curNum++;
                    _curIndex = curNum;

                    if (_neadToStopMining)
                    {
                        StopMiningProcces();
                        return;
                    }

                    if (curNum >= mySeries.Candles.Count)
                    {
                        StopMiningProcces();
                        MessageBox.Show("Поиск паттернов завершён. Мы проверили все данные");
                        return;
                    }

                    decimal profit = 0;

                    for (int i = 0; i < PositionsInTrades.Count; i++)
                    {
                        profit += PositionsInTrades[i].ProfitOperationPunkt;
                    }

                    decimal mO = 0;

                    if (PositionsInTrades.Count != 0)
                    {
                        mO = profit / PositionsInTrades.Count;
                    }

                    if (MiningMo < Math.Abs(mO) &&
                        MiningDealsCount < PositionsInTrades.Count &&
                        MiningProfit < Math.Abs(profit))
                    {
                        StopMiningProcces();
                        return;
                    }


                    GetPatternToIndex();
                    BackTestTempPattern(false);
                    TestResult result = new TestResult();

                    result.Pattern = GetTempPattern(_curTempPatternType).GetCopy();
                    result.Positions = PositionsInTrades;
                    result.ShortReport = GetShortReport();
                    result.SummProfit = _lastProfit;
                    result.Mo = _lastMo;

                    if (_testResults.Count == 0)
                    {
                        _testResults.Add(result);
                    }
                    else
                    {
                        bool isInArray = false;
                        for (int i = 0; i < _testResults.Count; i++)
                        {
                            if (_testResults[i].SummProfit < result.SummProfit)
                            {
                                isInArray = true;
                                _testResults.Insert(i, result);
                                break;
                            }
                        }
                        if (isInArray == false)
                        {
                            _testResults.Add(result);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
                _worker = null;
            }
        }

        private List<TestResult> _testResults = new List<TestResult>();

        /// <summary>
        /// остановить майнинг паттерна и прорисовать результаты
        /// </summary>
        private void StopMiningProcces()
        {
            _worker = null;
            if (PositionsInTrades.Count != 0)
            {
                _chart.SetPosition(PositionsInTrades);
            }
            PaintPatternsColorSeries();
            if (BackTestEndEvent != null)
            {
                BackTestEndEvent(GetShortReport());
            }
            _neadToStopMining = false;
        }

        AutoTestResultsUi _uiTestResuits;

        /// <summary>
        /// показать результаты тестов по паттернам
        /// </summary>
        public void ShowTestResults()
        {
            if (_uiTestResuits == null)
            {
                _uiTestResuits = new AutoTestResultsUi(_testResults);
                _uiTestResuits.Show();
                _uiTestResuits.Closing += (sender, args) => { _uiTestResuits = null; };
                _uiTestResuits.UserClickOnNewPattern += _uiTestResuits_UserClickOnNewPattern;
            }
            else
            {
                _uiTestResuits.Activate();
            }
        }

        void _uiTestResuits_UserClickOnNewPattern(TestResult result)
        {
            PositionsInTrades = result.Positions;
            _curTempPatternType = result.Pattern.Type;

            GetTempPattern(_curTempPatternType);

            for (int i = 0; i < _tempPatterns.Count; i++)
            {
                if (_tempPatterns[i].Type == result.Pattern.Type)
                {
                    _tempPatterns[i] = result.Pattern;
                }
            }
            PaintTempPattern();
            if (BackTestEndEvent != null)
            {
                BackTestEndEvent(GetShortReport());
            }
        }

// поиск нового паттернов

        /// <summary>
        /// взять  временный паттерн по типу
        /// </summary>
        public IPattern GetTempPattern(PatternType type)
        {
            _curTempPatternType = type;

            for (int i = 0; i < _tempPatterns.Count; i++)
            {
                if (_tempPatterns[i].Type == type)
                {
                    _tempPatterns[i].Weigth = WeigthToTempPattern;
                    _tempPatterns[i].Expand = ExpandToTempPattern;
                    return _tempPatterns[i];
                }
            }

            if (type == PatternType.Indicators)
            {
                _tempPatterns.Add(new PatternIndicators());
            }
            else if (type == PatternType.Time)
            {
                _tempPatterns.Add(new PatternTime());
            }
            else if (type == PatternType.Volume)
            {
                _tempPatterns.Add(new PatternVolume());
            }
            else if (type == PatternType.Candle)
            {
                _tempPatterns.Add(new PatternCandle());
            }

            return GetTempPattern(type);
        }

        /// <summary>
        /// сохранить временный паттерн
        /// </summary>
        public void SaveTempPattern()
        {
            IPattern pattern = GetTempPattern(_curTempPatternType).GetCopy();

            if (PlaceToUsePattern == UsePatternType.OpenPosition)
            {
                PatternsToOpen.Add(pattern);
                PaintOpenPattern(PatternsToOpen.Count - 1);
            }
            else
            {
                PatternsToClose.Add(pattern);
                PaintClosePattern(PatternsToClose.Count-1);
            }
            Save();
        }

        /// <summary>
        /// временный одиночные паттерны
        /// </summary>
        private List<IPattern> _tempPatterns = new List<IPattern>();

        /// <summary>
        /// вес временного паттерна
        /// </summary>
        public decimal WeigthToTempPattern
        {
            get { return _weigthToTempPattern; }
            set
            {
                _weigthToTempPattern = value;

                for (int i = 0; i < _tempPatterns.Count; i++)
                {
                    _tempPatterns[i].Weigth = value;
                }
            }
        }
        private decimal _weigthToTempPattern;

        /// <summary>
        /// узнаваемость временного паттерна
        /// </summary>
        public decimal ExpandToTempPattern
        {
            get { return _expandToTempPattern; }
            set
            {
                _expandToTempPattern = value;

                for (int i = 0; i < _tempPatterns.Count; i++)
                {
                    _tempPatterns[i].Expand = value;
                }
                if (_curIndex != 0)
                {
                    GetPatternToIndex();
                }
            }
        }
        private decimal _expandToTempPattern;

        /// <summary>
        /// место использования временного паттерна(открытие / закрытие позиции)
        /// </summary>
        public UsePatternType PlaceToUsePattern;

        /// <summary>
        /// текущий индекс с графика для временного паттерна
        /// </summary>
        private int _curIndex;

        /// <summary>
        /// тип временного паттерна
        /// </summary>
        private PatternType _curTempPatternType;

        /// <summary>
        /// провести тест с учётом временного паттерна
        /// </summary>
        /// <param name="neadToPaint">нужно ли прорисовывать результаты тестов</param>
        public void BackTestTempPattern(bool neadToPaint)
        {
            if (string.IsNullOrEmpty(SecurityToInter))
            {
                return;
            }

            try
            {
                IPattern pattern = GetTempPattern(_curTempPatternType);
                pattern.Weigth = WeigthToTempPattern;
                pattern.Expand = ExpandToTempPattern;

                if (pattern.Weigth == 0)
                {
                    return;
                }
                if (pattern.Expand == 0)
                {
                    return;
                }

                if (PlaceToUsePattern == UsePatternType.OpenPosition)
                {
                    PatternsToOpen.Add(pattern);
                }
                else
                {
                    PatternsToClose.Add(pattern);
                }

                Test(PatternsToOpen, PatternsToClose);

                if (PlaceToUsePattern == UsePatternType.OpenPosition)
                {
                    PatternsToOpen.Remove(pattern);
                }
                else
                {
                    PatternsToClose.Remove(pattern);
                }

                if (BackTestEndEvent != null)
                {
                    BackTestEndEvent(GetShortReport());
                }

                if (neadToPaint)
                {
                    if (PositionsInTrades.Count != 0)
                    {
                        _chart.SetPosition(PositionsInTrades);
                    }
                    PaintPatternsColorSeries();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// тестирование завершилось
        /// </summary>
        public event Action<string> BackTestEndEvent;

// прорисовка чарта и паттернов

        /// <summary>
        /// основной чарт с графиком
        /// </summary>
        private ChartMaster _chart;

        /// <summary>
        /// прямоугольник под чартом
        /// </summary>
        private Rectangle _rectChart;

        /// <summary>
        /// хост для размещения чарта
        /// </summary>
        private WindowsFormsHost _chartHost;

        /// <summary>
        /// чарт для отрисовки временного паттерна
        /// </summary>
        private ChartPainter _chartTempPattern;

        /// <summary>
        /// чарт для отрисовки одиночного паттерна на вход
        /// </summary>
        private ChartPainter _chartSingleOpenPattern;

        /// <summary>
        /// чарт для отрисовки одиночного паттерна на выход
        /// </summary>
        private ChartPainter _chartSingleClosePattern;

        /// <summary>
        /// индикатор объём
        /// </summary>
        private Volume _volume;

        /// <summary>
        /// отрисовать осноной чарт
        /// </summary>
        public void PaintPrimeChart()
        {
            if (string.IsNullOrEmpty(SecurityToInter))
            {
                return;
            }


            if (_chartHost == null ||
                _rectChart == null)
            {
                return;
            }

            _chart.Clear();
            _chart.StartPaint(_chartHost,_rectChart);

            if (CandleSeries == null)
            {
                return;
            }

            MinerCandleSeries series = CandleSeries.Find(ser => ser.Security.Name == SecurityToInter);

            if (series == null)
            {
                return;
            }
            
            _chart.SetCandles(series.Candles);

            if (_volume == null)
            {
                _volume = new Volume("Volume", false);
                _volume = (Volume)_chart.CreateIndicator(_volume, "VolumeArea");
            }
            _volume.Process(series.Candles);
            
        }

        /// <summary>
        /// отрисовать на основном чарте найденные в последнем тесте паттерны
        /// </summary>
        private void PaintPatternsColorSeries()
        {
            _chart.RefreshChartColor();
            for (int i = 0; i < ColorSeries.Count; i++)
            {
                _chart.PaintInDifColor(ColorSeries[i].IndexStart, ColorSeries[i].IndexEnd, ColorSeries[i].NameSeries);
            }
        }

        /// <summary>
        /// пользователь кликнул по чарту
        /// </summary>
        void _chart_ClickToIndexEvent(int index)
        {
            _curIndex = index;
            GetPatternToIndex();
        }

        /// <summary>
        /// взять временный паттерн по интексу
        /// </summary>
        public void GetPatternToIndex()
        {
            if (_curIndex == 0)
            {
                return;
            }

            IPattern pattern = GetTempPattern(_curTempPatternType);

            if (string.IsNullOrEmpty(SecurityToInter))
            {
                SendNewLogMessage("Установите бумагу для торговли",LogMessageType.Error);
                return;
            }

            if (WeigthToTempPattern <= 0)
            {
                SendNewLogMessage("Вес не может быть нулевым",LogMessageType.Error);
                return;
            }

            if (PatternsToOpen.Count == 0 &&
                PlaceToUsePattern == UsePatternType.ClosePosition)
            {
                SendNewLogMessage("Нельзя устанавливать паттерн на закрытие позиций в тот момент когда нет ни одного паттерна на открытие!", LogMessageType.Error);
                return;
            }

            _chart.StartPaint(_chartHost, _rectChart);


            MinerCandleSeries series = CandleSeries.Find(ser => ser.Security.Name == SecurityToInter);

            if (series == null)
            {
                SendNewLogMessage("Не найдены исторические данные, установленные для торговли по этому инструменту!", LogMessageType.Error);
                return;
            }

            pattern.SetFromIndex(series.Candles,_chart.Indicators,_curIndex);
            

            PaintTempPattern();
        }

        /// <summary>
        /// прорисовать временный паттерн на его индивидуальном чарте
        /// </summary>
        private void PaintTempPattern()
        {
            IPattern pattern = GetTempPattern(_curTempPatternType);

            PaintSinglePattern(pattern, _chartTempPattern);
        }

        /// <summary>
        /// прорисовать паттерн на открытие на его индивидуальном чарте
        /// </summary>
        public void PaintOpenPattern(int index)
        {
            if (PatternsToOpen.Count <= index)
            {
                return;
            }

            IPattern pattern = PatternsToOpen[index];

            PaintSinglePattern(pattern, _chartSingleOpenPattern);
        }

        /// <summary>
        /// прорисовать паттерн на закрытие на его индивидуальном чарте
        /// </summary>
        public void PaintClosePattern(int index)
        {
            if (PatternsToClose.Count <= index)
            {
                return;
            }
            IPattern pattern = PatternsToClose[index];

            PaintSinglePattern(pattern, _chartSingleClosePattern);
        }

        /// <summary>
        /// прорисовать паттерн на его индивидуальном чарте
        /// </summary>
        private void PaintSinglePattern(IPattern pattern, ChartPainter chart)
        {
            if (chart.GetChart().InvokeRequired)
            {
                chart.GetChart().Invoke(new Action<IPattern, ChartPainter>(PaintSinglePattern), pattern, chart);
                return;
            }
            chart.ClearDataPointsAndSizeValue();
            chart.ClearSeries();

            if (pattern.Type == PatternType.Candle)
            {
                chart.PaintSingleCandlePattern(((PatternCandle)pattern).GetInCandle());
            }
            if (pattern.Type == PatternType.Volume)
            {
                chart.PaintSingleVolumePattern(((PatternVolume)pattern).GetInCandle());
            }
            if (pattern.Type == PatternType.Indicators)
            {
                PatternIndicators pat = (PatternIndicators) pattern;



                for (int i = 0; pat.Indicators != null && i < pat.Indicators.Count; i++)
                {
                    if (chart.IndicatorIsCreate(pat.Indicators[i].Name + "0") == false)
                    {
                        chart.CreateSeries(chart.GetChartArea(pat.Indicators[i].NameArea), pat.Indicators[i].TypeIndicator, pat.Indicators[i].NameSeries + "0");
                    }

                    chart.ProcessIndicator(pat.Indicators[i]);
                }
            }
        }

    }

    /// <summary>
    /// класс сохранающий параметры прорисовки найденного паттерна
    /// </summary>
    public class ChartColorSeries
    {
        /// <summary>
        /// название серии
        /// </summary>
        public string NameSeries;

        /// <summary>
        /// начальный индекс
        /// </summary>
        public int IndexStart;

        /// <summary>
        /// конечный индекс
        /// </summary>
        public int IndexEnd;
    }

    /// <summary>
    /// результаты тестирования одного паттерна
    /// </summary>
    public class TestResult
    {
        public IPattern Pattern;

        public List<Position> Positions;

        public string ShortReport;

        public decimal SummProfit;

        public decimal Mo;
    }


    /// <summary>
    /// количество лотов
    /// </summary>
    public enum LotsCountType
    {
        /// <summary>
        /// один
        /// </summary>
        One,
        /// <summary>
        /// много
        /// </summary>
        All
    }
}
