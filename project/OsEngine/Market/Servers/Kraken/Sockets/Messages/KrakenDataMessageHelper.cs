using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Helper methods for handling kraken data messages
    /// </summary>
    internal static class KrakenDataMessageHelper
    {
        /// <summary>
        /// Ensures the raw message.
        /// </summary>
        /// <param name="rawMessage">The raw message.</param>
        /// <returns>The raw message as a <see cref="JArray"/> instance</returns>
        /// <exception cref="ArgumentNullException">rawMessage</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// rawMessage
        /// or
        /// rawMessage
        /// or
        /// rawMessage
        /// </exception>
        internal static JArray EnsureRawMessage(string rawMessage)
        {
            if (rawMessage == null)
            {
                throw new ArgumentNullException(nameof(rawMessage));
            }

            if (string.IsNullOrEmpty(rawMessage))
            {
                throw new ArgumentOutOfRangeException(nameof(rawMessage));
            }

            try
            {
                var token = JToken.Parse(rawMessage);
                if (!(token is JArray))
                {
                    throw new ArgumentOutOfRangeException(nameof(rawMessage));
                }

                return token as JArray;
            }
            catch (JsonReaderException ex)
            {
                throw new ArgumentOutOfRangeException(nameof(rawMessage), ex.Message);
            }
        }
    }
}
