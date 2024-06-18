
namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class LogonMessage: AFIXMessageBody
    {
        public int EncryptMethod;
        public int HeartBtInt;
        public bool ResetSeqNumFlag;
        public string Password;
        public string NewPassword = "";
        public string LanguageID = "R";

        public override string ToString()
        {
            string reset = ResetSeqNumFlag ? "Y" : "N";
            string newpassword = NewPassword == "" ? "" : $"925={NewPassword}\u0001";
            return $"98={EncryptMethod}\u0001108={HeartBtInt}\u0001141={reset}\u0001554={Password}\u0001{newpassword}6939={LanguageID}\u0001";
        }        
    }
}
