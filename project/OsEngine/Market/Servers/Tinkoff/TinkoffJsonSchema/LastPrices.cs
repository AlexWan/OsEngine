using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{

    public class LastPricesResponse
    {
       public List<LastPrice> lastPrices;
    }

    public class LastPrice
    {
        /*      {
        "lastPrices": [
          {
            "figi": "BBG00ZHCX1X2",
            "price": {
              "units": "343",
              "nano": 700000000
            },
            "time": "2022-05-26T09:45:55.577523Z"
          }
        ]
      }*/

        public string time;

        public Quotation price;

        public string figi;
    }
}
