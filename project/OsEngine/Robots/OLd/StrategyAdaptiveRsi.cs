using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.FoundBots;

namespace OsEngine.OsTrader
{
    public class StrategyAdaptiveRsi : BotPanel
    {
        // сервис
        public StrategyAdaptiveRsi(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _ma = new MovingAverage(name + "Ma", false)
            {
                Lenght = 150,
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
                TypeCalculationAverage = MovingAverageTypeCalculation.VolumeWeighted
            };

            _ma = _tab.CreateCandleIndicator(_ma, "Prime");
            _ma.Save();

            _rsi = new Rsi(name + "rsi", false)
            {
                Lenght = 14,
                ColorBase = Color.DodgerBlue,
                PaintOn = true
            };

            _rsi = _tab.CreateCandleIndicator(_rsi, "SecondArea");
            _rsi.Save();

            _alb = new AdaptiveLookBack(name + "alb", false)
            {
                Lenght = 5,
                ColorBase = Color.DodgerBlue,
                PaintOn = true
            };

            _alb = _tab.CreateCandleIndicator(_alb, "FhirdArea");
            _alb.Save();

            _adaptivRsi = new Line(name + "aRsi", false)
            {
                ColorBase = Color.DarkOrange,
                PaintOn = true
            };

            _adaptivRsi = _tab.CreateCandleIndicator(_adaptivRsi, "SecondArea");
            _adaptivRsi.Save();

            _bollingerUp = new Line(name + "upBollinger", false)
            {
                ColorBase = Color.ForestGreen,
                PaintOn = true,
            };

            _bollingerUp = _tab.CreateCandleIndicator(_bollingerUp, "SecondArea");
            _bollingerUp.Save();

            _bollingerDown = new Line(name + "downBollinger", false)
            {
                ColorBase = Color.DarkRed,
                PaintOn = true,
            };

            _bollingerDown = _tab.CreateCandleIndicator(_bollingerDown, "SecondArea");
            _bollingerDown.Save();

            _tab.CandleFinishedEvent += StrategyAdxVolatility_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.PositionOpeningFailEvent += _tab_PositionOpeningFailEvent;


            IsOn = false;
            Volume = 1;
            SlipageOpenSecond = 20;
            SlipageCloseSecond = 20;
            TimeFrom = 10;
            TimeTo = 22;
            AlertIsOn = false;
            EmulatorIsOn = false;
            Day = 48;

            LagTimeToOpenClose = new TimeSpan(0, 0, 0, 15);
            LagPunctToOpenClose = 20;
            Mode = true;

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
        }

        /// <summary>
        /// взять название робота
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "1StrategyAdaptiveRsi";
        }

        /// <summary>
        /// открыть окно настроек робота
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            FreeStylikStrategyAdaptiveRsiUi ui = new FreeStylikStrategyAdaptiveRsiUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка через которую торгуют боты
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// мувинг
        /// </summary>
        private IIndicator _ma;

        /// <summary>
        /// рсй обыкновенный
        /// </summary>
        private IIndicator _rsi;

        /// <summary>
        /// рчй адаптивный
        /// </summary>
        private IIndicator _adaptivRsi;

        /// <summary>
        /// верхняя линия боллинджера
        /// </summary>
        private IIndicator _bollingerUp;

        /// <summary>
        /// нижняя линия боллинджера
        /// </summary>
        private IIndicator _bollingerDown;

        private IIndicator _alb;

        // настройки публичные

        /// <summary>
        /// вкл/выкл
        /// </summary>
        public bool IsOn;
        /// <summary>
        /// включен ли алерт
        /// </summary>
        public bool AlertIsOn;
        /// <summary>
        /// вкллючен ли эмулятор
        /// </summary>
        public bool EmulatorIsOn;
        /// <summary>
        /// объём
        /// </summary>
        public decimal Volume;
        /// <summary>
        /// проскальзывание на закрытии
        /// </summary>
        public int SlipageCloseSecond;
        /// <summary>
        /// проскальзывание на открытии
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
        /// время начала торговли
        /// </summary>
        public int TimeFrom;
        /// <summary>
        /// время завершения торговли
        /// </summary>
        public int TimeTo;
        /// <summary>
        /// время на исполнение ордера, после чего он будет отозван
        /// </summary>
        public TimeSpan LagTimeToOpenClose;
        /// <summary>
        /// отступ цены от цены ордера, после чего он будет отозван
        /// </summary>
        public decimal LagPunctToOpenClose;
        /// <summary>
        /// если false то используем Adaptiv Rsi
        /// </summary>
        public bool Mode;
        /// <summary>
        /// количество свечей после которых мы выходим
        /// </summary>
        public int Day;
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

