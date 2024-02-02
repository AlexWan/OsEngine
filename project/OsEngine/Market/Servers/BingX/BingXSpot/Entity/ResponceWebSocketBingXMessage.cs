using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace OsEngine.Market.Servers.BingX.BingXSpot.Entity
{
    public class ResponceWebSocketBingXMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string id { get; set; }
        public string timestamp { get; set; }
        public T data { get; set; }
        public string dataType { get; set; }
        public string success { get; set; }
    }

    public class SubscriptionOrderUpdateData
    {
        public string e { get; set; } //Event Type
        public string E { get; set; } // event time
        public string s { get; set; } // trading pair
        public string S { get; set; } // Order direction
        public string o { get; set; } // order type
        public string q { get; set; } // Order original quantity
        public string p { get; set; } // Original order price
        public string x { get; set; } // Event Type
        public string X { get; set; } // order status
        public string i { get; set; } // Order ID
        public string l { get; set; } // Last order transaction volume
        public string z { get; set; } // Accumulated transaction volume of orders
        public string L { get; set; } // Last transaction price of the order
        public string n { get; set; } // Number of handling fees
        public string N { get; set; } // Handling fee asset category
        public string T { get; set; } // transaction time
        public string t { get; set; } // Transaction ID
        public string O { get; set; } // Order creation time
        public string Z { get; set; } // Accumulated transaction amount of orders
        public string Y { get; set; } // Last transaction amount of the order
        public string Q { get; set; } // Original order amount
        public string m { get; set; }
        public string C { get; set; } // Client ID
    }

    public class MarketDepthDataMessage
    {
        public List<string[]> bids;
        public List<string[]> asks;
    }
    public class SubscriptionAccountBalancePush
    {
        public string e; // Event Type
        public string E; // event time
        public string T; //Matching time
        public AccountСontainer a; // Asset Name
    }

    public class AccountСontainer
    {
        public List<BalancesСontainer> B;
        public string m; //event launch reason. includes the following possible types:
                         // DEPOSIT
                         // WITHDRAW
                         // ORDER
                         // FUNDING_FEE
                         // WITHDRAW_REJECT
                         // ADJUSTMENT
                         // INSURANCE_CLEAR
                         // ADMIN_DEPOSIT
                         // ADMIN_WITHDRAW
                         // MARGIN_TRANSFER
                         // MARGIN_TYPE_CHANGE
                         // ASSET_TRANSFER
                         // OPTIONS_PREMIUM_FEE
                         // OPTIONS_SETTLE_PROFIT
                         // AUTO_EXCHANGE
    }

    public class BalancesСontainer
    {
        public string a;  // Asset Name
        public string bc; // The amount of change in the asset account in this transaction
        public string wb; // Wallet Balance
        public string cw; // Cross Wallet Balance
    }

    public class ResponseWebSocketTrade
    {
        public string e { get; set; } // Event Type
        public string E { get; set; } // event time
        public string s { get; set; } // trading pair
        public string t { get; set; } // Transaction ID
        public string p { get; set; } // transaction price
        public string q { get; set; } // Executed quantity
        public string T { get; set; } // transaction time
        public string m { get; set; } // Whether the buyer is a market maker. If true, this transaction is an active sell order, otherwise it is an active buy order
    }

    public class ResponseWebSocketPing
    {
        public string ping { get; set; }
        public string time { get; set; }
    }
}
