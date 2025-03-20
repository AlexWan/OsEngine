
namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public partial class CreateOrderRequest
    {
        public string contract { get; set; }
        public string size { get; set; }
        public string iceberg { get; set; }
        public string price { get; set; }
        public string tif { get; set; }
        public string text { get; set; }
        public string amend_text { get; set; }

        //public string auto_size { get; set; }
        public string close { get; set; }
        public string reduce_only { get; set; }
    }

    public partial class CreateOrderRequestDoubleModeClose
    {
        public string contract { get; set; }
        public string size { get; set; }
        public string iceberg { get; set; }
        public string price { get; set; }
        public string tif { get; set; }
        public string text { get; set; }
        public string amend_text { get; set; }

        //public string auto_size { get; set; }
        public string close { get; set; }
        public string reduce_only { get; set; }
    }
}
