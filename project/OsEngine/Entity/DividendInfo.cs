/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Entity
{
    /// <summary>
    /// Information about a dividend payment credited to a bot during testing.
    /// Информация о выплате дивидендов, начисленной роботу в тестере.
    /// </summary>
    public class DividendInfo
    {
        /// <summary>
        /// Payment date / Дата выплаты
        /// </summary>
        public DateTime PaymentDate { get; set; }

        /// <summary>
        /// Position creation date / Дата создания синтетической позиции
        /// </summary>
        public DateTime PositionCreateDate { get; set; }

        /// <summary>
        /// Security name / Название инструмента
        /// </summary>
        public string SecurityName { get; set; }

        /// <summary>
        /// Bot name / Название робота
        /// </summary>
        public string BotName { get; set; }

        /// <summary>
        /// Volume of the base position / Объём базовой позиции
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Dividend amount credited / Сумма начисленных дивидендов
        /// </summary>
        public decimal Sum { get; set; }
    }
}
