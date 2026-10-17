/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;

namespace OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity
{
    /// <summary>
    /// Hierarchical Risk Parity (Hierarchical Risk Parity), Лопес де Прадо, 2016.
    /// Три шага: Tree Clustering, Quasi-Diagonalization, Recursive Bisection.
    /// </summary>
    public class HierarchicalRiskParityAlgorithm : IAllocationAlgorithm
    {
        /// <summary>
        /// Слияния дендрограммы (объединения кластеров) в последнем расчёте.
        /// </summary>
        public List<DendrogramNode> Merges { get; private set; }

        /// <summary>
        /// Порядок ботов после квази-диагонализации.
        /// </summary>
        public List<int> SortedIndices { get; private set; }

        public LinkageMethod LinkageMethod = LinkageMethod.Single;

        public CovarianceMethod CovarianceMethod = CovarianceMethod.Sample;

        public CorrelationType CorrelationType = CorrelationType.Pearson;

        public HierarchicalRiskParityAlgorithm()
        {
            Merges = new List<DendrogramNode>();
            SortedIndices = new List<int>();
        }

        public decimal[] CalculateWeights(ReturnsMatrix matrix)
        {
            int botsCount = matrix.BotsCount;

            double[,] corr = Statistics.CorrelationMatrix(matrix.Returns, CorrelationType);

            double[,] dist = BuildDistanceMatrix(corr, botsCount);

            Merges = BuildDendrogram(dist, botsCount, LinkageMethod);

            SortedIndices = QuasiDiagonalize(Merges, botsCount);

            double[,] cov = Statistics.CovarianceMatrix(matrix.Returns, CovarianceMethod);

            double[] weightsDouble = RecursiveBisection(cov, SortedIndices);

            decimal[] weights = new decimal[botsCount];

            for (int i = 0; i < botsCount; i++)
            {
                weights[i] = (decimal)weightsDouble[i];
            }

            return weights;
        }

        #region HRP steps

        public static double[,] BuildDistanceMatrix(double[,] corr, int botsCount)
        {
            double[,] dist = new double[botsCount, botsCount];

            for (int i = 0; i < botsCount; i++)
            {
                for (int j = 0; j < botsCount; j++)
                {
                    double c = corr[i, j];

                    if (c > 1)
                    {
                        c = 1;
                    }
                    if (c < -1)
                    {
                        c = -1;
                    }

                    dist[i, j] = Math.Sqrt(0.5 * (1 - c));
                }
            }

            return dist;
        }

        private static List<DendrogramNode> BuildDendrogram(double[,] dist, int botsCount, LinkageMethod linkage)
        {
            if (linkage == LinkageMethod.Ward)
            {
                return BuildDendrogramWard(dist, botsCount);
            }

            return BuildDendrogramLinkage(dist, botsCount, linkage);
        }

        public static List<DendrogramNode> BuildDendrogramLinkage(double[,] dist, int botsCount, LinkageMethod linkage)
        {
            List<List<int>> leaves = new List<List<int>>();
            List<int> ids = new List<int>();

            for (int i = 0; i < botsCount; i++)
            {
                leaves.Add(new List<int> { i });
                ids.Add(i);
            }

            List<DendrogramNode> merges = new List<DendrogramNode>();

            while (leaves.Count > 1)
            {
                int bestA = 0;
                int bestB = 1;
                double bestDist = double.MaxValue;

                for (int a = 0; a < leaves.Count; a++)
                {
                    for (int b = a + 1; b < leaves.Count; b++)
                    {
                        double d = ClusterDistance(leaves[a], leaves[b], dist, linkage);

                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestA = a;
                            bestB = b;
                        }
                    }
                }

                DendrogramNode node = new DendrogramNode
                {
                    Left = ids[bestA],
                    Right = ids[bestB],
                    Distance = bestDist,
                    Size = leaves[bestA].Count + leaves[bestB].Count
                };

                merges.Add(node);

                leaves[bestA].AddRange(leaves[bestB]);
                ids[bestA] = botsCount + merges.Count - 1;

                leaves.RemoveAt(bestB);
                ids.RemoveAt(bestB);
            }

