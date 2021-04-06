namespace Kraken.WebSockets.Messages
{
    public interface IKrakenMessageSerializer
    {
        TKrakenMessage Deserialize<TKrakenMessage>(string json) where TKrakenMessage : class, IKrakenMessage;

        string Serialize<TKrakenMessage>(TKrakenMessage message) where TKrakenMessage : class, IKrakenMessage; 
    }
}
