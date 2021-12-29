namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// This class provides all available / supported pair definitions you can subscribe to
    /// </summary>
    public static class Pair
    {
        public static string ConvertToSocketSecName(string nameInRest)
        {
            string result = "";
            //XXBTZEUR = "XBT/EUR"
            //XETHZEUR = ETH/EUR
            if (nameInRest == "XZECXXBT")
            {
                return "ZEC/XBT";
            }
            if (nameInRest == "XZECZEUR")
            {
                return "ZEC/EUR";
            }
            if (nameInRest == "XZECZUSD")
            {
                return "ZEC/USD";
            }

            if (nameInRest.StartsWith("XX"))
            {
                result = nameInRest.Replace("XX", "X");
                result = result.Replace("Z", "/");
                return result;
            }
            if (nameInRest.StartsWith("XETH"))
            {
                result = nameInRest.Replace("XX", "");
                result = result.Replace("Z", "/");

                return result;
            }
            if (nameInRest.EndsWith("ETH"))
            {
                string pap = nameInRest.Replace("ETH", "");

                if (pap.Length == 3)
                {
                    result = pap + "/" + "ETH";
                }
                else
                {
                    result = pap + "/" + "ETH";
                }

            }
            if (nameInRest.EndsWith("EUR"))
            {
                string pap = nameInRest.Replace("EUR", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "EUR";
                }
                else
                {
                    result = pap + "/" + "EUR";
                }

            }
            if (nameInRest.EndsWith("USD"))
            {
                if (nameInRest == "XETHZUSD")
                {

                }
                string pap = nameInRest.Replace("USD", "");

                if (pap.StartsWith("X") &&
                    pap.EndsWith("Z"))
                {
                    pap = pap.Replace("Z", "");
                    pap = pap.Replace("X", "");
                }

                if (pap.Length == 3)
                {
                    result = pap + "/" + "USD";
                }
                else
                {
                    result = pap + "/" + "USD";
                }
            }
            if (nameInRest.EndsWith("USDT"))
            {
                string pap = nameInRest.Replace("USDT", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "USDT";
                }
                else
                {
                    result = pap + "/" + "USDT";
                }

            }
            if (nameInRest.EndsWith("USDC"))
            {
                string pap = nameInRest.Replace("USDC", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "USDC";
                }
                else
                {
                    result = pap + "/" + "USDC";
                }

            }
            if (nameInRest.EndsWith("XBT"))
            {
                string pap = nameInRest.Replace("XBT", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "XBT";
                }
                else
                {
                    result = pap + "/" + "XBT";
                }
            }

            return result;
        }

        /// <summary>
        /// Dash / Euro
        /// </summary>
        public const string DASH_EUR = "DASH/EU";

        /// <summary>
        /// Dash / US Dollar
        /// </summary>
        public const string DASH_USD = "DASH/US";

        /// <summary>
        /// Dash / Bitcoin
        /// </summary>
        public const string DASH_XBT = "DASH/XB";

        /// <summary>
        /// EOS / Ether
        /// </summary>
        public const string EOS_ETH = "EOS/ETH";

        /// <summary>
        /// EOS / Euro
        /// </summary>
        public const string EOS_EUR = "EOS/EUR";

        /// <summary>
        /// EOS / US Dollar
        /// </summary>
        public const string EOS_USD = "EOS/USD";

        /// <summary>
        /// EOS / Bitcoin
        /// </summary>
        public const string EOS_XBT = "EOS/XBT";

        /// <summary>
        /// Gnosis / Ether
        /// </summary>
        public const string GNO_ETH = "GNO/ETH";

        /// <summary>
        /// Gnosis / Euro
        /// </summary>
        public const string GNO_EUR = "GNO/EUR";

        /// <summary>
        /// Gnosis / US Dollar
        /// </summary>
        public const string GNO_USD = "GNO/USD";

        /// <summary>
        /// Gnosis / Bitcoin
        /// </summary>
        public const string GNO_XBT = "GNO/XBT";

        /// <summary>
        /// Qtum / Canadian Dollar 
        /// </summary>
        public const string QTUM_CAD = "QTUM/CAD";

        /// <summary>
        /// Qtum / Ether
        /// </summary>
        public const string QTUM_ETH = "QTUM/ETH";

        /// <summary>
        /// Qtum / Euro
        /// </summary>
        public const string QTUM_EUR = "QTUM/EUR";

        /// <summary>
        /// Qtum / US Dollar
        /// </summary>
        public const string QTUM_USD = "QTUM/USD";

        /// <summary>
        /// Qtum / Bitcoin
        /// </summary>
        public const string QTUM_XBT = "QTUM/XBT";

        /// <summary>
        /// Tether / US Dollar
        /// </summary>
        public const string USDT_USD = "USDT/USD";

        /// <summary>
        /// Ether Classic / Ether
        /// </summary>
        public const string ETC_ETH = "ETC/ETH";

        /// <summary>
        /// Ether Classic / Bitcoin
        /// </summary>
        public const string ETC_XBT = "ETC/XBT";

        /// <summary>
        /// Ether Classic / Euro
        /// </summary>
        public const string ETC_EUR = "ETC/EUR";

        /// <summary>
        /// Ether Classic / US Dollar
        /// </summary>
        public const string ETC_USD = "ETC/USD";

        /// <summary>
        /// Ether / Bitcoin
        /// </summary>
        public const string ETH_XBT = "ETH/XBT";

        /// <summary>
        /// Ether / Canadian Dollar
        /// </summary>
        public const string ETH_CAD = "ETH/CAD";

        /// <summary>
        /// Ether / Euro
        /// </summary>
        public const string ETH_EUR = "ETH/EUR";

        /// <summary>
        /// Ether / British Pound
        /// </summary>
        public const string ETH_GBP = "ETH/GBP";

        /// <summary>
        /// Ether / Yen
        /// </summary>
        public const string ETH_JPY = "ETH/JPY";

        /// <summary>
        /// Ether / US Dollar
        /// </summary>
        public const string ETH_USD = "ETH/USD";

        /// <summary>
        /// Litecoin / Bitcoin
        /// </summary>
        public const string LTC_XBT = "LTC/XBT";

        /// <summary>
        /// Litecoin / Euro
        /// </summary>
        public const string LTC_EUR = "LTC/EUR";

        /// <summary>
        /// Litecoin / US Dollar
        /// </summary>
        public const string LTC_USD = "LTC/USD";

        /// <summary>
        /// Melon / Ether
        /// </summary>
        public const string MLN_ETH = "MLN/ETH";

        /// <summary>
        /// Melon / Bitcoin
        /// </summary>
        public const string MLN_XBT = "MLN/XBT";

        /// <summary>
        /// Augur / Ether
        /// </summary>
        public const string REP_ETH = "REP/ETH";

        /// <summary>
        /// Augur / Bitcoin
        /// </summary>
        public const string REP_XBT = "REP/XBT";

        /// <summary>
        /// Augur / Euro
        /// </summary>
        public const string REP_EUR = "REP/EUR";

        /// <summary>
        /// Augur / US Dollar
        /// </summary>
        public const string REP_USD = "REP/USD";

        /// <summary>
        /// StarCoin / Euro
        /// </summary>
        public const string STR_EUR = "STR/EUR";

        /// <summary>
        /// StarCoin / US Dollar
        /// </summary>
        public const string STR_USD = "STR/USD";

        /// <summary>
        /// Bitcoin / Canadian Dollar
        /// </summary>
        public const string XBT_CAD = "XBT/CAD";

        /// <summary>
        /// Bitcoin / Euro
        /// </summary>
        public const string XBT_EUR = "XBT/EUR";

        /// <summary>
        /// Bitcoin / British Pound
        /// </summary>
        public const string XBT_GBP = "XBT/GBP";

        /// <summary>
        /// Bitcoin / Yen
        /// </summary>
        public const string XBT_JPY = "XBT/JPY";

        /// <summary>
        /// Bitcoin / US Dollar
        /// </summary>
        public const string XBT_USD = "XBT/USD";

        /// <summary>
        /// Bitcoin / Canadian Dollar
        /// </summary>
        public const string BTC_CAD = "BTC/CAD";

        /// <summary>
        /// Bitcoin / Euro
        /// </summary>
        public const string BTC_EUR = "BTC/EUR";

        /// <summary>
        /// Bitcoin / British Pound
        /// </summary>
        public const string BTC_GBP = "BTC/GBP";

        /// <summary>
        /// Bitcoin / Yen
        /// </summary>
        public const string BTC_JPY = "BTC/JPY";

        /// <summary>
        /// Bitcoin / US Dollar
        /// </summary>
        public const string BTC_USD = "BTC/USD";

        /// <summary>
        /// Dogecoin / Bitcoin
        /// </summary>
        public const string XDG_XBT = "XDG/XBT";

        /// <summary>
        /// Stellar / Bitcoin
        /// </summary>
        public const string XLM_XBT = "XLM/XBT";

        /// <summary>
        /// Dogecoin / Bitcoin
        /// </summary>
        public const string DOGE_XBT = "DOGE/XBT";

        /// <summary>
        /// Starcoin / Bitcoin
        /// </summary>
        public const string STR_XBT = "STR/XBT";

        /// <summary>
        /// Stellar / Euro
        /// </summary>
        public const string XLM_EUR = "XLM/EUR";

        /// <summary>
        /// Stellar / US Dollar
        /// </summary>
        public const string XLM_USD = "XLM/USD";

        /// <summary>
        /// Monero / Bitcoin
        /// </summary>
        public const string XMR_XBT = "XMR/XBT";

        /// <summary>
        /// Monero / Euro
        /// </summary>
        public const string XMR_EUR = "XMR/EUR";

        /// <summary>
        /// Monero / US Dollar
        /// </summary>
        public const string XMR_USD = "XMR/USD";

        /// <summary>
        /// Ripple / Bitcoin
        /// </summary>
        public const string XRP_XBT = "XRP/XBT";

        /// <summary>
        /// Ripple / Canadian Dollar
        /// </summary>
        public const string XRP_CAD = "XRP/CAD";

        /// <summary>
        /// Ripple / Euro
        /// </summary>
        public const string XRP_EUR = "XRP/EUR";

        /// <summary>
        /// Ripple / Yen
        /// </summary>
        public const string XRP_JPY = "XRP/JPY";

        /// <summary>
        /// Ripple / US Dollar
        /// </summary>
        public const string XRP_USD = "XRP/USD";

        /// <summary>
        /// Zcash / Bitcoin
        /// </summary>
        public const string ZEC_XBT = "ZEC/XBT";

        /// <summary>
        /// Zcash / Euro
        /// </summary>
        public const string ZEC_EUR = "ZEC/EUR";

        /// <summary>
        /// Zcash / Yen
        /// </summary>
        public const string ZEC_JPY = "ZEC/JPY";

        /// <summary>
        /// Zcash / US Dollar
        /// </summary>
        public const string ZEC_USD = "ZEC/USD";

        /// <summary>
        /// Tezos / Canadian Dollar
        /// </summary>
        public const string XTZ_CAD = "XTZ/CAD";

        /// <summary>
        /// Tezos / Ether
        /// </summary>
        public const string XTZ_ETH = "XTZ/ETH";

        /// <summary>
        /// Tezos / Euro
        /// </summary>
        public const string XTZ_EUR = "XTZ/EUR";

        /// <summary>
        /// Tezos / US Dollar
        /// </summary>
        public const string XTZ_USD = "XTZ/USD";

        /// <summary>
        /// Tezos / Bitcoin
        /// </summary>
        public const string XTZ_XBT = "XTZ/XBT";
    }
}
