using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
