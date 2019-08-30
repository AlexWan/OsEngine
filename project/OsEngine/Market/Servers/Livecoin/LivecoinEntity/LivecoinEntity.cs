using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace OsEngine.Market.Servers.Livecoin.LivecoinEntity
{
    //public class Info
    //{
    //    public string name { get; set; }
    //    public string symbol { get; set; }
    //    public string walletStatus { get; set; }
    //    public string withdrawFee { get; set; }
    //    public string difficulty { get; set; }
    //    public string minDepositAmount { get; set; }
    //    public string minWithdrawAmount { get; set; }
    //    public string minOrderAmount { get; set; }
    //}

    //public class BalancesInfo
    //{
    //    public bool success { get; set; }
    //    public string minimalOrderBTC { get; set; }
    //    public List<Info> info { get; set; }

    //    public RestrictionSecurities Restriction;
    //}

    public class Restriction
    {
        public string currencyPair { get; set; }
        public string priceScale { get; set; }
        public string minLimitQuantity { get; set; }
    }

    public class RestrictionSecurities
    {
        public bool success { get; set; }
        public string minBtcVolume { get; set; }
        public List<Restriction> restrictions { get; set; }
    }

    public class Balance
    {
        public string type { get; set; }
        public string currency { get; set; }
        public string value { get; set; }
    }


    public class BalanceInfo
    {
        public string Name { get; set; }
        public List<Balance> Balances { get; set; }
    }
}
