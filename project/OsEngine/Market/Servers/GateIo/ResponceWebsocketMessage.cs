namespace OsEngine.Market.Servers.GateIo
{
    public class ResponceWebsocketMessage<T>
    {
        public string time;
        public string time_ms;
        public string channel;
        public string Event;
        public T result;
    }
}
