using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class AccountData
    {
        public string userid;

        public string margin_asset;

        public string margin_static;

        public string cross_margin_static;

        public string margin_frozen;

        public string withdraw_available;

        public string cross_risk_rate;

        public object[] cross_swap;

        public object[] cross_future;

        public object[] isolated_swap;
    }

    public class FuturesAccountInfo
    {
        public string status;

        public List<AccountData> data;

        public long ts;
    }
}
