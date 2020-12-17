using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Futures.Entity
{
    public class AccountResponseFutures
    {
        public List<AssetFutures> assets;

        public List<PositionFutures> positions;

        public string canDeposit;
        public string canTrade;
        public string canWithdraw;
        public string feeTier;
        public string maxWithdrawAmount;
        public string totalInitialMargin;
        public string totalMaintMargin;
        public string totalMarginBalance;
        public string totalOpenOrderInitialMargin;
        public string totalPositionInitialMargin;
        public string totalUnrealizedProfit;
        public string totalWalletBalance;
        public string updateTime;

    }

    public class PositionFutures
    {
        public string isolated;
        public string leverage;
        public string initialMargin;
        public string maintMargin;
        public string openOrderInitialMargin;
        public string positionInitialMargin;
        public string symbol;
        public string unrealizedProfit;
    }

    public class AssetFutures
    {
        public string asset;
        public string initialMargin;
        public string maintMargin;
        public string marginBalance;
        public string maxWithdrawAmount;
        public string openOrderInitialMargin;
        public string positionInitialMargin;
        public string unrealizedProfit;
        public string walletBalance;
    }


    public class AccountResponseFuturesFromWebSocket
    {
        public string e; //": "ACCOUNT_UPDATE",                // Event Type
        public string E; //": 1564745798939,                   // Event Time
        public string T; //": 1564745798938 ,                  // Transaction

        public AccountСontainer a;
    }

    public class AccountСontainer
    {
        public List<BalancesСontainer> B;

        public List<PositionContainer> P;
    }

    public class BalancesСontainer
    {
        public string a; //":"USDT",                   // Asset
        public string wb; //":"122624.12345678",       // Wallet Balance
        public string cw; //":"100.12345678"           // Cross Wallet Balance
    }

    public class PositionContainer
    {
        public string s; //":"BTCUSDT",            // Symbol
        public string pa; //":"1",                 // Position Amount
        public string ep; //":"9000",              // Entry Price
        public string cr; //":"200",               // (Pre-fee) Accumulated Realized
        public string up; //":"0.2732781800",      // Unrealized PnL
        public string mt; //":"isolated",              // Margin Type
        public string iw; //":"0.06391979"         // Isolated Wallet (if isolated position)
    }
    public class HedgeModeResponse
    {
        public bool dualSidePosition;
    }
}