/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{

    /// <summary>
    /// manual position support settings /
    /// ручный настройки сопровождения сделки
    /// </summary>
    public class BotManualControl
    {
        // работа потока
        // thread work part

        /// <summary>
        /// revocation thread
        /// поток отзывающий ордера
        /// </summary>
        public static Task Watcher;

        /// <summary>
        /// tabs that need to be checked
        /// вкладки которые нужно проверять
        /// </summary>
        public static List<BotManualControl> TabsToCheck = new List<BotManualControl>();

        private static object _activatorLocker = new object();

        /// <summary>
        /// activate stream to view deals
        /// активировать поток для просмотра сделок
        /// </summary>
        public static void Activate()
        {
            lock (_activatorLocker)
            {
                if (Watcher != null)
                {
                    return;
                }

                Watcher = new Task(WatcherHome);
                Watcher.Start();
            }

        }

        /// <summary>
        /// place of work thread that monitors the execution of transactions
        /// место работы потока который следит за исполнением сделок
        /// </summary>
        public static async void WatcherHome()
        {
            while (true)
            {
                await Task.Delay(2000);

                for (int i = 0; i < TabsToCheck.Count; i++)
                {
                    if (TabsToCheck[i] == null)
                    {
                        continue;
                    }
                    TabsToCheck[i].CheckPositions();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        private string _name;

        public BotManualControl(string name, BotTabSimple botTab,StartProgram startProgram)
        {
            _name = name;
            _startProgram = startProgram;

            StopIsOn = false;
            StopDistance = 30;
            StopSlipage = 5;
            ProfitIsOn = false;
            ProfitDistance = 30;
            ProfitSlipage = 5;
            DoubleExitIsOn = true;
            DoubleExitSlipage = 10;

            SecondToOpenIsOn = true;
            SecondToOpen = new TimeSpan(0, 0, 0, 50);

            SecondToCloseIsOn = true;
            SecondToClose = new TimeSpan(0, 0, 0, 50);

            SetbackToOpenIsOn = false;

            SetbackToOpenPosition = 10;

            SetbackToCloseIsOn = false;

            SetbackToClosePosition = 10;

            if (Load() == false)
            {
                Save();
            }

            _botTab = botTab;

            if (_startProgram != StartProgram.IsTester)
            {
                if (Watcher == null)
                {
                    Activate();
                }
                TabsToCheck.Add(this);
            }
        }

        /// <summary>
        /// load
        /// загрузить
        /// </summary>
        private bool Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"StrategSettings.txt"))
            {
                return false;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"StrategSettings.txt"))
                {

                    StopIsOn = Convert.ToBoolean(reader.ReadLine());
                    StopDistance = reader.ReadLine().ToDecimal();
                    StopSlipage = reader.ReadLine().ToDecimal();
                    ProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    ProfitDistance = reader.ReadLine().ToDecimal();
                    ProfitSlipage = reader.ReadLine().ToDecimal();
                    TimeSpan.TryParse(reader.ReadLine(), out _secondToOpen);
                    TimeSpan.TryParse(reader.ReadLine(), out _secondToClose);

                    DoubleExitIsOn = Convert.ToBoolean(reader.ReadLine());

                    SecondToOpenIsOn = Convert.ToBoolean(reader.ReadLine());
                    SecondToCloseIsOn = Convert.ToBoolean(reader.ReadLine());

                    SetbackToOpenIsOn = Convert.ToBoolean(reader.ReadLine());
                    SetbackToOpenPosition = reader.ReadLine().ToDecimal();
                    SetbackToCloseIsOn = Convert.ToBoolean(reader.ReadLine());
                    SetbackToClosePosition = reader.ReadLine().ToDecimal();

                    DoubleExitSlipage = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out TypeDoubleExitOrder);
                    Enum.TryParse(reader.ReadLine(), out ValuesType);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
            return true;
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"StrategSettings.txt", false))
                {
                    CultureInfo myCultureInfo = new CultureInfo("ru-RU");
                    writer.WriteLine(StopIsOn);
                    writer.WriteLine(StopDistance.ToString(myCultureInfo));
                    writer.WriteLine(StopSlipage.ToString(myCultureInfo));
                    writer.WriteLine(ProfitIsOn.ToString(myCultureInfo));
                    writer.WriteLine(ProfitDistance.ToString(myCultureInfo));
                    writer.WriteLine(ProfitSlipage.ToString(myCultureInfo));
                    writer.WriteLine(SecondToOpen.ToString());
                    writer.WriteLine(SecondToClose.ToString());

                    writer.WriteLine(DoubleExitIsOn);

                    writer.WriteLine(SecondToOpenIsOn);
                    writer.WriteLine(SecondToCloseIsOn);

                    writer.WriteLine(SetbackToOpenIsOn);
                    writer.WriteLine(SetbackToOpenPosition);
                    writer.WriteLine(SetbackToCloseIsOn);
                    writer.WriteLine(SetbackToClosePosition);
                    writer.WriteLine(DoubleExitSlipage);
                    writer.WriteLine(TypeDoubleExitOrder);
                    writer.WriteLine(ValuesType);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete
        /// удалить
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + _name + @"StrategSettings.txt"))
                {
                    File.Delete(@"Engine\" + _name + @"StrategSettings.txt");
                }

                TabsToCheck.Remove(this);

            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            BotManualControlUi ui = new BotManualControlUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// program that created the robot
        /// программа создавшая робота 
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// stop is enabled /
        /// включен ли стоп
        /// </summary>
        public bool StopIsOn;

        /// <summary>
        /// distance from entry to stop /
        /// расстояние от входа до стопа
        /// </summary>
        public decimal StopDistance;

        /// <summary>
        /// slippage for stop /
        /// проскальзывание для стопа
        /// </summary>
        public decimal StopSlipage;

        /// <summary>
        /// profit is enabled /
        /// вклюичен ли профит
        /// </summary>
        public bool ProfitIsOn;

        /// <summary>
        /// distance from trade entry to order profit /
        /// расстояние от входа в сделку до Профит ордера
        /// </summary>
        public decimal ProfitDistance;

        /// <summary>
        /// slippage /
        /// проскальзывание
        /// </summary>
        public decimal ProfitSlipage;

        /// <summary>
        /// open orders life time is enabled /
        /// включен ли отзыв заявки на открытие по времени
        /// </summary>
        public bool SecondToOpenIsOn;


        /// <summary>
        /// time to open a position in seconds, after which the order will be recalled / 
        /// время на открытие позиции в секундах, после чего ордер будет отозван
        /// </summary>
        public TimeSpan SecondToOpen
        {
            get
            {
                if (SecondToOpenIsOn)
                {
                    return _secondToOpen;
                }
                else
                {
                    return new TimeSpan(1, 0, 0, 0);
                }
            }
            set
            {
                _secondToOpen = value;
            }
        }
        private TimeSpan _secondToOpen;

        /// <summary>
        /// closed orders life time is enabled /
        /// включен ли отзыв заявки на закрытие по времени
        /// </summary>
        public bool SecondToCloseIsOn;

        /// <summary>
        /// time to close a position in seconds, after which the order will be recalled / 
        /// время на закрытие позиции в секундах, после чего ордер будет отозван
        /// </summary>
        public TimeSpan SecondToClose
        {
            get
            {
                if (SecondToCloseIsOn)
                {
                    return _secondToClose;
                }
                else
                {
                    return new TimeSpan(1, 0, 0, 0);
                }
            }
            set
            {
                _secondToClose = value;
            }
        }
        private TimeSpan _secondToClose;

        /// <summary>
        /// whether re-issuance of the request for closure is included if the first has been withdrawn /
        /// включено ли повторное выставление заявки на закрытие, если первая была отозвана 
        /// </summary>
        public bool DoubleExitIsOn;

        /// <summary>
        /// type of re-request for closure /
        /// тип повторной заявки на закрытие
        /// </summary>
        public OrderPriceType TypeDoubleExitOrder;

        /// <summary>
        /// slip to re-close /
        /// проскальзывание для повторного закрытия
        /// </summary>
        public decimal DoubleExitSlipage;

        /// <summary>
        /// is revocation of orders for opening on price rollback included / 
        /// включен ли отзыв ордеров на открытие по откату цены
        /// </summary>
        public bool SetbackToOpenIsOn;

        /// <summary>
        /// maximum rollback from order price when opening a position /
        /// максимальный откат от цены ордера при открытии позиции
        /// </summary>
        public decimal SetbackToOpenPosition;

        /// <summary>
        /// whether revocation of orders for closing on price rollback is included / 
        /// включен ли отзыв ордеров на закрытие по откату цены
        /// </summary>
        public bool SetbackToCloseIsOn;

        /// <summary>
        /// maximum rollback from order price when opening a position / 
        /// максимальный откат от цены ордера при открытии позиции
        /// </summary>
        public decimal SetbackToClosePosition;

        public ManualControlValuesType ValuesType;

        /// <summary>
        /// journal /
        /// журнал
        /// </summary>
        private BotTabSimple _botTab;

        public DateTime ServerTime = DateTime.MinValue;

        /// <summary>
        /// the method in which the thread monitors the execution of orders / 
        /// метод, в котором работает поток следящий за исполнением заявок
        /// </summary>
        private void CheckPositions()
        {

            if (MainWindow.ProccesIsWorked == false)
            {
                return;
            }

            if (ServerTime == DateTime.MinValue)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            try
            {
                List<Position> openDeals = _botTab.PositionsOpenAll;

                if (openDeals == null)
                {
                    return;
                }

                for (int i = 0; i < openDeals.Count; i++)
                {
                    Position position = openDeals[i];

                    for (int i2 = 0; position.OpenOrders != null && i2 < position.OpenOrders.Count; i2++)
                    {
                        // open orders / ОТКРЫВАЮЩИЕ ОРДЕРА
                        Order openOrder = position.OpenOrders[i2];

                        if (openOrder.State != OrderStateType.Activ &&
                            openOrder.State != OrderStateType.Patrial)
                        {
                            continue;
                        }

                        if (IsInArray(openOrder))
                        {
                            continue;
                        }

                        if (SecondToOpenIsOn &&
                            openOrder.TimeCreate.Add(openOrder.LifeTime) < ServerTime)
                        {
                            SendNewLogMessage(OsLocalization.Trader.Label70 + openOrder.NumberMarket,
                                LogMessageType.Trade);
                            SendOrderToClose(openOrder, openDeals[i]);
                        }

                        if (SetbackToOpenIsOn &&
                            openOrder.Side == Side.Buy)
                        {
                            decimal maxSpread = GetMaxSpread(openOrder);

                            if (Math.Abs(_botTab.PriceBestBid - openOrder.Price) > maxSpread)
                            {
                                SendNewLogMessage(OsLocalization.Trader.Label157 + openOrder.NumberMarket,
                                    LogMessageType.Trade);
                                SendOrderToClose(openOrder, openDeals[i]);
                            }
                        }

                        if (SetbackToOpenIsOn &&
                            openOrder.Side == Side.Sell)
                        {
                            decimal maxSpread = GetMaxSpread(openOrder);

                            if (Math.Abs(openOrder.Price - _botTab.PriceBestAsk) > maxSpread)
                            {
                                SendNewLogMessage(OsLocalization.Trader.Label157 + openOrder.NumberMarket,
                                    LogMessageType.Trade);
                                SendOrderToClose(openOrder, openDeals[i]);
                            }
                        }
                    }


                    for (int i2 = 0; position.CloseOrders != null && i2 < position.CloseOrders.Count; i2++)
                    {
                        // close orders / ЗАКРЫВАЮЩИЕ ОРДЕРА
                        Order closeOrder = position.CloseOrders[i2];

                        if ((closeOrder.State != OrderStateType.Activ &&
                             closeOrder.State != OrderStateType.Patrial))
                        {
                            continue;
                        }

                        if (IsInArray(closeOrder))
                        {
                            continue;
                        }

                        if (SecondToCloseIsOn &&
                            closeOrder.TimeCreate.Add(closeOrder.LifeTime) < ServerTime)
                        {
                            SendNewLogMessage(OsLocalization.Trader.Label70 + closeOrder.NumberMarket,
                                LogMessageType.Trade);
                            SendOrderToClose(closeOrder, openDeals[i]);
                        }

                        if (SetbackToCloseIsOn &&
                            closeOrder.Side == Side.Buy)
                        {
                            decimal priceRedLine = closeOrder.Price -
                                                   _botTab.Securiti.PriceStep * SetbackToClosePosition;

                            if (_botTab.PriceBestBid <= priceRedLine)
                            {
                                SendNewLogMessage(OsLocalization.Trader.Label157 + closeOrder.NumberMarket,
                                    LogMessageType.Trade);
                                SendOrderToClose(closeOrder, openDeals[i]);
                            }
                        }

                        if (SetbackToCloseIsOn &&
                            closeOrder.Side == Side.Sell)
                        {
                            decimal priceRedLine = closeOrder.Price +
                                                   _botTab.Securiti.PriceStep * SetbackToClosePosition;

                            if (_botTab.PriceBestAsk >= priceRedLine)
                            {
                                SendNewLogMessage(OsLocalization.Trader.Label157 + closeOrder.NumberMarket,
                                    LogMessageType.Trade);
                                SendOrderToClose(closeOrder, openDeals[i]);
                            }
                        }
                    }
                }
            }
            catch
                (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void TryReloadStopAndProfit(BotTabSimple bot, Position position)
        {
            if (StopIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal priceRedLine = position.EntryPrice - GetStopDistance(position,bot.Securiti);
                    decimal priceOrder = priceRedLine - GetStopSlippageDistance(position, bot.Securiti);

                    bot.CloseAtStop(position, priceRedLine, priceOrder);
                }

                if (position.Direction == Side.Sell)
                {
                    decimal priceRedLine = position.EntryPrice + GetStopDistance(position, bot.Securiti);
                    decimal priceOrder = priceRedLine + GetStopSlippageDistance(position, bot.Securiti);

                    bot.CloseAtStop(position, priceRedLine, priceOrder);
                }
            }

            if (ProfitIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal priceRedLine = position.EntryPrice + GetProfitDistance(position, bot.Securiti);
                    decimal priceOrder = priceRedLine - GetProfitDistanceSlippage(position, bot.Securiti);

                    bot.CloseAtProfit(position, priceRedLine, priceOrder);
                }

                if (position.Direction == Side.Sell)
                {
                    decimal priceRedLine = position.EntryPrice - GetProfitDistance(position, bot.Securiti);
                    decimal priceOrder = priceRedLine + GetProfitDistanceSlippage(position, bot.Securiti);

                    bot.CloseAtProfit(position, priceRedLine, priceOrder);
                }
            }
        }

        public void TryEmergencyClosePosition(BotTabSimple bot, Position position)
        {
            if (TypeDoubleExitOrder == OrderPriceType.Market)
            {
                bot.CloseAtMarket(position, position.OpenVolume);
            }
            else if (TypeDoubleExitOrder == OrderPriceType.Limit)
            {
                decimal price;
                if (position.Direction == Side.Buy)
                {
                    price = bot.PriceBestBid - GetEmergencyExitDistance(bot, position);
                }
                else
                {
                    price = bot.PriceBestAsk + GetEmergencyExitDistance(bot, position);
                }

                bot.CloseAtLimit(position, price, position.OpenVolume);
            }
        }

        private decimal GetEmergencyExitDistance(BotTabSimple bot, Position position)
        {
            decimal result = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                result = bot.Securiti.PriceStep * DoubleExitSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                result = DoubleExitSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                if (position.Direction == Side.Buy)
                {
                    result = bot.PriceBestBid * DoubleExitSlipage / 100;
                }
                else
                {
                    result = bot.PriceBestAsk * DoubleExitSlipage / 100;
                }
            }

            return result;
        }

        public decimal GetProfitDistanceSlippage(Position position,Security security)
        {
            decimal result = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                result = security.PriceStep * ProfitSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                result = ProfitSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                result = position.EntryPrice * ProfitSlipage / 100;
            }

            return result;
        }

        public decimal GetProfitDistance(Position position, Security security)
        {
            decimal result = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                result = security.PriceStep * ProfitDistance;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                result = ProfitDistance;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                result = position.EntryPrice * ProfitDistance / 100;
            }

            return result;
        }

        private decimal GetStopDistance(Position position, Security security)
        {
            decimal result = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                result = security.PriceStep * StopDistance;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                result = StopDistance;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                result = position.EntryPrice * StopDistance / 100;
            }

            return result;

        }

        private decimal GetStopSlippageDistance(Position position, Security security)
        {
            decimal result = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                result = security.PriceStep * StopSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                result = StopSlipage;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                result = position.EntryPrice * StopSlipage / 100;
            }

            return result;
        }

        private decimal GetMaxSpread(Order order)
        {
            decimal maxSpread = 0;

            if (ValuesType == ManualControlValuesType.MinPriceStep)
            {
                maxSpread = _botTab.Securiti.PriceStep * SetbackToOpenPosition;
            }
            else if (ValuesType == ManualControlValuesType.Absolute)
            {
                maxSpread = SetbackToOpenPosition;
            }
            else if (ValuesType == ManualControlValuesType.Percent)
            {
                maxSpread = order.Price * SetbackToOpenPosition / 100;
            }

            return maxSpread;
        }

        /// <summary>
        /// orders already sent for closure
        /// ордера, уже высланные на закрытие
        /// </summary>
        private List<Order> _ordersToClose = new List<Order>();

        /// <summary>
        /// send a review order /
        /// выслать ордер на отзыв
        /// </summary>
        /// <param name="order">order / ордер</param>
        /// <param name="deal">position of which order belongs / позиция которой ордер принадлежит</param>
        private void SendOrderToClose(Order order, Position deal)
        {
            if (IsInArray(order))
            {
                return;
            }

            _ordersToClose.Add(order);

            if (DontOpenOrderDetectedEvent != null)
            {
                DontOpenOrderDetectedEvent(order, deal);
            }
        }

        /// <summary>
        /// Is this order already sent for review? /
        /// этот ордер уже выслан на отзыв?
        /// </summary>
        private bool IsInArray(Order order)
        {
            for (int i = 0; i < _ordersToClose.Count; i++)
            {
                if (_ordersToClose[i].NumberUser == order.NumberUser)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// new order for withdrawal event / 
        /// новый ордер для отзыва
        /// </summary>
        public event Action<Order, Position> DontOpenOrderDetectedEvent;

        // log / сообщения в лог 

        /// <summary>
        /// send a new log message 
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing message for log / 
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// тип переменных для подсчёта расстояния
    /// </summary>
    public enum ManualControlValuesType
    {
        /// <summary>
        /// Минимальный шаг цены инструмента
        /// </summary>
        MinPriceStep,

        /// <summary>
        /// абсолютные значения
        /// </summary>
        Absolute,

        /// <summary>
        /// %
        /// </summary>
        Percent
    }
}
