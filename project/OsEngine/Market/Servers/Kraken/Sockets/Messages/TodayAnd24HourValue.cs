using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    public class TodayAnd24HourValue<TValue>
    {
        public TValue Today { get; private set; }

        public TValue Last24Hours { get; private set; }

        private TodayAnd24HourValue()
        {
        }

        public static TodayAnd24HourValue<TValue> CreateFromJArray(JArray tokenArray)
        {
            return new TodayAnd24HourValue<TValue>
            {
                Today = (TValue)ConvertToken(tokenArray[0]),
                Last24Hours = (TValue) ConvertToken(tokenArray[1])
            };
        }

        private static object ConvertToken(JToken token)
        {
            if (typeof(TValue) == typeof(decimal))
            {
                return Convert.ToDecimal(token);
            }
            if (typeof(TValue) == typeof(int))
            {
                return Convert.ToInt32(token);
            }

            return null;
        }
    }
}
