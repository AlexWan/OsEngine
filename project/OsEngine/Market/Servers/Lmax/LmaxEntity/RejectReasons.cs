using System.Collections.Generic;

namespace OsEngine.Market.Servers.Lmax.LmaxEntity
{
    /// <summary>
	/// dictionary of error
    /// словарь ошибок
    /// </summary>
    public class RejectReasons
    {
        public Dictionary<string, string> OrdRejReason;

        public Dictionary<string, string> CxlRejReason;

        public RejectReasons()
        {
            OrdRejReason = new Dictionary<string, string>();

            OrdRejReason.Add("0", "Broker / Exchange option");
            OrdRejReason.Add("1", "Unknown symbol");
            OrdRejReason.Add("2", "Exchange closed");
            OrdRejReason.Add("3", "Order exceeds limit");
            OrdRejReason.Add("4", "Too late to enter");
            OrdRejReason.Add("5", "Unknown Order");
            OrdRejReason.Add("6", "Duplicate Order (e.g. duplicate ClOrdID ())");
            OrdRejReason.Add("7", "Duplicate of a verbally communicated order");
            OrdRejReason.Add("8", "Stale Order");
            OrdRejReason.Add("9", "Trade Along required");
            OrdRejReason.Add("10", "Invalid Investor ID");
            OrdRejReason.Add("11", "Unsupported order characteristic");
            OrdRejReason.Add("12", "Surveillence Option");
            OrdRejReason.Add("13", "Incorrect quantity");
            OrdRejReason.Add("14", "Incorrect allocated quantity");
            OrdRejReason.Add("15", "Unknown account(s)");
            OrdRejReason.Add("99", "Other");


            CxlRejReason = new Dictionary<string, string>();

            CxlRejReason.Add("1", "Unknown order");
            CxlRejReason.Add("2", "Broker / Exchange Option");
            CxlRejReason.Add("6", "Duplicate ClOrdID(11) received (only for Order Cancel Replace Request)");
        }
    }
}
