using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class CurrencyQuotation
    {
        public string nano;

        public string currency;

        public string units;

        private decimal _value = Decimal.MinValue;

        public decimal GetValue()
        {
            if (_value != Decimal.MinValue)
            {
                return _value;
            }

            string unitsWithNoMin = units.Replace("-", "");
            string nanoWithNoMin = nano.Replace("-", "");

            string valInStr = unitsWithNoMin + ",";

            for (int i = 0; i < nanoWithNoMin.Length; i++)
            {
                valInStr += nanoWithNoMin[i].ToString();
            }

            if (unitsWithNoMin.Length != units.Length
                || nanoWithNoMin.Length != nano.Length)
            {
                valInStr.Insert(0, "-");
            }

            _value = valInStr.ToDecimal();

            return _value;
        }

    }

    public class PortfoliosResponse
    {
        public CurrencyQuotation totalAmountBonds;
        public CurrencyQuotation totalAmountFutures;
        public CurrencyQuotation totalAmountCurrencies;
        public Quotation expectedYield;
        public CurrencyQuotation totalAmountShares;
        public CurrencyQuotation totalAmountEtf;

        public List<TinkoffApiPosition> positions;
    }

    public class TinkoffApiPosition
    {
        public CurrencyQuotation averagePositionPrice;
        public string instrumentType;
        public Quotation quantity;
        public Quotation averagePositionPricePt;
        public CurrencyQuotation averagePositionPriceFifo;
        public CurrencyQuotation currentNkd;
        public CurrencyQuotation currentPrice;
        public string figi;
        public Quotation expectedYield;
        public Quotation quantityLots;
    }   
}
