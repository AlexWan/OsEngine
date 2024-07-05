
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class MarketDataRequestMessage: AFIXMessageBody
    {
        public string ApplID = "OLR"; //"TLR";
        public string ApplBegSeqNum;
        public string ApplEndSeqNum;
        
        public override string ToString()
        {
            return $"1180={ApplID}\u00011182={ApplBegSeqNum}\u00011183={ApplEndSeqNum}\u0001";
        }
    }
}
