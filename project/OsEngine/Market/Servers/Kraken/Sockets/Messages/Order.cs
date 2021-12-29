using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Open orders. Feed to show all the open orders belonging to the user authenticated API key.
    /// </summary>
    /// <remarks>
    /// Initial snapshot will provide list of all open orders and then any
    /// updates to the open orders list will be sent. For status change updates,
    /// such as 'closed', the fields orderid and status will be present in the payload.
    /// </remarks>
    public class Order
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Order"/> class from being created.
        /// </summary>
        private Order()
        {
        }

        /// <summary>
        /// Creates from new instance json object.
        /// </summary>
        /// <param name="orderId">The order identifier.</param>
        /// <param name="orderObject">The order object.</param>
        /// <returns></returns>
        internal static Order CreateFromJObject(string orderId, JObject orderObject)
        {
            return new Order()
            {
                OrderId = orderId,
                RefId = orderObject.Value<string>("refid"),
                UserRef = orderObject.Value<long>("userref"),
                Status = orderObject.Value<string>("status"),
                OpenTimestamp = orderObject.Value<decimal?>("opentm"),
                StartTimestamp = orderObject.Value<decimal?>("starttm"),
                ExpireTimestamp = orderObject.Value<decimal?>("expiretm"),
                Description = OrderDescription.CreateFromJObject(orderObject.Value<JObject>("descr")),
                Volume = orderObject.Value<decimal?>("vol"),
                VolumeExecuted = orderObject.Value<decimal>("vol_exec"),
                Cost = orderObject.Value<decimal?>("cost"),
                Fee = orderObject.Value<decimal?>("fee"),
                Price = orderObject.Value<decimal?>("price"),
                StopPrice = orderObject.Value<decimal?>("stopprice"),
                LimitPrice = orderObject.Value<decimal?>("limitprice"),
                Miscellaneous = orderObject.Value<string>("misc"),
                OrderFlags = orderObject.Value<string>("oflags")
            };
        }

        /// <summary>
        /// Gets the order identifier.
        /// </summary>
        /// <value>
        /// The order identifier.
        /// </value>
        public string OrderId { get; private set; }

        /// <summary>
        /// Gets the referral order transaction id that created this order.
        /// </summary>
        /// <value>
        /// The referral order transaction id that created this order.
        /// </value>
        public string RefId { get; private set; }

        /// <summary>
        /// Gets the user reference id.
        /// </summary>
        /// <value>
        /// The user reference id.
        /// </value>
        public long? UserRef { get; private set; }

        /// <summary>
        /// Gets the status of order.
        /// </summary>
        /// <value>
        /// The status of order.
        /// </value>
        public string Status { get; private set; }

        /// <summary>
        /// Gets the unix timestamp of when order was placed.
        /// </summary>
        /// <value>
        /// The unix timestamp of when order was placed.
        /// </value>
        public decimal? OpenTimestamp { get; private set; }

        /// <summary>
        /// Gets the unix timestamp of order start time (if set).
        /// </summary>
        /// <value>
        /// The unix timestamp of order start time (if set).
        /// </value>
        public decimal? StartTimestamp { get; private set; }

        /// <summary>
        /// Gets the unix timestamp of order end time (if set).
        /// </summary>
        /// <value>
        /// The unix timestamp of order end time (if set).
        /// </value>
        public decimal? ExpireTimestamp { get; private set; }

        /// <summary>
        /// Gets the order description info.
        /// </summary>
        /// <value>
        /// The order description info.
        /// </value>
        public OrderDescription Description { get; private set; }

        /// <summary>
        /// Gets the volume of order (base currency unless viqc set in orderflags).
        /// </summary>
        /// <value>
        /// The volume of order (base currency unless viqc set in orderflags).
        /// </value>
        public decimal? Volume { get; private set; }

        /// <summary>
        /// Gets the volume executed (base currency unless viqc set in oflags).
        /// </summary>
        /// <value>
        /// The volume executed (base currency unless viqc set in oflags).
        /// </value>
        public decimal? VolumeExecuted { get; private set; }

        /// <summary>
        /// Gets the total cost (quote currency unless unless viqc set in oflags).
        /// </summary>
        /// <value>
        /// The total cost (quote currency unless unless viqc set in oflags).
        /// </value>
        public decimal? Cost { get; private set; }
        /// <summary>
        /// Gets the total fee (quote currency).
        /// </summary>
        /// <value>
        /// The total fee (quote currency).
        /// </value>
        public decimal? Fee { get; private set; }

        /// <summary>
        /// Gets the average price (quote currency unless viqc set in oflags).
        /// </summary>
        /// <value>
        /// The average price (quote currency unless viqc set in oflags).
        /// </value>
        public decimal? Price { get; private set; }
        /// <summary>
        /// Gets the stop price (quote currency, for trailing stops).
        /// </summary>
        /// <value>
        /// The stop price (quote currency, for trailing stops).
        /// </value>
        public decimal? StopPrice { get; private set; }
        /// <summary>
        /// Gets the triggered limit price (quote currency, when limit based order type triggered).
        /// </summary>
        /// <value>
        /// The triggered limit price (quote currency, when limit based order type triggered).
        /// </value>
        public decimal? LimitPrice { get; private set; }

        /// <summary>
        /// Gets the comma delimited list of miscellaneous info.
        /// </summary>
        /// <value>
        /// The comma delimited list of miscellaneous info.
        /// </value>
        /// <remarks>
        /// stopped     = triggered by stop price
        /// touched     = triggered by touch price
        /// liquidation = liquidation
        /// partial     = partial fill
        /// </remarks>
        public string Miscellaneous { get; private set; }

        /// <summary>
        /// Gets the comma delimited list of order flags (optional).
        /// </summary>
        /// <value>
        /// The comma delimited list of order flags (optional).
        /// </value>
        /// <remarks>
        /// viqc  = volume in quote currency (not available for leveraged orders)
        /// fcib  = prefer fee in base currency
        /// fciq  = prefer fee in quote currency
        /// nompp = no market price protection
        /// post  = post only order (available when ordertype = limit)
        /// </remarks>
        public string OrderFlags { get; private set; }
    }
}