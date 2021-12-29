namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Subscription status names.
    /// </summary>
    public static class SubscriptionStatusNames
    {
        /// <summary>
        /// The subscribe.
        /// </summary>
        public const string Subscribe = "subscribed";
        /// <summary>
        /// The unsubscribe.
        /// </summary>
        public const string Unsubscribe = "unsubscribed";
        /// <summary>
        /// The error.
        /// </summary>
        public const string Error = "error";
    }
}