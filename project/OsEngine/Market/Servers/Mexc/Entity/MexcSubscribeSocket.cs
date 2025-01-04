/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using Newtonsoft.Json;


namespace OsEngine.Market.Servers.Mexc.Json
{

    public class WSRequest
    {
        public enum Operation
        {
            SUBSCRIPTION = 1,
            UNSUBSCRIPTION = 2,
        }

        private class JsonImpl
        {
            public string method { get; set; }
            public List<string> @params { get; set; }
        }

        public WSRequest() 
        {
            
        }

        public string GetJson()
        {
            JsonImpl body = new JsonImpl();
            body.method = this.op.ToString();
            body.@params = this.args;

            return JsonConvert.SerializeObject(body);
        }

        public Operation op;
        public List<string> args = new List<string>();
    }

    public class WSRequestSubscribe : WSRequest
    {
        public enum Channel 
        {
            Depth,
            Trade
        }

        public WSRequestSubscribe(Channel channel, string security)
        {
            this.op = Operation.SUBSCRIPTION;

            string channelStr = "";

            switch(channel)
            {
                case Channel.Depth:
                    channelStr = "spot@public.limit.depth.v3.api@" + security + "@20";
                    break;
                case Channel.Trade:
                    channelStr = "spot@public.deals.v3.api@"+security;
                    break;
            }

            this.args.Add(channelStr);
        }
    }

    public class WSRequestBalance : WSRequest
    {
        public WSRequestBalance()
        {
            this.op = Operation.SUBSCRIPTION;
            this.args.Add("spot@private.account.v3.api");
        }
    }

    public class WSRequestOrder : WSRequest
    {
        public WSRequestOrder()
        {
            this.op = Operation.SUBSCRIPTION;

            //this.args.Add("spot@private.deals.v3.api");
            this.args.Add("spot@private.orders.v3.api");
        }
    }
}
