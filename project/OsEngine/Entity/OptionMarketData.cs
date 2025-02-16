using System;

namespace OsEngine.Entity
{
    public class OptionMarketData
    {       
        public decimal Delta;

        public decimal Vega;

        public decimal Gamma;

        public decimal Theta;

        public decimal Rho;

        public decimal MarkIV;

        public decimal MarkPrice;

        public string SecurityName;

        public DateTime TimeCreate;

        public decimal OpenInterest;
                
        public decimal BidIV;

        public decimal AskIV;

        public decimal UnderlyingPrice;

        public string UnderlyingAsset;
    }

    public class OptionMarketDataForConnector
    {
        public string Delta;

        public string Vega;

        public string Gamma;

        public string Theta;

        public string Rho;

        public string MarkIV;

        public string MarkPrice;

        public string SecurityName;

        public string TimeCreate;

        public string OpenInterest;

        public string BidIV;

        public string AskIV;

        public string UnderlyingPrice;

        public string UnderlyingAsset;
    }
}
