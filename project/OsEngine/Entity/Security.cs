﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// security
    /// инструмент
    /// </summary>
    public class Security
    {
        /// <summary>
        /// security
        /// инструмент
        /// </summary>
        public Security()
        {
            PriceLimitLow = 0;
            PriceLimitHigh = 0;
        }

        /// <summary>
        /// securuty name
        /// название инструмена
        /// </summary>
        public string Name;

        /// <summary>
        /// full name
        /// полное название
        /// </summary>
        public string NameFull;

        /// <summary>
        /// class code
        /// код класса
        /// </summary>
        public string NameClass;

        /// <summary>
        /// Unique tool identifier.
        /// It is used in some platforms as the main instrument key in the trading system.
        /// Уникальный идентификатор инструмента.
        ///  Используется в некоторых платформах как главный ключ инструмента в торговой системе.
        /// </summary>
        public string NameId;

        /// <summary>
        /// Биржа на которой торгуется инструмент
        /// </summary>
        public string Exchange;

        /// <summary>
        /// the trading status of this instrument on the stock exchange
        /// состояние торгов этим инструментом на бирже
        /// </summary>
        public SecurityStateType State;

        /// <summary>
        /// price step, i.e. minimal price change for the instrument
        /// шаг цены, т.е. минимальное изменение цены для инструмента
        /// </summary>
        public decimal PriceStep
        {
            get { return _priceStep; }
            set
            {
                _priceStep = value;

                if (_priceStep >= 1 ||
                    _priceStep == 0)
                {
                    _decimals = 0;
                    return;
                }

                string step = Convert.ToDecimal(Convert.ToDouble(_priceStep)).ToString(new CultureInfo("ru-RU"));
               
                if(step.Split(',').Length == 1)
                {
                    _decimals = 0;
                }
                else
                {
                    _decimals = step.Split(',')[1].Length;
                }
            }
        }

        private decimal _priceStep;

        /// <summary>
        /// lot
        /// лот
        /// </summary>
        public decimal Lot;

        /// <summary>
        /// the cost of a step of the price, i.e. how much profit is dripping on the deposit for one step of the price
        /// стоимость шага цены, т.е. сколько профита капает на депозит за один шаг цены
        /// </summary>
        public decimal PriceStepCost;

        /// <summary>
        /// warranty coverage
        /// гарантийное обеспечение
        /// </summary>
        public decimal Go;

        /// <summary>
        /// security type
        /// тип бумаги
        /// </summary>
        public SecurityType SecurityType;

        /// <summary>
        /// open the Paper Settings window
        /// вызвать окно настроек бумаги
        /// </summary>
        public void ShowDialog()
        {
            SecurityUi ui = new SecurityUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// the number of decimal places of the instrument price.
        /// if the price step is higher, or the raver 1, for example 10, then it still returns 0
        /// количество знаков после запятой цены инструмента.
        /// если шаг цены больше, либо равер 1, например 10, то возвращается всё равно 0
        /// </summary>
        public int Decimals
        {
            get
            {
                if (_decimals != 0)
                {
                    return _decimals;
                }

                if (PriceStep >= 1 ||
                    PriceStep == 0)
                {
                    return 0;
                }


                string step = Convert.ToDecimal(Convert.ToDouble(PriceStep)).ToString(new CultureInfo("ru-RU"));

                if (step.Split(',').Length > 1)
                {
                    _decimals = step.Split(',')[1].Length;
                }
                else if(step.Split('.').Length > 1)
                {
                    _decimals = step.Split('.')[1].Length;
                }

                return _decimals;

            }
            set
            {
                if (value >= 0)
                {
                    _decimals = value;
                }
            }
        }
        private int _decimals = -1;

        /// <summary>
        /// the number of decimal places of the instrument volume
        /// количество знаков после запятой объёма инструмента
        /// </summary>
        public int DecimalsVolume;

        /// <summary>
        /// минимальный объём торгов по активу
        /// </summary>
        public decimal MinTradeAmount = 1;

        /// <summary>
        /// Lower price limit for bids. If you place an order with a price lower - the system will reject
        /// Нижний лимит цены для заявок. Если выставить ордер с ценой ниже - система отвергнет
        /// </summary>
        public decimal PriceLimitLow;

        /// <summary>
        /// Upper price limit for bids. If you place an order with a price higher - the system will reject
        /// Верхний лимит цены для заявок. Если выставить ордер с ценой выше - система отвергнет
        /// </summary>
        public decimal PriceLimitHigh;
        // For options
        // для опционов

        /// <summary>
        /// option type
        /// тип опциона
        /// </summary>
        public OptionType OptionType;

        /// <summary>
        /// strike
        /// страйк
        /// </summary>
        public decimal Strike;

        /// <summary>
        /// expiration date
        /// дата экспирации
        /// </summary>
        public DateTime Expiration;
        // save and load
        // сохранение и загрузка

        /// <summary>
        /// upload from the line
        /// загрузить из строки
        /// </summary>
        public void LoadFromString(string save)
        {
            string[] array = save.Split('\n');

            Name = array[0];
            NameClass = array[1];
            NameFull = array[2];
            NameId = array[3];
            Enum.TryParse(array[4],out State);
            PriceStep = array[5].ToDecimal();
            Lot = array[6].ToDecimal();
            PriceStepCost = array[7].ToDecimal();
            Go = array[8].ToDecimal();
            Enum.TryParse(array[9],out SecurityType);
            _decimals = Convert.ToInt32(array[10]);
            PriceLimitLow = array[11].ToDecimal();
            PriceLimitHigh = array[12].ToDecimal();
            Enum.TryParse(array[13], out OptionType);
            Strike = array[14].ToDecimal();
            Expiration = Convert.ToDateTime(array[15]);
            DecimalsVolume = Convert.ToInt32(array[16]);
            MinTradeAmount = array[17].ToDecimal();
        }

        /// <summary>
        /// save the line
        /// взять строку сохранения
        /// </summary>
        public string GetSaveStr()
        {
            string result = Name + "\n";
            result += NameClass + "\n";
            result += NameFull + "\n";
            result += NameId + "\n";
            result += State + "\n";
            result += PriceStep + "\n";
            result += Lot + "\n";
            result += PriceStepCost + "\n";
            result += Go + "\n";
            result += SecurityType + "\n";
            result += _decimals + "\n";
            result += PriceLimitLow + "\n";
            result += PriceLimitHigh + "\n";
            result += OptionType + "\n";
            result += Strike + "\n";
            result += Expiration + "\n";
            result += DecimalsVolume + "\n";
            result += MinTradeAmount;
            return result;
        }

    }

    /// <summary>
    /// stock market conditions
    /// состояние бумаги на бирже
    /// </summary>
    public enum SecurityStateType
    {
        /// <summary>
        /// we don't know if the bidding's going on
        /// неизвестно, идут ли торги
        /// </summary>
        UnKnown,

        /// <summary>
        /// trading on the paper is active
        /// торги по бумаге активны
        /// </summary>
        Activ,

        /// <summary>
        /// paper auction is closed.
        /// торги по бумаге закрыты
        /// </summary>
        Close
    }

    /// <summary>
    /// instrumental type
    /// тип инструмента
    /// </summary>
    public enum SecurityType
    {
        /// <summary>
        /// none
        /// не определено
        /// </summary>
        None,

        /// <summary>
        /// currency. Including crypt
        /// валюта. В т.ч. и крипта
        /// </summary>
        CurrencyPair,

        /// <summary>
        /// акция
        /// </summary>
        Stock,

        /// <summary>
        /// облигация
        /// </summary>
        Bond,

        /// <summary>
        /// futures
        /// фьючерс
        /// </summary>
        Futures,

        /// <summary>
        /// option
        /// опцион
        /// </summary>
        Option,

        /// <summary>
        /// index индекс
        /// </summary>
        Index,

        /// <summary>
        /// exchange fund
        /// </summary>
        Fund
    }

    /// <summary>
    /// option type
    /// тип опциона
    /// </summary>
    public enum OptionType
    {
        /// <summary>
        /// none
        /// не определено
        /// </summary>
        None,
        /// <summary>
        /// put 
        /// пут
        /// </summary>
        Put,

        /// <summary>
        /// call
        /// колл
        /// </summary>
        Call
    }
}
