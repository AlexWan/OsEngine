using System.Collections.Generic;

namespace OsEngine.Market.Servers.Lmax.LmaxEntity
{
    /// <summary>
    /// словарь ошибок
    /// </summary>
    public class RejectReasons
    {
        public Dictionary<string, string> OrdRejReason;

        public Dictionary<string, string> CxlRejReason;

        public RejectReasons()
        {
            OrdRejReason = new Dictionary<string, string>
            {
                { "0", "Broker / Exchange option" },
                { "1", "Unknown symbol" },
                { "2", "Exchange closed" },
                { "3", "Order exceeds limit" },
                { "4", "Too late to enter" },
                { "5", "Unknown Order" },
                { "6", "Duplicate Order (e.g. duplicate ClOrdID ())" },
                { "7", "Duplicate of a verbally communicated order" },
                { "8", "Stale Order" },
                { "9", "Trade Along required" },
                { "10", "Invalid Investor ID" },
                { "11", "Unsupported order characteristic" },
                { "12", "Surveillence Option" },
                { "13", "Incorrect quantity" },
                { "14", "Incorrect allocated quantity" },
                { "15", "Unknown account(s)" },
                { "99", "Other" }
            };


            CxlRejReason = new Dictionary<string, string>
            {
                { "1", "Unknown order" },
                { "2", "Broker / Exchange Option" },
                { "6", "Duplicate ClOrdID(11) received (only for Order Cancel Replace Request)" }
            };
        }
    }
}
