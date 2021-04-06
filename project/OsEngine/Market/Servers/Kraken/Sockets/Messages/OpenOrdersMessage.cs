using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Open orders. Feed to show all the open orders belonging to the user authenticated API key.
    /// </summary>
    /// <remarks>
    /// Initial snapshot will provide list of all open orders and then any updates to the open orders
    /// list will be sent. For status change updates, such as 'closed', the fields <code>orderid</code>
    /// and <code>status</code> will be present in the payload.
    /// </remarks>
    public class OpenOrdersMessage
    {
        /// <summary>
        /// Gets the name of the channel.
        /// </summary>
        /// <value>
        /// The name of the channel.
        /// </value>
        public string ChannelName { get; private set; }

        /// <summary>
        /// Gets the list of open orders.
        /// </summary>
        public IEnumerable<Order> Orders { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="OpenOrdersMessage"/> class from being created.
        /// </summary>
        private OpenOrdersMessage()
        {
        }

        /// <summary>
        /// Creates from string.
        /// </summary>
        /// <param name="rawMessage">The raw message.</param>
        /// <returns></returns>
        internal static OpenOrdersMessage CreateFromString(string rawMessage)
        {
            var message = KrakenDataMessageHelper.EnsureRawMessage(rawMessage);
            var orders = message[0]
                .Select(x => (x as JObject)?.ToObject<Dictionary<string, JObject>>())
                .Select(items => items != null && items.Count == 1 ?
                    new
                    {
                        TradeId = items.Keys.ToArray()[0],
                        TradeObject = items.Values.ToArray()[0]
                    } :
                    null)
                .Where(x => x != null)
                .Select(x => Order.CreateFromJObject(x.TradeId, x.TradeObject))
                .ToList();

            return new OpenOrdersMessage()
            {
                Orders = orders,
                ChannelName = message[1].Value<string>()
            };
        }
    }
}