            return merges;
        }

        private static double ClusterDistance(List<int> a, List<int> b, double[,] dist, LinkageMethod linkage)
        {
            if (linkage == LinkageMethod.Single)
            {
                double min = double.MaxValue;

                for (int i = 0; i < a.Count; i++)
                {
                    for (int j = 0; j < b.Count; j++)
                    {
                        min = Math.Min(min, dist[a[i], b[j]]);
                    }
                }

                return min;
            }

            if (linkage == LinkageMethod.Complete)
            {
                double max = double.MinValue;

                for (int i = 0; i < a.Count; i++)
                {
                    for (int j = 0; j < b.Count; j++)
                    {
                        max = Math.Max(max, dist[a[i], b[j]]);
                    }
                }

                return max;
            }

            double sum = 0;
            int count = 0;

            for (int i = 0; i < a.Count; i++)
            {
                for (int j = 0; j < b.Count; j++)
                {
                    sum += dist[a[i], b[j]];
                    count++;
                }
            }

            return sum / count;
        }

        private static List<DendrogramNode> BuildDendrogramWard(double[,] dist, int botsCount)
        {
            double[,] squaredDist = new double[botsCount, botsCount];

            for (int i = 0; i < botsCount; i++)
            {
                for (int j = 0; j < botsCount; j++)
                {
                    squaredDist[i, j] = dist[i, j] * dist[i, j];
                }
            }

            List<List<int>> leaves = new List<List<int>>();
            List<int> ids = new List<int>();

            for (int i = 0; i < botsCount; i++)
            {
                leaves.Add(new List<int> { i });
                ids.Add(i);
            }

            List<List<double>> clusterDist = new List<List<double>>();

            for (int i = 0; i < botsCount; i++)
            {
                List<double> row = new List<double>();

                for (int j = 0; j < botsCount; j++)
                {
                    row.Add(i == j ? 0 : squaredDist[i, j]);
                }

                clusterDist.Add(row);
            }

            List<DendrogramNode> merges = new List<DendrogramNode>();

            int activeCount = botsCount;

            while (activeCount > 1)
            {
                int bestA = 0;
                int bestB = 1;
                double bestDist = double.MaxValue;

                for (int a = 0; a < activeCount; a++)
                {
                    for (int b = a + 1; b < activeCount; b++)
                    {
                        if (clusterDist[a][b] < bestDist)
                        {
                            bestDist = clusterDist[a][b];
                            bestA = a;
                            bestB = b;
                        }
                    }
                }

                DendrogramNode node = new DendrogramNode
                {
                    Left = ids[bestA],
                    Right = ids[bestB],
                    Distance = Math.Sqrt(bestDist),
                    Size = leaves[bestA].Count + leaves[bestB].Count
                };

                merges.Add(node);

                int sizeA = leaves[bestA].Count;
                int sizeB = leaves[bestB].Count;
                double distanceAB = bestDist;

                for (int k = 0; k < activeCount; k++)
                {
                    if (k == bestA || k == bestB)
                    {
                        continue;
                    }

                    int sizeK = leaves[k].Count;

                    double distanceAK = clusterDist[bestA][k];
                    double distanceBK = clusterDist[bestB][k];

                    double newDistance = ((sizeA + sizeK) * distanceAK
                        + (sizeB + sizeK) * distanceBK
                        - sizeK * distanceAB) / (double)(sizeA + sizeB + sizeK);

                    clusterDist[bestA][k] = newDistance;
                    clusterDist[k][bestA] = newDistance;
                }

                leaves[bestA].AddRange(leaves[bestB]);
                ids[bestA] = botsCount + merges.Count - 1;

                leaves.RemoveAt(bestB);
                ids.RemoveAt(bestB);
                clusterDist.RemoveAt(bestB);

                for (int k = 0; k < clusterDist.Count; k++)
                {
                    clusterDist[k].RemoveAt(bestB);
                }

                activeCount--;
            }

            return merges;
        }

