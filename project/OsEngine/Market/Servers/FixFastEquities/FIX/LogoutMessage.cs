
namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class LogoutMessage
    {        
        public string Text = " ";

        public override string ToString()
        {
            return $"58={Text}\u0001";
        }

        public int GetMessageSize()
        {
            return ToString().Length;
        }
    }
}
