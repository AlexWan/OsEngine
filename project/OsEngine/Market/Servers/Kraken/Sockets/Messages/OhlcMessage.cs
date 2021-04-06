using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Open High Low Close (Candle) feed for a currency pair and interval period.
    /// </summary>
    /// <remarks>
    /// When subscribed for OHLC, a snapshot of the last valid candle (irrespective of the endtime) will be sent, 
    /// followed by updates to the running candle. For example, if a subscription is made to 1 min candle and there 
    /// have been no trades for 5 mins, a snapshot of the last 1 min candle from 5 mins ago will be published. 
    /// The endtime can be used to determine that it is an old candle.
    /// </remarks>
    public sealed class OhlcMessage
    {
        /// <summary>
        /// Gets the ChannelID of pair-ohlc subscription
        /// </summary>
        /// <value>
        /// ChannelID of pair-ohlc subscription
        /// </value>
        public long ChannelId { get; private set; }

        /// <summary>
        /// Gets the Time, seconds since epoch.
        /// </summary>
        /// <value>
        /// The Time, seconds since epoch.
        /// </value>
        public decimal Time { get; private set; }

        /// <summary>
        /// Gets the End Timestamp of the interval
        /// </summary>
        /// <value>
        /// TheEnd Timestamp of the interval
        /// </value>
        public decimal EndTime { get; private set; }

        /// <summary>
        /// Gets the First traded price of the interval
        /// </summary>
        /// <value>
        /// The First traded price of the interval
        /// </value>
        public decimal Open { get; private set; }

        /// <summary>
        /// Gets the Highest traded price of the interval.
        /// </summary>
        /// <value>
        /// The Highest traded price of the interval.
        /// </value>
        public decimal High { get; private set; }

        /// <summary>
        /// Gets the Lowest traded price of the interval.
        /// </summary>
        /// <value>
        /// The Lowest traded price of the interval.
        /// </value>
        public decimal Low { get; private set; }

        /// <summary>
        /// Gets the Last traded price of the interval.
        /// </summary>
        /// <value>
        /// The Last traded price of the interval.
        /// </value>
        public decimal Close { get; private set; }

        /// <summary>
        /// Gets the Volume weighted average price of the interval.
        /// </summary>
        /// <value>
        /// The Volume weighted average price of the interval.
        /// </value>
        public decimal Vwap { get; private set; }

        /// <summary>
        /// Gets the Accumulated volume of the interval.
        /// </summary>
        /// <value>
        /// The Accumulated volume of the interval.
        /// </value>
        public decimal Volume { get; private set; }

        /// <summary>
        /// Gets the Number of trades in the interval.
        /// </summary>
        /// <value>
        /// The Number of trades in the interval.
        /// </value>
        public long Count { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="OhlcMessage"/> class from being created.
        /// </summary>
        private OhlcMessage()
        { }

        /// <summary>
        /// Creates a new <see cref="OhlcMessage" /> from well-formed string.
        /// </summary>
        /// <param name="rawOhlcMessage">The raw ohlc message.</param>
        /// <returns></returns>
        public static OhlcMessage CreateFromString(string rawOhlcMessage)
        {
            var ohlcMessage = KrakenDataMessageHelper.EnsureRawMessage(rawOhlcMessage);
            var dataArray = ohlcMessage[1] as JArray;
            return new OhlcMessage
            {
                ChannelId = Convert.ToInt64(ohlcMessage.First),
                Time = Convert.ToDecimal(dataArray[0]),
                EndTime = Convert.ToDecimal(dataArray[1]),
                Open = Convert.ToDecimal(dataArray[2]),
                High = Convert.ToDecimal(dataArray[3]),
                Low = Convert.ToDecimal(dataArray[4]),
                Close = Convert.ToDecimal(dataArray[5]),
                Vwap = Convert.ToDecimal(dataArray[6]),
                Volume = Convert.ToDecimal(dataArray[7]),
                Count = Convert.ToInt64(dataArray[8])
            };
        }
    }
}
