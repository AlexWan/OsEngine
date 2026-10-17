/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Алгоритм распределения весов (аллокации) по матрице доходностей.
    /// </summary>
    public interface IAllocationAlgorithm
    {
        /// <summary>
        /// Рассчитать веса ботов. Индекс веса соответствует индексу бота в матрице.
        /// Сумма весов равна 1 (100%).
        /// </summary>
        decimal[] CalculateWeights(ReturnsMatrix matrix);
    }
}
