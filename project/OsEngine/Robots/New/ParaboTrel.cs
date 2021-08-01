using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
//using OsEngine.Robots.FoundBots;
using OsEngine.Robots.MoiRoboti.New;

namespace OsEngine.Robots.MoiRoboti.New
{
    public class ParaboTrel : BotPanel, INotifyPropertyChanged
    {
        // сервис
        /// <summary>
        /// КОНСТРУКТОР 
        /// </summary>
        public ParaboTrel(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _atr = new Atr(name + "Atr", false)
            {
                Lenght = 10,
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };

            _atr = _tab.CreateCandleIndicator(_atr, "atrArea");
            _atr.Save();

            _ma = new MovingAverage(name + "Ma", false)
            {
                Lenght = 100,
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };

            _ma = _tab.CreateCandleIndicator(_ma, "Prime");
            _ma.Save();

            _eR = new EfficiencyRatio(name + "eR", false)
            {
                Lenght = 10,
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };

            _eR = _tab.CreateCandleIndicator(_eR, "erArea");
            _eR.Save();

            _alert = new AlertToPrice(NameStrategyUniq);
            _alert.IsOn = false;
            _tab.DeleteAllAlerts();
            _tab.SetNewAlert(_alert);

            _tab.CandleFinishedEvent += StrategyAdxVolatility_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.PositionOpeningFailEvent += _tab_PositionOpeningFailEvent;
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;

            IsOn = false;
            Volume = 1;
            SlipageOpenSecond = 0;
            SlipageCloseSecond = 0;
            TimeFrom = 00;
            TimeTo = 00;
            AlertIsOn = false;
            EmulatorIsOn = false;

            LagTimeToOpenClose = new TimeSpan(0, 0, 0, 30);
            LagPunctToOpenClose = 20;

            DistLongInit = 6;
            LongAdj = 0.1m;

            DistShortInit = 6;
            ShortAdj = 0.1m;
            SlipageToAlert = 10;
            lengthStartStop = 0.5m;
            toProfit = CreateParameter("Забирать профит от %", 0.5m, 0.5m, 50m, 0.5m);
            slippage = CreateParameter("Велич.проскаль.у ордеров", 5, 1, 200, 5);
            vklRasCandl = CreateParameter("Считать стоп по свечам?",false); 

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
        /// закрылась позиция
        /// </summary>
        private void _tab_PositionClosingSuccesEvent(Position position)
        {
            Profit = 0;
        }

        /// <summary>
        /// взять имя робота
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ParaboTrel";
        }
        /// <summary>
        /// показать окно настроек робота
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            ParaboTrelUi ui = new ParaboTrelUi(this);
            ui.Show();
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// Атр
        /// </summary>
        private IIndicator _atr;

        /// <summary>
        /// мувинг
        /// </summary>
        private IIndicator _ma;

        /// <summary>
        /// KaufmanEr
        /// </summary>
        private IIndicator _eR;

        // настройки публичные
        private StrategyParameterInt slippage; // величина проскальзывание при установки ордеров 
        private StrategyParameterDecimal toProfit; // расстояние от цены до трейлинг стопа в %
        private StrategyParameterBool vklRasCandl; // включать ли расчет стопа по свечам
        private decimal _percent; // поле хранения 
        public decimal Profit
        {
            get => _percent;
            set => Set(ref _percent, value);
        }
        // настройки публичные
        /// <summary>
        /// цена товара на рынке
        /// </summary>
        private decimal _price; // поле хранения цены
        public decimal Price_market
        {
            get => _price;
            set => Set(ref _price, value);
        }
        public decimal stopBuy;
        public decimal stopSell;
        public decimal indent;

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
        /// объём
        /// </summary>
        public decimal Volume;
        /// <summary>
        /// проскальзыание на закрытии
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
        /// <summary>
        /// количество свечей после которых мы выходим
        /// </summary>
        public int Day;
        public decimal DistLongInit;
        public decimal LongAdj;
        public decimal DistShortInit;
        public decimal ShortAdj;

