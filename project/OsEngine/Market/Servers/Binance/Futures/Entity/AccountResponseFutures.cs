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
        public string symbol;
        public string initialMargin;
        public string maintMargin;
        public string unrealizedProfit;
        public string positionInitialMargin;
        public string openOrderInitialMargin;
        public string leverage;
        public string isolated;
        public string entryPrice;
        public string maxNotional;
        public string positionSide;
        public string positionAmt;
        public string updateTime;
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

    public class AssetFuturesCoinM
    {
        public string asset;
        public string balance;
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

    public class PremiumIndex
    {
        public string symbol;                       //"BTCUSDT",
        public string markPrice;                    //"11793.63104562",  // mark price
        public string indexPrice;                   //"11781.80495970", // index price
        public string lastFundingRate;              //"0.00038246",  // This is the lasted funding rate
        public string nextFundingTime;              //1597392000000,
        public string interestRate;                 //0.00010000",
    }

    public class PriceTicker
    {
        public string symbol;                       // "BTCUSDT",
        public string price;                        //"6000.01",
        public string time;                         // 1589437530011   // Transaction time
    }
}