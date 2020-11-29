namespace OsEngine.Entity
{
    /// <summary> какая программа запустила класс </summary>
    public enum StartProgram
    {
        /// <summary> тестер </summary>
        IsTester,

        /// <summary> оптимизатор </summary>
        IsOsOptimizer,

        /// <summary> качалка данных </summary>
        IsOsData,

        /// <summary> терминал </summary>
        IsOsTrader,

        /// <summary> конвертер тиков в свечи </summary>
        IsOsConverter,

        /// <summary> майнер паттернов </summary>
        IsOsMiner
    }
}
