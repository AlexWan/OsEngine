namespace OsEngine.Market.Servers.Alor.Dto
{
    public class AlorPortfolioSummary
    {
        /// <summary>
        ///  Покупательская способность на утро
        /// </summary>
        public decimal buyingPowerAtMorning { get; set; }
       
        /// <summary>
        /// Покупательская способность
        /// </summary>
        public decimal buyingPower { get; set; }
        
        /// <summary>
        /// Прибыль за сегодня
        /// </summary>
        public decimal profit { get; set; }

        /// <summary>
        /// Норма прибыли, %
        /// </summary>
        public decimal profitRate { get; set; }
        
        /// <summary>
        /// Ликвидный портфель
        /// </summary>
        public decimal portfolioEvaluation { get; set; }
        
        /// <summary>
        /// Оценка портфеля
        /// </summary>
        public decimal portfolioLiquidationValue { get; set; }
        
        /// <summary>
        /// Маржа
        /// </summary>
        public decimal initialMargin { get; set; }
        
        /// <summary>
        /// Риск до закрытия
        /// </summary>
        public decimal riskBeforeForcePositionClosing { get; set; }
    }
}