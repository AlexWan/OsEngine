using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class AccountResponseMargin
    {
        public string borrowEnabled;

        public string marginLevel;

        public string totalAssetOfBtc;

        public string totalLiabilityOfBtc;

        public string totalNetAssetOfBtc;

        public string tradeEnabled;

        public string transferEnabled;

        public List<UserAssets> userAssets;
    }


    public class UserAssets
    {
        public string asset;

        public string borrowed;

        public string free;

        public string interest;

        public string locked;

        public string netAsset;

    }

}