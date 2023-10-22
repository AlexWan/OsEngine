namespace OsEngine.Market.Servers.Alor.Dto
{
    public class AlorPortfolioRiskForts
    {
        /// <summary>
        /// Идентификатор клиентского портфеля
        /// </summary>
        public string portfolio { get; set; }
            
        /// <summary>
        ///  Свободные средства. Сумма рублей и залогов, дисконтированных в рубли, доступная для открытия позиций. (MoneyFree = MoneyAmount + VmInterCl – MoneyBlocked – VmReserve – Fee)
        /// </summary>
        public decimal moneyFree { get; set; }
       
        /// <summary>
        /// Средства, заблокированные под ГО
        /// </summary>
        public decimal moneyBlocked { get; set; }
        
        /// <summary>
        /// Списанный сбор
        /// </summary>
        public decimal fee { get; set; }
        
        /// <summary>
        /// Общее количество рублей и дисконтированных в рубли залогов на начало сессии
        /// </summary>
        public decimal moneyOld { get; set; }
        
        /// <summary>
        /// Общее количество рублей и дисконтированных в рубли залогов
        /// </summary>
        public decimal moneyAmount { get; set; }
        
        /// <summary>
        /// Сумма залогов, дисконтированных в рубли
        /// </summary>
        public decimal moneyPledgeAmount { get; set; }
        
        /// <summary>
        /// Вариационная маржа, списанная или полученная в пром. клиринг
        /// </summary>
        public decimal vmInterCl { get; set; }
        
        /// <summary>
        /// Сагрегированная вармаржа по текущим позициям
        /// </summary>
        public decimal vmCurrentPositions { get; set; }
        
        /// <summary>
        /// VmCurrentPositions + VmInterCl
        /// </summary>
        public decimal varMargin { get; set; }
        
        /// <summary>
        /// Наличие установленных денежного и залогового лимитов
        /// </summary>
        public bool isLimitsSet { get; set; }
    }
}