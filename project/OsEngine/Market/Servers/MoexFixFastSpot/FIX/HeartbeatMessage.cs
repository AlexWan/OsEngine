

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class HeartbeatMessage: AFIXMessageBody
    {
        public string TestReqID { get; set; }

        public override string ToString()
        {
            return $"112={TestReqID}\u0001";
        }        
    }
}
