
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class LogoutMessage: AFIXMessageBody
    {        
        public string Text = " ";

        public override string ToString()
        {
            return $"58={Text}\u0001";
        }
    }
}
