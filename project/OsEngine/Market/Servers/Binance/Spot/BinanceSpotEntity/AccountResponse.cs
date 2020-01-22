using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class Balance
    {
        public string asset { get; set; }
        public string free { get; set; }
        public string locked { get; set; }
    }

    public class AccountResponse
    {
        public int makerCommission { get; set; }
        public int takerCommission { get; set; }
        public int buyerCommission { get; set; }
        public int sellerCommission { get; set; }
        public bool canTrade { get; set; }
        public bool canWithdraw { get; set; }
        public bool canDeposit { get; set; }
        public long updateTime { get; set; }
        public List<Balance> balances { get; set; }
    }
}