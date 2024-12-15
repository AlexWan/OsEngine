/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Entity
{
    /// <summary>
    /// what program start the object
    /// </summary>
    public enum StartProgram
    {
        /// <summary>
        /// tester
        /// </summary>
        IsTester,

        /// <summary>
        /// optimizer
        /// </summary>
        IsOsOptimizer,

        /// <summary>
        /// data downloader
        /// </summary>
        IsOsData,

        /// <summary>
        /// trade terminal
        /// </summary>
        IsOsTrader,

        /// <summary>
        /// ticks to candles converter
        /// </summary>
        IsOsConverter,

        /// <summary>
        /// pattern miner
        /// </summary>
        IsOsMiner
    }
}
