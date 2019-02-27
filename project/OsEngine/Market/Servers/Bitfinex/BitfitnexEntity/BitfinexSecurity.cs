namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{
    /// <summary>
    /// instrument instance from the bitfinex exchange 
    /// экземпляр инструмента с биржи bitfinex
    /// </summary>

    public class BitfinexSecurity
    {
        /// <summary>
        /// security code
        /// код бумаги
        /// </summary>
        public string pair { get; set; }

        /// <summary>
        /// number of digits in price
        /// колличество цифр в цене
        /// </summary>
        public int price_precision { get; set; }

        /// <summary>
        /// initial margin for opening a position in this pair
        /// Начальная маржа, необходимая для открытия позиции в этой паре
        /// </summary>
        public string initial_margin { get; set; }

        /// <summary>
        /// minimum margin to maintain (%)
        /// Минимальная маржа для поддержания (в %)
        /// </summary>
        public string minimum_margin { get; set; }

        /// <summary>
        /// maximum order size for a pair
        /// Максимальный размер ордера пары
        /// </summary>
        public string maximum_order_size { get; set; }

        /// <summary>
        /// minimum order size for pair
        /// Минимальный размер заказа пары
        /// </summary>
        public string minimum_order_size { get; set; }

        /// <summary>
        /// Validity for limited contracts / pairs
        /// Срок действия для ограниченных контрактов / пар
        /// </summary>
        public string expiration { get; set; }

        /// <summary>
        /// shows whether margin trading is included for this pair
        /// маржинальная торговля включена для этой пары
        /// </summary>
        public bool margin { get; set; }
    }
}
