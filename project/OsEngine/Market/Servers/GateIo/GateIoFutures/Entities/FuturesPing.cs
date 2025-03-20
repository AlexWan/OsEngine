
namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public class FuturesPing
    {
        public long time { get; set; }
        public string channel { get; set; }
    }

    public class FuturesPong
    {
        public string time { get; set; }
        public string channel { get; set; }
        public string @event { get; set; }
        public string error { get; set; }
        public string result { get; set; }
    }
}
