using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    struct CexWsTransactionUpdate
    {
        public string market { get; set; }

        public List<CexTransactionItem> deal_list { get; set; }
    }

    // Spot and web socket both
    struct CexTransactionItem
    {
        // Deal id [3514376759]
        public long deal_id { get; set; }

        // Taker side, "buy" or "sell"
        public string side { get; set; }

        // Transaction timestamp (milliseconds)
        public long created_at { get; set; }

        // Filled price [30718.42]
        public string price { get; set; }

        // Executed Amount [0.00015729]
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string amount { get; set; }

        public static explicit operator Trade(CexTransactionItem cexTrade) {
            Trade trade = new Trade();
            //trade.SecurityNameCode = cexTrade.Market;
            trade.Price = cexTrade.price.ToString().ToDecimal();
            //trade.Time = CoinExServerRealization.ConvertToDateTimeFromUnixFromMilliseconds(cexTrade.created_at);
            trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(cexTrade.created_at);
            //trade.Id = quotes.s_t.ToString() + quotes.side + quotes.symbol;
            trade.Id = cexTrade.deal_id.ToString();

            trade.Side = (cexTrade.side == CexOrderSide.BUY.ToString()) ? Side.Buy : trade.Side = Side.Sell;

            trade.Volume = cexTrade.amount.ToDecimal();

            return trade;
        }
    }

}
