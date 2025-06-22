using System;

namespace OsEngine.Entity
{
    public class PublicMarketData
    {  
        public PublicMarketData() 
        {
            Funding = new Funding();
        }

        public Funding Funding; 
        
        public string SecurityName;
               
        /// <summary>
        /// volume in currency
        /// </summary>
        public decimal Volume24h;

        /// <summary>
        /// volume in USDT
        /// </summary>
        public decimal Turnover24h;        
    }

    public class Funding
    {
        public decimal CurrentValue;

        public DateTime NextFundingTime = new DateTime(1970, 1, 1, 0, 0, 0);

        public DateTime TimeUpdate = new DateTime(1970, 1, 1, 0, 0, 0);

        public decimal PreviousValue;

        public DateTime PreviousFundingTime = new DateTime(1970, 1, 1, 0, 0, 0);

        public int FundingIntervalHours;

        public decimal MaxFundingRate;

        public decimal MinFundingRate;
    }    
}