        /// <summary>
        /// стартовая (начальная) величина отступа для стоп приказа  в процентах от цены открытия позиции
        /// </summary>
        public decimal lengthStartStop;

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

                    writer.WriteLine(DistLongInit);
                    writer.WriteLine(LongAdj);

                    writer.WriteLine(DistShortInit);
                    writer.WriteLine(ShortAdj);
                    writer.WriteLine(SlipageToAlert);
                    writer.WriteLine(EmulatorIsOn);
                    writer.WriteLine(Day);

                    writer.WriteLine(SlipageCloseFirst);
                    writer.WriteLine(SlipageOpenFirst);
                    writer.WriteLine(NeadToPaintEmu);
                    writer.WriteLine(lengthStartStop);

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

                    DistLongInit = Convert.ToDecimal(reader.ReadLine());
                    LongAdj = Convert.ToDecimal(reader.ReadLine());

                    DistShortInit = Convert.ToDecimal(reader.ReadLine());
                    ShortAdj = Convert.ToDecimal(reader.ReadLine());
                    SlipageToAlert = Convert.ToInt32(reader.ReadLine());
                    EmulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    Day = Convert.ToInt32(reader.ReadLine());

                    SlipageCloseFirst = Convert.ToInt32(reader.ReadLine());
                    SlipageOpenFirst = Convert.ToInt32(reader.ReadLine());

                    NeadToPaintEmu = Convert.ToBoolean(reader.ReadLine());
                    lengthStartStop = Convert.ToDecimal(reader.ReadLine());

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
        /// место работы потока который отключает робота в нерабочее время
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
        /// входящее событие о том что открылась некая сделка
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            // выставляем стоп по отступу в обход вызова из метода окончания свечи
            indent = lengthStartStop * Price_market / 100;  // отступ для стопа
            decimal priceOpenPos = _tab.PositionsLast.EntryPrice;  // цена открытия позиции

            if (position.Direction == Side.Buy)
            {
                stopBuy = Math.Round(priceOpenPos - indent, _tab.Securiti.Decimals);
                decimal lineSell = priceOpenPos - indent;

                decimal priceOrderSell = lineSell - _tab.Securiti.PriceStep * SlipageCloseFirst;
                decimal priceRedLineSell = lineSell + _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLineSell - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                if (position.StopOrderPrice == 0 ||
                    position.StopOrderPrice < priceRedLineSell)
                {
                    _tab.CloseAtStop(position, priceRedLineSell, priceOrderSell);
                }

                if (position.StopOrderIsActiv == false)
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
                decimal lineBuy = priceOpenPos + indent; ;
                stopSell = Math.Round(priceOpenPos + indent, _tab.Securiti.Decimals);
                if (lineBuy == 0)
                {
                    return;
                }

                decimal priceOrder = lineBuy + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = lineBuy - _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLine + _tab.Securiti.PriceStep * 5 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk + _tab.Securiti.PriceStep * SlipageCloseFirst, position.OpenVolume);
                    return;
                }

                if (position.StopOrderPrice == 0 ||
                    position.StopOrderPrice > priceRedLine)
                {
                    _tab.CloseAtStop(position, priceRedLine, priceOrder);
                }

