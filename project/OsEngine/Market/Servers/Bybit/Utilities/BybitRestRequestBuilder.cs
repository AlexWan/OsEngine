using Newtonsoft.Json.Linq;
using OsEngine.Market.Servers.Bybit.Entities;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace OsEngine.Market.Servers.Bybit.Utilities
{
    public static class BybitRestRequestBuilder
    {
        public static JToken CreatePrivateGetQuery(Client client, string end_point, Dictionary<string, string> parameters)
        {
            //int time_factor = 1;

            //if (client.NetMode == "Main")
            //    time_factor = 0;

            Dictionary<string, string> sorted_params = parameters.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            StringBuilder sb = new StringBuilder();

            foreach (var param in sorted_params)
            {
                sb.Append(param.Key + $"=" + param.Value + $"&");
            }

            long nonce = Utils.GetMillisecondsFromEpochStart();

            string str_params = sb.ToString() + "timestamp=" + (nonce).ToString();

            string url = client.RestUrl + end_point + $"?" + str_params;

            Uri uri = new Uri(url + $"&sign=" + BybitSigner.CreateSignature(client, str_params));

            var http_web_request = (HttpWebRequest)WebRequest.Create(uri);

            http_web_request.Method = "Get";

            http_web_request.Host = client.RestUrl.Replace($"https://", "");

            HttpWebResponse http_web_response = (HttpWebResponse)http_web_request.GetResponse();

            string response_msg;

            using (var stream = http_web_response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    response_msg = reader.ReadToEnd();
                }
            }

            http_web_response.Close();

            if (http_web_response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Failed request " + response_msg);
            }

            return JToken.Parse(response_msg);
        }

        public static JToken CreatePublicGetQuery(Client client, string end_point)
        {
            string url = client.RestUrl + end_point;

            Uri uri = new Uri(url);

            var http_web_request = (HttpWebRequest)WebRequest.Create(uri);

            HttpWebResponse http_web_response = (HttpWebResponse)http_web_request.GetResponse();

            string response_msg;

            using (var stream = http_web_response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    response_msg = reader.ReadToEnd();
                }
            }

            http_web_response.Close();

            if (http_web_response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Failed request " + response_msg);
            }

            return JToken.Parse(response_msg);
        }

        public static JToken CreatePrivatePostQuery(Client client, string end_point, Dictionary<string, string> parameters)
        {
            parameters.Add("timestamp", (Utils.GetMillisecondsFromEpochStart()).ToString());

            Dictionary<string, string> sorted_params = parameters.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            StringBuilder sb = new StringBuilder();

            foreach (var param in sorted_params)
            {
                if (param.Value == "false" || param.Value == "true")
                    sb.Append("\"" + param.Key + "\":" + param.Value + ",");
                else
                    sb.Append("\"" + param.Key + "\":\"" + param.Value + "\",");
            }




            StringBuilder sb_signer = new StringBuilder();

            foreach (var param in sorted_params)
            {
                sb_signer.Append(param.Key + $"=" + param.Value + $"&");
            }

            string str_signer = sb_signer.ToString();

            str_signer = str_signer.Remove(str_signer.Length - 1);

            sb.Append("\"sign\":\"" + BybitSigner.CreateSignature(client, str_signer) + "\""); // api_key=bLP2z8x0sEeFHgt14S&close_on_trigger=False&order_link_id=&order_type=Limit&price=11018.00&qty=1&side=Buy&symbol=BTCUSD&time_in_force=GoodTillCancel&timestamp=1600513511844
                                                                                               // api_key=bLP2z8x0sEeFHgt14S&close_on_trigger=False&order_link_id=&order_type=Limit&price=10999.50&qty=1&side=Buy&symbol=BTCUSD&time_in_force=GoodTillCancel&timestamp=1600514673126
                                                                                               // {"api_key":"bLP2z8x0sEeFHgt14S","close_on_trigger":"False","order_link_id":"","order_type":"Limit","price":"11050.50","qty":"1","side":"Buy","symbol":"BTCUSD","time_in_force":"GoodTillCancel","timestamp":"1600515164173","sign":"fb3c69fa5d30526810a4b60fe4b8f216a3baf2c81745289ff7ddc21ab8232ccc"}


            string url = client.RestUrl + end_point;

            string str_data = "{" + sb.ToString() + "}";

            byte[] data = Encoding.UTF8.GetBytes(str_data);

            Uri uri = new Uri(url);

            var http_web_request = (HttpWebRequest)WebRequest.Create(uri);

            http_web_request.Method = "POST";

            http_web_request.ContentType = "application/json";

            http_web_request.ContentLength = data.Length;

            using (Stream req_tream = http_web_request.GetRequestStream())
            {
                req_tream.Write(data, 0, data.Length);
            }

            HttpWebResponse httpWebResponse = (HttpWebResponse)http_web_request.GetResponse();

            string response_msg;

            using (var stream = httpWebResponse.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    response_msg = reader.ReadToEnd();
                }
            }

            httpWebResponse.Close();

            return JToken.Parse(response_msg);
        }
    }
}
