using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class MarketDataRequestMessage: AFIXMessageBody
    {
        public string ApplID = "OLR"; //"TLR";
        public string ApplBegSeqNum;
        public string ApplEndSeqNum;
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("1180=").Append(ApplID).Append('\u0001');
            sb.Append("1182=").Append(ApplBegSeqNum).Append('\u0001');
            sb.Append("1183=").Append(ApplEndSeqNum).Append('\u0001');

            return sb.ToString();
        }
    }
}
