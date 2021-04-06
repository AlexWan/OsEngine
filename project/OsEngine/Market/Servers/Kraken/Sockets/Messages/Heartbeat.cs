namespace Kraken.WebSockets.Messages
{
    public sealed class Heartbeat : KrakenMessage
    {
        public const string EventName = "heartbeat";

        public Heartbeat() : base(EventName)
        { }
    }
}
