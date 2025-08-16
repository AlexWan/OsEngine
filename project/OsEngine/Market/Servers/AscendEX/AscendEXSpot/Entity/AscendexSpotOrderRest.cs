using System.Collections.Generic;

namespace OsEngine.Market.Servers.AscendexSpot.Entity
{

    public class AscendexSpotOrderResponse
    {
        public string code { get; set; }
        public AscendexSpotQueryOrderResponse data { get; set; }
    }

    public class AscendexSpotQueryOrderResponse
    {
        public string accountId { get; set; }                  // Account ID
        public string ac { get; set; }                         // Account type
        public string action { get; set; }                     // Action (e.g. place-order)/(cancel-order)/(cancel-all)
        public string status { get; set; }                     // Request status (e.g. ACCEPT)
        public AscendexSpotOrderInfo info { get; set; }        //Detailed order info
    }

    public class AscendexSpotOrderInfo
    {
        public string avgPx { get; set; }            // Average price
        public string cumFee { get; set; }           // Cumulative fee
        public string cumFilledQty { get; set; }     // Cumulative filled quantity
        public string errorCode { get; set; }        // Error code
        public string feeAsset { get; set; }         // Fee asset
        public string id { get; set; }
        public string lastExecTime { get; set; }     // Last execution time
        public string orderId { get; set; }          // Order ID
        public string orderQty { get; set; }         // Order quantity
        public string orderType { get; set; }        // Order type
        public string price { get; set; }            // Price
        public string seqNum { get; set; }           // Sequence number
        public string side { get; set; }             // Order side (Buy/Sell)
        public string status { get; set; }           // Order status
        public string stopPrice { get; set; }        // Stop price
        public string symbol { get; set; }           // Trading pair
        public string execInst { get; set; }         // Execution instruction
        public string fillQty { get; set; }
        public string fee { get; set; }
        public string createTime { get; set; }
    }

    public class AscendexQueryOrderResponse//(byId)
    {
        public string code { get; set; }
        public string accountCategory { get; set; }
        public string accountId { get; set; }
        public AscendexSpotOrderInfo data { get; set; }
    }

    public class AscendexSpotOpenOrdersResponse
    {
        public string ac { get; set; }              // AccountCategory 
        public string accountId { get; set; }       // AccountId
        public string code { get; set; }            // Response code
        public List<AscendexSpotOrderInfo> data { get; set; }
    }

    public class AscendexSpotCancelOrderResponse
    {
        public string code { get; set; }
        public string accountId { get; set; }
        public string ac { get; set; }
        public string action { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string reason { get; set; }
        public AscendexSpotCancelOrderData data { get; set; }
    }

    public class AscendexSpotCancelOrderData
    {
        public string accountId { get; set; }
        public string ac { get; set; }
        public string action { get; set; }
        public string status { get; set; }
        public AscendexSpotCancelOrderInfo info { get; set; }
    }

    public class AscendexSpotCancelOrderInfo
    {
        public string id { get; set; }
        public string orderId { get; set; }
        public string orderType { get; set; }
        public string symbol { get; set; }
        public string timestamp { get; set; }
    }
}
