namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{
    /// <summary>
    /// экземпляр инструмента с биржи bitfinex
    /// </summary>
    
    public class BitfinexSecurity
    {
        /// <summary>
        /// код бумаги
        /// </summary>
        public string pair { get; set; }

        /// <summary>
        /// колличество цифр в цене
        /// </summary>
        public int price_precision { get; set; }

        /// <summary>
        /// Начальная маржа, необходимая для открытия позиции в этой паре
        /// </summary>
        public string initial_margin { get; set; }

        /// <summary>
        /// Минимальная маржа для поддержания (в %)
        /// </summary>
        public string minimum_margin { get; set; }

        /// <summary>
        /// Максимальный размер ордера пары
        /// </summary>
        public string maximum_order_size { get; set; }

        /// <summary>
        /// Минимальный размер заказа пары
        /// </summary>
        public string minimum_order_size { get; set; }

        /// <summary>
        /// Срок действия для ограниченных контрактов / пар
        /// </summary>
        public string expiration { get; set; }

        /// <summary>
        /// маржинальная торговля включена для этой пары
        /// </summary>
        public bool margin { get; set; }
    }
}
