using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace OsEngine.Market.Servers.HTX.Entity
{
    public class GetRequest
    {
        private Dictionary<string, string> _params;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request">The initial object</param>
        public GetRequest(GetRequest request = null)
        {
            if (request != null)
            {
                _params = new Dictionary<string, string>(request._params);
            }
            else
            {
                _params = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Add URL escape property and value pair
        /// </summary>
        /// <param name="property">property</param>
        /// <param name="value">value</param>
        /// <returns>Current object</returns>
        public GetRequest AddParam(string property, string value)
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
        public GetRequest AddParam(GetRequest request)
        {
            IDictionaryEnumerator enumerator = request._params.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!_params.ContainsKey((string)enumerator.Key))
                {
                    _params.Add((string)enumerator.Key, (string)enumerator.Value);
                }              
            }
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

            IDictionaryEnumerator enumerator = _params.GetEnumerator();
            while (enumerator.MoveNext())
            {
                sb.Append('&');
                sb.Append(enumerator.Key).Append('=').Append(enumerator.Value);
            }

            return sb.ToString().Substring(1);
        }
    }
}
