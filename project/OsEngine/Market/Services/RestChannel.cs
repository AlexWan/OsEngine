using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace OsEngine.Market.Services
{
    public class RestChannel
    {
        public RestChannel()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        public string SendGetQuery(string baseUri, string endPoint)
        {
            Uri uri = new Uri(baseUri + endPoint);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            string responseMsg;

            using (var stream = httpWebResponse.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    responseMsg = reader.ReadToEnd();
                }
            }

            httpWebResponse.Close();

            if (httpWebResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Failed request " + responseMsg);
            }

            return responseMsg;
        }
        
        public string SendPostQuery(string url, string endPoint, byte[] data, Dictionary<string, string> headers)
        {
            Uri uri = new Uri(url + endPoint);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

            httpWebRequest.Method = "post";
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";

            foreach (var header in headers)
            {
                httpWebRequest.Headers.Add(header.Key, header.Value);
            }
            
            httpWebRequest.ContentLength = data.Length;

            using (Stream reqStream = httpWebRequest.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }

            string responseMsg;

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            using (var stream = httpWebResponse.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    responseMsg = reader.ReadToEnd();
                }
            }
            return responseMsg;
        }
    }
}
