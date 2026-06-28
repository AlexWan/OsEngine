namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class WebSocketStatusMessage
    {
        public string @event { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public object success { get; set; }
        public object fails { get; set; }
    }
}
