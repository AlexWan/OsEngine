/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using Newtonsoft.Json;


namespace OsEngine.Market.Servers.BitMart.Json
{

    public class WSRequest
    {
        public enum Operation
        {
            subscribe = 1,
            unsubscribe = 2,
            login = 3,
        }

        private class JsonImpl
        {
            public string op;
            public List<string> args;
        }

        public WSRequest() 
        {
            
        }

        public string GetJson()
        {
            JsonImpl body = new JsonImpl();
            body.op = this.op.ToString();
            body.args = this.args;

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
            this.op = Operation.subscribe;

            string channelStr = "";

            switch(channel)
            {
                case Channel.Depth:
                    channelStr = "spot/depth20";
                    break;
                case Channel.Trade:
                    channelStr = "spot/trade";
                    break;
            }

            this.args.Add(channelStr + ":" + security);
        }
    }

    public class WSRequestAuth : WSRequest
    {
        public class AuthArgs
        {
            public string apiKey;
            public string timestamp;
            public string sign;
        }

        public WSRequestAuth(AuthArgs auth)
        {
            this.op = Operation.login;
            this.args.Add(auth.apiKey);
            this.args.Add(auth.timestamp);
            this.args.Add(auth.sign);
        }
    }

    public class WSRequestBalance : WSRequest
    {
        public WSRequestBalance()
        {
            this.op = Operation.subscribe;
            this.args.Add("spot/user/balance:BALANCE_UPDATE");
        }
    }

    public class WSRequestOrder : WSRequest
    {
        public WSRequestOrder(string symbol)
        {
            this.op = Operation.subscribe;
            this.args.Add("spot/user/order:" + symbol);
        }
    }
}
