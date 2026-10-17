/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity;
using OsEngine.OsCapitalAllocation.Algorithms.CorrelationClustering;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Состояние и расчёт модуля риск-паритета. UI (`CapitalAllocationUi`) — тонкая обёртка,
    /// MCP (`CapitalAllocationApi`) работает с этим же master'ом.
    /// </summary>
    public class CapitalAllocationMaster
    {
        /// <summary>
        /// Загруженные боты (стратегии).
        /// </summary>
        public List<BotFile> Bots = new List<BotFile>();

        /// <summary>
        /// Точность агрегации доходности (неделя/месяц).
        /// </summary>
        public ReturnPeriodType PeriodType = ReturnPeriodType.Week;

        /// <summary>
        /// Начало диапазона периодов (null — без ограничения).
        /// </summary>
        public DateTime? StartTime;

        /// <summary>
        /// Конец диапазона периодов (null — без ограничения).
        /// </summary>
        public DateTime? EndTime;

        /// <summary>
        /// Последний рассчитанный алгоритм.
        /// </summary>
        public CapitalAllocationAlgorithmType Algorithm = CapitalAllocationAlgorithmType.CorrelationClustering;

        /// <summary>
        /// Порог корреляции для CorrelationClustering.
        /// </summary>
        public double MinCorrelation = 0.4;

        /// <summary>
        /// Метод линковки для Hierarchical Risk Parity.
        /// </summary>
        public LinkageMethod LinkageMethod = LinkageMethod.Single;

        /// <summary>
        /// Способ оценки ковариации для Hierarchical Risk Parity.
        /// </summary>
        public CovarianceMethod CovarianceMethod = CovarianceMethod.Sample;

        /// <summary>
        /// Тип корреляции для Hierarchical Risk Parity.
        /// </summary>
        public CorrelationType CorrelationType = CorrelationType.Pearson;

        /// <summary>
        /// Последняя построенная матрица доходностей.
        /// </summary>
        public ReturnsMatrix Matrix;

        /// <summary>
        /// Последние рассчитанные веса.
        /// </summary>
        public decimal[] Weights;

        /// <summary>
        /// Кластеры CorrelationClustering (последний расчёт).
        /// </summary>
        public List<CorrelationCluster> Clusters;

        /// <summary>
        /// Результат Hierarchical Risk Parity (последний расчёт).
        /// </summary>
        public HierarchicalRiskParityAlgorithm Hrp;

        /// <summary>
        /// Добавить ботов из памяти (например, из журнала).
        /// </summary>
        public void AddBots(List<BotFile> bots)
        {
            if (bots == null)
            {
                return;
            }

            for (int i = 0; i < bots.Count; i++)
            {
                Bots.Add(bots[i]);
            }
        }

        /// <summary>
        /// Добавить ботов из файлов по путям.
        /// </summary>
        public void AddBotsFromPaths(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (string path in paths)
            {
                BotFile bot = BotFileLoader.Load(path);

                if (bot != null && bot.Positions.Count > 0)
                {
                    Bots.Add(bot);
                }
            }
        }

        /// <summary>
        /// Очистить список ботов.
        /// </summary>
        public void ClearBots()
        {
            Bots.Clear();
        }

        /// <summary>
        /// Построить матрицу доходностей по текущим ботам и настройкам. null, если данных нет.
        /// </summary>
        public ReturnsMatrix BuildMatrix()
        {
            if (Bots.Count == 0)
            {
                Matrix = null;
                return null;
            }

            ReturnsMatrix matrix = ReturnsMatrixBuilder.Build(Bots, PeriodType, StartTime, EndTime);

            if (matrix.PeriodsCount == 0)
            {
                Matrix = null;
                return null;
            }

            Matrix = matrix;

            return matrix;
        }

        /// <summary>
        /// Рассчитать CorrelationClustering.
        /// </summary>
        public void CalculateCorrelationClustering()
        {
            ReturnsMatrix matrix = BuildMatrix();

            if (matrix == null)
            {
                return;
            }

            CorrelationClusteringAlgorithm clustering = new CorrelationClusteringAlgorithm(MinCorrelation);

            Weights = clustering.CalculateWeights(matrix);
            Clusters = clustering.Clusters;
            Hrp = null;
            Algorithm = CapitalAllocationAlgorithmType.CorrelationClustering;
        }

        /// <summary>
        /// Рассчитать Hierarchical Risk Parity.
        /// </summary>
        public void CalculateHierarchicalRiskParity()
        {
            ReturnsMatrix matrix = BuildMatrix();

            if (matrix == null)
            {
                return;
            }

            HierarchicalRiskParityAlgorithm hrp = new HierarchicalRiskParityAlgorithm();
            hrp.LinkageMethod = LinkageMethod;
            hrp.CovarianceMethod = CovarianceMethod;
            hrp.CorrelationType = CorrelationType;

            Weights = hrp.CalculateWeights(matrix);
            Hrp = hrp;
            Clusters = null;
            Algorithm = CapitalAllocationAlgorithmType.HierarchicalRiskParity;
        }
    }
}
