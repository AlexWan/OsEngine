using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public class StrategyBreakout : BotPanel
    {

        // сервис
        public StrategyBreakout(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _adx = new Adx(name + "Adx", false)
            {
                Lenght = 14,
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };

            _adx = _tab.CreateCandleIndicator(_adx, "adxArea");
            _adx.Save();

            _alert = new AlertToPrice(NameStrategyUniq);
            _alert.IsOn = false;
            _tab.DeleteAllAlerts();
            _tab.SetNewAlert(_alert);

            _tab.CandleFinishedEvent += StrategyAdxVolatility_CandleFinishedEvent;

            IsOn = false;
            Volume = 1;
            SlipageOpenSecond = 0;
            SlipageCloseSecond = 0;
            TimeFrom = 10;
            TimeTo = 22;
            AlertIsOn = false;
            EmulatorIsOn = false;
            LagTimeToOpenClose = new TimeSpan(0, 0, 0, 15);
            LagPunctToOpenClose = 20;

            AdxHigh = 20;
            Lookback = 20;
            TrailBars = 5;
            SlipageToAlert = 10;
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
        /// взять название робота
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "1StrategyBreakout";
        }

        /// <summary>
        /// открыть окно настроек бота
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            FreeStylikBreakoutUi ui = new FreeStylikBreakoutUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// адх
        /// </summary>
        private IIndicator _adx;

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
        /// включен ли эмулятор
        /// </summary>
        public bool EmulatorIsOn;
        /// <summary>
        /// объём для входа
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
        /// время на открытие ордера, после чего он будет отозван
        /// </summary>
        public TimeSpan LagTimeToOpenClose;
        /// <summary>
        /// откат цены от цены ордера, после чего он будет отозван
        /// </summary>
        public decimal LagPunctToOpenClose;
        /// <summary>
        /// обратное проскальзывание для цены активации стопОрдера на закрытии
        /// </summary>
        public int SlipageReversClose;
        /// <summary>
        /// обратное проскальзывание для цены активации стопОрдера на открытии
        /// </summary>
        public int SlipageReversOpen;
        /// <summary>
        /// отступ для Алерта
        /// </summary>
        public int SlipageToAlert;
        /// <summary>
        /// алерт
        /// </summary>
        private AlertToPrice _alert;

        public int AdxHigh;
        public int Lookback;
        public int TrailBars;

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
                    writer.WriteLine(SlipageReversOpen);

                    writer.WriteLine(AdxHigh);
                    writer.WriteLine(Lookback);
                    writer.WriteLine(TrailBars);
                    writer.WriteLine(SlipageToAlert);
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
                    SlipageReversOpen = Convert.ToInt32(reader.ReadLine());

                    AdxHigh = Convert.ToInt32(reader.ReadLine());
                    Lookback = Convert.ToInt32(reader.ReadLine());
                    TrailBars = Convert.ToInt32(reader.ReadLine());
                    SlipageToAlert = Convert.ToInt32(reader.ReadLine());
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
        /// место работы потока который следит за отключением робота в неторговые часы
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
        /// основная точка входа в логику. Вызывается когда формируется новая свеча
        /// </summary>
        void StrategyAdxVolatility_CandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count < 60)
            {
                return;
            }

            if (IsOn == false)
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
        /// проверить условия для входа в позицию
        /// </summary>
        private void TryOpenPosition(List<Candle> candles)
        {

            // _tab.BuyAtLimit(Volume, candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * 50);

            // return;
            decimal lastAdx = ((Adx)_adx).Values[candles.Count - 1];

            if (lastAdx == 0 ||
                ((Adx)_adx).Values.Count + 1 < Lookback)
            {
                return;
            }

            if (candles.Count < 23)
            {
                return;
            }

            decimal adxMax = 0;

            for (int i = ((Adx)_adx).Values.Count - 1; i > ((Adx)_adx).Values.Count - 1 - Lookback && i > 0; i--)
            {
                decimal value = ((Adx)_adx).Values[i];

                if (value > adxMax)
                {
                    adxMax = value;
                }
            }

            if (adxMax > AdxHigh)
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

            // БАЙ

            decimal lineBuy = GetPriceToOpenPos(Side.Buy, candles, candles.Count - 1);

            if (lineBuy + _tab.Securiti.PriceStep * 5 < candles[candles.Count - 1].Close)
            {// это костыль, основной вход через отложенный ордер.
                // т.к. в Велсе тип заявки: СтопМаркет(по любой цене). А у нас СтопЛимит(с проскальзываниями и отступами)
                // чтобы было хоть немного похоже, если стоп на открытие у нас уже в деньгах
                // выставляемся по закрытию свечи.
                // в этом случае Велс просто рисует вход по открытию свечи!  

                _tab.BuyAtLimit(Volume, candles[candles.Count - 1].Close + _tab.Securiti.PriceStep * 10);
                return;
            }

            decimal priceOrder = lineBuy + _tab.Securiti.PriceStep * SlipageOpenFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
            decimal priceRedLine = lineBuy - _tab.Securiti.PriceStep * SlipageReversOpen;


            _tab.BuyAtStop(Volume, priceOrder, priceRedLine, StopActivateType.HigherOrEqual);

            if (StartProgram != StartProgram.IsTester && AlertIsOn)
            {
                _alert.PriceActivation = priceRedLine - SlipageToAlert * _tab.Securiti.PriceStep;
                _alert.TypeActivation = PriceAlertTypeActivation.PriceHigherOrEqual;
                _alert.MessageIsOn = true;
                _alert.MusicType = AlertMusic.Duck;
                _alert.Message = "Приближаемся к точке входа";
                _alert.IsOn = true;
            }

            // СЕЛЛ

            decimal lineSell = GetPriceToOpenPos(Side.Sell, candles, candles.Count - 1);

            if (lineSell - _tab.Securiti.PriceStep * 5 > candles[candles.Count - 1].Close)
            {// это костыль, основной вход через отложенный ордер.
                // т.к. в Велсе тип заявки: СтопМаркет(по любой цене). А у нас СтопЛимит(с проскальзываниями и отступами)
                // чтобы было хоть немного похоже, если стоп на открытие у нас уже в деньгах
                // выставляемся по закрытию свечи.
                // в этом случае Велс просто рисует вход по открытию свечи!  

                _tab.SellAtLimit(Volume, candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * 10);
                return;
            }

            decimal priceOrderSell = lineSell - _tab.Securiti.PriceStep * SlipageOpenFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
            decimal priceRedLineSell = lineSell + _tab.Securiti.PriceStep * SlipageReversOpen;

            _tab.SellAtStop(Volume, priceOrderSell, priceRedLineSell, StopActivateType.LowerOrEqyal);

            if (StartProgram != StartProgram.IsTester && AlertIsOn)
            {
                _alert.PriceActivation = priceRedLineSell + SlipageToAlert * _tab.Securiti.PriceStep;
                _alert.TypeActivation = PriceAlertTypeActivation.PriceLowerOrEqual;
                _alert.MessageIsOn = true;
                _alert.MusicType = AlertMusic.Duck;
                _alert.Message = "Приближаемся к точке входа";
                _alert.IsOn = true;
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

            // выход по стопам
            if (position.Direction == Side.Buy)
            {
                decimal price = GetPriseStop(Side.Buy, candles, candles.Count - 1);
                if (price == 0)
                {
                    return;
                }

                decimal priceOrder = price - _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = price + _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLine - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                if (position.StopOrderRedLine == 0 || position.StopOrderRedLine < priceRedLine)
                {
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
                else if (position.StopOrderIsActiv == false)
                {
                    if (position.StopOrderRedLine - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                    {
                        _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                        return;
                    }
                    position.StopOrderIsActiv = true;
                }
            }

            if (position.Direction == Side.Sell)
            {
                decimal price = GetPriseStop(Side.Sell, candles, candles.Count - 1);
                if (price == 0)
                {
                    return;
                }

                decimal priceOrder = price + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = price - _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                if (position.StopOrderRedLine == 0 || position.StopOrderRedLine > priceRedLine)
                {
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
                else if (position.StopOrderIsActiv == false)
                {
                    if (position.StopOrderRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                    {
                        _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                        return;
                    }
                    position.StopOrderIsActiv = true;
                }
            }
        }

        /// <summary>
        /// взять цену для входа в позиции
        /// </summary>
        private decimal GetPriceToOpenPos(Side side, List<Candle> candles, int index)
        {
            if (side == Side.Buy)
            {
                decimal price = 0;

                for (int i = index; i > 0 && i > index - Lookback; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }
                return price;// - _tab.Securiti.PriceStep;
            }
            if (side == Side.Sell)
            {
                decimal price = decimal.MaxValue;
                for (int i = index; i > 0 && i > index - Lookback; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;// + _tab.Securiti.PriceStep;
            }

            return 0;
        }

        /// <summary>
        /// взять цену стопПриказа
        /// </summary>
        private decimal GetPriseStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailBars)
            {
                return 0;
            }

            if (side == Side.Buy)
            { // рассчитываем цену стопа при Лонге
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailBars; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - TrailBars; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
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
        /// включена ли реактивация ордера
        /// </summary>
        private bool _reActivatorIsOn;

        /// <summary>
        /// время до которого реактивация будет активка
        /// </summary>
        private DateTime _reActivatorMaxTime;

        /// <summary>
        /// цена реактивации
        /// </summary>
        private decimal _reActivatorPrice;

        /// <summary>
        /// ордер который нужно перевыставлять
        /// </summary>
        private Order _reActivatorOrder;

        /// <summary>
        /// следить за текущим ордером
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
        /// прогрузить активатор новым трейдом
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
        /// ордера робота
        /// </summary>
        private List<Order> _myOrders;

        /// <summary>
        /// новый ордер в системе
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
        /// новый тик в системе
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
            _lastEmuStop = 0;

            for (int i = 60; i < candles.Count - 1; i++)
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
        /// попробовать войти в позицию в эмуляторе
        /// </summary>
        private int TryInter(List<Candle> candles, int index)
        {

            decimal lastAdx = ((Adx)_adx).Values[index];

            if (lastAdx == 0 ||
                ((Adx)_adx).Values.Count + 1 < Lookback)
            {
                return 0;
            }

            if (candles.Count < 23)
            {
                return 0;
            }

            DateTime lastTradeTime = candles[index].TimeStart;

            if (lastTradeTime.Hour == 10 &&
                lastTradeTime.Day == 8 &&
                lastTradeTime.Minute == 17)
            {

            }

            decimal adxMax = 0;

            for (int i = index; i > index - Lookback && i > 0; i--)
            {
                decimal value = ((Adx)_adx).Values[i];

                if (value > adxMax)
                {
                    adxMax = value;
                }
            }

            if (adxMax > AdxHigh)
            {
                return 0;
            }

            // БАЙ

            decimal lineBuy = GetPriceToOpenPos(Side.Buy, candles, index);

            if (BuyAtLimit(candles, index + 1, lineBuy))
            {
                PaintOpen(1, candles, index + 1, lineBuy);
                return 1;
            }

            // СЕЛЛ

            decimal lineSell = GetPriceToOpenPos(Side.Sell, candles, index);

            if (SellAtLimit(candles, index + 1, lineSell))
            {
                PaintOpen(-1, candles, index + 1, lineSell);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// последний стоп в эмуляторе
        /// </summary>
        private decimal _lastEmuStop;

        /// <summary>
        /// попробовать выйти из позции в эмуляторе
        /// </summary>
        private int TryOut(List<Candle> candles, int index, int currentPos)
        {
            if (currentPos == 1)
            {
                decimal priceEtalon = GetPriseStop(Side.Buy, candles, index);

                if (_lastEmuStop != 0 && _lastEmuStop > priceEtalon)
                {
                    priceEtalon = _lastEmuStop;
                }
                _lastEmuStop = priceEtalon;

                if (SellAtLimit(candles, index + 1, priceEtalon))
                {
                    PaintClose(1, candles, index, priceEtalon);
                    _lastEmuStop = 0;
                    return 0;
                }
            }

            if (currentPos == -1)
            {
                decimal priceEtalon = GetPriseStop(Side.Sell, candles, index);

                if (_lastEmuStop != 0 && _lastEmuStop < priceEtalon)
                {
                    priceEtalon = _lastEmuStop;
                }
                _lastEmuStop = priceEtalon;

                if (BuyAtLimit(candles, index + 1, priceEtalon))
                {
                    PaintClose(-1, candles, index, priceEtalon);
                    _lastEmuStop = 0;
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
        /// прорисовать открытие позиции
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
        /// прорисовать закрытие позиции
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
