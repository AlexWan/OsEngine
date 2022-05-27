using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class MyTradeResponce
    {
        public string quantity;

        public string tradeId;

        public CurrencyQuotation price;

    }

    public class OrderStateResponce
    {
        public string orderId;

        public string figi;

        /// <summary>
        /// EXECUTION_REPORT_STATUS_UNSPECIFIED, 
        /// EXECUTION_REPORT_STATUS_FILL, 
        /// EXECUTION_REPORT_STATUS_REJECTED, 
        /// EXECUTION_REPORT_STATUS_CANCELLED, 
        /// EXECUTION_REPORT_STATUS_NEW, 
        /// EXECUTION_REPORT_STATUS_PARTIALLYFILL
        /// </summary>
        public string executionReportStatus;

        public CurrencyQuotation initialOrderPrice;

        public CurrencyQuotation initialCommission;

        public CurrencyQuotation averagePositionPrice;

        public string lotsExecuted;

        public CurrencyQuotation totalOrderAmount;

        public string lotsRequested;

        public CurrencyQuotation executedOrderPrice;

        public CurrencyQuotation executedCommission;

        public CurrencyQuotation initialSecurityPrice;

        public CurrencyQuotation serviceCommission;

        public string currency;

        public string orderDate;

        public List<MyTradeResponce> stages;

    }

    public class OrderCancelResponce
    {
        public string code;

        public string message;

        public List<OrderCancelResponseDetails> details;

    }

    public class OrderCancelResponseDetails
    {
        public string type;
    }

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
