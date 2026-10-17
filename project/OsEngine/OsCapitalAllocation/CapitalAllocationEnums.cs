/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Алгоритм расчёта весов.
    /// </summary>
    public enum CapitalAllocationAlgorithmType
    {
        CorrelationClustering,
        HierarchicalRiskParity
    }

    /// <summary>
    /// Метод линковки для иерархической кластеризации Hierarchical Risk Parity.
    /// </summary>
    public enum LinkageMethod
    {
        Single,
        Complete,
        Average,
        Ward
    }

    /// <summary>
    /// Способ оценки ковариационной матрицы.
    /// </summary>
    public enum CovarianceMethod
    {
        Sample,
        Shrinkage
    }

    /// <summary>
    /// Тип корреляции для матрицы расстояний.
    /// </summary>
    public enum CorrelationType
    {
        Pearson,
        Spearman
    }
}
