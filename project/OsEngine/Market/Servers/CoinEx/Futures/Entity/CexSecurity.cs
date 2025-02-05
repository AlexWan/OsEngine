using OsEngine.Entity;
using OsEngine.OsData;
using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/market/http/list-market#http-request
    */
    struct CexSecurity
    {
        // Market ETHUSDT
        public string market { get; set; }

        // Base Currency ETH
        public string base_ccy { get; set; }

        // Base Currency Decimals 8
        public long base_ccy_precision { get; set; }

        // Quote Currency USDT
        public string quote_ccy { get; set; }

        // Quote Currency Decimals 2
        public long quote_ccy_precision { get; set; }

        // Maker Fee Rate 0.002
        public string maker_fee_rate { get; set; }

        // Taker Fee Rate 0.002
        public string taker_fee_rate { get; set; }

        // Min. transaction volume
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string min_amount { get; set; }

        // Whether to enable AMM function
        public bool is_amm_available { get; set; }

        // Whether to enable margin trading
        public bool is_margin_available { get; set; }

        public bool is_pre_trading_available { get; set; }

        public static explicit operator Security(CexSecurity cexSecurity)
        {
            Security security = new Security();
            security.Name = cexSecurity.market;
            security.NameId = cexSecurity.market;
            security.NameFull = cexSecurity.market;
            security.NameClass = cexSecurity.quote_ccy;
            security.State = SecurityStateType.Activ; // TODo check
            security.Decimals = Convert.ToInt32(cexSecurity.quote_ccy_precision);
            security.DecimalsVolume = 8; // Число знаков объёма
            security.PriceStep = CoinExServerRealization.GetPriceStep(security.Decimals);
            security.PriceStepCost = security.PriceStep; // FIX Сомнительно! Проверить!
            security.Lot = 1;
            security.SecurityType = SecurityType.CurrencyPair;
            security.Exchange = ServerType.CoinExSpot.ToString();
            security.MinTradeAmount = cexSecurity.min_amount.ToString().ToDecimal();

            return security;
        }
    }
}
