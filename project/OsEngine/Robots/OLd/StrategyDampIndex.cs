using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab;
using MessageBox = System.Windows.MessageBox;
using Rectangle = System.Windows.Shapes.Rectangle;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots.FoundBots;

namespace OsEngine.OsTrader
{
    public class StrategyDampIndex : BotPanel
    {

        // сервис

        public StrategyDampIndex(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _smaHighIndex = new MovingAverage(name + "MovingHighIndex", false)
            {
                Lenght = 5,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHighIndex = _tab.CreateCandleIndicator(_smaHighIndex, "Prime");
            _smaHighIndex.Save();

            _smaHigh2Period = new MovingAverage(name + "MovingHigh2Period", false)
            {
                Lenght = 2,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHigh2Period = _tab.CreateCandleIndicator(_smaHigh2Period, "Prime");
            _smaHigh2Period.Save();

            _smaHigh3Period = new MovingAverage(name + "MovingHigh3Period", false)
            {
                Lenght = 3,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHigh3Period = _tab.CreateCandleIndicator(_smaHigh3Period, "Prime");
            _smaHigh3Period.Save();


            _smaHigh4Period = new MovingAverage(name + "MovingHigh4Period", false)
            {
                Lenght = 4,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };

            _smaHigh4Period = _tab.CreateCandleIndicator(_smaHigh4Period, "Prime");
            _smaHigh4Period.Save();


            _dampIndex = new Line(name + "dampIndex", false)
            {
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };

            _dampIndex = _tab.CreateCandleIndicator(_dampIndex, "dampArea");
            _dampIndex.Save();

            _smaLowIndex = new MovingAverage(name + "MovingLowIndex", false)
            {
                Lenght = 5,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLowIndex = _tab.CreateCandleIndicator(_smaLowIndex, "Prime");
            _smaLowIndex.Save();

            _smaLow2Period = new MovingAverage(name + "Moving2LowPeriod", false)
            {
                Lenght = 2,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow2Period = _tab.CreateCandleIndicator(_smaLow2Period, "Prime");
            _smaLow2Period.Save();


            _smaLow3Period = new MovingAverage(name + "Moving3LowPeriod", false)
            {
                Lenght = 3,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow3Period = _tab.CreateCandleIndicator(_smaLow3Period, "Prime");
            _smaLow3Period.Save();

            _smaLow4Period = new MovingAverage(name + "Moving4LowPeriod", false)
            {
                Lenght = 4,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow4Period = _tab.CreateCandleIndicator(_smaLow4Period, "Prime");
            _smaLow4Period.Save();

            _tab.CandleFinishedEvent += StrategyAdxVolatility_CandleFinishedEvent;

            IsOn = false;
            Volume = 1;
            SlipageOpenSecond = 20;
            SlipageCloseSecond = 0;
            TimeFrom = 10;
            TimeTo = 22;
            AlertIsOn = false;
            EmulatorIsOn = false;
            _alert = new AlertToPrice(NameStrategyUniq);
            _alert.IsOn = false;
            _tab.DeleteAllAlerts();
            _tab.SetNewAlert(_alert);

            LagTimeToOpenClose = new TimeSpan(0, 0, 0, 15);
            LagPunctToOpenClose = 20;
            SlipageReversClose = 0;
            SlipageToAlert = 10;
            NeadToPaintEmu = false;

            Load();

            Thread worker = new Thread(TimeWatcherArea);
            worker.IsBackground = true;
            worker.Start();

            Thread worker2 = new Thread(WatcherOpenPosition);
            worker2.IsBackground = true;
            worker2.Start();

            Thread worker3 = new Thread(AreaCloserPositionThread);
            worker3.IsBackground = true;
            worker3.Start();

            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent;
            _tab.NewTickEvent += _tab_NewTickEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;
            _tab.PositionOpeningFailEvent += _tab_PositionOpeningFailEvent;

        }

        /// <summary>
        /// взять уникальное название робота
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "1StrategyDampIndex";
        }

        /// <summary>
        /// показать окно настроек робота
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            FreeStylikDampIndexUi ui = new FreeStylikDampIndexUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка робота для торговли
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        private IIndicator _smaHighIndex;
        private IIndicator _smaLowIndex;
        private IIndicator _smaHigh2Period;
        private IIndicator _smaHigh3Period;
        private IIndicator _smaHigh4Period;
        private IIndicator _smaLow2Period;
        private IIndicator _smaLow3Period;
        private IIndicator _smaLow4Period;
        private IIndicator _dampIndex;

        // настройки публичные

        /// <summary>
        /// вкл / выкл
        /// </summary>
        public bool IsOn;
        /// <summary>
        /// включены ли алерты
        /// </summary>
        public bool AlertIsOn;
        /// <summary>
        /// включен ли эмулятор
        /// </summary>
        public bool EmulatorIsOn;
        /// <summary>
        /// объём для входа
        /// </summary>
        public decimal Volume;
        /// <summary>
        /// проскальзывание для закрытия
        /// </summary>
        public int SlipageCloseSecond;
        /// <summary>
        /// проскальзывание на открытие
        /// </summary>
        public int SlipageOpenSecond;
        /// <summary>
        /// проскальзывание для закрытия первый ордер
        /// </summary>
        public int SlipageCloseFirst;
        /// <summary>
        /// проскальзывание на открытие первый ордер
        /// </summary>
        public int SlipageOpenFirst;
        /// <summary>
        /// время начала торгов
        /// </summary>
        public int TimeFrom;
        /// <summary>
        /// время завершения торгов
        /// </summary>
        public int TimeTo;
        /// <summary>
        /// количество свечей после которых мы выходим
        /// </summary>
        public int Day;
        /// <summary>
        /// время для открытия ордера, после чего он будет отозван
        /// </summary>
        public TimeSpan LagTimeToOpenClose;
        /// <summary>
        /// откат цены от цены выставления для отзыва ордера при его открытии / закрытии
        /// </summary>
        public decimal LagPunctToOpenClose;
        /// <summary>
        /// обратное проскальзывание для цены активации стопОрдера
        /// </summary>
        public int SlipageReversClose;
        /// <summary>
        /// проскальзывание для алерта
        /// </summary>
        public int SlipageToAlert;
        /// <summary>
        /// алерт
        /// </summary>
        private AlertToPrice _alert;
        /// <summary>
        /// нужно ли прорисовывать сделки эмулятора
        /// </summary>
        public bool NeadToPaintEmu;

        /// <summary>
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(IsOn);
                    writer.WriteLine(Volume);
                    writer.WriteLine(SlipageOpenSecond);
                    writer.WriteLine(SlipageCloseSecond);
                    writer.WriteLine(TimeFrom);
                    writer.WriteLine(TimeTo);
                    writer.WriteLine(AlertIsOn);
                    writer.WriteLine(LagTimeToOpenClose);
                    writer.WriteLine(LagPunctToOpenClose);
                    writer.WriteLine(SlipageReversClose);
                    writer.WriteLine(SlipageToAlert);
                    writer.WriteLine(Day);
                    writer.WriteLine(EmulatorIsOn);

                    writer.WriteLine(SlipageCloseFirst);
                    writer.WriteLine(SlipageOpenFirst);
                    writer.WriteLine(NeadToPaintEmu);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    IsOn = Convert.ToBoolean(reader.ReadLine());
                    Volume = Convert.ToDecimal(reader.ReadLine());
                    SlipageOpenSecond = Convert.ToInt32(reader.ReadLine());
                    SlipageCloseSecond = Convert.ToInt32(reader.ReadLine());
                    TimeFrom = Convert.ToInt32(reader.ReadLine());
                    TimeTo = Convert.ToInt32(reader.ReadLine());
                    AlertIsOn = Convert.ToBoolean(reader.ReadLine());
                    TimeSpan.TryParse(reader.ReadLine(), out LagTimeToOpenClose);
                    LagPunctToOpenClose = Convert.ToDecimal(reader.ReadLine());
                    SlipageReversClose = Convert.ToInt32(reader.ReadLine());
                    SlipageToAlert = Convert.ToInt32(reader.ReadLine());
                    Day = Convert.ToInt32(reader.ReadLine());
                    EmulatorIsOn = Convert.ToBoolean(reader.ReadLine());

                    SlipageCloseFirst = Convert.ToInt32(reader.ReadLine());
                    SlipageOpenFirst = Convert.ToInt32(reader.ReadLine());
                    NeadToPaintEmu = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        // расчёт индикатора DampIndex

        /// <summary>
        /// damp index
        /// </summary>
        private List<decimal> _index;

        /// <summary>
        /// перезагрузить индикатор дамп индекс
        /// </summary>
        private void ReloadDampIndex()
        {
            if (_index == null)
            {
                _index = new List<decimal>();
            }

            if (((MovingAverage)_smaHighIndex).Values.Count - 1 == _index.Count)
            {
                // обновляем только последнее значение
                _index.Add(GetDapmIndex(((MovingAverage)_smaHighIndex).Values.Count - 1));
            }
            else
            {
                _index = new List<decimal>();
                for (int i = 0; i < ((MovingAverage)_smaHighIndex).Values.Count; i++)
                {
                    _index.Add(GetDapmIndex(i));
                }
            }


            ((Line)_dampIndex).ProcessDesimals(_index);
        }

        /// <summary>
        /// взять дапм индекс по индексу
        /// </summary>
        private decimal GetDapmIndex(int index)
        {
            decimal result = 0;

            try
            {
                if (index - ((MovingAverage)_smaHighIndex).Lenght >= 0 &&
                    index - ((MovingAverage)_smaLowIndex).Lenght >= 0)
                {
                    decimal smaHigh = ((MovingAverage)_smaHighIndex).Values[index];
                    decimal smaHigh2 = ((MovingAverage)_smaHighIndex).Values[index - ((MovingAverage)_smaHighIndex).Lenght];
                    decimal smaLow = ((MovingAverage)_smaLowIndex).Values[index];
                    decimal smaLow2 = ((MovingAverage)_smaLowIndex).Values[index - ((MovingAverage)_smaLowIndex).Lenght];

                    if (smaHigh2 - smaLow2 != 0)
                    {
                        result = Math.Round((smaHigh - smaLow) / (smaHigh2 - smaLow2), 4);
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
            return result;
        }

        // логика

        void _tab_PositionOpeningFailEvent(Position position)
        {
            if (!string.IsNullOrWhiteSpace(position.Comment))
            {
                return;
            }

            if (position.OpenVolume != 0)
            {
                return;
            }

            if (StartProgram == StartProgram.IsTester)
            {
                return;
            }

            if (position.OpenOrders.Count > 1 ||
            position.Comment == "Second")
            {
                return;
            }

            List<Position> openPos = _tab.PositionsOpenAll;
            if (openPos != null && openPos.Count > 1 ||
               openPos != null && openPos.Count == 1 &&
                openPos[0].Direction == position.Direction)
            {
                return;
            }

            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            if (position.Direction == Side.Buy)
            {
                decimal price = _tab.PriceBestBid + SlipageOpenSecond * _tab.Securiti.PriceStep;

                Position pos = _tab.BuyAtLimit(position.OpenOrders[0].Volume, price);
                pos.Comment = "Second";
            }
            else if (position.Direction == Side.Sell)
            {
                decimal price = _tab.PriceBestAsk - SlipageOpenSecond * _tab.Securiti.PriceStep;

                Position pos = _tab.SellAtLimit(position.OpenOrders[0].Volume, price);
                pos.Comment = "Second";
            }
        }

        /// <summary>
        /// ошибка с закрытием заявки
        /// </summary>
        void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.OpenVolume > 0)
            {
                position.State = PositionStateType.Open;
            }
            if (position.OpenVolume < 0)
            {
                position.State = PositionStateType.ClosingSurplus;
            }
            if (StartProgram == StartProgram.IsTester)
            {
                return;
            }
            if (_positionToClose != null && _positionToClose.Number == position.Number)
            {
                return;
            }

            if (position.OpenVolume == 0)
            {
                return;
            }

            if (position.CloseOrders.Count > 1)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                decimal price = _tab.PriceBestAsk - SlipageCloseSecond * _tab.Securiti.PriceStep;
                _tab.CloseAtLimit(position, price, position.OpenVolume);
            }
            else if (position.Direction == Side.Sell)
            {
                decimal price = _tab.PriceBestBid + SlipageCloseSecond * _tab.Securiti.PriceStep;
                _tab.CloseAtLimit(position, price, position.OpenVolume);
            }
        }

        /// <summary>
        /// метод в котором работает поток следящий за тем чтобы робот переставал 
        /// торговать после определённого времени
        /// </summary>
        private void TimeWatcherArea()
        {
            if (StartProgram == StartProgram.IsTester)
            {
                return;
            }
            while (true)
            {
                Thread.Sleep(3000);

                DateTime lastTradeTime = DateTime.Now;

                if (lastTradeTime.Hour <= 8)
                {
                    continue;
                }

                if (lastTradeTime.Hour < TimeFrom && TimeFrom != 0 ||
                    lastTradeTime.Hour > TimeTo && TimeTo != 0)
                {
                    List<Position> positions = _tab.PositionsOpenAll;

                    if (positions == null || positions.Count == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < positions.Count; i++)
                    {
                        Position pos = positions[i];


                        if (pos.StopOrderIsActiv == true ||
                            pos.ProfitOrderIsActiv == true)
                        {
                            pos.StopOrderIsActiv = false;
                            pos.ProfitOrderIsActiv = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// главный вход в логику робота. Вызывается когда на рынке заканчивается свеча
        /// </summary>
        void StrategyAdxVolatility_CandleFinishedEvent(List<Candle> candles)
        {
            ReloadDampIndex();

            if (IsOn == false)
            {
                return;
            }

            if (candles.Count < 14)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            DateTime lastTradeTime = candles[candles.Count - 1].TimeStart;

            if (lastTradeTime.Add(_tab.TimeFrame).Hour < TimeFrom && TimeFrom != 0 ||
                lastTradeTime.Add(_tab.TimeFrame).Hour > TimeTo && TimeTo != 0)
            {
                for (int i = 0; positions != null && i < positions.Count; i++)
                {
                    positions[i].StopOrderIsActiv = false;
                    positions[i].ProfitOrderIsActiv = false;
                }

                return;
            }

            if (positions == null || positions.Count == 0)
            {
                TryOpenPosition(candles);
            }
            else
            {
                TryClosePosition(positions[0], candles);
            }
        }

        /// <summary>
        /// проверить условия на вход в позицию
        /// </summary>
        private void TryOpenPosition(List<Candle> candles)
        {
            decimal damping;
            decimal avgHigh2;
            decimal avgLow3;
            decimal avgLow2;
            decimal avgHigh3;
            decimal avgHighShifted;
            decimal avgLowShifted;
            try
            {
                if (((MovingAverage)_smaHigh4Period).Values.Count - 1 - 10 < 0)
                {
                    return;
                }

                damping = ((Line)_dampIndex).Values[((Line)_dampIndex).Values.Count - 1];
                avgHigh2 = ((MovingAverage)_smaHigh2Period).Values[((MovingAverage)_smaHigh2Period).Values.Count - 1]; // SMA.Series(High, 2);
                avgLow3 = ((MovingAverage)_smaLow3Period).Values[((MovingAverage)_smaLow3Period).Values.Count - 1]; //SMA.Series(Low, 3);
                avgLow2 = ((MovingAverage)_smaLow3Period).Values[((MovingAverage)_smaLow3Period).Values.Count - 1]; //SMA.Series(Low, 3);
                avgHigh3 = ((MovingAverage)_smaHigh3Period).Values[((MovingAverage)_smaHigh3Period).Values.Count - 1];//SMA.Series(High, 3);
                avgHighShifted = ((MovingAverage)_smaHigh4Period).Values[((MovingAverage)_smaHigh4Period).Values.Count - 1 - 10];//SMA.Series(High, 4) >> 10;
                avgLowShifted = ((MovingAverage)_smaLow4Period).Values[((MovingAverage)_smaLow4Period).Values.Count - 1 - 10]; //SMA.Series(Low, 4) >> 10;
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
                return;
            }

            if (EmulatorIsOn)
            {
                int currentEmuPos = GetCurrentPosition();
                if (currentEmuPos != 0)
                {
                    return;
                }
            }

            if (damping < 1 && (candles[candles.Count - 1].Close > avgHigh2) && (avgLow3 > avgHighShifted))
            {
                _tab.SetNewLogMessage("Системный вход в лонг. Время: " + candles[candles.Count - 1].TimeStart, LogMessageType.Signal);

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.BuyAtMarket(Volume);
                }
                else
                {
                    _tab.BuyAtLimit(Volume, candles[candles.Count - 1].Close + _tab.Securiti.PriceStep * SlipageOpenFirst); // ЗДЕСЬ!!!!!!!!!!!!!!
                }

                return;
            }
            else if (damping < 1 && (candles[candles.Count - 1].Close < avgLow2) && (avgHigh3 < avgLowShifted))
            {
                _tab.SetNewLogMessage("Системный вход в шорт. Время: " + candles[candles.Count - 1].TimeStart, LogMessageType.Signal);

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.SellAtMarket(Volume);
                }
                else
                {
                    _tab.SellAtLimit(Volume, candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * SlipageOpenFirst); // ЗДЕСЬ!!!!!!!!!!!!!!
                }

                return;
            }
            else if (damping <= 1)
            {
                if (AlertIsOn)
                {
                    AlertMessageManager.ThrowAlert(Properties.Resources.Duck, NameStrategyUniq, "Внимание! DI меньше 1, но входа не произошло. На следующей свече возможно открытие");
                }
            }
        }

        /// <summary>
        /// выставить стоп приказ по открытой позиции
        /// </summary>
        private void TryClosePosition(Position position, List<Candle> candles)
        {
            if (EmulatorIsOn)
            {
                int currentEmuPos = GetCurrentPosition();

                if (currentEmuPos == 0 ||
                    currentEmuPos == 1 && position.Direction == Side.Sell ||
                    currentEmuPos == -1 && position.Direction == Side.Buy)
                {
                    _tab.SetNewLogMessage("Кроем позицию по эмулятору. Номер позиции: " + position.Number, LogMessageType.System);
                    // Выход по эмулятору! позиции нет. Нужно закрывать полюбой цене
                    _tab.CloseAllOrderToPosition(position);
                    _timeToClose = DateTime.Now.AddSeconds(3);
                    _positionToClose = position;
                    return;
                }
            }

            if (position.Direction == Side.Buy)
            {
                decimal priceEtalon = GetPriseStop(Side.Buy, candles.Count - 1);

                decimal priceOrder = priceEtalon - _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = priceEtalon + SlipageReversClose * _tab.Securiti.PriceStep;

                if (priceRedLine - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                _tab.CloseAtStop(position, priceRedLine, priceOrder);

                if (StartProgram != StartProgram.IsTester && AlertIsOn)
                {
                    _alert.PriceActivation = priceRedLine + SlipageToAlert * _tab.Securiti.PriceStep;
                    _alert.TypeActivation = PriceAlertTypeActivation.PriceLowerOrEqual;
                    _alert.MessageIsOn = true;
                    _alert.MusicType = AlertMusic.Duck;
                    _alert.Message = "Приближаемся к точке выхода";
                    _alert.IsOn = true;
                }
            }

            if (position.Direction == Side.Sell)
            {
                decimal priceEtalon = GetPriseStop(Side.Sell, candles.Count - 1);

                decimal priceOrder = priceEtalon + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = priceEtalon - SlipageReversClose * _tab.Securiti.PriceStep;

                if (priceRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                _tab.CloseAtStop(position, priceRedLine, priceOrder);

                if (StartProgram != StartProgram.IsTester && AlertIsOn)
                {
                    _alert.PriceActivation = priceRedLine - SlipageToAlert * _tab.Securiti.PriceStep;
                    _alert.TypeActivation = PriceAlertTypeActivation.PriceHigherOrEqual;
                    _alert.MessageIsOn = true;
                    _alert.MusicType = AlertMusic.Duck;
                    _alert.Message = "Приближаемся к точке выхода";
                    _alert.IsOn = true;
                }
            }
        }

        /// <summary>
        /// взять цену для стопПриказа
        /// </summary>
        /// <param name="side">сторона входа</param>
        /// <param name="index">индекс свечи</param>
        /// <returns>цена стопПриказа</returns>
        private decimal GetPriseStop(Side side, int index)
        {
            List<Candle> myCandles = _tab.CandlesAll;

            if (myCandles == null)
            {
                return 0;
            }


            if (side == Side.Buy)
            { // стоп для лонга
                decimal min = decimal.MaxValue;
                for (int i = index; i > -1 && i > index - Day; i--)
                {
                    if (myCandles[i].Low < min)
                    {
                        min = myCandles[i].Low;
                    }
                }
                return min;// + _tab.Securiti.PriceStep;
            }
            else
            { // стоп для шорта
                decimal max = decimal.MinValue;
                for (int i = index; i > -1 && i > index - Day; i--)
                {
                    if (myCandles[i].High > max)
                    {
                        max = myCandles[i].High;
                    }
                }
                return max;// - _tab.Securiti.PriceStep;
            }
        }

        // отложенное закрытие позиции. Чтобы при выходе по эмулятору дать системе время отозвать ордер
        private Position _positionToClose;

        private DateTime _timeToClose;

        private void AreaCloserPositionThread()
        {
            while (true)
            {
                Thread.Sleep(1000);
                if (_positionToClose == null)
                {
                    continue;
                }

                if (DateTime.Now < _timeToClose)
                {
                    continue;
                }

                if (_positionToClose.OpenVolume != 0 && _positionToClose.Direction == Side.Buy)
                {
                    _tab.CloseAtLimit(_positionToClose, _tab.PriceBestAsk - _tab.Securiti.PriceStep * 10, _positionToClose.OpenVolume);
                }
                else if (_positionToClose.OpenVolume != 0 && _positionToClose.Direction == Side.Sell)
                {
                    _tab.CloseAtLimit(_positionToClose, _tab.PriceBestAsk + _tab.Securiti.PriceStep * 10, _positionToClose.OpenVolume);
                }

                _positionToClose = null;
            }
        }

        // отзыв заявок по времени и отступу

        /// <summary>
        /// слежение за выставленными и ещё не исполненными ордерами
        /// </summary>
        void WatcherOpenPosition()
        {
            while (true)
            {
                Thread.Sleep(1000);
                // этот метод создан для того, чтобы инициализировать закрытие 
                // не полностью открытых ордеров в конце периода
                if (StartProgram == StartProgram.IsTester)
                { // если тестируем
                    return;
                }

                Thread.Sleep(1000);

                try
                {

                    List<Position> positions = _tab.PositionsOpenAll;

                    if (positions == null ||
                        positions.Count == 0)
                    {
                        continue;
                    }

                    // смотрим первый выход - 3 секунды 

                    List<Order> myOrderToFirstClose = new List<Order>();

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].OpenOrders.Count == 1 &&
                            positions[i].OpenOrders[0].State == OrderStateType.Activ &&
                            positions[i].Comment != "Second")
                        {
                            myOrderToFirstClose.Add(positions[i].OpenOrders[positions[i].OpenOrders.Count - 1]);
                        }

                        if (positions[i].CloseOrders != null && positions[i].CloseOrders.Count == 1 &&
                            positions[i].CloseOrders[positions[i].CloseOrders.Count - 1].State == OrderStateType.Activ)
                        {
                            myOrderToFirstClose.Add(positions[i].CloseOrders[positions[i].CloseOrders.Count - 1]);
                        }
                    }

                    for (int i = 0; i < myOrderToFirstClose.Count; i++)
                    {
                        if (myOrderToFirstClose[i].TimeCallBack.AddSeconds(3) < _tab.TimeServerCurrent)
                        {
                            _reActivatorIsOn = false;
                            _tab.CloseOrder(myOrderToFirstClose[i]);
                        }
                    }

                    // смотрим классический выход

                    List<Order> myOrder = new List<Order>();

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].OpenOrders[positions[i].OpenOrders.Count - 1].State == OrderStateType.Activ)
                        {
                            myOrder.Add(positions[i].OpenOrders[positions[i].OpenOrders.Count - 1]);
                        }

                        if (positions[i].CloseOrders != null && positions[i].CloseOrders[positions[i].CloseOrders.Count - 1].State == OrderStateType.Activ)
                        {
                            myOrder.Add(positions[i].CloseOrders[positions[i].CloseOrders.Count - 1]);
                        }
                    }


                    for (int i = 0; i < myOrder.Count; i++)
                    {
                        Order order = myOrder[i];
                        // бежим по коллекции ордеров
                        if (order.State != OrderStateType.Done &&
                            order.State != OrderStateType.Fail &&
                            order.State != OrderStateType.None)
                        {
                            // если какойто не исполнен полностью

                            DateTime startTime = order.TimeCallBack;
                            DateTime marketTime = _tab.TimeServerCurrent;

                            if (startTime == DateTime.MinValue ||
                                startTime == DateTime.MaxValue)
                            {
                                continue;
                            }

                            if (startTime.AddSeconds(LagTimeToOpenClose.TotalSeconds) < marketTime)
                            {
                                _tab.CloseOrder(order);
                                Thread.Sleep(2000);
                                if (AlertIsOn)
                                {
                                    AlertMessageManager.ThrowAlert(Properties.Resources.wolf01, NameStrategyUniq, "Отзываем ордер по времени");
                                }
                                _tab.SetNewLogMessage("Отзываем ордер по времени", LogMessageType.System);
                            }
                            else
                            {
                                decimal priceBid = _tab.PriceBestBid;
                                decimal priceAsk = _tab.PriceBestAsk;

                                if (order.Side == Side.Buy &&
                                    order.Price + LagPunctToOpenClose * _tab.Securiti.PriceStep < priceAsk)
                                {
                                    _tab.CloseOrder(order);
                                    Thread.Sleep(2000);
                                    if (AlertIsOn)
                                    {
                                        AlertMessageManager.ThrowAlert(Properties.Resources.wolf01, NameStrategyUniq, "Отзываем ордер по отклонению");
                                    }
                                    _tab.SetNewLogMessage("Отзываем ордер по отклонению", LogMessageType.System);
                                }

                                if (order.Side == Side.Sell &&
                                    order.Price - LagPunctToOpenClose * _tab.Securiti.PriceStep > priceBid)
                                {
                                    _tab.CloseOrder(order);
                                    Thread.Sleep(2000);
                                    if (AlertIsOn)
                                    {
                                        AlertMessageManager.ThrowAlert(Properties.Resources.wolf01, NameStrategyUniq,
                                            "Отзываем ордер по отклонению");
                                    }
                                    _tab.SetNewLogMessage("Отзываем ордер по отклонению", LogMessageType.System);
                                }
                            }
                        }
                        else if (order.State == OrderStateType.Fail)
                        {
                            if (AlertIsOn)
                            {
                                AlertMessageManager.ThrowAlert(Properties.Resources.wolf01, NameStrategyUniq, "Ошибка выставления ордера");
                            }
                            myOrder.Remove(order);
                            i--;
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        // уровень переоткрытия ордеров и уровней для пробоя

        /// <summary>
        /// включена ли слежение за ордером для его переактивации
        /// </summary>
        private bool _reActivatorIsOn;

        /// <summary>
        /// крайнее время переактивации ордера
        /// </summary>
        private DateTime _reActivatorMaxTime;

        /// <summary>
        /// цена при достижении которой ордер будет перевыставлен
        /// </summary>
        private decimal _reActivatorPrice;

        /// <summary>
        /// ордер который нужно перевыставлять если что
        /// </summary>
        private Order _reActivatorOrder;

        /// <summary>
        /// начать следить за ордером для перевыставления
        /// в случае наступления условий
        /// </summary>
        private void AlarmReActivator(Order order, decimal activatePrice, DateTime maxTime)
        {
            if (StartProgram == StartProgram.IsTester)
            {
                return;
            }
            _reActivatorOrder = order;
            _reActivatorMaxTime = maxTime;
            _reActivatorPrice = activatePrice;
            _reActivatorIsOn = true;
        }

        /// <summary>
        /// прогрузить РеАктиватор новым тиком
        /// </summary>
        /// <param name="trade"></param>
        private void ChekReActivator(Trade trade)
        {
            // если ордер отозван
            // и цена пересекла цену переактивации
            // и время активации не кончалось
            // вызываем переактивацию стопов и уровней на пробой
            // отключаем активатор

            if (_reActivatorIsOn == false)
            {
                return;
            }

            if (_reActivatorOrder.State == OrderStateType.Fail ||
                 _reActivatorOrder.State == OrderStateType.Done ||
                _reActivatorOrder.VolumeExecute != 0)
            { // ордер с ошибкой или уже частично исполнен
                _reActivatorIsOn = false;
                return;
            }

            if (_reActivatorOrder.State != OrderStateType.Done &&
                _reActivatorOrder.State != OrderStateType.Cancel)
            { // ордер ещё выставлен
                return;
            }

            if (DateTime.Now > _reActivatorMaxTime)
            {
                _reActivatorIsOn = false;
                return;
            }

            if (_reActivatorOrder.Side == Side.Buy &&
                trade.Price <= _reActivatorPrice)
            {
                if (AlertIsOn)
                {
                    AlertMessageManager.ThrowAlert(null, NameStrategyUniq, "Подошли к уровню выставления ордера после его отзыва. Перевыставляем");
                }
                _tab.SetNewLogMessage("Перевыставляем ордер, бот: " + NameStrategyUniq, LogMessageType.System);
                _reActivatorIsOn = false;
                ReActivateOrder(_reActivatorOrder);
            }
            else if (_reActivatorOrder.Side == Side.Sell &&
                trade.Price >= _reActivatorPrice)
            {
                if (AlertIsOn)
                {
                    AlertMessageManager.ThrowAlert(null, NameStrategyUniq, "Подошли к уровню выставления ордера после его отзыва. Перевыставляем");
                }
                _tab.SetNewLogMessage("Перевыставляем ордер, бот: " + NameStrategyUniq, LogMessageType.System);
                _reActivatorIsOn = false;
                ReActivateOrder(_reActivatorOrder);
            }
        }

        /// <summary>
        /// перевыставить ордер
        /// </summary>
        /// <param name="order">ордер для перевыставления</param>
        private void ReActivateOrder(Order order)
        {
            // 1 находим позицию по которой прошёл ордер

            List<Position> allPositions = _tab.PositionsAll;

            if (allPositions == null)
            {
                return;
            }

            Position myPosition = null;

            for (int i = allPositions.Count - 1; i > -1; i--)
            {
                if (allPositions[i].OpenOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    myPosition = allPositions[i];
                    break;
                }
                if (allPositions[i].CloseOrders != null && allPositions[i].CloseOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    myPosition = allPositions[i];
                    break;
                }
            }

            if (myPosition == null)
            {
                return;
            }

            if (myPosition.OpenVolume == 0)
            {
                if (_reActivatorOrder.Side == Side.Buy)
                {
                    _tab.BuyAtLimit(Convert.ToInt32(_reActivatorOrder.Volume), _reActivatorOrder.Price);
                }
                else if (_reActivatorOrder.Side == Side.Sell)
                {
                    _tab.SellAtLimit(Convert.ToInt32(_reActivatorOrder.Volume), _reActivatorOrder.Price);
                }
            }
            else if (myPosition.OpenVolume != 0)
            {
                _tab.CloseAtLimit(myPosition, order.Price, order.Volume);
            }
        }

        /// <summary>
        /// на бирже изменился ордер
        /// </summary>
        void _tab_OrderUpdateEvent(Order order)
        {
            if (_myOrders == null)
            {
                _myOrders = new List<Order>();
            }

            if (_myOrders.Find(order1 => order1.NumberUser == order.NumberUser) == null)
            {
                _myOrders.Add(order);
                AlarmReActivator(order, order.Price, DateTime.Now + LagTimeToOpenClose);
            }
        }

        /// <summary>
        /// все ордера панели
        /// </summary>
        private List<Order> _myOrders;

        /// <summary>
        /// входящий тик
        /// </summary>
        void _tab_NewTickEvent(Trade trade)
        {
            ChekReActivator(trade);
        }

        // эмулятор

        /// <summary>
        /// взять текущую позицию эмулятора
        /// </summary>
        /// <returns>0 - ничего, 1 - лонг, -1 - шорт</returns>
        private int GetCurrentPosition()
        {
            List<Candle> candles = _tab.CandlesAll;

            if (candles.Count < 14)
            {
                ClearPoint();
                return 0;
            }

            int currentPosition = 0;

            for (int i = 14; i < candles.Count - 1; i++)
            {
                if (candles[i].TimeStart.Add(_tab.TimeFrame).Hour > TimeTo && TimeTo != 0 ||
                    candles[i].TimeStart.Add(_tab.TimeFrame).Hour < TimeFrom && TimeFrom != 0)
                {
                    continue;
                }

                if (currentPosition == 0)
                {
                    currentPosition = TryInter(candles, i);
                }
                else if (currentPosition == -1)
                {
                    currentPosition = TryOut(candles, i, currentPosition);
                }
                else if (currentPosition == 1)
                {
                    currentPosition = TryOut(candles, i, currentPosition);
                }
            }

            return currentPosition;
        }

        /// <summary>
        /// попробовать войти по индексу в эмуляторе
        /// </summary>
        private int TryInter(List<Candle> candles, int index)
        {
            decimal damping;
            decimal avgHigh2;
            decimal avgLow3;
            decimal avgLow2;
            decimal avgHigh3;
            decimal avgHighShifted;
            decimal avgLowShifted;
            try
            {
                if (((MovingAverage)_smaHigh4Period).Values.Count - 1 - 10 < 0)
                {
                    return 0;
                }

                damping = ((Line)_dampIndex).Values[index];
                avgHigh2 = ((MovingAverage)_smaHigh2Period).Values[index]; // SMA.Series(High, 2);
                avgLow3 = ((MovingAverage)_smaLow3Period).Values[index]; //SMA.Series(Low, 3);
                avgLow2 = ((MovingAverage)_smaLow3Period).Values[index]; //SMA.Series(Low, 3);
                avgHigh3 = ((MovingAverage)_smaHigh3Period).Values[index];//SMA.Series(High, 3);
                avgHighShifted = ((MovingAverage)_smaHigh4Period).Values[index - 10];//SMA.Series(High, 4) >> 10;
                avgLowShifted = ((MovingAverage)_smaLow4Period).Values[index - 10]; //SMA.Series(Low, 4) >> 10;
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
                return 0;
            }

            if (damping < 1 && (candles[index].Close > avgHigh2) && (avgLow3 > avgHighShifted))
            {
                PaintOpen(1, candles, index + 1, candles[index + 1].Open);
                return 1;
            }
            else if (damping < 1 && (candles[index].Close < avgLow2) && (avgHigh3 < avgLowShifted))
            {
                PaintOpen(-1, candles, index + 1, candles[index + 1].Open);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// попробовать выйти из позиции по индексу в эмуляторе
        /// </summary>
        private int TryOut(List<Candle> candles, int index, int currentPos)
        {
            if (currentPos == 1)
            {
                decimal priceEtalon = GetPriseStop(Side.Buy, index);

                if (SellAtLimit(candles, index + 1, priceEtalon))
                {
                    PaintClose(1, candles, index, priceEtalon);
                    return 0;
                }
            }

            if (currentPos == -1)
            {
                decimal priceEtalon = GetPriseStop(Side.Sell, index);

                if (BuyAtLimit(candles, index + 1, priceEtalon))
                {
                    PaintClose(-1, candles, index, priceEtalon);
                    return 0;
                }
            }

            return currentPos;
        }

        /// <summary>
        /// купить лимиткой в эмуляторе
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс свечи для входа</param>
        /// <param name="price">цена входа</param>
        private bool BuyAtLimit(List<Candle> candles, int index, decimal price)
        {
            if (candles[index].High >= price)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// продать лимиткой в эмуляторе
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс свечи для входа</param>
        /// <param name="price">цена входа</param>
        private bool SellAtLimit(List<Candle> candles, int index, decimal price)
        {
            if (candles[index].Low <= price)
            {
                return true;
            }
            return false;
        }

        // прорисовка позиций

        /// <summary>
        /// точки входа/выхода эмулятора
        /// </summary>
        private List<PointElement> _points;

        /// <summary>
        /// прорисовать открытие
        /// </summary>
        private void PaintOpen(int posCurrent, List<Candle> candles, int index, decimal price)
        {
            if (NeadToPaintEmu == false)
            {
                return;
            }

            if (_points == null)
            {
                _points = new List<PointElement>();
            }

            PointElement point = _points.Find(element => element.TimePoint == candles[index].TimeStart);

            if (point != null)
            {
                return;
            }

            point = new PointElement(candles[index].TimeStart.ToString(), "Prime");
            point.TimePoint = candles[index].TimeStart;
            point.Style = MarkerStyle.Cross;
            point.Y = price;
            point.Size = 15;
            if (posCurrent == 1)
            {
                point.Color = Color.DarkSeaGreen;
            }
            else
            {
                point.Color = Color.DarkOrchid;
            }

            _points.Add(point);
            _tab.SetChartElement(point);
        }

        /// <summary>
        /// прорисовать закрытие
        /// </summary>
        private void PaintClose(int posLast, List<Candle> candles, int index, decimal price)
        {
            if (NeadToPaintEmu == false)
            {
                return;
            }
            if (_points == null)
            {
                _points = new List<PointElement>();
            }

            PointElement point = _points.Find(element => element.TimePoint == candles[index + 1].TimeStart);

            if (point != null)
            {
                return;
            }

            point = new PointElement(candles[index + 1].TimeStart.ToString(), "Prime");
            point.TimePoint = candles[index + 1].TimeStart;
            point.Style = MarkerStyle.Cross;
            point.Size = 15;

            if (posLast == 1)
            {
                point.Color = Color.Gold;
                point.Y = price;
            }
            else
            {
                point.Color = Color.Gold;
                point.Y = price;
            }

            _points.Add(point);
            _tab.SetChartElement(point);
        }

        /// <summary>
        /// очитстить все точки на графике
        /// </summary>
        private void ClearPoint()
        {
            if (_points == null
                || _points.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _points.Count; i++)
            {
                _tab.DeleteChartElement(_points[i]);

            }
            _points = new List<PointElement>();
        }
    }
}
