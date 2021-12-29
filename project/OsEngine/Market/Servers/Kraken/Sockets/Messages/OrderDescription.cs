using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Order description info
    /// </summary>
    public class OrderDescription
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="OrderDescription"/> class from being created.
        /// </summary>
        private OrderDescription()
        { }

        /// <summary>
        /// Creates from <see cref="JObject"/>.
        /// </summary>
        /// <param name="orderDescriptionObject">The order description object.</param>
        /// <returns></returns>
        internal static OrderDescription CreateFromJObject(JObject orderDescriptionObject)
        {
            if(orderDescriptionObject == null)
            {
                return null;
            }

            return new OrderDescription
            {
                Pair = orderDescriptionObject.Value<string>("pair"),
                Type = orderDescriptionObject.Value<string>("type"),
                OrderType = orderDescriptionObject.Value<string>("ordertype"),
                Price = orderDescriptionObject.Value<decimal?>("price"),
                SecondPrice = orderDescriptionObject.Value<decimal?>("price2"),
                Leverage = orderDescriptionObject.Value<string>("leverage"),
                Order = orderDescriptionObject.Value<string>("order"),
                Close = orderDescriptionObject.Value<string>("close"),
            };
        }

        /// <summary>
        /// Gets the asset pair.
        /// </summary>
        /// <value>
        /// The asset pair.
        /// </value>
        public string Pair { get; private set; }

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
        /// Gets the primary price.
        /// </summary>
        /// <value>
        /// The primary price.
        /// </value>
        public decimal? Price { get; private set; }

        /// <summary>
        /// Gets the secondary price.
        /// </summary>
        /// <value>
        /// The secondary price.
        /// </value>
        public decimal? SecondPrice { get; private set; }

        /// <summary>
        /// Gets the amount of leverage.
        /// </summary>
        /// <value>
        /// The amount of leverage.
        /// </value>
        public string Leverage { get; private set; }

        /// <summary>
        /// Gets the order description.
        /// </summary>
        /// <value>
        /// The order description.
        /// </value>
        public string Order { get; private set; }

        /// <summary>
        /// Gets the conditional close order description (if conditional close set).
        /// </summary>
        /// <value>
        /// The conditional close order description (if conditional close set).
        /// </value>
        public string Close { get; private set; }
    }
}