/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity;

namespace OsEngine.OsCapitalAllocation.Algorithms.CorrelationClustering
{
    /// <summary>
    /// CorrelationClustering (равновесная) аллокация: complete-linkage кластеризация
    /// по корреляции + равные веса. Классический HAC: дендрограмма (complete linkage)
    /// срезается по порогу корреляции — детерминированно, кластеры = клики (все пары ≥ порога).
    /// </summary>
    public class CorrelationClusteringAlgorithm : IAllocationAlgorithm
    {
        private readonly double _minCorrelation;

        /// <summary>
        /// Кластеры, полученные в последнем расчёте.
        /// </summary>
        public List<CorrelationCluster> Clusters { get; private set; }

        public CorrelationClusteringAlgorithm(double minCorrelation)
        {
            _minCorrelation = minCorrelation;
            Clusters = new List<CorrelationCluster>();
        }

        public decimal[] CalculateWeights(ReturnsMatrix matrix)
        {
            int botsCount = matrix.BotsCount;

            decimal[] weights = new decimal[botsCount];
            Clusters = new List<CorrelationCluster>();

            if (botsCount == 0)
            {
                return weights;
            }

            double[,] corr = Statistics.CorrelationMatrix(matrix.Returns);
            double[,] dist = HierarchicalRiskParityAlgorithm.BuildDistanceMatrix(corr, botsCount);

            List<DendrogramNode> merges = HierarchicalRiskParityAlgorithm.BuildDendrogramLinkage(dist, botsCount, LinkageMethod.Complete);

            double threshold = Math.Sqrt(0.5 * (1 - _minCorrelation));

            // Union-Find по узлам дендрограммы (боты + узлы слияний), срез по порогу
            int totalNodes = botsCount + merges.Count;
            int[] parent = new int[totalNodes];

            for (int i = 0; i < totalNodes; i++)
            {
                parent[i] = i;
            }

            for (int k = 0; k < merges.Count; k++)
            {
                if (merges[k].Distance <= threshold)
                {
                    int nodeId = botsCount + k;
                    Union(parent, nodeId, merges[k].Left);
                    Union(parent, nodeId, merges[k].Right);
                }
            }

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();

            for (int b = 0; b < botsCount; b++)
            {
                int root = Find(parent, b);

                if (groups.TryGetValue(root, out List<int> group) == false)
                {
                    group = new List<int>();
                    groups[root] = group;
                }

                group.Add(b);
            }

            foreach (KeyValuePair<int, List<int>> pair in groups)
            {
                CorrelationCluster cluster = new CorrelationCluster();
                cluster.BotIndices = pair.Value;
                Clusters.Add(cluster);
            }

            decimal perCluster = 1m / Clusters.Count;

            for (int i = 0; i < Clusters.Count; i++)
            {
                CorrelationCluster cluster = Clusters[i];
                decimal perBot = perCluster / cluster.BotIndices.Count;

                for (int j = 0; j < cluster.BotIndices.Count; j++)
                {
                    weights[cluster.BotIndices[j]] = perBot;
                }
            }

            return weights;
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);

            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }
    }

    /// <summary>
    /// Кластер ботов (список индексов ботов в матрице).
    /// </summary>
    public class CorrelationCluster
    {
        public List<int> BotIndices = new List<int>();
    }
}
