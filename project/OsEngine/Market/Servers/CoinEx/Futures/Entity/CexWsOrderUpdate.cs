namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    // https://docs.coinex.com/api/v2/futures/order/ws/user-order#user-order-update-push

    struct CexWsOrderUpdate
    {
        // My trade

        // Order update event type [ put | update | modify | finish ]
        public string @event { get; set; }

        public CexOrderUpdate order { get; set; }
    }

}