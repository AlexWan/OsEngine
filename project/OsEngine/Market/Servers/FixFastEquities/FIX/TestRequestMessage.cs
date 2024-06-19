

namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class TestRequestMessage: AFIXMessageBody
    {
        public string TestReqID;

        public override string ToString()
        {
            return $"112={TestReqID}\u0001";
        }        
    }
}
