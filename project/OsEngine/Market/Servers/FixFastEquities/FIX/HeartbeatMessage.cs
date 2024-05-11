

namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class HeartbeatMessage
    {
        public string TestReqID { get; set; }

        public override string ToString()
        {
            return $"112={TestReqID}\u0001";
        }

        public int GetMessageSize()
        {
            return ToString().Length;
        }
    }
}
