/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using Newtonsoft.Json;


namespace OsEngine.Market.Servers.BitMartFutures.Json
{

    public class WSRequest
    {
        public enum Operation
        {
            subscribe = 1,
            unsubscribe = 2,
            access = 3,
        }

        private class JsonImpl
        {
            public string action;
            public List<string> args;
        }

        public WSRequest() 
        {
            
        }

        public string GetJson()
        {
            JsonImpl body = new JsonImpl();
            body.action = this.action.ToString();
            body.args = this.args;

            return JsonConvert.SerializeObject(body);
        }

        public Operation action;
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
            this.action = Operation.subscribe;

            string channelStr = "";

            switch(channel)
            {
                case Channel.Depth:
                    channelStr = "futures/depth20";
                    break;
                case Channel.Trade:
                    channelStr = "futures/trade";
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
            this.action = Operation.access;
            this.args.Add(auth.apiKey);
            this.args.Add(auth.timestamp);
            this.args.Add(auth.sign);
            this.args.Add("web");
        }
    }

    public class WSRequestBalance : WSRequest
    {
        public WSRequestBalance(List<string> currencies)
        {
            this.action = Operation.subscribe;
            for (int i = 0; i < currencies.Count; i++)
            {
                this.args.Add("futures/asset:" + currencies[i]);
            }
        }
    }

    public class WSRequestPosition : WSRequest
    {
        public WSRequestPosition()
        {
            this.action = Operation.subscribe;
            this.args.Add("futures/position");
        }
    }

    public class WSRequestOrder : WSRequest
    {
        public WSRequestOrder()
        {
            this.action = Operation.subscribe;
            this.args.Add("futures/order");
        }
    }
}
