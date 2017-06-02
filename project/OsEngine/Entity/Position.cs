﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// Сделка
    /// </summary>
    public class Position
    {
        public Position()
        {
            State = PositionStateType.None;
        }

        /// <summary>
        /// ордер, открывший сделку
        /// </summary>
        public List<Order> OpenOrders
        {
            get
            {
                return _openOrders;
            }
        }
        private List<Order> _openOrders;

        /// <summary>
        /// загрузить в позицию новый ордер закрывающий позицию
        /// </summary>
        /// <param name="openOrder"></param>
        public void AddNewOpenOrder(Order openOrder)
        {
            if (_openOrders == null)
            {
                _openOrders = new List<Order>();
            }
            _openOrders.Add(openOrder);
            
            State = PositionStateType.Opening;
        }

        /// <summary>
        /// ордера, закрывающие сделку
        /// </summary>
        public List<Order> CloseOrders
        {
            get
            {
                return _closeOrders;
            }
        }
        private List<Order> _closeOrders;

        public List<MyTrade> MyTrades
        {
            get
            {
                List<MyTrade> myTrades = new List<MyTrade>();

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    List<MyTrade> newTrades = _openOrders[i].MyTrades;
                    if (newTrades != null &&
                        newTrades.Count != 0)
                    {
                        myTrades.AddRange(newTrades);
                    }
                }

                for (int i = 0; _closeOrders != null && i < _closeOrders.Count; i++)
                {   
                    List<MyTrade> newTrades = _closeOrders[i].MyTrades;
                    if (newTrades != null &&
                        newTrades.Count != 0)
                    {
                        myTrades.AddRange(newTrades);
                    }
                }

                return myTrades;
            }
        }

        /// <summary>
        /// загрузить в позицию новый ордер закрывающий позицию
        /// </summary>
        /// <param name="closeOrder"></param>
        public void AddNewCloseOrder(Order closeOrder)
        {
            if (CloseOrders == null)
            {
                _closeOrders = new List<Order>();
            }
            _closeOrders.Add(closeOrder);

            State = PositionStateType.Closing;
        }

        /// <summary>
        /// есть ли активные ордера на открытие позиции
        /// </summary>
        public bool OpenActiv
        {
            get
            {
                if (OpenOrders == null ||
                    OpenOrders.Count == 0)
                {
                    return false;
                }

                if (OpenOrders.Find(order => order.State == OrderStateType.Activ || order.State == OrderStateType.Pending || order.State == OrderStateType.None) != null)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// есть ли активные ордера на закрытие позиции
        /// </summary>
        public bool CloseActiv
        {
            get
            {
                if (CloseOrders == null ||
                    CloseOrders.Count == 0)
                {
                    return false;
                }

                if (CloseOrders.Find(order => order.State == OrderStateType.Activ || order.State == OrderStateType.Pending || order.State == OrderStateType.None) != null)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// активен ли стопПриказ
        /// </summary>
        public bool StopOrderIsActiv;

        /// <summary>
        /// цена заявки стоп приказа
        /// </summary>
        public decimal StopOrderPrice;

        /// <summary>
        /// стоп - цена, цена после достижения которой в систему будет выставлени приказ
        /// </summary>
        public decimal StopOrderRedLine;

        /// <summary>
        /// активен ли профит приказ
        /// </summary>
        public bool ProfitOrderIsActiv;

        /// <summary>
        /// цена заявки профит приказа
        /// </summary>
        public decimal ProfitOrderPrice;

        /// <summary>
        /// профит - цена, цена после достижения которой в систему будет выставлени приказ
        /// </summary>
        public decimal ProfitOrderRedLine;

        /// <summary>
        /// направление сделки Buy / Sell
        /// </summary>
        public Side Direction;

        private PositionStateType _state;

        /// <summary>
        /// статус сделки Open / Close / Opening
        /// </summary>
        public PositionStateType State
        {
            get { return _state; }
            set
            {
                _state = value;
                if (value == PositionStateType.ClosingFail)
                {
                    
                }
            }
        }

        /// <summary>
        /// номер сделки
        /// </summary>
        public int Number;

        /// <summary>
        /// Бумага по которой открыта позиция
        /// </summary>
        public string SecurityName
        {
            get
            {
                if (_openOrders != null && _openOrders.Count != 0)
                {
                    return _openOrders[0].SecurityNameCode;
                }
                return "";
            }
        }

        /// <summary>
        /// имя бота, которому принадлежит сделка
        /// </summary>
        public string NameBot;

        /// <summary>
        /// количество прибыли по операции в процентах 
        /// </summary>
        public decimal ProfitOperationPersent;

        /// <summary>
        /// количество прибыли по операции в абсолютном выражении
        /// </summary>
        public decimal ProfitOperationPunkt;

        /// <summary>
        /// комментарий
        /// </summary>
        public string Comment;

        /// <summary>
        /// тип сигнала на открытие
        /// </summary>
        public string SignalTypeOpen;

        /// <summary>
        /// тип сигнала за закрытие
        /// </summary>
        public string SignalTypeClose;

        /// <summary>
        /// максимальный объём по позиции
        /// </summary>
        public int MaxVolume
        {
            get
            {
                int value = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    value += _openOrders[i].VolumeExecute;
                }

                return value;

            }
        }

        /// <summary>
        /// количество контрактов открытых в сделке
        /// </summary>
        public int OpenVolume 
        {
            get
            {
                if (CloseOrders == null)
                {
                    int volume = 0;

                    for (int i = 0;_openOrders != null && i < _openOrders.Count; i++)
                    {
                        volume += _openOrders[i].VolumeExecute;
                    }
                    return volume;
                }

                int valueClose = 0;

                if (CloseOrders != null)
                {
                    for (int i = 0; i < CloseOrders.Count; i++)
                    {
                        valueClose += CloseOrders[i].VolumeExecute;
                    }
                }

                int volumeOpen = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    volumeOpen += _openOrders[i].VolumeExecute;
                }

                int value = volumeOpen - valueClose;

                return value;

            }
        }

        /// <summary>
        /// количество котрактов ожидающих открытия
        /// </summary>
        public int WaitVolume
        {
            get
            {
                int volumeWait = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    if (_openOrders[i].State == OrderStateType.Activ)
                    {
                        volumeWait += _openOrders[i].Volume - _openOrders[i].VolumeExecute;
                    }
                }

                return volumeWait;
            }
        }

        /// <summary>
        /// цена открытия позиции
        /// </summary>
        public decimal EntryPrice
        {
            get
            {
                if (_openOrders == null ||
                    _openOrders.Count == 0)
                {
                    return 0;
                }

                decimal price = 0;
                int volume = 0;
                for (int i = 0; i < _openOrders.Count; i++)
                {
                    int volumeEx = _openOrders[i].VolumeExecute;
                    if (volumeEx != 0)
                    {
                        volume += _openOrders[i].VolumeExecute;
                        price += _openOrders[i].VolumeExecute * _openOrders[i].PriceReal;
                    }
                    
                }
                if (volume == 0)
                {
                    return 0;
                }

                return price/volume;
            }
        }

        /// <summary>
        /// цена закрытия позиции
        /// </summary>
        public decimal ClosePrice
        {
            get
            {
                if (CloseOrders != null && CloseOrders.Count != 0)
                {
                    return CloseOrders[CloseOrders.Count - 1].PriceReal;
                }
                return 0;
            }
        }

        /// <summary>
        /// проверить входящий ордер, на принадлежность этой сделке
        /// </summary>
        public void SetOrder(Order newOrder)
        {

            Order openOrder = null;

            if (_openOrders != null)
            {
                for (int i = 0; i < _openOrders.Count; i++)
                {
                    if (_openOrders[i].NumberUser == newOrder.NumberUser)
                    {
                        if (_openOrders[i].State == OrderStateType.Fail && newOrder.State == OrderStateType.Fail ||
                            _openOrders[i].State == OrderStateType.Cancel && newOrder.State == OrderStateType.Cancel)
                        {
                            return;
                        }
                        openOrder = _openOrders[i];
                        break;
                    }
                }
            }

            if (openOrder != null)
            {
                openOrder.State = newOrder.State;
                openOrder.NumberMarket = newOrder.NumberMarket;

                if (openOrder.TimeCallBack == DateTime.MinValue)
                {
                    openOrder.TimeCallBack = newOrder.TimeCallBack;
                }
                
                openOrder.TimeCancel = newOrder.TimeCancel;
                openOrder.VolumeExecute = newOrder.VolumeExecute;

                if (openOrder.State == OrderStateType.Done && openOrder.TradesIsComing &&
                    OpenVolume != 0 && !CloseActiv)
                {
                    State = PositionStateType.Open;
                }
                else if (newOrder.State == OrderStateType.Fail && newOrder.VolumeExecute == 0 && 
                    OpenVolume == 0)
                {
                    State = PositionStateType.OpeningFail;
                }
                else if (newOrder.State == OrderStateType.Cancel && newOrder.VolumeExecute == 0 &&
                    OpenVolume == 0)
                {
                    State = PositionStateType.OpeningFail;
                }
                else if (newOrder.State == OrderStateType.Cancel && OpenVolume != 0)
                {
                    State = PositionStateType.Open;
                }
                else if (newOrder.State == OrderStateType.Done && OpenVolume == 0 
                    && CloseOrders != null && CloseOrders.Count > 0)
                {
                    State = PositionStateType.Done;
                }
            }

            Order closeOrder = null;

            if (CloseOrders != null)
            {
                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    if (CloseOrders[i].NumberUser == newOrder.NumberUser)
                    {
                        if (CloseOrders[i].State == OrderStateType.Fail &&newOrder.State == OrderStateType.Fail ||
                            CloseOrders[i].State == OrderStateType.Cancel && newOrder.State == OrderStateType.Cancel)
                        {
                            return;
                        }
                        closeOrder = CloseOrders[i];

                        break;
                    }
                }
            }

            if (closeOrder != null)
            {
                closeOrder.State = newOrder.State;
                closeOrder.NumberMarket = newOrder.NumberMarket;

                if (closeOrder.TimeCallBack == DateTime.MinValue)
                {
                    closeOrder.TimeCallBack = newOrder.TimeCallBack;
                }
                closeOrder.TimeCancel = newOrder.TimeCancel;
                closeOrder.VolumeExecute = newOrder.VolumeExecute;

                if (closeOrder.State == OrderStateType.Done && OpenVolume == 0)
                {
                    //AlertMessageManager.ThrowAlert(null, "Done", "");
                    State = PositionStateType.Done;
                }
                else if (closeOrder.State == OrderStateType.Fail && !CloseActiv && OpenVolume != 0)
                {
                    //AlertMessageManager.ThrowAlert(null, "Fail", "");
                    State = PositionStateType.ClosingFail;
                }
                else if (closeOrder.State == OrderStateType.Cancel && !CloseActiv && OpenVolume != 0)
                {// если не полностью закрылись и это последний ордер в ордерах на закрытие
                    //AlertMessageManager.ThrowAlert(null, "Cancel", "");
                    State = PositionStateType.ClosingFail;
                }
                else if (closeOrder.State == OrderStateType.Done && OpenVolume < 0)
                {
                    State = PositionStateType.ClosingSurplus;
                }
                
                if (State == PositionStateType.Done && CloseOrders != null && EntryPrice != 0)
                {
                    //AlertMessageManager.ThrowAlert(null, "Done пересчёт", "");
                    decimal medianPriceClose = 0;
                    int countValue = 0;

                    for (int i = 0; i < CloseOrders.Count; i++)
                    {
                        if (CloseOrders[i].VolumeExecute != 0)
                        {
                            medianPriceClose += CloseOrders[i].PriceReal * CloseOrders[i].VolumeExecute;
                            countValue += CloseOrders[i].VolumeExecute;
                        }
                    }

                    if (countValue != 0)
                    {
                        medianPriceClose = medianPriceClose/countValue;
                    }

                    if (medianPriceClose == 0)
                    {
                        return;
                    }

                    if (Direction == Side.Buy)
                    {
                        ProfitOperationPersent = medianPriceClose / EntryPrice * 100 - 100;
                        ProfitOperationPunkt = medianPriceClose - EntryPrice;
                    }
                    else
                    {
                        ProfitOperationPunkt = EntryPrice - medianPriceClose;
                        ProfitOperationPersent = -(medianPriceClose / EntryPrice * 100 - 100);
                    }
                    ProfitOperationPersent = Math.Round(ProfitOperationPersent, 5);
                }
            }
        }

        /// <summary>
        /// проверить входящий трейд, на принадлежность этой сделке
        /// </summary>
        public void SetTrade(MyTrade trade)
        {
            if (_openOrders != null)
            {

                for (int i = 0; i < _openOrders.Count; i++)
                {
                    if (_openOrders[i].NumberMarket == trade.NumberOrderParent)
                    {
                        _openOrders[i].SetTrade(trade);
                        if (OpenVolume != 0)
                        {
                            State = PositionStateType.Open;
                        }
                        else if (OpenVolume == 0)
                        {
                            State = PositionStateType.Done;
                        }
                    }
                }
            }

            if (CloseOrders != null)
            {
                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    if (CloseOrders[i].NumberMarket == trade.NumberOrderParent)
                    {
                        CloseOrders[i].SetTrade(trade);
                        if (OpenVolume == 0)
                        {
                            State = PositionStateType.Done;
                        }
                        else if (OpenVolume < 0)
                        {
                            State = PositionStateType.ClosingSurplus;
                        }      
                    }
                }
            }


            if (State == PositionStateType.Done && CloseOrders != null && EntryPrice != 0 )
            {
                decimal medianPriceClose = 0;
                int countValue = 0;

                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    if (CloseOrders[i].VolumeExecute != 0)
                    {
                        medianPriceClose += CloseOrders[i].PriceReal * CloseOrders[i].VolumeExecute;
                        countValue += CloseOrders[i].VolumeExecute;
                    }
                }

                if (countValue != 0)
                {
                    medianPriceClose = medianPriceClose / countValue;
                }

                if (medianPriceClose == 0)
                {
                    return;
                }
                if (Direction == Side.Buy)
                {
                    ProfitOperationPersent = medianPriceClose / EntryPrice * 100 - 100;
                    ProfitOperationPunkt = medianPriceClose - EntryPrice;
                }
                else
                {
                    ProfitOperationPunkt = EntryPrice - medianPriceClose;
                    ProfitOperationPersent = -(medianPriceClose / EntryPrice * 100 - 100);
                }

                ProfitOperationPersent = Math.Round(ProfitOperationPersent, 3);
            }
        }

        /// <summary>
        /// загрузить в сделку бид с аском, чтобы пересчитать прибыльность
        /// </summary>
        public void SetBidAsk(decimal bid, decimal ask)
        {
            if (State == PositionStateType.Open)
            {
                if (EntryPrice == 0)
                {
                    return;
                }

                if (Direction == Side.Buy)
                {
                    ProfitOperationPersent = ask / EntryPrice * 100 - 100;
                    ProfitOperationPunkt = ask - EntryPrice;
                }
                else
                {
                    ProfitOperationPersent = -(bid / EntryPrice * 100 - 100);
                    ProfitOperationPunkt = EntryPrice - bid;
                }
            }
        }

        /// <summary>
        /// взять строку для сохранения
        /// </summary>
        public string GetStringForSave()
        {
            string result = "";

            result += Direction + "#";

            result += State + "#";

            result += NameBot + "#";

            result += ProfitOperationPersent.ToString(new CultureInfo("ru-RU")) + "#";

            result += ProfitOperationPunkt.ToString(new CultureInfo("ru-RU")) + "#";

            if (OpenOrders == null)
            {
                result += "null" + "#";
            }
            else
            {
                for(int i = 0;i < OpenOrders.Count;i++)
                {
                    result += OpenOrders[i].GetStringForSave() + "^";
                }
                result += "#";
            }

            result += Number + "#";

            result += Comment + "#";

            result += StopOrderIsActiv + "#";
            result += StopOrderPrice + "#";
            result += StopOrderRedLine + "#";

            result += ProfitOrderIsActiv + "#";
            result += ProfitOrderPrice + "#";

            result += Lots + "#";
            result += PriceStepCost + "#";
            result += PriceStep + "#";
            result += PortfolioValueOnOpenPosition + "#";

            result += ProfitOrderRedLine + "#";
            result += SignalTypeOpen + "#";
            result += SignalTypeClose;

            if (CloseOrders != null)
            {
                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    result += "#" + CloseOrders[i].GetStringForSave();
                }
            }

            return result;
        }

        /// <summary>
        /// загрузить сделку из входящей строки
        /// </summary>
        public void SetDealFromString(string save)
        {
            string[] arraySave = save.Split('#');

            Enum.TryParse(arraySave[0], true, out Direction);

            NameBot = arraySave[2];

            ProfitOperationPersent = Convert.ToDecimal(arraySave[3]);

            ProfitOperationPunkt = Convert.ToDecimal(arraySave[4]);

            if (arraySave[5] != "null")
            {
               string[] ordersOpen = arraySave[5].Split('^');
                if (ordersOpen.Length != 1)
                {
                    _openOrders = new List<Order>();
                    for (int i = 0; i < ordersOpen.Length - 1; i++)
                    {
                        _openOrders.Add(new Order());
                        _openOrders[i].SetOrderFromString(ordersOpen[i]);
                    }
                }
            }

            Number = Convert.ToInt32(arraySave[6]);
            Comment = arraySave[7];

            StopOrderIsActiv = Convert.ToBoolean(arraySave[8]);
            StopOrderPrice = Convert.ToDecimal(arraySave[9]);
            StopOrderRedLine = Convert.ToDecimal(arraySave[10]);

            ProfitOrderIsActiv = Convert.ToBoolean(arraySave[11]);
            ProfitOrderPrice = Convert.ToDecimal(arraySave[12]);

            Lots = Convert.ToDecimal(arraySave[13]);
            PriceStepCost = Convert.ToDecimal(arraySave[14]);
            PriceStep = Convert.ToDecimal(arraySave[15]);
            PortfolioValueOnOpenPosition = Convert.ToDecimal(arraySave[16]);

            ProfitOrderRedLine = Convert.ToDecimal(arraySave[17]);

            SignalTypeOpen = arraySave[18];
            SignalTypeClose = arraySave[19];

            for (int i = 0; i < 10; i++)
            {
                if (arraySave.Length > 20 + i)
                {
                    Order newOrder = new Order();
                    newOrder.SetOrderFromString(arraySave[20 + i]);
                    AddNewCloseOrder(newOrder);
                }
            }

            PositionStateType state;
            Enum.TryParse(arraySave[1], true, out state);
            State = state;
        }

        /// <summary>
        /// время создания позиции
        /// </summary>
        public DateTime TimeCreate
        {
            get
            {
                if (_openOrders != null)
                {
                    return _openOrders[_openOrders.Count-1].TimeCallBack;
                }
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// время закрытия позиции
        /// </summary>
        public DateTime TimeClose
        {
            get
            {
                if (CloseOrders != null && CloseOrders.Count != 0)
                {
                    return CloseOrders[CloseOrders.Count - 1].TimeCallBack;
                }
                return TimeCreate;
            }
        }

// профит для портфеля

        /// <summary>
        /// количество прибыли относительно портфеля в процентах
        /// </summary>
        public decimal ProfitPortfolioPersent
        {
            get
            {
                if (PortfolioValueOnOpenPosition == 0)
                {
                    return 0;
                }

                return ProfitPortfolioPunkt / PortfolioValueOnOpenPosition*100;
            }
        }

        /// <summary>
        /// количество прибыли относительно портфеля в абсолютном выражении
        /// </summary>
        public decimal ProfitPortfolioPunkt
        {
            get
            {
                int volume = 0;

                for (int i = 0; i < _openOrders.Count; i++)
                {
                    volume += _openOrders[i].VolumeExecute;
                }

                if(volume == 0||
                    PriceStepCost == 0 ||
                    MaxVolume == 0)
                {
                    return 0;
                }

                return (ProfitOperationPunkt/PriceStep)*PriceStepCost*MaxVolume*1; //  Lots;
            } 
        }

        /// <summary>
        /// количество лотов в одной сделке
        /// </summary>
        public decimal Lots;

        /// <summary>
        /// стоимость шага цены
        /// </summary>
        public decimal PriceStepCost;

        /// <summary>
        /// шаг цены
        /// </summary>
        public decimal PriceStep;

        /// <summary>
        /// размер портфеля на момент открытия портфеля
        /// </summary>
        public decimal PortfolioValueOnOpenPosition;

    }

    /// <summary>
    /// способ открытия сделки
    /// </summary>
    public enum PositionOpenType
    {
        /// <summary>
        /// заявка по определённой цене
        /// </summary>
        Limit,

        /// <summary>
        /// заявка по любой цене
        /// </summary>
        Market,

        /// <summary>
        /// айсберг заявка. Заявка состоящая из нескольких лимитных заявок
        /// </summary>
        Aceberg
    }

    /// <summary>
    /// статус сделки
    /// </summary>
    public enum PositionStateType
    {
        /// <summary>
        /// не назначен
        /// </summary>
        None,

        /// <summary>
        /// открывается
        /// </summary>
        Opening,

        /// <summary>
        /// закрыта
        /// </summary>
        Done,

        /// <summary>
        /// ошибка
        /// </summary>
        OpeningFail,

        /// <summary>
        /// открыта
        /// </summary>
        Open,

        /// <summary>
        /// закрывается
        /// </summary>
        Closing,

        /// <summary>
        /// ошибка на закрытии
        /// </summary>
        ClosingFail,

        /// <summary>
        /// перебор во время закрытия.
        /// </summary>
        ClosingSurplus
    }

    /// <summary>
    /// направление сделки
    /// </summary>
    public enum Side
    {
        /// <summary>
        /// купля
        /// </summary>
        Buy,

        /// <summary>
        /// продажа
        /// </summary>
        Sell,

        /// <summary>
        /// неизвестно
        /// </summary>
        UnKnown
    }
}
