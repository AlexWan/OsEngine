using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Huobi.Entities
{
    /// <summary>
    /// GetAccountBalance response
    /// </summary>
    public class GetAccountBalanceResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        public string status;

        /// <summary>
        /// Error code
        /// </summary>
        [JsonProperty("err-code", NullValueHandling = NullValueHandling.Ignore)]
        public string errorCode;

        /// <summary>
        /// Error message
        /// </summary>
        [JsonProperty("err-msg", NullValueHandling = NullValueHandling.Ignore)]
        public string errorMessage;

        /// <summary>
        /// Response body
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Data data;

        /// <summary>
        /// Account info
        /// </summary>
        public class Data
        {
            /// <summary>
            /// Unique account id
            /// </summary>
            public int id;

            /// <summary>
            /// The type of this account
            /// Possible values: [spot, margin, otc, point, super-margin]
            /// </summary>
            public string type;

            /// <summary>
            /// Account state
            /// Possible values: [working, lock]
            /// </summary>
            public string state;

            /// <summary>
            /// The balance details of each currency
            /// </summary>
            public Balance[] list;

            public class Balance
            {
                /// <summary>
                /// The currency of this balance
                /// </summary>
                public string currency;

                /// <summary>
                /// The balance type
                /// Possible values: [trade, frozen]
                /// </summary>
                public string type;

                /// <summary>
                /// The balance in the main currency unit
                /// </summary>
                public decimal balance;
            }
        }
    }
}
