namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class CandleResponse
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string currency { get; set; }
        public string datetime { get; set; }
        public string timestamp { get; set; }
        public string last_quote_at { get; set; }
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string volume { get; set; }
        public string previous_close { get; set; }
        public string change { get; set; }
        public string percent_change { get; set; }
        public string average_volume { get; set; }
        public string rolling_1d_change { get; set; }
        public string rolling_7d_change { get; set; }
        public string rolling_change { get; set; }
        public string is_market_open { get; set; }
        public FiftyTwoWeek fifty_two_week { get; set; }
        public string extended_change { get; set; }
        public string extended_percent_change { get; set; }
        public string extended_price { get; set; }
        public string extended_timestamp { get; set; }
    }

    public class FiftyTwoWeek
    {
        public string low { get; set; }
        public string high { get; set; }
        public string low_change { get; set; }
        public string high_change { get; set; }
        public string low_change_percent { get; set; }
        public string high_change_percent { get; set; }
        public string range { get; set; }
    }
}
