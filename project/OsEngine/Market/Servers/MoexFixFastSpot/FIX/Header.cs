using System;


namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class Header : AFIXHeader
    {        
        public Header()
        {
            BeginString = "FIX.4.4"; //Версия FIX "FIX.4.4",
        }
      
        public override string GetHalfMessage()
        {
            string sendingTime = SendingTime.ToString("yyyyMMdd-HH:mm:ss.fff");
            return $"35={MsgType}\u000134={MsgSeqNum}\u000149={SenderCompID}\u000152={sendingTime}\u000156={TargetCompID}\u0001";
        }        
    }
}
