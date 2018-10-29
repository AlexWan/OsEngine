/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{

    /// <summary>
    /// ручный настройки сопровождения сделки
    /// </summary>
    public class BotManualControl
    {
        // статическая часть с работой потока проверяющего не нужно ли чего отзывать

        /// <summary>
        /// поток отзывающий ордера
        /// </summary>
        public static Thread Watcher;

        /// <summary>
        /// вкладки которые нужно проверять
        /// </summary>
        public static List<BotManualControl> TabsToCheck = new List<BotManualControl>();

        private static object _activatorLocker = new object();

        /// <summary>
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
                Watcher = new Thread(WatcherHome);
                Watcher.Name = "BotManualControlThread";
                Watcher.IsBackground = true;
                Watcher.Start();
            }

        }

        /// <summary>
        /// место работы потока который следит за исполнением сделок
        /// </summary>
        public static void WatcherHome()
        {
            while (true)
            {
                Thread.Sleep(2000);

                for (int i = 0; i < TabsToCheck.Count; i++)
                {
                    TabsToCheck[i].CheckPositions();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        // объект 

        private string _name;

        public BotManualControl(string name, BotTabSimple botTab,StartProgram startProgram)
        {
            _name = name;
            _startProgram = startProgram;

            // грузим настройки по умолчанию

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

            // грузим настройки из файла

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
                    StopDistance = Convert.ToInt32(reader.ReadLine());
                    StopSlipage = Convert.ToInt32(reader.ReadLine());
                    ProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    ProfitDistance = Convert.ToInt32(reader.ReadLine());
                    ProfitSlipage = Convert.ToInt32(reader.ReadLine());
                    TimeSpan.TryParse(reader.ReadLine(), out SecondToOpen);
                    TimeSpan.TryParse(reader.ReadLine(), out SecondToClose);

                    DoubleExitIsOn = Convert.ToBoolean(reader.ReadLine());

                    SecondToOpenIsOn = Convert.ToBoolean(reader.ReadLine());
                    SecondToCloseIsOn = Convert.ToBoolean(reader.ReadLine());

                    SetbackToOpenIsOn = Convert.ToBoolean(reader.ReadLine());
                    SetbackToOpenPosition = Convert.ToInt32(reader.ReadLine());
                    SetbackToCloseIsOn = Convert.ToBoolean(reader.ReadLine());
                    SetbackToClosePosition = Convert.ToInt32(reader.ReadLine());

                    DoubleExitSlipage = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out TypeDoubleExitOrder);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
            return true;
        }

        /// <summary>
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
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
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
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            BotManualControlUi ui = new BotManualControlUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// программа создавшая робота 
        /// </summary>
        private StartProgram _startProgram;

        // стоп
        /// <summary>
        /// включен ли стоп
        /// </summary>
        public bool StopIsOn;

        /// <summary>
        /// расстояние от входа до стопа
        /// </summary>
        public int StopDistance;

        /// <summary>
        /// проскальзывание для стопа
        /// </summary>
        public int StopSlipage;

        // профит

        /// <summary>
        /// вклюичен ли профит
        /// </summary>
        public bool ProfitIsOn;

        /// <summary>
        /// расстояние от входа в сделку до Профит ордера
        /// </summary>
        public int ProfitDistance;

        /// <summary>
        /// проскальзывание
        /// </summary>
        public int ProfitSlipage;

        // время на вход / выход

        /// <summary>
        /// включен ли отзыв заявки на открытие по времени
        /// </summary>
        public bool SecondToOpenIsOn;

        /// <summary>
        /// время на открытие позиции в секундах, после чего ордер будет отозван
        /// </summary>
        public TimeSpan SecondToOpen;

        /// <summary>
        /// включен ли отзыв заявки на закрытие по времени
        /// </summary>
        public bool SecondToCloseIsOn;

        /// <summary>
        /// время на закрытие позиции в секундах, после чего ордер будет отозван, а позиция докроется по рынку
        /// </summary>
        public TimeSpan SecondToClose;

        // реакция на отмену заявки на закрытие

        /// <summary>
        /// включено ли повторное выставление заявки на закрытие, если первая была отозвана 
        /// </summary>
        public bool DoubleExitIsOn;

        /// <summary>
        /// тип повторной заявки на закрытие
        /// </summary>
        public OrderPriceType TypeDoubleExitOrder;

        /// <summary>
        /// проскальзывание для повторного закрытия
        /// </summary>
        public int DoubleExitSlipage;

        // отход от цены для отмены заявки 

        /// <summary>
        /// включен ли отзыв ордеров на открытие по откату цены
        /// </summary>
        public bool SetbackToOpenIsOn;

        /// <summary>
        /// максимальный откат от цены ордера при открытии позиции, после чего ордер будет отозван
        /// </summary>
        public int SetbackToOpenPosition;

        /// <summary>
        /// включен ли отзыв ордеров на закрытие по откату цены
        /// </summary>
        public bool SetbackToCloseIsOn;

        /// <summary>
        /// максимальный откат от цены ордера при открытии позиции, после чего ордер будет отозван
        /// </summary>
        public int SetbackToClosePosition;

        // отзыв ордеров

        /// <summary>
        /// журнал
        /// </summary>
        private BotTabSimple _botTab;

        public DateTime ServerTime = DateTime.MinValue;

        /// <summary>
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
                        // ОТКРЫВАЮЩИЕ ОРДЕРА
                        Order openOrder = position.OpenOrders[i2];

                        if (openOrder.State != OrderStateType.Activ &&
                            openOrder.State != OrderStateType.Patrial)
                        {
                            continue;
                        }

                        if (SecondToOpenIsOn &&
                            openOrder.TimeCreate.Add(openOrder.LifeTime) < ServerTime)
                        {
                            SendOrderToClose(openOrder, openDeals[i]);
                        }

                        if (SetbackToOpenIsOn &&
                            openOrder.Side == Side.Buy)
                        {
                            decimal maxSpread = _botTab.Securiti.PriceStep * SetbackToOpenPosition;

                            if (Math.Abs(_botTab.PriceBestBid - openOrder.Price) > maxSpread)
                            {
                                SendOrderToClose(openOrder, openDeals[i]);
                            }
                        }

                        if (SetbackToOpenIsOn &&
                            openOrder.Side == Side.Sell)
                        {
                            decimal maxSpread = _botTab.Securiti.PriceStep * SetbackToOpenPosition;

                            if (Math.Abs(_botTab.PriceBestAsk - openOrder.Price) > maxSpread)
                            {
                                SendOrderToClose(openOrder, openDeals[i]);
                            }
                        }
                    }


                    for (int i2 = 0; position.CloseOrders != null && i2 < position.CloseOrders.Count; i2++)
                    {
                        // ЗАКРЫВАЮЩИЕ ОРДЕРА
                        Order closeOrder = position.CloseOrders[i2];

                        if ((closeOrder.State != OrderStateType.Activ &&
                             closeOrder.State != OrderStateType.Patrial))
                        {
                            continue;
                        }

                        if (SecondToCloseIsOn &&
                            closeOrder.TimeCreate.Add(closeOrder.LifeTime) < ServerTime)
                        {
                            SendOrderToClose(closeOrder, openDeals[i]);
                        }

                        if (SetbackToCloseIsOn &&
                            closeOrder.Side == Side.Buy)
                        {
                            decimal priceRedLine = closeOrder.Price -
                                                   _botTab.Securiti.PriceStep * SetbackToClosePosition;

                            if (_botTab.PriceBestBid <= priceRedLine)
                            {
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

        /// <summary>
        /// ордера, уже высланные на закрытие
        /// </summary>
        private Order[] _ordersToClose;

        /// <summary>
        /// выслать ордер на отзыв
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="deal">позиция которой ордер принадлежит</param>
        private void SendOrderToClose(Order order, Position deal)
        {
            if (IsInArray(order))
            {
                return;
            }

            SetInArray(order);

            if (DontOpenOrderDetectedEvent != null)
            {
                DontOpenOrderDetectedEvent(order, deal);
            }
        }

        /// <summary>
        /// этот ордер уже выслан на отзыв?
        /// </summary>
        private bool IsInArray(Order order)
        {
            if (_ordersToClose == null)
            {
                return false;
            }

            for (int i = 0; i < _ordersToClose.Length; i++)
            {
                if (_ordersToClose[i].NumberUser == order.NumberUser)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// добавить ордер в массив отзываемых ордеров
        /// </summary>
        private void SetInArray(Order order)
        {

            if (_ordersToClose == null)
            {
                _ordersToClose = new[] { order };
            }
            else
            {
                Order[] newOrders = new Order[_ordersToClose.Length + 1];

                for (int i = 0; i < _ordersToClose.Length; i++)
                {
                    newOrders[i] = _ordersToClose[i];
                }

                newOrders[newOrders.Length - 1] = order;

                _ordersToClose = newOrders;
            }
        }

        /// <summary>
        /// новый ордер для отзыва
        /// </summary>
        public event Action<Order, Position> DontOpenOrderDetectedEvent;

        // сообщения в лог 

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