                    writer.WriteLine(Mode);
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

                    Mode = Convert.ToBoolean(reader.ReadLine());
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
        /// место работы потока который отключает бота в не торговые часы
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
        /// открылась позиция. входящее событие
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();
        }

        /// <summary>
        /// главная точка входа в робота. Вызывается когда завершается свеча
        /// </summary>
        void StrategyAdxVolatility_CandleFinishedEvent(List<Candle> candles)
        {
            ReloadRsiAdaptiv(candles);
            ReloadUpBollinger(candles);
            ReloadDownBollinger(candles);

            if (candles.Count < 200)
            {
                return;
            }

            if (IsOn == false)
            {
                return;
            }

            DateTime lastTradeTime = candles[candles.Count - 1].TimeStart;

            if (lastTradeTime.Add(_tab.TimeFrame).Hour < TimeFrom && TimeFrom != 0 ||
                lastTradeTime.Add(_tab.TimeFrame).Hour > TimeTo && TimeTo != 0)
            {
                return;
            }

            if (lastTradeTime.Hour == 17 &&
                lastTradeTime.Minute == 30 &&
                lastTradeTime.Day == 13 &&
                lastTradeTime.Month == 01)
            {

            }

            List<Position> positions = _tab.PositionsOpenAll;

            if (positions != null && positions.Count != 0)
            {
                TryClosePosition(positions[0], candles);
            }
            else
            {
                TryOpenPosition(candles);
            }

        }

        /// <summary>
        /// проверить условия на вход в позицию
        /// </summary>
        private void TryOpenPosition(List<Candle> candles)
        {
            if (candles.Count <= ((MovingAverage)_ma).Lenght)
            {
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

            bool uptrend;
            bool downtrend;

            List<decimal> rsi;

            if (Mode == false)
            {
                rsi = ((Rsi)_rsi).Values;
            }
            else
            {
                rsi = ((Line)_adaptivRsi).Values;
            }

            uptrend = (candles[candles.Count - 1].Close > ((MovingAverage)_ma).Values[((MovingAverage)_ma).Values.Count - 1]) && (rsi[rsi.Count - 1] > 0);
            downtrend = (candles[candles.Count - 1].Close < ((MovingAverage)_ma).Values[((MovingAverage)_ma).Values.Count - 1]) && (rsi[rsi.Count - 1] > 0);


            if (uptrend & CrossOver(candles.Count - 1, rsi, _downChanel))
            {// Buy

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.BuyAtMarket(Volume);
                }
                else
                {
                    decimal priceOrder = candles[candles.Count - 1].Close + _tab.Securiti.PriceStep * SlipageOpenFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                    _tab.BuyAtLimit(Volume, priceOrder);
                }
            }
            if (downtrend & CrossUnder(candles.Count - 1, rsi, _upChanel))
            {// Short

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.SellAtMarket(Volume);
                }
                else
                {
                    decimal priceOrder = candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * SlipageOpenFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                    _tab.SellAtLimit(Volume, priceOrder);
                }
            }
        }

        /// <summary>
        /// проверить условия для выхода из позиции
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

            int inPos = 0;

            for (int i = candles.Count - 1; i > -1; i--)
            {
                if (candles[i].TimeStart > position.TimeCreate)
                {
                    inPos++;
                }
                else
                {
                    break;
                }
            }

            if (inPos <= Day - 2)
            {
                return;
            }

            // БАЙ
            if (position.Direction == Side.Sell)
            {
                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                }
                else
                {

                    decimal priceOrder = candles[candles.Count - 1].Close + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                    _tab.CloseAtLimit(position, priceOrder, position.OpenVolume);
                }
            }

            // СЕЛЛ
            if (position.Direction == Side.Buy)
            {

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                }
                else
                {
                    decimal priceOrderSell = candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                    _tab.CloseAtLimit(position, priceOrderSell, position.OpenVolume);
                }
            }
        }

        /// <summary>
        /// первое значение пересекает второе снизу вверх
        /// </summary>
        private bool CrossOver(int index, List<decimal> valuesOne, List<decimal> valuesTwo)
        {
            if (valuesOne[index - 1] <= valuesTwo[index - 1] &&
                valuesOne[index] > valuesTwo[index])
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// первое значение пересекает второе сверху вниз
        /// </summary>
        private bool CrossUnder(int index, List<decimal> valuesOne, List<decimal> valuesTwo)
        {
            if (valuesOne[index - 1] >= valuesTwo[index - 1] &&
                valuesOne[index] < valuesTwo[index])
            {
                return true;
            }

            return false;
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

        // расчёт адаптивной RSI

        /// <summary>
        /// а Rsi
        /// </summary>
        private List<decimal> _rsiValues;

        /// <summary>
        /// перегрузить адаптивную Рсй
        /// </summary>
        private void ReloadRsiAdaptiv(List<Candle> candles)
        {
            if (_rsiValues == null)
            {
                _rsiValues = new List<decimal>();
            }

            if (candles.Count < 150)
            {
                _rsiValues.Add(0);
            }

            if (candles.Count - 1 == _rsiValues.Count)
            {
                // обновляем только последнее значение
                _rsiValues.Add(GetRsi(candles, candles.Count - 1, Math.Max(1, (int)((AdaptiveLookBack)_alb).Values[candles.Count - 1])));
            }
            else
            {
                _rsiValues = new List<decimal>();
                for (int i = 0; i < candles.Count; i++)
                {
                    _rsiValues.Add(GetRsi(candles, i, Math.Max(1, (int)((AdaptiveLookBack)_alb).Values[i])));
                }
            }

            ((Line)_adaptivRsi).ProcessDesimals(_rsiValues);
        }

        /// <summary>
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetRsi(List<Candle> candles, int index, int lenght)
        {
            if (index - lenght - 1 <= 0)
            {
                return 0;
            }

            int startIndex = 1;

            if (index > 150)
            {
                startIndex = index - 150;
            }

            decimal[] priceChangeHigh = new decimal[candles.Count];
            decimal[] priceChangeLow = new decimal[candles.Count];

            decimal[] priceChangeHighAverage = new decimal[candles.Count];
            decimal[] priceChangeLowAverage = new decimal[candles.Count];

            for (int i = startIndex; i < candles.Count; i++)
            {
                if (candles[i].Close - candles[i - 1].Close > 0)
                {
                    priceChangeHigh[i] = candles[i].Close - candles[i - 1].Close;
                    priceChangeLow[i] = 0;
                }
                else
                {
                    priceChangeLow[i] = candles[i - 1].Close - candles[i].Close;
                    priceChangeHigh[i] = 0;
                }

                MovingAverageHard(priceChangeHigh, priceChangeHighAverage, lenght, i);
                MovingAverageHard(priceChangeLow, priceChangeLowAverage, lenght, i);
            }

            decimal averageHigh = priceChangeHighAverage[index];
            decimal averageLow = priceChangeLowAverage[index];

            decimal rsi;

            if (averageHigh != 0 &&
                averageLow != 0)
            {
                rsi = 100 * (1 - averageLow / (averageLow + averageHigh));
                //rsi = 100 - 100 / (1 + averageHigh / averageLow);
            }
            else
            {
                rsi = 0;
            }

            return Math.Round(rsi, 4);
        }

        /// <summary>
        /// взять экспоненциальную среднюю по индексу
        /// </summary>
        /// <param name="valuesSeries">сирия данных для рассчёта индекса</param>
        /// <param name="moving">предыдущие значения средней</param>
        /// <param name="length">длинна машки</param>
        /// <param name="index">индекс</param>
        private void MovingAverageHard(decimal[] valuesSeries, decimal[] moving, int length, int index)
        {
            if (index == length)
            { // это первое значение. Рассчитываем как простую машку

                decimal lastMoving = 0;

                for (int i = index; i > index - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                lastMoving = lastMoving / length;

                moving[index] = lastMoving;
            }
            else if (index > length)
            {
                // decimal a = 2.0m / (length * 2 - 0.15m);

                decimal a = Math.Round(2.0m / (length * 2), 4);

                decimal lastValueMoving = moving[index - 1];

                decimal lastValueSeries = Math.Round(valuesSeries[index], 4);

                decimal nowValueMoving;

                //if (lastValueSeries != 0)
                // {
                nowValueMoving = Math.Round(lastValueMoving + a * (lastValueSeries - lastValueMoving), 4);
                // }
                // else
                // {
                //     nowValueMoving = lastValueMoving;
                // }

                moving[index] = nowValueMoving;
            }
        }

        // расчёт линии верхней боллинджера

        /// <summary>
        /// верхний канал
        /// </summary>
        private List<decimal> _upChanel;

        /// <summary>
        /// пересчитать верхний канал
        /// </summary>
        private void ReloadUpBollinger(List<Candle> candles)
        {
            if (_upChanel == null)
            {
                _upChanel = new List<decimal>();
            }

            List<decimal> values;

            if (!Mode)
            {
                values = ((Rsi)_rsi).Values;
            }
            else
            {
                values = ((Line)_adaptivRsi).Values;
            }

            if (candles.Count - 1 == _upChanel.Count)
            {

                // обновляем только последнее значение
                _upChanel.Add(GetUpBollinger(candles.Count - 1, values));
            }
            else
            {
                _upChanel = new List<decimal>();
                for (int i = 0; i < candles.Count; i++)
                {
                    _upChanel.Add(GetUpBollinger(i, values));
                }
            }

            ((Line)_bollingerUp).ProcessDesimals(_upChanel);
        }

        /// <summary>
        /// взять верхний боллинджер по индексу
        /// </summary>
        private decimal GetUpBollinger(int index, List<decimal> values)
        {
            int lenght = 100;
            int deviation = 2;

            if (index - lenght - 1 <= 0)
            {
                return 0;
            }

            // 1 считаем СМА

            double valueSma = 0;

            for (int i = index - lenght + 1; i < index + 1; i++)
            {
                // бежим по прошлым периодам и собираем значения
                valueSma += Convert.ToDouble(values[i]);
            }

            valueSma = valueSma / lenght;

            // 2 считаем среднее отклонение

            // находим массив отклонений от средней
            double[] valueDev = new double[lenght];
            for (int i = index - lenght + 1, i2 = 0; i < index + 1; i++, i2++)
            {
                // бежим по прошлым периодам и собираем значения
                valueDev[i2] = Convert.ToDouble(values[i]) - valueSma;
            }

            // возводим этот массив в квадрат
            for (int i = 0; i < valueDev.Length; i++)
            {
                valueDev[i] = Math.Pow(Convert.ToDouble(valueDev[i]), 2);
            }

            // складываем

            double summ = 0;

            for (int i = 0; i < valueDev.Length; i++)
            {
                summ += Convert.ToDouble(valueDev[i]);
            }

            //делим полученную сумму на количество элементов в выборке (или на n-1, если n>30)
            if (lenght > 30)
            {
                summ = summ / (lenght - 1);
            }
            else
            {
                summ = summ / lenght;
            }
            // вычисляем корень

            summ = Math.Sqrt(summ);

            // 3 считаем линии боллинжера

            double result = valueSma + summ * deviation;

            return Convert.ToDecimal(Math.Round(result, 4));
        }


        // расчёт линии нижней боллинджера

        /// <summary>
        /// нижняя линия боллинджера
        /// </summary>
        private List<decimal> _downChanel;

        /// <summary>
        /// пересчитать нижнию линию боллинджера
        /// </summary>
        private void ReloadDownBollinger(List<Candle> candles)
        {
            if (_downChanel == null)
            {
                _downChanel = new List<decimal>();
            }

            List<decimal> values;

            if (!Mode)
            {
                values = ((Rsi)_rsi).Values;
            }
            else
            {
                values = ((Line)_adaptivRsi).Values;
            }

            if (candles.Count - 1 == _downChanel.Count)
            {
                // обновляем только последнее значение
                _downChanel.Add(GetDownBollinger(candles.Count - 1, values));
            }
            else
            {
                _downChanel = new List<decimal>();
                for (int i = 0; i < candles.Count; i++)
                {
                    _downChanel.Add(GetDownBollinger(i, values));
                }
            }

            ((Line)_bollingerDown).ProcessDesimals(_downChanel);
        }

        /// <summary>
        /// взять нижнюю линию боллинджера по индексу
        /// </summary>
        private decimal GetDownBollinger(int index, List<decimal> values)
        {
            int lenght = 100;
            int deviation = 2;

            if (index - lenght - 1 <= 0)
            {
                return 0;
            }

            // 1 считаем СМА

            double valueSma = 0;

            for (int i = index - lenght + 1; i < index + 1; i++)
            {
                // бежим по прошлым периодам и собираем значения
                valueSma += Convert.ToDouble(values[i]);
            }

            valueSma = valueSma / lenght;

            // 2 считаем среднее отклонение

            // находим массив отклонений от средней
            double[] valueDev = new double[lenght];
            for (int i = index - lenght + 1, i2 = 0; i < index + 1; i++, i2++)
            {
                // бежим по прошлым периодам и собираем значения
                valueDev[i2] = Convert.ToDouble(values[i]) - valueSma;
            }

            // возводим этот массив в квадрат
            for (int i = 0; i < valueDev.Length; i++)
            {
                valueDev[i] = Math.Pow(Convert.ToDouble(valueDev[i]), 2);
            }

            // складываем

            double summ = 0;

            for (int i = 0; i < valueDev.Length; i++)
            {
                summ += Convert.ToDouble(valueDev[i]);
            }

            //делим полученную сумму на количество элементов в выборке (или на n-1, если n>30)
            if (lenght > 30)
            {
                summ = summ / (lenght - 1);
            }
            else
            {
                summ = summ / lenght;
            }
            // вычисляем корень

            summ = Math.Sqrt(summ);

            // 3 считаем линии боллинжера

            double result = valueSma - summ * deviation;

            return Convert.ToDecimal(Math.Round(result, 4));
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
        /// включена ли реактивация
        /// </summary>
        private bool _reActivatorIsOn;

        /// <summary>
        /// время до которого включена реАктивация
        /// </summary>
        private DateTime _reActivatorMaxTime;

        /// <summary>
        /// цена реАктивации ордера
        /// </summary>
        private decimal _reActivatorPrice;

        /// <summary>
        /// ордер для реАктивации
        /// </summary>
        private Order _reActivatorOrder;

        /// <summary>
        /// загрузить новый ордер для просмотра на реАктивацию
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
        /// прогрузить реАктиватор трейдом
        /// </summary>
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
                _tab.SetNewLogMessage("Перевыставляем ордер", LogMessageType.System);

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
                _tab.SetNewLogMessage("Перевыставляем ордер", LogMessageType.System);
                _reActivatorIsOn = false;
                ReActivateOrder(_reActivatorOrder);
            }
        }

        /// <summary>
        /// раАктивировать ордер
        /// </summary>
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
        /// ордера в системе
        /// </summary>
        private List<Order> _myOrders;

        /// <summary>
        /// входящий из системы ордер
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
        /// входящий из системы тик
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

            if (candles.Count < 20)
            {
                ClearPoint();
                return 0;
            }

            int currentPosition = 0;

            for (int i = 200; i < candles.Count - 1; i++)
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
        /// пробовать войти в эмуляторе
        /// </summary>
        private int TryInter(List<Candle> candles, int index)
        {
            if (candles.Count <= ((MovingAverage)_ma).Lenght)
            {
                return 0;
            }
            bool uptrend;
            bool downtrend;

            List<decimal> rsi;

            if (!Mode)
            {
                rsi = ((Rsi)_rsi).Values;
            }
            else
            {
                rsi = ((Line)_adaptivRsi).Values;
            }

            uptrend = (candles[index].Close > ((MovingAverage)_ma).Values[index]) && (rsi[index] > 0);
            downtrend = (candles[index].Close < ((MovingAverage)_ma).Values[index]) && (rsi[index] > 0);

            if (uptrend & CrossOver(index, rsi, _downChanel))
            {// Buy

                if (BuyAtLimit(candles, index + 1, candles[index + 1].Open))
                {
                    _lastEmuEnter = candles[index + 1].TimeStart;
                    PaintOpen(1, candles, index + 1, candles[index + 1].Open);
                    return 1;
                }
            }
            if (downtrend & CrossUnder(index, rsi, _upChanel))
            {// Short

                if (SellAtLimit(candles, index + 1, candles[index + 1].Open))
                {
                    _lastEmuEnter = candles[index + 1].TimeStart;
                    PaintOpen(-1, candles, index + 1, candles[index + 1].Open);
                    return -1;
                }
            }
            return 0;
        }

        /// <summary>
        /// последнее время входа в эмуляторе
        /// </summary>
        private DateTime _lastEmuEnter;

        /// <summary>
        /// пробовать выйти в эмуляторе
        /// </summary>
        private int TryOut(List<Candle> candles, int index, int currentPos)
        {
            int inPos = 0;

            for (int i = index; i > -1; i--)
            {
                if (candles[i].TimeStart > _lastEmuEnter)
                {
                    inPos++;
                }
                else
                {
                    break;
                }
            }

            if (inPos <= Day - 2)
            {
                return currentPos;
            }


            if (currentPos == 1)
            {
                if (SellAtLimit(candles, index + 1, candles[index + 1].Open))
                {
                    PaintClose(1, candles, index, candles[index + 1].Open);
                    return 0;
                }
            }

            if (currentPos == -1)
            {
                if (BuyAtLimit(candles, index + 1, candles[index + 1].Open))
                {
                    PaintClose(-1, candles, index, candles[index + 1].Open);
                    return 0;
                }
            }

            return currentPos;
        }

        /// <summary>
        /// купить лимит в эмуляторе
        /// </summary>
        private bool BuyAtLimit(List<Candle> candles, int index, decimal price)
        {
            if (candles[index].High >= price)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// продать лимит в эмуляторе
        /// </summary>
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
        /// все точки на графике этого робота
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
        /// очистить все точки на графике
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
