using System.Collections.Generic;

namespace OsEngine.Market.Servers.Woo.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string topic { get; set; }
        public string ts { get; set; }
        public T data { get; set; }
    }

    public class ResponseChannelTrades
    {
        public string s { get; set; }       // symbol
        public string px { get; set; }      // price
        public string sx { get; set; }      // size
        public string sd { get; set; }      // side
        public int src { get; set; }        // trade source
        public bool rpi { get; set; }
        public long ts { get; set; }        // trade generation time
    }

    public class ResponseChannelBook
    {
        public string s { get; set; } // symbol
        public string prevTs { get; set; } // previous orderbook generation time
        public List<List<string>> asks { get; set; } // delta updates ask side
        public List<List<string>> bids { get; set; } // delta updates bid side
        public string ts { get; set; } // current orderbook generation time
    }

    public class BalanceData
    {
        public List<ResponseChannelPortfolio> balances { get; set; }
    }

    public class ResponseChannelPortfolio
    {
        public string t { get; set; }     // token
        public string h { get; set; }     // holding
        public string f { get; set; }     // frozen
        public string i { get; set; }     // interest
        public string psq { get; set; }   // pending short qty
        public string plq { get; set; }   // pending long qty
        public string s { get; set; }     // staked
        public string u { get; set; }     // unbonding
        public string v { get; set; }     // vault
        public string l { get; set; }     // launchpad vault
        public string e { get; set; }     // earn
        public string aop { get; set; }   // average open price
        public string pnl { get; set; }   // 24h PnL
        public string roi { get; set; }   // 24h PnL percentage
        public string fee { get; set; }   // 24h fee
        public string mp { get; set; }    // mark price
        public string ver { get; set; }      // version
        public string b { get; set; }     // trial fund bonus
        public string lcr { get; set; }      // loss cover ratio
        public string ts { get; set; }      // timestamp of balance change
    }

    public class ResponseChannelOrder
    {
        public string mt { get; set; }             // message type
        public string s { get; set; }           // symbol
        public string cid { get; set; }            // client order id
        public string oid { get; set; }           // order id
        public string t { get; set; }           // order type
        public string sd { get; set; }          // side
        public string ps { get; set; }          // position side
        public string sx { get; set; }          // order quantity
        public string px { get; set; }          // order price
        public string tid { get; set; }           // trade id
        public string esx { get; set; }         // executed quantity
        public string epx { get; set; }         // executed price
        public string f { get; set; }           // transaction fee amount
        public string fa { get; set; }          // transaction fee asset
        public string tesx { get; set; }        // total executed quantity of the order
        public string aepx { get; set; }        // average executed price of the order
        public string ss { get; set; }          // status
        public string rs { get; set; }          // reason
        public string tg { get; set; }          // order tag
        public string tf { get; set; }          // total fee of the order
        public string tfc { get; set; }         // fee currency of the order
        public string tr { get; set; }          // total rebate of the order
        public string trc { get; set; }         // rebate currency of the order
        public string vsx { get; set; }         // visible quantity of the order
        public string ts { get; set; }            // executed timestamp
        public string ro { get; set; }              // reduce only
        public string mk { get; set; }               // whether it is a maker transaction
        public string lv { get; set; }                 // leverage
        public string rpi { get; set; }               // whether execution was matched against RPI order
        public string m { get; set; }                // margin mode
    }

    public class PositionData
    {
        public List<ResponseChannelPositions> positions { get; set; }
    }

    public class ResponseChannelPositions
    {
        public string s { get; set; }      // symbol
        public string h { get; set; }      // holding
        public string plq { get; set; }    // pending long quantity
        public string psq { get; set; }    // pending short quantity
        public string aop { get; set; }    // average open price
        public string pnl { get; set; }    // 24h pnl
        public string roi { get; set; }    // 24h pnl % change
        public string fee { get; set; }    // 24h fee
        public string sp { get; set; }     // last settle price
        public string mp { get; set; }     // mark price
        public string ot { get; set; }       // opening time
        public string aq { get; set; }        // adl quantile
        public string lv { get; set; }        // leverage
        public string m { get; set; }      // margin mode
        public string ps { get; set; }     // position side
        public string it { get; set; }     // isolated margin token
        public string ia { get; set; }     // isolated margin amount
        public string il { get; set; }     // isolated margin frozen long
        public string @is { get; set; }     // isolated margin frozen short
        public string ver { get; set; }       // version
        public string ts { get; set; }       // timestamp of position update      
    }

    public class ResponseChannelAccount
    {
        public string m { get; set; }      // trading mode
        public string M { get; set; }      // default margin mode for futures
        public string P { get; set; }      // position mode
        public string l { get; set; }      // account leverage for margin
        public string mr { get; set; }     // margin ratio
        public string omr { get; set; }    // open margin ratio
        public string imr { get; set; }    // initial margin ratio
        public string mmr { get; set; }    // maintenance margin ratio
        public string tc { get; set; }     // total collateral
        public string fc { get; set; }     // free collateral
        public string v { get; set; }      // account total value
    }

    public class TickerData
    {
        public string s { get; set; }  // symbol
        public string o { get; set; }  // open
        public string c { get; set; }  // close
        public string h { get; set; }  // high
        public string l { get; set; }  // low
        public string v { get; set; }  // volume in base token
        public string a { get; set; }  // amount in USDT
        public string q { get; set; }  // aggregated volume in base token
        public string u { get; set; }  // aggregated amount in USDT
        public string cnt { get; set; }  // trade count
        public string ts { get; set; }  // ticker generation time
        public string tts { get; set; }  // last trade time
    }

    public class FundingRateData
    {
        public string s { get; set; }    // symbol
        public string r { get; set; }    // funding rate
        public string ft { get; set; }     // next funding time
        public string ts { get; set; }     // system timestamp
    }

    public class OpenInterestItem
    {
        public string s { get; set; }    // symbol
        public string oi { get; set; }   // open interest
        public string ts { get; set; }     // time of last update
    }
}

