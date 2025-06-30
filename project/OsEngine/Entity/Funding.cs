using System;

namespace OsEngine.Entity
{   
    public class Funding
    {
        public string SecurityNameCode;

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
