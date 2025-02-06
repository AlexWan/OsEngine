using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum
     * 
     * TODO PortfolioSubscribe
     * TODO securites list and batch subscribe
     */

    public sealed class CexWsOperation
    {

        private readonly String name;
        private readonly int value;

        // https://docs.coinex.com/api/v2/common/ws/sign
        public static readonly CexWsOperation SIGN = new CexWsOperation(1, "server.sign");
        public static readonly CexWsOperation PING = new CexWsOperation(1, "server.ping");
        public static readonly CexWsOperation TIME = new CexWsOperation(1, "server.time");
        public static readonly CexWsOperation ORDER_SUBSCRIBE = new CexWsOperation(1, "order.subscribe");
        public static readonly CexWsOperation ORDER_UNSUBSCRIBE = new CexWsOperation(1, "order.unsubscribe");
        public static readonly CexWsOperation BALANCE_SUBSCRIBE = new CexWsOperation(1, "balance.subscribe");
        public static readonly CexWsOperation BALANCE_UNSUBSCRIBE = new CexWsOperation(1, "balance.unsubscribe");
        public static readonly CexWsOperation DEALS_SUBSCRIBE = new CexWsOperation(1, "deals.subscribe");
        public static readonly CexWsOperation DEALS_UNSUBSCRIBE = new CexWsOperation(1, "deals.unsubscribe");
        public static readonly CexWsOperation USER_DEALS_SUBSCRIBE = new CexWsOperation(1, "user_deals.subscribe");
        public static readonly CexWsOperation USER_DEALS_UNSUBSCRIBE = new CexWsOperation(1, "user_deals.unsubscribe");
        public static readonly CexWsOperation MARKET_DEPTH_SUBSCRIBE = new CexWsOperation(1, "depth.subscribe");
        public static readonly CexWsOperation MARKET_DEPTH_UNSUBSCRIBE = new CexWsOperation(1, "depth.unsubscribe");

        private CexWsOperation(int value, String name)
        {
            this.name = name;
            this.value = value;
        }

        public override String ToString()
        {
            return name;
        }

    }
}
