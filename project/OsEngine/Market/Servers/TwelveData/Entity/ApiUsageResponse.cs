namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class ApiUsageResponse
    {
        public string timestamp { get; set; }
        public string current_usage { get; set; }
        public string plan_limit { get; set; }
        public string daily_usage { get; set; }
        public string plan_daily_limit { get; set; }
        public string plan_category { get; set; }
    }
}
