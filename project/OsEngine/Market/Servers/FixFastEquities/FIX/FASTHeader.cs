using System;


namespace OsEngine.Market.Servers.FixFastEquities.FIX
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
    class FASTHeader
    {
        public string BeginString = "FIXT.1.1";
        public int BodyLength;
        public string MsgType;
        public string AppVerID = "9"; // FIX50SP2
        public string SenderCompID = "OsEngine";
        public string TargetCompID = "MOEX"; // вообще сюда надо отправлять идентификатор фирмы
        public int MsgSeqNum;
        public DateTime SendingTime;
        public string MessageEncoding = "UTF-8";

        public FASTHeader()
        {
            SendingTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"8={BeginString}\u00019={BodyLength}\u0001" + GetHalfMessage();
        }

        public string GetHalfMessage()
        {
            string sendingTime = SendingTime.ToString("yyMMddHHmmssffffff"); // yyMMDDHHmmSSuuuuuu
            //string sendingTime = SendingTime.ToString("yyyyMMdd-HH:mm:ss"); // yyMMDDHHmmSSuuuuuu
            return $"35={MsgType}\u00011128={AppVerID}\u000149={SenderCompID}\u000156={TargetCompID}\u000134={MsgSeqNum}\u000152={sendingTime}\u0001";
        }

        public int GetHeaderSize()
        {
            string tmpString = GetHalfMessage();
            return tmpString.Length;
        }
    }
}
