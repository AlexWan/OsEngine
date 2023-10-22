using Newtonsoft.Json.Linq;

namespace OsEngine.Market.Servers.Alor.Dto
{
    public class MessageData
    {
        public string guid { get; set; }
    
        public JToken data { get; set; }
    
        public string message { get; set; }
    
        public MessageSchemeEnum messageScheme { get; set; }
    
        public MessageTypeEnum msgType { get; set; }
    }
    
    public enum MessageSchemeEnum
    {
        TradeMsg,
        OrderbookSnapshotMsg,
        AcknowledgementMsg,
        SecurityMsg,
        PortfolioMsg,
        MyNewTradeMsg,
        Unknown
    }
    
    public enum MessageTypeEnum
    {
        Ack,
        Close,
        Error,
        Generic,
        Ping,
        Pong,
        Post,
        Refresh,
        Request,
        Status,
        Update
    }
}