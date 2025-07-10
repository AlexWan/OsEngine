namespace OsEngine.Market.Servers.GateIo
{
    public class ResponseWebsocketMessage<T>
    {
        public string time;
        public string time_ms;
        public string channel;
        public string Event;
        public string error;
        public T result;
    }
}
