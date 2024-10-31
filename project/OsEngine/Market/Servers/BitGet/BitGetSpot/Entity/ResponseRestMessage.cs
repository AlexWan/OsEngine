﻿using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code;
        public string msg;
        public string requestTime;
        public T data;
    }

    public class RestMessageCandle
    {
        public List<List<string>> data { get; set; }
    }

    public class RestMessageSymbol
    {
        public string symbol;
        public string quantityPrecision;
        public string pricePrecision;
        public string status;
    }

    public class RestMessageAccount
    {
        public string coin;
        public string frozen;
        public string available;
    }

    public class RestMessagePositions
    {
        public string marginCoin;
        public string symbol;
        public string holdSide;
        public string openDelegateCount;
        public string margin;
        public string available;
        public string locked;
        public string total;
        public string leverage;
        public string achievedProfits;
        public string averageOpenPrice;
        public string marginMode;
        public string holdMode;
        public string unrealizedPL;
        public string liquidationPrice;
        public string keepMarginRate;
        public string marketPrice;
        public string cTime;
    }

    public class RestMessageOrders
    {
        public string symbol;
        public string orderId;
        public string clientOid;
        public string priceAvg;
        public string status;
        public string side;
        public string orderType;
        public string leverage;
        public string size;
        public string cTime;
        public string price;
    }
  
    public class DataOrderStatus
    {
        public string symbol;
        public string size;
        public string orderId;
        public string clientOid;
        public string price;
        public string status;
        public string side;
        public string orderType;
        public string cTime;
    }

    public class RestMyTradesResponce
    {
        public string code;

        public string msg;

        public List<DataMyTrades> data;
    }

    public class DataMyTrades
    {
        public string tradeId; 
        public string symbol; 
        public string orderId; 
        public string price; 
        public string size; 
        public FeeDetail feeDetail; 
        public string side;
        public string cTime;
        public string priceAvg;
    }

    public class FeeDetail
    {
        public string feeCoin;
        public string totalFee;
    }
}