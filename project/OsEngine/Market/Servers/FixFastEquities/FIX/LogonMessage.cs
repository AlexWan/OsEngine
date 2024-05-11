
namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class LogonMessage
    {
        public int EncryptMethod { get; set; }
        public int HeartBtInt { get; set; }
        public bool ResetSeqNumFlag { get; set; }

        public string Password { get; set; }

        public string LanguageID = "R";

        public override string ToString()
        {
            string reset = ResetSeqNumFlag ? "Y" : "N";
            return $"98={EncryptMethod}\u0001108={HeartBtInt}\u0001141={reset}\u0001554={Password}\u00016939={LanguageID}\u0001";
        }

        public int GetMessageSize()
        {
            return ToString().Length;
        }
    }
}
