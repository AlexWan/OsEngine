using System;

namespace OsEngine.Entity
{
    public class OptionMarketData
    {       
        public double Delta;

        public double Vega;

        public double Gamma;

        public double Theta;

        public double Rho;

        public double MarkIV;

        public double MarkPrice;

        public string SecurityName;

        public DateTime TimeCreate;

        public double OpenInterest;
                
        public double BidIV;

        public double AskIV;

        public double UnderlyingPrice;

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
