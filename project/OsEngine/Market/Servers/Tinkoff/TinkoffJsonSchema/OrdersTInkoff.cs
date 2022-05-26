using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class OrderTinkoff
    {
        public string orderId;

        public string figi;

        public string direction;

        public string orderType;

        public string message;

        public string executionReportStatus;

        public string lotsExecuted;

        public string lotsRequested;

        public CurrencyQuotation initialOrderPrice;

        public CurrencyQuotation initialCommission;

        public CurrencyQuotation totalOrderAmount;

        public Quotation initialOrderPricePt;

        public CurrencyQuotation executedOrderPrice;

        public CurrencyQuotation executedCommission;

        public CurrencyQuotation initialSecurityPrice;

        public CurrencyQuotation aciValue;

    }
}
