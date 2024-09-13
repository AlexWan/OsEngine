
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    internal abstract class AFIXMessageBody
    {       
        public int GetMessageSize()
        {
            return ToString().Length;
        }
    }
}
