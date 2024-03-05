using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Pionex.Entity
{
    public class SendNewOrder
    {
        public string symbol;
        public string side;
        public string type;
        public string clientOrderId;
        public string size;            // Quantity. Required in limit order and market sell order.
        public string price;           // Required in limit order.
        public string amount;          // Buying amount. Required in market buy order.
        public bool IOC;               // Immediate or Cancel (IOC) Order
    }
}
