namespace Kraken.WebSockets.Messages
{
    public interface IKrakenMessage
    {
        string Event { get; }
    }
}