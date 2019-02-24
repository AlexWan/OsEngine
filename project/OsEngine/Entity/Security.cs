/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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

                string step = Convert.ToDecimal(Convert.ToDouble(PriceStep)).ToString(new CultureInfo("ru-RU"));
                _decimals = step.Split(',')[1].Length;
            }
        }

        private decimal _priceStep;

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
        public SecurityType SecurityType;

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
        private int _decimals = -1;

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

// сохранение и загрузка

        /// <summary>
        /// загрузить из строки
        /// </summary>
        public void LoadFromString(string save)
        {
            string[] array = save.Split('!');

            Name = array[0];
            NameClass = array[1];
            NameFull = array[2];
            NameId = array[3];
            NameFull = array[4];
            Enum.TryParse(array[5],out State);
            PriceStep = Convert.ToDecimal(array[6].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Lot = Convert.ToDecimal(array[7].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            PriceStepCost = Convert.ToDecimal(array[8].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Go = Convert.ToDecimal(array[9].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Enum.TryParse(array[10],out SecurityType);
            _decimals = Convert.ToInt32(array[11]);
            PriceLimitLow = Convert.ToDecimal(array[12].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            PriceLimitHigh = Convert.ToDecimal(array[13].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Enum.TryParse(array[14], out OptionType);
            Strike = Convert.ToDecimal(array[15].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Expiration = Convert.ToDateTime(array[16]);

        }

        /// <summary>
        /// взять строку сохранения
        /// </summary>
        public string GetSaveStr()
        {
            string result = Name + "!";
            result += NameClass + "!";
            result += NameFull + "!";
            result += NameId + "!";
            result += NameFull + "!";
            result += State + "!";
            result += PriceStep + "!";
            result += Lot + "!";
            result += PriceStepCost + "!";
            result += Go + "!";
            result += SecurityType + "!";
            result += _decimals + "!";
            result += PriceLimitLow + "!";
            result += PriceLimitHigh + "!";
            result += OptionType + "!";
            result += Strike + "!";
            result += Expiration + "!";

            return result;
        }

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
        /// не определено
        /// </summary>
        None,

        /// <summary>
        /// валюта. В т.ч. и крипта
        /// </summary>
        CurrencyPair,

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
        /// не определено
        /// </summary>
        None,
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
