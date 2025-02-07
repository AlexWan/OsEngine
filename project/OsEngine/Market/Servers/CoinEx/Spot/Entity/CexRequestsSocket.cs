/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market.Servers.Entity;
using OsEngine.Entity;
using System.Collections.Generic;
using RestSharp;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System;


namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{

    public class CexRequestSocket
    {
        public string method;
        //public long id = TimeManager.GetUnixTimeStampMilliseconds();
        public Dictionary<string, Object> parameters = new Dictionary<string, Object>();
        protected string _secret;

        public CexRequestSocket() { }

        public override string ToString()
        {
            long id = TimeManager.GetUnixTimeStampMilliseconds();
            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("method", method);
            jsonContent.Add("params", parameters);
            jsonContent.Add("id", id);
            return jsonContent.ToString();
        }
    }

    public class CexRequestSocketSign : CexRequestSocket
    {
        public CexRequestSocketSign(string access_id, string secret)
        {
            _secret = secret;
            method = CexWsOperation.SIGN.ToString();
            parameters.Add("access_id", access_id);
            long ts = TimeManager.GetUnixTimeStampMilliseconds();
            parameters.Add("signed_str", Signer.Sign(ts.ToString(), _secret));
            parameters.Add("timestamp", ts);
        }
    }

    public class CexRequestSocketPing : CexRequestSocket
    {
        public CexRequestSocketPing()
        {
            method = CexWsOperation.PING.ToString();
        }
    }
    
    public class CexRequestSocketTime : CexRequestSocket
    {
        public CexRequestSocketTime()
        {
            method = CexWsOperation.TIME.ToString();
        }
    }

    public class CexRequestSocketSubscribePortfolio : CexRequestSocket
    {
        public CexRequestSocketSubscribePortfolio()
        {
            method = CexWsOperation.BALANCE_SUBSCRIBE.ToString();
            parameters.Add("ccy_list", new string[0][]);
        }
    }

    public class CexRequestSocketSubscribeDeals : CexRequestSocket
    {
        public CexRequestSocketSubscribeDeals(List<Security> securities)
        {
            method = CexWsOperation.DEALS_SUBSCRIBE.ToString();
            List<string> secs = new List<string>();
            for(int i = 0; i < securities.Count; i++)
            {
                secs.Add(securities[i].Name);
            }
            parameters.Add("market_list", secs);
        }
    }
    
    public class CexRequestSocketSubscribeMarketDepth : CexRequestSocket
    {
        public CexRequestSocketSubscribeMarketDepth(List<Security> securities, int depth)
        {
            method = CexWsOperation.MARKET_DEPTH_SUBSCRIBE.ToString();

            List<Object[]> data = new List<Object[]>();
            for (int i = 0; i < securities.Count; i++)
            {
                data.Add(new object[4] { securities[i].Name, depth, "0", true });
            }

            parameters.Add("market_list", data);
        }
    }
    
    public class CexRequestSocketSubscribeMyOrders : CexRequestSocket
    {
        public CexRequestSocketSubscribeMyOrders(List<Security> securities)
        {
            method = CexWsOperation.ORDER_SUBSCRIBE.ToString();
            List<string> secs = new List<string>();
            for (int i = 0; i < securities.Count; i++)
            {
                secs.Add(securities[i].Name);
            }
            parameters.Add("market_list", secs);
        }
    }

    public class CexRequestSocketUnsubscribe : CexRequestSocket
    {
        public CexRequestSocketUnsubscribe(string unMethod, List<string> securities)
        {
            method = unMethod;
            parameters.Add("market_list", securities);
        }
    }
}