using System.Collections.Generic;

namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json
{
    public class ResponseWebSocketBulletPrivate
    {
        public string endpoint;
        public string rotocol;
        public string encrypt;
        public string pingInterval;
        public string pingTimeout;
        public string token;
    }
    public class ResponseWebSocketMessageAction<T>
    {
        public string type;
        public string topic;
        public string subject;
        
        public T data;
    }

    public class ResponseWebSocketMessageTrade
    {
        public string symbol;       //Market of the symbol
        public string sequence;     //Sequence number which is used to judge the continuity of the pushed messages
        public string side;         //Transaction side of the last traded taker order
        public string price;        //Filled price
        public string size;         //Filled quantity
        public string tradeId;      //Order ID
        public string bestBidSize;  //Best bid size
        public string bestBidPrice; //Best bid
        public string bestAskPrice; //Best ask size
        public string bestAskSize;  //Best ask
        public string ts;           //Filled time - nanosecond
    }
    
    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;
        
        public string timestamp;
    }

    public class ResponseWebSocketOrder
    {
        // https://www.kucoin.com/docs/websocket/futures-trading/private-channels/trade-orders
        public string orderId; //"5cdfc138b21023a909e5ad55", // Order ID
        public string symbol; //"XBTUSDM", // symbol
        public string type; //"match", // Message Type: "open", "match", "filled", "canceled", "update"
        public string status; //"open", // Order Status: "match", "open", "done"
        public string matchSize; //"", // Match Size (when the type is "match")
        public string matchPrice; //"", // Match Price (when the type is "match")
        public string orderType; //"limit", // Order Type, "market" indicates market order, "limit" indicates limit order
        public string side; //"buy", // Trading direction,include buy and sell
        public string price; //"3600", // Order Price
        public string size; //"20000", // Order Size
        public string remainSize; //"20001", // Remaining Size for Trading
        public string filledSize; //"20000", // Filled Size
        public string canceledSize; //"0", // In the update message, the Size of order reduced
        public string tradeId; //"5ce24c16b210233c36eexxxx", // Trade ID (when the type is "match")
        public string clientOid; //"5ce24c16b210233c36ee321d", // clientOid
        public string orderTime; //1545914149935808589, // Order Time
        public string oldSize ; //"15000", // Size Before Update (when the type is "update")
        public string liquidity; //"maker", // Trading direction, buy or sell in taker
        public string ts; //1545914149935808589 // Timestamp（match engine）
    }


   
    public class ResponseWebSocketPosition
    {
        // https://www.kucoin.com/docs/websocket/futures-trading/private-channels/position-change-events
        public string realisedGrossPnl; //0e-8, //Accumulated realised profit and loss
        public string symbol; //public string XBTUSDM", //Symbol
        public string crossMode; //false, //Cross mode or not
        public string liquidationPrice; //1000000.0, //Liquidation price
        public string posLoss; //0e-8, //Manually added margin amount
        public string avgEntryPrice; //7508.22, //Average entry price
        public string unrealisedPnl; //-0.00014735, //Unrealised profit and loss
        public string markPrice; //7947.83, //Mark price
        public string posMargin; //0.00266779, //Position margin
        public string autoDeposit; //false, //Auto deposit margin or not
        public string riskLimit; //100000, //Risk limit
        public string unrealisedCost; //0.00266375, //Unrealised value
        public string posComm; //0.00000392, //Bankruptcy cost
        public string posMaint; //0.00001724, //Maintenance margin
        public string posCost; //0.00266375, //Position value
        public string maintMarginReq; //0.005, //Maintenance margin rate
        public string bankruptPrice; //1000000.0, //Bankruptcy price
        public string realisedCost; //0.00000271, //Currently accumulated realised position value
        public string markValue; //0.0025164, //Mark value
        public string posInit; //0.00266375, //Position margin
        public string realisedPnl; //-0.00000253, //Realised profit and losts
        public string maintMargin; //0.00252044, //Position margin
        public string realLeverage; //1.06, //Leverage of the order
        public string changeReason; //"positionChange", //changeReason:marginChange、positionChange、liquidation、autoAppendMarginStatusChange、adl
        public string currentCost; //0.00266375, //Current position value
        public string openingTimestamp; //1558433191000, //Open time
        public string currentQty; //-20, //Current position
        public string delevPercentage; //0.52, //ADL ranking percentile
        public string currentComm; //0.00000271, //Current commission
        public string realisedGrossCost; //0e-8, //Accumulated reliased gross profit value
        public string isOpen; //true, //Opened position or not
        public string posCross; //1.2e-7, //Manually added margin
        public string currentTimestamp; //1558506060394, //Current timestamp
        public string unrealisedRoePcnt; //-0.0553, //Rate of return on investment
        public string unrealisedPnlPcnt; //-0.0553, //Position profit and loss ratio
        public string settleCurrency; //"XBT" //Currency used to clear and settle the trades
    }

    public class ResponseWebSocketPortfolio
    {
        // https://www.kucoin.com/docs/websocket/futures-trading/private-channels/account-balance-events
        public string availableBalance; // 5923, //Current available amount
        public string holdBalance; // 2312, //Frozen amount = positionMargin + orderMargin + frozenFunds
        public string currency; // "USDT", //Currency
        public string timestamp; // 1553842862614
    }
}
