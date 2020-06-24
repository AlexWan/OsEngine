using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    public class RestRequestBuilder
    {
        private Dictionary<string, string> _params;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request">The initial object</param>
        public RestRequestBuilder(RestRequestBuilder request = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            if (request != null)
            {
                _params = new Dictionary<string, string>(request._params);
            }
            else
            {
                _params = new Dictionary<string, string>();
            }
        }

        public void ClearParams()
        {
            _params = new Dictionary<string, string>();
        }


        /// <summary>
        /// Add URL escape property and value pair
        /// </summary>
        /// <param name="property">property</param>
        /// <param name="value">value</param>
        /// <returns>Current object</returns>
        public RestRequestBuilder AddParam(string property, string value)
        {
            if ((property != null) && (value != null))
            {
                _params.Add(Uri.EscapeDataString(property), Uri.EscapeDataString(value));
            }

            return this;
        }

        /// <summary>
        /// Add and merge another object
        /// </summary>
        /// <param name="request">The object that want to add</param>
        /// <returns>Current object</returns>
        public RestRequestBuilder AddParam(RestRequestBuilder request)
        {
            _params.Concat(request._params);

            return this;
        }

        /// <summary>
        /// Concat the property and value pair
        /// </summary>
        /// <returns>string</returns>
        public string BuildParams()
        {
            if (_params.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            foreach (var para in _params.OrderBy(i => i.Key, StringComparer.Ordinal))
            {
                sb.Append('&');
                sb.Append(para.Key).Append('=').Append(para.Value);
            }

            return sb.ToString().Substring(1);
        }

        public string SendGetQuery(string method, string baseUri, string endPoint, Dictionary<string, string> headers)
        {
            Uri uri = new Uri(baseUri + endPoint);

            if (uri.ToString().Contains("?"))
            {
                var t = 6;
            }

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

            httpWebRequest.Method = method;

            httpWebRequest.Accept = "application/json";
            httpWebRequest.ContentType = "application/json";

            foreach (var header in headers)
            {
                httpWebRequest.Headers.Add(header.Key, header.Value);
            }

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

        public string SendPostQuery(string method, string url, string endPoint, byte[] data, Dictionary<string, string> headers)
        {
            Uri uri = new Uri(url + endPoint);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

            httpWebRequest.Method = method;

            httpWebRequest.Accept = "application/json";
            httpWebRequest.ContentType = "application/json";



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
