
namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response
{
    public class GfAccount
    {
        public string order_margin { get; set; }
        public string point { get; set; }
        public CancelOrderResponseHistory history { get; set; }
        public string unrealised_pnl { get; set; }
        public string total { get; set; }
        public string available { get; set; }
        public string currency { get; set; }
        public string position_margin { get; set; }
        public string user { get; set; }
    }

    public class CancelOrderResponseHistory
    {
        public decimal dnw { get; set; }
        public string pnl { get; set; }
        public string point_refr { get; set; }
        public string refr { get; set; }
        public string point_fee { get; set; }
        public string fund { get; set; }
        public string fee { get; set; }
        public string point_dnw { get; set; }
    }
}
