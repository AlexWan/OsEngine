using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    /*
     * <string name="MessageType" id="35"><constant value="A" /></string>
		<string name="BeginString" id="8"><constant value="FIXT.1.1"/></string>
		<string name="SenderCompID" id="49"><constant value="MOEX"/></string>
		<string name="TargetCompID" id="56"></string>
		<uInt32 name="MsgSeqNum" id="34"></uInt32>
		<uInt64 name="SendingTime" id="52"></uInt64>
		<int32 name="HeartBtInt" id="108"></int32>
		<string name="Username" id="553" presence="optional"></string>
		<string name="Password" id="554" presence="optional"></string>
		<string name="DefaultApplVerID" id="1137"></string>
     */
    class FASTHeader : AFIXHeader
    {
        public string AppVerID = "9"; // FIX50SP2
        public string MessageEncoding = "UTF-8";

        public FASTHeader()
        {
            BeginString = "FIXT.1.1";
            SenderCompID = "OsEngine";
            TargetCompID = "MOEX"; // вообще сюда надо отправлять идентификатор фирмы
        }

        public override string GetHalfMessage()
        {
            string sendingTime = SendingTime.ToString("yyMMddHHmmssffffff"); // yyMMDDHHmmSSuuuuuu

            StringBuilder sb = new StringBuilder();

            sb.Append("35=").Append(MsgType).Append('\u0001');
            sb.Append("1128=").Append(AppVerID).Append('\u0001');
            sb.Append("49=").Append(SenderCompID).Append('\u0001');
            sb.Append("56=").Append(TargetCompID).Append('\u0001');
            sb.Append("34=").Append(MsgSeqNum).Append('\u0001');
            sb.Append("52=").Append(sendingTime).Append('\u0001');

            return sb.ToString();
        }
    }
}
