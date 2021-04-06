using System;
using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Trade object
    /// </summary>
    public sealed class TradeObject
    {
        /// <summary>
        /// Gets the trade identifier.
        /// </summary>
        /// <value>
        /// The trade identifier.
        /// </value>
        public string TradeId { get; private set; }

        /// <summary>
        /// Gets the order responsible for execution of trade.
        /// </summary>
        /// <value>
        /// The order responsible for execution of trade.
        /// </value>
        public string OrderTxId { get; private set; }

        /// <summary>
        /// Gets the Position trade id.
        /// </summary>
        /// <value>
        /// The Position trade id.
        /// </value>
        public string PosTxId { get; private set; }

        /// <summary>
        /// Gets the Asset pair.
        /// </summary>
        /// <value>
        /// The Asset pair.
        /// </value>
        public string Pair { get; private set; }

        /// <summary>
        /// Gets the unix timestamp of trade.
        /// </summary>
        /// <value>
        /// The unix timestamp of trade.
        /// </value>
        public decimal Time { get; private set; }

        /// <summary>
        /// Gets the type of order (buy/sell).
        /// </summary>
        /// <value>
        /// The type of order (buy/sell).
        /// </value>
        public string Type { get; private set; }

        /// <summary>
        /// Gets the type of the order.
        /// </summary>
        /// <value>
        /// The type of the order.
        /// </value>
        public string OrderType { get; private set; }

        /// <summary>
        /// Gets the average price order was executed at (quote currency).
        /// </summary>
        /// <value>
        /// The average price order was executed at (quote currency).
        /// </value>
        public decimal Price { get; private set; }

        /// <summary>
        /// Gets the total cost of order (quote currency).
        /// </summary>
        /// <value>
        /// The total cost of order (quote currency).
        /// </value>
        public decimal Cost { get; private set; }

        /// <summary>
        /// Gets the total fee (quote currency).
        /// </summary>
        /// <value>
        /// The total fee (quote currency).
        /// </value>
        public decimal Fee { get; private set; }

        /// <summary>
        /// Gets the volume (base currency).
        /// </summary>
        /// <value>
        /// The volume (base currency).
        /// </value>
        public decimal Volume { get; private set; }

        /// <summary>
        /// Gets the initial margin (quote currency).
        /// </summary>
        /// <value>
        /// The initial margin (quote currency).
        /// </value>
        public decimal Margin { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="TradeObject"/> class from being created.
        /// </summary>
        private TradeObject() { }

        public static TradeObject CreateFromJObject(string tradeId, JObject jObject)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            return new TradeObject()
            {
                TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId)),
                OrderTxId = jObject.Value<string>("ordertxid"),
                PosTxId = jObject.Value<string>("postxid"),
                Pair = jObject.Value<string>("pair"),
                Time = jObject.Value<decimal>("time"),
                Type = jObject.Value<string>("type"),
                OrderType = jObject.Value<string>("ordertype"),
                Price = jObject.Value<decimal>("price"),
                Cost = jObject.Value<decimal>("cost"),
                Fee = jObject.Value<decimal>("fee"),
                Volume = jObject.Value<decimal>("vol"),
                Margin = jObject.Value<decimal>("margin"),
            };
        }
    }
}