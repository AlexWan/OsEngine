namespace OsEngine.Market.Servers.Alor.Dto
{
    public class AlorPortfolioRisk
    {
        /// <summary>
        /// Идентификатор клиентского портфеля
        /// </summary>
        public string portfolio { get; set; }

        /// <summary>
        /// Биржа
        /// </summary>
        public string exchange { get; set; }

        /// <summary>
        /// Общая стоимость портфеля
        /// </summary>
        public decimal portfolioEvaluation { get; set; }
        
        /// <summary>
        /// Стоимость ликвидного портфеля
        /// </summary>
        public decimal portfolioLiquidationValue { get; set; }
        
        /// <summary>
        /// Начальная маржа
        /// </summary>
        public decimal initialMargin { get; set; }
        
        /// <summary>
        ///  Минимальная маржа
        /// </summary>
        public decimal minimalMargin { get; set; }
       
        /// <summary>
        /// Скорректированная маржа
        /// </summary>
        public decimal correctedMargin { get; set; }
        
        /// <summary>
        /// НПР1
        /// </summary>
        public decimal riskCoverageRatioOne { get; set; }

        /// <summary>
        /// НПР2
        /// </summary>
        public decimal riskCoverageRatioTwo { get; set; }
        
        /// <summary>
        ///  Категория риска.
        /// </summary>
        public int riskCategoryId { get; set; }
        
        /// <summary>
        /// Тип клиента
        /// </summary>
        public string clientType { get; set; }
            
        /// <summary>
        /// Имеются ли запретные позиции
        /// </summary>
        public bool hasForbiddenPositions { get; set; }
        
        /// <summary>
        /// Имеются ли отрицательные количества
        /// </summary>
        public bool hasNegativeQuantity { get; set; }
    }
}