using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class AccountData
    {
        public string symbol { get; set; }
        public decimal margin_balance { get; set; }
        public decimal margin_position { get; set; }
        public decimal margin_frozen { get; set; }
        public decimal margin_available { get; set; }
        public decimal profit_real { get; set; }
        public decimal profit_unreal { get; set; }
        public object risk_rate { get; set; }
        public decimal withdraw_available { get; set; }
        public object liquidation_price { get; set; }
        public int lever_rate { get; set; }
        public decimal adjust_factor { get; set; }
        public decimal margin_static { get; set; }
        public int is_debit { get; set; }
    }

    public class FuturesAccountInfo
    {
        public string status { get; set; }
        public IList<AccountData> data { get; set; }
        public long ts { get; set; }
    }
}
