using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{

    /// <summary>
    /// Own trades, on subscription last 50 trades for the user will be sent, followed by new trades.
    /// </summary>
    public sealed class OwnTradesMessage
    {
        private OwnTradesMessage()
        { }

        /// <summary>
        /// Creates the <see cref="OwnTradesMessage"/> from it's from string representation coming from the api.
        /// </summary>
        /// <param name="rawMessage">The raw message.</param>
        /// <returns></returns>
        public static OwnTradesMessage CreateFromString(string rawMessage)
        {
            var message = KrakenDataMessageHelper.EnsureRawMessage(rawMessage);
            var trades = message[0]
                .Select(x => (x as JObject)?.ToObject<Dictionary<string, JObject>>())
                .Select(items => items != null && items.Count == 1 ?
                    new
                    {
                        TradeId = items.Keys.ToArray()[0],
                        TradeObject = items.Values.ToArray()[0]
                    } :
                    null)
                .Where(x => x != null)
                .Select(x => TradeObject.CreateFromJObject(x.TradeId, x.TradeObject))
                .ToList();

            return new OwnTradesMessage
            {
                Trades = trades,
                Name = message[1].ToString()
            };

        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the list of trades.
        /// </summary>
        /// <value>
        /// The list of trades.
        /// </value>
        public List<TradeObject> Trades { get; private set; } = new List<TradeObject>();
    }
}
