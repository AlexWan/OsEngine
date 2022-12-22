using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class InstrumentsResponse
    {
       public List<Instrument> instruments;
    }

    public class Instrument
    {
        public string isoCurrencyName;
        public string figi;
        public Quotation dshortMin;
        public string countryOfRisk;
        public string lot;
        public string uid;
        public Quotation dlong;
        public Nominal nominal;
        public string sellAvailableFlag;
        public string currency;
        public string buyAvailableFlag;
        public string classCode;
        public string ticker;
        public string apiTradeAvailableFlag;
        public Quotation dlongMin;
        public string shortEnabledFlag;
        public Quotation kshort;
        public Quotation minPriceIncrement;
        public string otcFlag;
        public Quotation klong;
        public Quotation dshort;
        public string name;
        public string exchange;
        public string countryOfRiskName;
        public string isin;
    }


    /// <summary>
    ///  Котировка - денежная сумма без указания валюты
    /// </summary>
    public class Quotation
    {
        /// <summary>
        /// nano int32   Дробная часть суммы, может быть отрицательным числом
        /// </summary>
        public string nano;

        /// <summary>
        /// units int64   Целая часть суммы, может быть отрицательным числом
        /// </summary>
        public string units;

        private decimal _value = Decimal.MinValue;

        public decimal GetValue()
        {
            if(_value != Decimal.MinValue)
            {
                return _value;
            }

            string unitsWithNoMin = units.Replace("-", "");
            string nanoWithNoMin;
            nanoWithNoMin = "0";
            if (nano != null)
            {
                nanoWithNoMin = nano.Replace("-", "");
            }

            // 23.11 -> {"units":"23","nano":110000000}"
            // 23.01 -> {"units":"23","nano":10000000}"

            while (nanoWithNoMin.Length < 9)
            {
                nanoWithNoMin = nanoWithNoMin.Insert(0, "0");
            }

            string valInStr = unitsWithNoMin + ",";

            for(int i = 0;i < nanoWithNoMin.Length; i++)
            {
                valInStr += nanoWithNoMin[i].ToString();
            }

            //if(unitsWithNoMin.Length != units.Length 
            //    || nanoWithNoMin.Length != nano.Length)
            //{
            //    valInStr.Insert(0, "-");
            //}

            _value = valInStr.ToDecimal().ToStringWithNoEndZero().ToDecimal();

            return _value;
        }
    }

    public class Nominal
    {
        public string nano;
        public string currency;
        public string units;
    }
}
