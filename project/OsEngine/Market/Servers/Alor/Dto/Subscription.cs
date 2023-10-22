namespace OsEngine.Market.Servers.Alor.Dto
{
    public class Subscription
    {
        public string guid { get; set; }
        
        public string ticker { get; set; }
        
        public string opcode { get; set; }
        
        public SubscriptionStatusEnum state { get; set; }
    }
    
    public enum SubscriptionStatusEnum
    {
        Active,
        Pending,
        Off
    }
}