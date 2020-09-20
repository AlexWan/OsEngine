using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    public class Client
    {
        public string main_host = "https://api.bybit.com";
        public string test_host = "https://api-testnet.bybit.com";

        private string base_url_wss = "wss://stream.bybit.com";
        private string test_base_url_wss = "wss://stream-testnet.bybit.com";

        private string wss_inverse_public_postfix = "/realtime";
        private string wss_inverse_private_postfix = "/realtime";

        private string wss_usdt_public_postfix = "/realtime_public";
        private string wss_usdt_private_postfix = "/realtime_private";

        private bool is_inverse = true;
        private bool is_main_net = true;



        public string ApiKey { get; }
        public string ApiSecret { get; }

        public string FuturesMode
        {
            get
            {
                if (is_inverse)
                    return "Inverse";
                else
                    return "USDT";
            }
        }

        public string NetMode
        {
            get
            {
                if (is_main_net)
                    return "Main";
                else
                    return "Test";
            }
        }

        public string RestUrl
        {
            get
            {
                if (is_main_net)
                    return main_host;

                if (!is_main_net)
                    return test_host;

                return test_host;
            }
        }
        public string WsPublicUrl
        {
            get
            {
                if (is_inverse && is_main_net)
                    return base_url_wss + wss_inverse_public_postfix;

                if (is_inverse && !is_main_net)
                    return test_base_url_wss + wss_inverse_public_postfix;

                if (!is_inverse && is_main_net)
                    return base_url_wss + wss_usdt_public_postfix;

                if (!is_inverse && !is_main_net)
                    return test_base_url_wss + wss_usdt_public_postfix;

                return "";
            }
        }

        public string WsPrivateUrl
        {
            get
            {
                if (is_inverse && is_main_net)
                    return base_url_wss + wss_inverse_private_postfix;

                if (is_inverse && !is_main_net)
                    return test_base_url_wss + wss_inverse_private_postfix;

                if (!is_inverse && is_main_net)
                    return base_url_wss + wss_usdt_private_postfix;

                if (!is_inverse && !is_main_net)
                    return test_base_url_wss + wss_usdt_private_postfix;

                return "";
            }
        }

        public Client(string api_key, string api_secret, bool is_inverse, bool is_main_net)
        {
            ApiKey = api_key;
            ApiSecret = api_secret;
            this.is_inverse = is_inverse;
            this.is_main_net = is_main_net;
        }
    }
}
