using System.Collections.Generic;

namespace OsEngine.Market.Servers.BingX.BingXFutures.Entity
{
    public class ResponseFuturesBingX<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public List<T> data { get; set; }
    }

    public class ResponseFuturesBingXMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public T data { get; set; }
    }

    public class JsonErrorResponse
    {
        public string code { get; set; }
        public string msg { get; set; }
        public Dictionary<string, object> data { get; set; }
    }

    public class PositionData
    {
        public string PositionId { get; set; }
        public string Symbol { get; set; }
        public string Currency { get; set; }
        public string PositionAmt { get; set; }
        public string AvailableAmt { get; set; }
        public string PositionSide { get; set; }
        public string Isolated { get; set; }
        public string AvgPrice { get; set; }
        public string InitialMargin { get; set; }
        public string Leverage { get; set; }
        public string UnrealizedProfit { get; set; }
        public string RealisedProfit { get; set; }
        public string LiquidationPrice { get; set; }
        public string PnlRatio { get; set; }
        public string MaxMarginReduction { get; set; }
        public string RiskRate { get; set; }
        public string MarkPrice { get; set; }
        public string PositionValue { get; set; }
        public string OnlyOnePosition { get; set; }
    }

    public class CandlestickChartDataFutures
    {
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
        public string time { get; set; }
    }

    public class BingXFuturesSymbols
    {
        public string contractId { get; set; } // contract ID
        public string symbol { get; set; } // trading pair, for example: BTC-USDT
        public string size { get; set; } // contract size, such as 0.0001 BTC
        public string quantityPrecision { get; set; } // transaction quantity precision
        public string pricePrecision { get; set; } // price precision
        public string feeRate { get; set; } // transaction fee
        public string tradeMinLimit { get; set; } // The smallest trading unit
        public string maxLongLeverage { get; set; }
        public string maxShortLeverage { get; set; }
        public string currency { get; set; } // settlement and margin currency asset
        public string asset { get; set; } // contract trading asset
        public string status { get; set; } // 0 offline, 1 online
        public string apiStateOpen { get; set; } // Whether the API can open a position
        public string apiStateClose { get; set; } // Whether API can close positions
    }

    public class BalanceInfoBingXFutures
    {
        public string userId { get; set; } // user ID
        public string asset { get; set; } // user asset
        public string balance { get; set; } // asset balance
        public string equity { get; set; } // net asset value
        public string unrealizedProfit { get; set; } // unrealized profit and loss
        public string realisedProfit { get; set; } // realized profit and loss
        public string availableMargin { get; set; } // available margin
        public string usedMargin { get; set; } // used margin
        public string freezedMargin { get; set; } // frozen margin
    }

    public class OrderDetails
    {
        public string orderId { get; set; } // Order ID
        public string symbol { get; set; } // trading pair, for example: BTC-USDT
        public string positionSide { get; set; } // Position direction, required for single position as BOTH,
                                                 // for both long and short positions only LONG or SHORT can be chosen, defaults to LONG if empty
        public string side { get; set; } // buying and selling direction | BUY or SELL
        public string type { get; set; } // LIMIT: Limit Order / MARKET: Market Order / STOP_MARKET: Stop Market Order 
                                         // TAKE_PROFIT_MARKET: Take Profit Market Order / STOP: Stop Limit Order 
                                         // TAKE_PROFIT: Take Profit Limit Order / TRIGGER_LIMIT: Stop Limit Order with Trigger 
                                         // TRIGGER_MARKET: Stop Market Order with Trigger / TRAILING_STOP_MARKET: Trailing Stop Market Order
        public string price { get; set; }
        public string quantity { get; set; } // Original quantity, only support units by COIN ,Ordering with quantity U is not currently supported.
        public string stopPrice { get; set; }
        public string workingType { get; set; } // StopPrice trigger price types: MARK_PRICE, CONTRACT_PRICE, INDEX_PRICE, default MARK_PRICE
        public string clientOrderId { get; set; } // Customized order ID for users
        public string timeInForce { get; set; }
        public string priceRate { get; set; }
        public StopLoss stopLoss { get; set; }
        public TakeProfit takeProfit { get; set; }
        public string reduceOnly { get; set; }
    }

    public class StopLoss
    {
        public string type { get; set; }
        public string quantity { get; set; }
        public string stopPrice { get; set; }
        public string price { get; set; }
        public string workingType { get; set; }
    }

    public class TakeProfit
    {
        public string type { get; set; }
        public string quantity { get; set; }
        public string stopPrice { get; set; }
        public string price { get; set; }
        public string workingType { get; set; }
    }

    public class OrderData
    {
        public OrderDetails order { get; set; }
    }

    public class Balance
    {
        public BalanceInfoBingXFutures balance { get; set; }
    }


}
