/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// инструмент
    /// </summary>
    public class Security
    {
        /// <summary>
        /// инструмент
        /// </summary>
        public Security()
        {
            PriceLimitLow = 0;
            PriceLimitHigh = 0;
        }

        /// <summary>
        /// название инструмена
        /// </summary>
        public string Name;

        /// <summary>
        /// полное название
        /// </summary>
        public string NameFull;

        /// <summary>
        /// код класса
        /// </summary>
        public string NameClass;

        /// <summary>
        /// Уникальный идентификатор инструмента.
        ///  Используется в некоторых платформах как главный ключ инструмента в торговой системе.
        /// </summary>
        public string NameId;

        /// <summary>
        /// состояние торгов этим инструментом на бирже
        /// </summary>
        public SecurityStateType State;

        /// <summary>
        /// шаг цены, т.е. минимальное изменение цены для инструмента
        /// </summary>
        public decimal PriceStep;

        /// <summary>
        /// лот
        /// </summary>
        public decimal Lot;

        /// <summary>
        /// стоимость шага цены, т.е. сколько профита капает на депозит за один шаг цены
        /// </summary>
        public decimal PriceStepCost;

        /// <summary>
        /// гарантийное обеспечение
        /// </summary>
        public decimal Go;

        /// <summary>
        /// тип бумаги
        /// </summary>
        public SecurityType Type;

        /// <summary>
        /// вызвать окно настроек бумаги
        /// </summary>
        public void ShowDialog()
        {
            SecurityUi ui = new SecurityUi(this);
            ui.ShowDialog();
        }

        /// <summary>
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

                _decimals = step.Split(',')[1].Length;

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
        private int _decimals;

        /// <summary>
        /// Нижний лимит цены для заявок. Если выставить ордер с ценой ниже - система отвергнет
        /// </summary>
        public decimal PriceLimitLow;

        /// <summary>
        /// Верхний лимит цены для заявок. Если выставить ордер с ценой выше - система отвергнет
        /// </summary>
        public decimal PriceLimitHigh;

// для опционов

        /// <summary>
        /// тип опциона
        /// </summary>
        public OptionType OptionType;

        /// <summary>
        /// страйк
        /// </summary>
        public decimal Strike;

        /// <summary>
        /// дата экспирации
        /// </summary>
        public DateTime Expiration;
    }

    /// <summary>
    /// состояние бумаги на бирже
    /// </summary>
    public enum SecurityStateType
    {
        /// <summary>
        /// торги по бумаге активны
        /// </summary>
        Activ,

        /// <summary>
        /// торги по бумаге закрыты
        /// </summary>
        Close,

        /// <summary>
        /// неизвестно, идут ли торги
        /// </summary>
        UnKnown
    }

    /// <summary>
    /// тип инструмента
    /// </summary>
    public enum SecurityType
    {
        /// <summary>
        /// акция
        /// </summary>
        Stock,

        /// <summary>
        /// фьючерс
        /// </summary>
        Futures,

        /// <summary>
        /// опцион
        /// </summary>
        Option
    }

    /// <summary>
    /// тип опциона
    /// </summary>
    public enum OptionType
    {
        /// <summary>
        /// пут
        /// </summary>
        Put,

        /// <summary>
        /// колл
        /// </summary>
        Call
    }
}