        private static List<int> QuasiDiagonalize(List<DendrogramNode> merges, int botsCount)
        {
            List<int> sortedIndices;

            if (merges.Count == 0)
            {
                sortedIndices = new List<int>();

                for (int i = 0; i < botsCount; i++)
                {
                    sortedIndices.Add(i);
                }

                return sortedIndices;
            }

            DendrogramNode last = merges[merges.Count - 1];

            sortedIndices = new List<int> { last.Left, last.Right };

            while (true)
            {
                bool hasMerge = false;

                for (int i = 0; i < sortedIndices.Count; i++)
                {
                    if (sortedIndices[i] >= botsCount)
                    {
                        hasMerge = true;
                        break;
                    }
                }

                if (hasMerge == false)
                {
                    break;
                }

                List<int> newSorted = new List<int>();

                for (int i = 0; i < sortedIndices.Count; i++)
                {
                    int value = sortedIndices[i];

                    if (value >= botsCount)
                    {
                        DendrogramNode node = merges[value - botsCount];
                        newSorted.Add(node.Left);
                        newSorted.Add(node.Right);
                    }
                    else
                    {
                        newSorted.Add(value);
                    }
                }

                sortedIndices = newSorted;
            }

            return sortedIndices;
        }

        private static double[] RecursiveBisection(double[,] cov, List<int> sortedIndices)
        {
            int count = sortedIndices.Count;

            double[] weights = new double[count];

            for (int i = 0; i < count; i++)
            {
                weights[i] = 1;
            }

            List<List<int>> clusterItems = new List<List<int>>();

            clusterItems.Add(new List<int>(sortedIndices));

            while (clusterItems.Count > 0)
            {
                List<List<int>> newItems = new List<List<int>>();

                for (int i = 0; i < clusterItems.Count; i++)
                {
                    List<int> item = clusterItems[i];

                    if (item.Count > 1)
                    {
                        int mid = item.Count / 2;

                        newItems.Add(item.GetRange(0, mid));
                        newItems.Add(item.GetRange(mid, item.Count - mid));
                    }
                }

                clusterItems = newItems;

                for (int i = 0; i + 1 < clusterItems.Count; i += 2)
                {
                    List<int> left = clusterItems[i];
                    List<int> right = clusterItems[i + 1];

                    double leftVariance = ClusterVariance(cov, left);
                    double rightVariance = ClusterVariance(cov, right);

                    double alpha;

                    if (leftVariance + rightVariance == 0)
                    {
                        alpha = 0.5;
                    }
                    else
                    {
                        alpha = 1 - leftVariance / (leftVariance + rightVariance);
                    }

                    for (int j = 0; j < left.Count; j++)
                    {
                        weights[left[j]] *= alpha;
                    }

                    for (int j = 0; j < right.Count; j++)
                    {
                        weights[right[j]] *= (1 - alpha);
                    }
                }
            }

            return weights;
        }

        private static double ClusterVariance(double[,] cov, List<int> items)
        {
            int count = items.Count;

            if (count == 0)
            {
                return 0;
            }

            double[] inverseVariance = new double[count];
            double sum = 0;

            for (int i = 0; i < count; i++)
            {
                double variance = cov[items[i], items[i]];

                if (variance <= 0)
                {
                    variance = double.Epsilon;
                }

                inverseVariance[i] = 1.0 / variance;
                sum += inverseVariance[i];
            }

            for (int i = 0; i < count; i++)
            {
                inverseVariance[i] = inverseVariance[i] / sum;
            }

            double portfolioVariance = 0;

            for (int a = 0; a < count; a++)
            {
                for (int b = 0; b < count; b++)
                {
                    portfolioVariance += inverseVariance[a] * cov[items[a], items[b]] * inverseVariance[b];
                }
            }

            return portfolioVariance;
        }

        #endregion
    }

    /// <summary>
    /// Узел дендрограммы: слияние двух кластеров.
    /// </summary>
    public class DendrogramNode
    {
        public int Left;

        public int Right;

        public double Distance;

        public int Size;
    }
}
