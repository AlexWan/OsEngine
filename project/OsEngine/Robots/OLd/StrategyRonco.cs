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
    public class StrategyRonco : BotPanel
    {
        // сервис
        public StrategyRonco(string name, StartProgram startProgram) : base(name, startProgram)
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

            _xIndicator = new Line(name + "xIndicator", false)
            {
                ColorBase = Color.DodgerBlue,
                PaintOn = true,

            };

            _xIndicator = _tab.CreateCandleIndicator(_xIndicator, "xArea");
            _xIndicator.Save();

            _upChanelIndicator = new Line(name + "upChanel", false)
            {
                ColorBase = Color.DodgerBlue,
                PaintOn = true,

            };

            _upChanelIndicator = _tab.CreateCandleIndicator(_upChanelIndicator, "Prime");
            _upChanelIndicator.Save();

            _downChanelIndicator = new Line(name + "downChanel", false)
            {
                ColorBase = Color.DarkRed,
                PaintOn = true,

            };

            _downChanelIndicator = _tab.CreateCandleIndicator(_downChanelIndicator, "Prime");
            _downChanelIndicator.Save();


            _alert = new AlertToPrice(NameStrategyUniq);
            _alert.IsOn = false;
            _tab.DeleteAllAlerts();
            _tab.SetNewAlert(_alert);

            _tab.CandleFinishedEvent += StrategyAdxVolatility_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

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


            Ratio = 150;
            Shift = 0;
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
            return "1StrategyRonco";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            FreeStylikRoncoUi ui = new FreeStylikRoncoUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка через которую торгует бот
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// адх
        /// </summary>
        private IIndicator _adx;

        /// <summary>
        /// х
        /// </summary>
        private IIndicator _xIndicator;

        /// <summary>
        /// верхний канал
        /// </summary>
        private IIndicator _upChanelIndicator;

        /// <summary>
        /// нижний канал
        /// </summary>
        private IIndicator _downChanelIndicator;

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
        /// проскальзывание для выхода
        /// </summary>
        public int SlipageCloseSecond;
        /// <summary>
        /// проскальзывание для входа
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
        /// время на исполнение ордера, после чего он будет отозват
        /// </summary>
        public TimeSpan LagTimeToOpenClose;
        /// <summary>
        /// откат от цены ордера, после чего он будет отозван
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
        /// отклоненение для канала
        /// </summary>
        public int Shift;
        /// <summary>
        /// отклонение для алерта
        /// </summary>
        public int SlipageToAlert;
        /// <summary>
        /// алерт
        /// </summary>
        private AlertToPrice _alert;

        public int Ratio;

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

                    writer.WriteLine(Ratio);
                    writer.WriteLine(Shift);
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

                    Ratio = Convert.ToInt32(reader.ReadLine());
                    Shift = Convert.ToInt32(reader.ReadLine());
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
        /// место работы потока который отключает робота в нерабочее вермя
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
        /// событие оповещающие о успешном открытии позиции
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();
        }

        private bool _onLastTradeFixConfuze;

        /// <summary>
        /// главная точка входа в логику робота. Вызывается когда завершает формирование свеча
        /// </summary>
        void StrategyAdxVolatility_CandleFinishedEvent(List<Candle> candles)
        {
            if (((Adx)_adx).Values != null)
            {
                ReloadXIndicator(candles);
                ReloadUpChanel(candles);
                ReloadDownChanel(candles);
            }
            else
            {
                return;
            }

            if (IsOn == false)
            {
                return;
            }

            // КОСТЫЛЬ
            /* int currentPosBot = 10;

             List<Position> positions1 = _tab.PositionsOpenAll;

             if (positions1 != null)
             {
                 for (int i = 0; i < positions1.Count; i++)
                 {
                     if (positions1[i].Direction == Side.Buy)
                     {
                         currentPosBot += positions1[i].OpenVolume;
                     }
                     if (positions1[i].Direction == Side.Sell)
                     {
                         currentPosBot -= positions1[i].OpenVolume;
                     }
                 }
             }

             PositionOnBoard positionOnBoard = ServerMaster.GetPositionOnBoard(_tab.Securiti, _tab.Portfolio.Number);

             if (positionOnBoard != null)
             {
                 decimal pos = positionOnBoard.ValueCurrent;

                 if (_onLastTradeFixConfuze &&
                     pos != currentPosBot)
                 {
                     _tab.SetNewLogMessage("Количество позиций не совпадает второй раз! Откллючаем бота!", LogMessageType.Error);
                     IsOn = false;
                     return;
                 }

                 if (pos == currentPosBot)
                 {
                     _onLastTradeFixConfuze = false;
                 }
                 if (pos != currentPosBot)
                 {
                     _onLastTradeFixConfuze = true;
                 }
             }*/
            // КОНЕЦ КОСТЫЛЯ



            if (candles.Count < 50)
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

            if (positions != null && positions.Count > 1)
            {
                // _tab.SetNewLogMessage("Две позиции!!",LogMessageType.Error);
            }

            Side openSide = Side.None;

            if (positions != null && positions.Count != 0)
            {
                openSide = positions[0].Direction;
            }

            TryOpenPosition(candles, openSide);


            for (int i = 0; positions != null && i < positions.Count; i++)
            {
                TryClosePosition(positions[i]);
            }
        }

        /// <summary>
        /// проверить условия на открытие позиции
        /// </summary>
        private void TryOpenPosition(List<Candle> candles, Side openSide)
        {
            // БАЙ
            if (openSide != Side.Buy)
            {
                decimal lineBuy = GetPriceToOpenPos(Side.Buy, _downChanel.Count - 1);

                if (lineBuy == 0)
                {
                    return;
                }

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
                    _alert.Message = "Приближаемся к точке выхода";
                    _alert.IsOn = true;
                }
            }

            // СЕЛЛ
            if (openSide != Side.Sell)
            {
                decimal lineSell = GetPriceToOpenPos(Side.Sell, _downChanel.Count - 1);

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

                decimal priceOrderSell = lineSell - _tab.Securiti.PriceStep * SlipageOpenFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLineSell = lineSell + _tab.Securiti.PriceStep * SlipageReversOpen;

                _tab.SellAtStop(Volume, priceOrderSell, priceRedLineSell, StopActivateType.LowerOrEqyal);

                if (StartProgram != StartProgram.IsTester && AlertIsOn)
                {
                    _alert.PriceActivation = priceRedLineSell + SlipageToAlert * _tab.Securiti.PriceStep;
                    _alert.TypeActivation = PriceAlertTypeActivation.PriceLowerOrEqual;
                    _alert.MessageIsOn = true;
                    _alert.MusicType = AlertMusic.Duck;
                    _alert.Message = "Приближаемся к точке выхода";
                    _alert.IsOn = true;
                }
            }
        }

        /// <summary>
        /// проверить условия на закрытие позиции
        /// </summary>
        private void TryClosePosition(Position position)
        {
            if (EmulatorIsOn)
            {
                int currentEmuPos = GetCurrentPosition();

                if (currentEmuPos == 0 ||
                    currentEmuPos == 1 && position.Direction == Side.Sell ||
                    currentEmuPos == -1 && position.Direction == Side.Buy)
                {
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
                decimal lineBuy = GetPriceToClosePos(Side.Buy);

                if (lineBuy == 0)
                {
                    return;
                }

                decimal priceOrder = lineBuy + _tab.Securiti.PriceStep * SlipageCloseFirst; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = lineBuy - _tab.Securiti.PriceStep * SlipageReversClose;


                if (priceRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                _tab.CloseAtStop(position, priceRedLine, priceOrder);
            }

            // СЕЛЛ
            if (position.Direction == Side.Buy)
            {
                decimal lineSell = GetPriceToClosePos(Side.Sell);

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
                _tab.CloseAtStop(position, priceRedLineSell, priceOrderSell);
            }
        }

        /// <summary>
        /// взять цену открытия позиции
        /// </summary>
        private decimal GetPriceToOpenPos(Side side, int index)
        {
            decimal price = 0;

            decimal lastUpChanal = _upChanel[index];
            decimal lastDownChanal = _downChanel[index];

            if (side == Side.Sell)
            {
                if (lastDownChanal == 0)
                {
                    return 0;
                }
                price = lastDownChanal - _tab.Securiti.PriceStep;
            }
            if (side == Side.Buy)
            {
                if (lastUpChanal == 0)
                {
                    return 0;
                }
                price = lastUpChanal + _tab.Securiti.PriceStep;
            }

            return price;
        }

        /// <summary>
        /// взять цену закрытия позции
        /// </summary>
        private decimal GetPriceToClosePos(Side side)
        {

            decimal price = 0;

            decimal lastUpChanal = _upChanel[_upChanel.Count - 1];
            decimal lastDownChanal = _downChanel[_downChanel.Count - 1];

            if (side == Side.Sell)
            {
                if (lastDownChanal == 0)
                {
                    return 0;
                }
                price = lastDownChanal - _tab.Securiti.PriceStep;
            }
            if (side == Side.Buy)
            {
                if (lastUpChanal == 0)
                {
                    return 0;
                }
                price = lastUpChanal + _tab.Securiti.PriceStep;
            }

            return price;
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
                    _tab.SetNewLogMessage("Кроем позицию по эмулятору. Номер позиции: " + _positionToClose.Number, LogMessageType.System);
                    _tab.CloseAtLimit(_positionToClose, _tab.PriceBestAsk - _tab.Securiti.PriceStep * 10, _positionToClose.OpenVolume);
                }
                else if (_positionToClose.OpenVolume != 0 && _positionToClose.Direction == Side.Sell)
                {
                    _tab.SetNewLogMessage("Кроем позицию по эмулятору. Номер позиции: " + _positionToClose.Number, LogMessageType.System);
                    _tab.CloseAtLimit(_positionToClose, _tab.PriceBestAsk + _tab.Securiti.PriceStep * 10, _positionToClose.OpenVolume);
                }

                _positionToClose = null;
            }
        }

        // расчёт индикатора X

        /// <summary>
        /// х индикатор
        /// </summary>
        private List<decimal> _xIndex;

        /// <summary>
        /// перегрузить х индикатора
        /// </summary>
        private void ReloadXIndicator(List<Candle> candles)
        {
            if (((Adx)_adx).Values == null)
            {
                return;
            }

            if (_xIndex == null)
            {
                _xIndex = new List<decimal>();
            }

            if (((Adx)_adx).Values.Count - 1 == _xIndex.Count)
            {
                // обновляем только последнее значение
                _xIndex.Add(GetX(candles.Count - 1, ((Adx)_adx).Values));
            }
            else
            {
                _xIndex = new List<decimal>();
                for (int i = 0; i < ((Adx)_adx).Values.Count; i++)
                {
                    _xIndex.Add(GetX(i, ((Adx)_adx).Values));
                }
            }

            ((Line)_xIndicator).ProcessDesimals(_xIndex);
        }

        /// <summary>
        /// взять х-индикатор по индексу
        /// </summary>
        private decimal GetX(int index, List<decimal> adxArray)
        {
            decimal result = 0;

            try
            {
                if (index < 50)
                { // если рассчёт не возможен
                    return 0;
                }

                decimal adxOnIndex = adxArray[index];

                if (Ratio != 0 && adxOnIndex != 0)
                {
                    result = Math.Max(Math.Truncate(Ratio / adxOnIndex), 1);
                }

            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return result;
        }

        // расчёт канала верхнего

        /// <summary>
        /// верхний канал
        /// </summary>
        private List<decimal> _upChanel;

        /// <summary>
        /// перегрузить верхний канал
        /// </summary>
        private void ReloadUpChanel(List<Candle> candles)
        {
            if (_upChanel == null)
            {
                _upChanel = new List<decimal>();
            }

            if (candles.Count - 1 == _upChanel.Count)
            {
                // обновляем только последнее значение
                _upChanel.Add(GetUpChanel(candles.Count - 1, candles));
            }
            else
            {
                _upChanel = new List<decimal>();
                for (int i = 0; i < candles.Count; i++)
                {
                    _upChanel.Add(GetUpChanel(i, candles));
                }
            }

            ((Line)_upChanelIndicator).ProcessDesimals(_upChanel);
        }

        /// <summary>
        /// взять верхний канал по индексу
        /// </summary>
        private decimal GetUpChanel(int index, List<Candle> candles)
        {
            int length = Convert.ToInt32(((Line)_xIndicator).Values[index]);

            if (length == 0)
            {
                return 0;
            }

            decimal result = 0;

            int firstIndex = 0;

            if (index - length + 1 > 0)
            {
                firstIndex = index - length + 1;
            }

            try
            {
                for (int i = firstIndex; i < index + 1; i++)
                {
                    if (candles[i].High > result)
                    {
                        result = candles[i].High;
                    }
                }
                result += Shift;
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return result;
        }

        // расчёт канала нижнего

        /// <summary>
        /// нижний канал
        /// </summary>
        private List<decimal> _downChanel;

        /// <summary>
        /// пересчитать нижний канал
        /// </summary>
        private void ReloadDownChanel(List<Candle> candles)
        {
            if (_downChanel == null)
            {
                _downChanel = new List<decimal>();
            }

            if (candles.Count - 1 == _downChanel.Count)
            {
                // обновляем только последнее значение
                _downChanel.Add(GetDownChanel(candles.Count - 1, candles));
            }
            else
            {
                _downChanel = new List<decimal>();
                for (int i = 0; i < candles.Count; i++)
                {
                    _downChanel.Add(GetDownChanel(i, candles));
                }
            }

            ((Line)_downChanelIndicator).ProcessDesimals(_downChanel);
        }

        /// <summary>
        /// взять нижний канал по индексу
        /// </summary>
        private decimal GetDownChanel(int index, List<Candle> candles)
        {
            int length = Convert.ToInt32(((Line)_xIndicator).Values[index]);

            if (length == 0)
            {
                return 0;
            }

            decimal result = 0;

            try
            {

                result = decimal.MaxValue;

                int firstIndex = 0;

                if (index - length + 1 > 0)
                {
                    firstIndex = index - length + 1;
                }


                for (int i = firstIndex; i < index + 1; i++)
                {
                    if (candles[i].Low < result)
                    {
                        result = candles[i].Low;
                    }
                }
                result -= Shift;
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return result;
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
        /// влкючена ли реАктивация ордера
        /// </summary>
        private bool _reActivatorIsOn;

        /// <summary>
        /// время до которого включена реАктивация
        /// </summary>
        private DateTime _reActivatorMaxTime;

        /// <summary>
        /// цена реактиваации
        /// </summary>
        private decimal _reActivatorPrice;

        /// <summary>
        /// ордер для реАктивации
        /// </summary>
        private Order _reActivatorOrder;

        /// <summary>
        /// загрузить новый ордер для слежения
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
        /// прогрузить реактиватор новым тиком
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
        /// реАктивировать ордер
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
        /// из системы пришёл новый ордер
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
        /// из системы пришёл новый тик
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

            for (int i = 50; i < candles.Count - 1; i++)
            {
                if (candles[i].TimeStart.Add(_tab.TimeFrame).Hour > TimeTo && TimeTo != 0 ||
                    candles[i].TimeStart.Add(_tab.TimeFrame).Hour < TimeFrom && TimeFrom != 0)
                {
                    continue;
                }

                currentPosition = TryInter(currentPosition, candles, i);
            }

            return currentPosition;
        }

        /// <summary>
        /// пробовать войти в позицию. Эмулятор
        /// </summary>
        private int TryInter(int posNow, List<Candle> candles, int index)
        {
            // БАЙ
            if (posNow != 1)
            {
                decimal lineBuy = GetPriceToOpenPos(Side.Buy, index);

                if (lineBuy == 0)
                {
                    return posNow;
                }

                if (BuyAtLimit(candles, index + 1, lineBuy))
                {
                    PaintOpen(1, candles, index + 1, lineBuy);
                    return 1;
                }
            }

            // СЕЛЛ
            if (posNow != -1)
            {
                decimal lineSell = GetPriceToOpenPos(Side.Sell, index);

                if (lineSell == 0)
                {
                    return posNow;
                }

                if (SellAtLimit(candles, index + 1, lineSell))
                {
                    PaintOpen(-1, candles, index + 1, lineSell);
                    return -1;
                }
            }

            return posNow;
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
