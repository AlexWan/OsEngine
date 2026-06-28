namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class SecurityResponse<T>
    {
        public string count { get; set; }
        public T data { get; set; }
        public string status { get; set; }

    }

    public class StockData
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string currency { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string country { get; set; }
        public string type { get; set; }
        public string figi_code { get; set; }
        public string cfi_code { get; set; }
        public string isin { get; set; }
        public string cusip { get; set; }
        public Access access { get; set; }
    }

    public class Access
    {
        public string global { get; set; }
        public string plan { get; set; }
        public string plan_business { get; set; }
    }

    public class ForexData
    {
        public string symbol { get; set; }
        public string currency_group { get; set; }
        public string currency_base { get; set; }
        public string currency_quote { get; set; }
    }

    public class EtfData
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string currency { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string country { get; set; }
        public string figi_code { get; set; }
        public string cfi_code { get; set; }
        public string isin { get; set; }
        public string cusip { get; set; }
        public Access access { get; set; }
    }

    public class CommoditiesData
    {
        public string category { get; set; }
        public string description { get; set; }
        public string name { get; set; }
        public string symbol { get; set; }
    }
}
