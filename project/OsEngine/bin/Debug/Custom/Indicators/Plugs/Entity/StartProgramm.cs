using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Entity
{
    /// <summary>
    /// what program start the class
    /// какая программа запустила класс
    /// </summary>
    public enum StartProgram
    {
        /// <summary>
        /// tester
        /// тестер
        /// </summary>
        IsTester,

        /// <summary>
        /// optimizator
        /// оптимизатор
        /// </summary>
        IsOsOptimizer,

        /// <summary>
        /// data downloading
        /// качалка данных
        /// </summary>
        IsOsData,

        /// <summary>
        /// terminal
        /// терминал
        /// </summary>
        IsOsTrader,

        /// <summary>
        /// ticks to candles converter
        /// конвертер тиков в свечи
        /// </summary>
        IsOsConverter,

        /// <summary>
        /// pattern miner
        /// майнер паттернов
        /// </summary>
        IsOsMiner
    }
}