                if (position.StopOrderIsActiv == false)
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
        /// основной вход в логику робота. Вызывается когда завершилась свеча
        /// </summary>
        void StrategyAdxVolatility_CandleFinishedEvent(List<Candle> candles)
        {

            if (candles.Count < 100)
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

            if (candles.Count < ((MovingAverage)_ma).Lenght)
            {
                return;
            }

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
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastSma = ((MovingAverage)_ma).Values[candles.Count - 1];

            if (EmulatorIsOn)
            {
                int currentEmuPos = GetCurrentPosition();
                if (currentEmuPos != 0)
                {
                    return;
                }
            }
            // БАЙ
            if (lastPrice >= lastSma)
            {
                decimal lineBuy = GetPriceToOpenPos(Side.Buy, candles, candles.Count - 1);

                if (lineBuy == 0)
                {
                    return;
                }

                if (lineBuy + _tab.Securiti.PriceStep * 15 < candles[candles.Count - 1].Close)
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
            }

            // СЕЛЛ
            if (lastPrice <= lastSma)
            {
                decimal lineSell = GetPriceToOpenPos(Side.Sell, candles, candles.Count - 1);

                if (lineSell == 0)
                {
                    return;
                }

                if (lineSell - _tab.Securiti.PriceStep * 5 > candles[candles.Count - 1].Close)
                {// это костыль, основной вход через отложенный ордер.
                    // т.к. в Велсе тип заявки: СтопМаркет(по любой цене). А у нас СтопЛимит(с проскальзываниями и отступами)
                    // чтобы было хоть немного похоже, если стоп на открытие у нас уже в деньгах
                    // выставляемся по закрытию свечи.
                    // в этом случае Велс просто рисует вход по открытию свечи!  

                    _tab.SellAtLimit(Volume, candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * 10);
                    return;
                }

                decimal priceOrderSell = lineSell - _tab.Securiti.PriceStep * SlipageOpenFirst;
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
        }

        /// <summary>
        /// проверить условия на выход из позиции
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
                    _tab.SetNewLogMessage("Кроем позицию по эмулятору. Номер позиции: " + position.Number, LogMessageType.Error);
                    // Выход по эмулятору! позиции нет. Нужно закрывать полюбой цене
                    _tab.CloseAllOrderToPosition(position);
                    _timeToClose = DateTime.Now.AddSeconds(3);
                    _positionToClose = position;
                    return;
                }
            }

            // БАЙ
            if (position.Direction == Side.Sell)
            {
                decimal lineBuy = GetPriceToStopOrder(position.TimeCreate, position.Direction, candles, candles.Count - 1);

                if (lineBuy == 0)
                {
                    return;
                }

                decimal priceOrder = lineBuy + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = lineBuy - _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLine + _tab.Securiti.PriceStep * 5 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk + _tab.Securiti.PriceStep * SlipageCloseFirst, position.OpenVolume);
                    return;
                }

                if (position.StopOrderPrice == 0 ||
                    position.StopOrderPrice > priceRedLine)
                {
                    _tab.CloseAtStop(position, priceRedLine, priceOrder);

                }

                if (position.StopOrderIsActiv == false)
                {
                    if (position.StopOrderRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                    {
                        _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                        return;
                    }
                    position.StopOrderIsActiv = true;
                }
            }

            // СЕЛЛ
            if (position.Direction == Side.Buy)
            {
                decimal lineSell = GetPriceToStopOrder(position.TimeCreate, position.Direction, candles, candles.Count - 1);

                if (lineSell == 0)
                {
                    return;
                }

                decimal priceOrderSell = lineSell - _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLineSell = lineSell + _tab.Securiti.PriceStep * SlipageReversClose;

                if (priceRedLineSell - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                if (position.StopOrderPrice == 0 ||
                    position.StopOrderPrice < priceRedLineSell)
                {
                    _tab.CloseAtStop(position, priceRedLineSell, priceOrderSell);
                }

                if (position.StopOrderIsActiv == false)
                {
                    if (position.StopOrderRedLine - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                    {
                        _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                        return;
                    }
                    position.StopOrderIsActiv = true;
                }
            }
        }

        /// <summary>
        /// взять цену для открытия позиции
        /// </summary>
        private decimal GetPriceToOpenPos(Side side, List<Candle> candles, int index)
        {
            if (side == Side.Buy)
            {
                decimal price = 0;

                for (int i = index; i > 0 && i > index - Day; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }
                return price;// +_tab.Securiti.PriceStep;
            }
            if (side == Side.Sell)
            {
                decimal price = decimal.MaxValue;
                for (int i = index; i > 0 && i > index - Day; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price; // -_tab.Securiti.PriceStep;
            }

            return 0;
        }

        /// <summary>
        /// для сдвига стопа  
        /// </summary>
        public void Min_loss()
        {
            indent = lengthStartStop * Price_market / 100;  // отступ для стопа
            decimal priceOpenPos = 0; // цена открытия позиции

            if (_tab.PositionsOpenAll.Count != 0)
            {
                priceOpenPos = _tab.PositionsLast.EntryPrice;
            }
            if (priceOpenPos == 0)
            {
                return;
            }
            if (_tab.PositionsLast.Direction == Side.Buy)
            {

                if (Price_market < priceOpenPos)
                {
                    return;
                }

                if (priceOpenPos + indent < Price_market) // если цена выросла  больше допустимого стопа переносим стоп сделки в безубыток
                {
                    stopBuy = priceOpenPos + 0.5m * indent;
                }
            }
            if (_tab.PositionsLast.Direction == Side.Sell)
            {
                if (Price_market > priceOpenPos)
                {
                    return;
                }

                if (Price_market < priceOpenPos - indent) // если цена снизилась  больше допустимого стопа переносим стоп сделки в безубыток
                {
                    {
                        stopSell = priceOpenPos - 0.3m * indent;
                    }
                }
            }
        }

        /// <summary>
        /// для движения трлейлинг стопа  
        /// </summary>
        public void To_stop_profit()
        {
            decimal priceOpenPos = 0; // цена открытия позиции

            if (_tab.PositionsOpenAll.Count != 0)
            {
                priceOpenPos = _tab.PositionsLast.EntryPrice;
            }
            if (priceOpenPos == 0)
            {
                return;
            }
            decimal komis = Price_market /100 * 0.04m;
            if (_tab.PositionsLast.Direction == Side.Buy)
            {

                if (Price_market < priceOpenPos)
                {
                    return;
                }
  
                    decimal stopActivacion = Price_market - Price_market * (toProfit.ValueDecimal / 100);
                    decimal stopOrderPrice = stopActivacion - slippage.ValueInt * _tab.Securiti.PriceStep;
                if (stopActivacion <= priceOpenPos + komis)
                {
                    stopActivacion = Price_market - Price_market * (lengthStartStop / 100);
                    _tab.CloseAtTrailingStop(_tab.PositionsLast, stopActivacion, stopOrderPrice);
                    stopBuy = stopActivacion;
                }

                if (Price_market > priceOpenPos + komis + Price_market * (toProfit.ValueDecimal / 100)) 
                {
                     stopActivacion = Price_market - Price_market * (toProfit.ValueDecimal / 100);
                     stopOrderPrice = stopActivacion - slippage.ValueInt * _tab.Securiti.PriceStep;
                    _tab.CloseAtTrailingStop(_tab.PositionsLast, stopActivacion, stopOrderPrice);
                    stopBuy = stopActivacion;
                }
            }
            if (_tab.PositionsLast.Direction == Side.Sell)
            {
                if (Price_market > priceOpenPos)
                {
                    return;
                }
                decimal stopActivacion = Price_market + Price_market * (toProfit.ValueDecimal / 100);
                decimal stopOrderPrice = stopActivacion + slippage.ValueInt * _tab.Securiti.PriceStep;

                if (stopActivacion >= priceOpenPos- komis) // пока стоп не перекрыл безубыток, держим его на расстоянии lengthStartStop
                {
                    stopActivacion = Price_market + Price_market * (lengthStartStop / 100);
                    _tab.CloseAtTrailingStop(_tab.PositionsLast, stopActivacion, stopOrderPrice);
                    stopSell = stopActivacion;
                }

                if(stopActivacion < priceOpenPos - komis - Price_market * (toProfit.ValueDecimal / 100))  // когда стоп перекрыл безубыток, держим его на расстоянии toProfit
                {
                        stopActivacion = Price_market + Price_market * (toProfit.ValueDecimal / 100);
                        stopOrderPrice = stopActivacion + slippage.ValueInt * _tab.Securiti.PriceStep;
                        _tab.CloseAtTrailingStop(_tab.PositionsLast, stopActivacion, stopOrderPrice);
                        stopSell = stopActivacion;
                }
            }
        }

        /// <summary>
        /// взять цену для выхода из позиции
        /// </summary>
        private decimal GetPriceToStopOrder(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null)
            {
                return 0;
            }

            if (side == Side.Buy)
            { // рассчитываем цену стопа при Лонге
              // 1 находим максимум за время от открытия сделки и до текущего
                decimal maxHigh = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // смотрим индекс свечи, после которой произошло открытие позы
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i < index + 1; i++)
                { // смотрим максимум после открытия

                    if (candles[i].High > maxHigh)
                    {
                        maxHigh = candles[i].High;
                    }
                }

                // 2 рассчитываем текущее отклонение для стопа

                decimal distanse = DistLongInit;

                for (int i = indexIntro; i < index + 1; i++)
                { // смотрим коэффициент

                    DateTime lastTradeTime = candles[i].TimeStart;

                    if (lastTradeTime.Hour < TimeFrom && TimeFrom != 0 ||
                        lastTradeTime.Hour > TimeTo && TimeTo != 0 ||
                        lastTradeTime.Hour == 10 && TimeFrom == 10 && lastTradeTime.Minute == 0)
                    {
                        continue;
                    }

                    decimal kauf = ((EfficiencyRatio)_eR).Values[i];

                    if (kauf >= 0.6m)
                    {
                        distanse -= 2.0m * LongAdj;
                    }
                    if (kauf >= 0.3m)
                    {
                        distanse -= 1.0m * LongAdj;
                    }
                }

                // 3 рассчитываем цену Стопа
                Min_loss();  // расчет стопа
                decimal lastAtr = ((Atr)_atr).Values[index];

                decimal stopCandel = maxHigh - lastAtr * distanse; // стоп рассчитываемый по свечам
                if (stopCandel < stopBuy)
                {
                    return stopBuy;
                }
                if (stopCandel > stopBuy)
                {
                    if (vklRasCandl.ValueBool ==true)
                    {
                        return stopCandel;
                    }
                    else
                    {
                        return stopBuy;
                    }
                    
                }
            }
            if (side == Side.Sell)
            {
                // рассчитываем цену стопа при Шорте
                // 1 находим максимум за время от открытия сделки и до текущего
                decimal minLow = decimal.MaxValue;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // смотрим индекс свечи, после которой произошло открытие позы
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i < index + 1; i++)
                { // смотрим Минимальный лой

                    if (candles[i].Low < minLow)
                    {
                        minLow = candles[i].Low;
                    }
                }

                // 2 рассчитываем текущее отклонение для стопа

                decimal distanse = DistShortInit;

                for (int i = indexIntro; i < index + 1; i++)
                { // смотрим коэффициент

                    DateTime lastTradeTime = candles[i].TimeStart;

                    if (lastTradeTime.Hour < TimeFrom && TimeFrom != 0 ||
                        lastTradeTime.Hour > TimeTo && TimeTo != 0 ||
                        lastTradeTime.Hour == 10 && TimeFrom == 10 && lastTradeTime.Minute == 0)
                    {
                        continue;
                    }

                    decimal kauf = ((EfficiencyRatio)_eR).Values[i];

                    if (kauf > 0.6m)
                    {
                        distanse -= 2.0m * ShortAdj;
                    }
                    if (kauf > 0.3m)
                    {
                        distanse -= 1.0m * ShortAdj;
                    }
                }

                // 3 рассчитываем цену Стопа по свечам и от величины минимально стопака 

                decimal lastAtr = ((Atr)_atr).Values[index];
                Min_loss();

                decimal stopCandle = Math.Round(minLow + lastAtr * distanse, _tab.Securiti.Decimals); // стоп рассчитываемый по свечам
                if (stopCandle > stopSell)
                {
                    return stopSell;
                }
                if (stopCandle < stopSell)
                {
                    if (vklRasCandl.ValueBool == true)
                    {
                        return stopCandle;
                    }
                    else
                    {
                        return stopSell;
                    }
                }
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
        /// включена ли реАктивация ордера
        /// </summary>
        private bool _reActivatorIsOn;

        /// <summary>
        /// время до которого реактивация возможна
        /// </summary>
        private DateTime _reActivatorMaxTime;

        /// <summary>
        /// цена реАктивации
        /// </summary>
        private decimal _reActivatorPrice;

        /// <summary>
        /// ордер который следует реАктивировать
        /// </summary>
        private Order _reActivatorOrder;

        /// <summary>
        /// поставить на слежение новый ордер
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
        /// прогрузить реАктивартор новым трейдом
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
        /// реактивировать ордер
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
        /// ордера
        /// </summary>
        private List<Order> _myOrders;

        /// <summary>
        /// в системе новый ордер
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
        /// ТЕСТОВЫЕ ПЕРЕМЕННЫЕ ТУТ !! в системе новый тик 
        /// </summary>
        void _tab_NewTickEvent(Trade trade)
        {
            Price_market = trade.Price;
            ChekReActivator(trade);
            if (_tab.PositionsOpenAll.Count != 0)
            {
                Profit = _tab.PositionsLast.ProfitPortfolioPunkt;
                To_stop_profit();
            }
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

            for (int i = 100; i < candles.Count - 1; i++)
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
        /// попытаться войти в позицию. Эмулятор
        /// </summary>
        private int TryInter(List<Candle> candles, int index)
        {
            decimal lastPrice = candles[index].Close;
            decimal lastSma = ((MovingAverage)_ma).Values[index];

            if (lastPrice >= lastSma)
            {
                decimal lineBuy = GetPriceToOpenPos(Side.Buy, candles, index);

                if (lineBuy == 0)
                {
                    return 0;
                }

                if (BuyAtLimit(candles, index + 1, lineBuy))
                {
                    _lastTimeEnter = candles[index + 1].TimeStart;
                    PaintOpen(1, candles, index + 1, lineBuy);
                    return 1;
                }
            }

            // СЕЛЛ
            if (lastPrice <= lastSma)
            {
                decimal lineSell = GetPriceToOpenPos(Side.Sell, candles, index);

                if (lineSell == 0)
                {
                    return 0;
                }

                if (SellAtLimit(candles, index + 1, lineSell))
                {
                    _lastTimeEnter = candles[index + 1].TimeStart;
                    PaintOpen(-1, candles, index + 1, lineSell);
                    return -1;
                }
            }

            return 0;
        }

        /// <summary>
        /// последний стоп в эмуляторе
        /// </summary>
        private decimal _lastEmuStop;

        /// <summary>
        /// последняя цена входа в эмуляторе
        /// </summary>
        private DateTime _lastTimeEnter;

        /// <summary>
        /// попытаться выйти в эмуляторе
        /// </summary>
        private int TryOut(List<Candle> candles, int index, int currentPos)
        {

            if (currentPos == 1)
            {
                decimal priceEtalon = GetPriceToStopOrder(_lastTimeEnter, Side.Buy, candles, index);

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
                decimal priceEtalon = GetPriceToStopOrder(_lastTimeEnter, Side.Sell, candles, index);

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
        /// точки на графике
        /// </summary>
        private List<PointElement> _points;

        /// <summary>
        /// прорисовать вход
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
        /// прорисовать выход
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

        /// <summary>
        /// обработчик события изменения свойств
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected void СallUpdate(string name)  // сигнализирует об изменении свойств
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        ///  сверяет значения любых типов данных и выдает сигнал об изменении 
        /// </summary>
        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (!field.Equals(value))
            {
                field = value;
                СallUpdate(name);
            }
        }
    }
}
