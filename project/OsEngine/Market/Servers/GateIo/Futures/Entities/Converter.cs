using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    public static class Converter
    {
        public static decimal StringToDecimal(string value)
        {
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return Convert.ToDecimal(value.Replace(",", sep).Replace(".", sep));
        }
    }
}
