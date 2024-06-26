

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class ResendRequestMessage: AFIXMessageBody
    {
        public long BeginSeqNo;
        public long EndSeqNo = 0; // 0 - infinity, i.e. all messages

        public override string ToString()
        {
            return $"7={BeginSeqNo}\u000116={EndSeqNo}\u0001";
        }        
    }
}
