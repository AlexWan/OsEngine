
namespace OsEngine.Market.Servers.Finam.Entity
{
    public class FinamSecurity
    {
        /// <summary>
        /// unique number
        /// уникальный номер
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// код контракта
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// name of market
        /// название рынка 
        /// </summary>
        public string Market { get; set; }

        /// <summary>
        /// name of market as a number
        /// название рынка в виде цифры
        /// </summary>
        public string MarketId { get; set; }

        /// <summary>
        /// хз
        /// </summary>
        public string Decp { get; set; }

        /// <summary>
        /// хз
        /// </summary>
        public string EmitentChild { get; set; }

        /// <summary>
        /// web-site adress
        /// адрес на сайте
        /// </summary>
        public string Url { get; set; }
    }
}
