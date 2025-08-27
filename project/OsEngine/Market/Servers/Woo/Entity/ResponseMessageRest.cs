using System.Collections.Generic;

namespace OsEngine.Market.Servers.Woo.Entity
{
    public class ResponseMessageRest<T>
    {
        public string success { get; set; }
        public string timestamp { get; set; }
        public T data { get; set; }
    }

    public class ResponseListenKey
    {
        public string authKey { get; set; }
        public string expiredTime { get; set; }
    }

    public class ResponseSystemStatus
    {
        public string status { get; set; }
        public string msg { get; set; }
        public string estimatedEndTime { get; set; }
    }

    public class ResponseSecurities
    {
        public List<RowsSymbol> rows { get; set; }
    }

    public class RowsSymbol
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public string baseAssetMultiplier { get; set; }
        public string quoteAsset { get; set; }
        public string quoteMin { get; set; }
        public string quoteMax { get; set; }
        public string quoteTick { get; set; }
        public string baseMin { get; set; }
        public string baseMax { get; set; }
        public string baseTick { get; set; }
        public string minNotional { get; set; }
        public string bidCapRatio { get; set; }
        public string bidFloorRatio { get; set; }
        public string askCapRatio { get; set; }
        public string askFloorRatio { get; set; }
        public string fundingIntervalHours { get; set; }
        public string fundingCap { get; set; }
        public string fundingFloor { get; set; }
        public string orderMode { get; set; }
        public string baseIMR { get; set; }
        public string baseMMR { get; set; }
        public string isAllowedRpi { get; set; }
    }

    public class ResponseCommonPortfolio
    {
        public string applicationId { get; set; }
        public string account { get; set; }
        public string alias { get; set; }
        public string otpauth { get; set; }
        public string accountMode { get; set; }
        public string positionMode { get; set; }
        public string leverage { get; set; }
        public string marginRatio { get; set; }
        public string openMarginRatio { get; set; }
        public string initialMarginRatio { get; set; }
        public string maintenanceMarginRatio { get; set; }
        public string totalCollateral { get; set; }
        public string freeCollateral { get; set; }
        public string totalAccountValue { get; set; }
        public string totalTradingValue { get; set; }
        public string totalVaultValue { get; set; }
        public string totalStakingValue { get; set; }
        public string totalEarnValue { get; set; }
        public string totalLaunchpadValue { get; set; }
        public string referrerID { get; set; }
        public string accountType { get; set; }
    }

    public class ResponsePortfolios
    {
        public List<Holding> holding { get; set; }
    }

    public class Holding
    {
        public string token { get; set; }
        public string holding { get; set; }
        public string frozen { get; set; }
        public string staked { get; set; }
        public string unbonding { get; set; }
        public string vault { get; set; }
        public string interest { get; set; }
        public string earn { get; set; }
        public string pendingShortQty { get; set; }
        public string pendingLongQty { get; set; }
        public string availableBalance { get; set; }
        public string averageOpenPrice { get; set; }
        public string markPrice { get; set; }
        public string pnl24H { get; set; }
        public string fee24H { get; set; }
        public string timestamp { get; set; }
    }

    public class ResponseMessagePositions
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Positions> positions { get; set; }
        }
        public class Positions
        {
            public string symbol { get; set; }
            public string holding { get; set; }
        }
    }

    public class ResponseMessageCandles
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Rows> rows { get; set; }

        }

        public class Rows
        {
            public string open { get; set; }
            public string close { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string volume { get; set; }
            public string start_timestamp { get; set; }
        }


    }
}
