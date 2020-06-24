using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Huobi.Entities
{
    /// <summary>
    /// GetSymbols response
    /// </summary>
    public class GetSymbolsResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        public string status;

        /// <summary>
        /// Response body
        /// </summary>
        public Symbol[] data;

        /// <summary>
        /// Trading symbol
        /// </summary>
        public class Symbol
        {
            /// <summary>
            /// Base currency in a trading symbol
            /// </summary>
            [JsonProperty(PropertyName = "base-currency")]
            public string baseCurrency;

            /// <summary>
            /// Quote currency in a trading symbol
            /// </summary>
            [JsonProperty(PropertyName = "quote-currency")]
            public string quoteCurrency;

            /// <summary>
            /// Quote currency precision when quote price(decimal places)
            /// </summary>
            [JsonProperty(PropertyName = "price-precision")]
            public int pricePrecision;

            /// <summary>
            /// Base currency precision when quote amount(decimal places)
            /// </summary>
            [JsonProperty(PropertyName = "amount-precision")]
            public int amountPrecision;

            /// <summary>
            /// Trading section
            /// Possible values: [main，innovation]
            /// </summary>
            [JsonProperty(PropertyName = "symbol-partition")]
            public string symbolPartition;

            /// <summary>
            /// Trading symbol
            /// </summary>
            [JsonProperty(PropertyName = "symbol")]
            public string symbol;

            /// <summary>
            /// The status of the symbol；Allowable values: [online，offline,suspend].
            /// "online" - Listed, available for trading,
            /// "offline" - de-listed, not available for trading，
            /// "suspend"-suspended for trading
            /// </summary>
            [JsonProperty(PropertyName = "state")]
            public string state;

            /// <summary>
            /// Precision of value in quote currency (value = price * amount)
            /// </summary>
            [JsonProperty(PropertyName = "value-precision")]
            public int valuePrecision;

            /// <summary>
            /// Minimum order amount (order amount is the ‘amount’ defined in ‘v1/order/orders/place’ when it’s a limit order or sell-market order)
            /// </summary>
            [JsonProperty(PropertyName = "min-order-amt")]
            public float minOrderAmt;

            /// <summary>
            /// Max order amount
            /// </summary>
            [JsonProperty(PropertyName = "max-order-amt")]
            public float maxOrderAmt;

            /// <summary>
            /// Minimum order value (order value refers to ‘amount’ * ‘price’ defined in ‘v1/order/orders/place’ when it’s a limit order or ‘amount’ when it’s a buy-market order)
            /// </summary>
            [JsonProperty(PropertyName = "min-order-value")]
            public float minOrderValue;

            /// <summary>
            /// The applicable leverage ratio
            /// </summary>
            [JsonProperty(PropertyName = "leverage-ratio")]
            public float leverageRatio;
        }
    }
}
