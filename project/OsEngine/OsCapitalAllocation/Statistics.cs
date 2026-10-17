/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Статистические функции для матрицы доходностей.
    /// </summary>
    public static class Statistics
    {
        /// <summary>
        /// Коэффициент корреляции Пирсона.
        /// </summary>
        public static double Pearson(double[] x, double[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
            {
                return 0;
            }

            int n = x.Length;

            if (n == 0)
            {
                return 0;
            }

            double sumX = 0;
            double sumY = 0;
            double sumXY = 0;
            double sumX2 = 0;
            double sumY2 = 0;

            for (int i = 0; i < n; i++)
            {
                sumX += x[i];
                sumY += y[i];
                sumXY += x[i] * y[i];
                sumX2 += x[i] * x[i];
                sumY2 += y[i] * y[i];
            }

            double numerator = n * sumXY - sumX * sumY;
            double denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));

            if (denominator == 0)
            {
                return 0;
            }

            return numerator / denominator;
        }

        /// <summary>
        /// Коэффициент ранговой корреляции Спирмена.
        /// </summary>
        public static double Spearman(double[] x, double[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
            {
                return 0;
            }

            double[] rankX = Rank(x);
            double[] rankY = Rank(y);

            return Pearson(rankX, rankY);
        }

        /// <summary>
        /// Корреляционная матрица [бот, бот].
        /// </summary>
        public static double[,] CorrelationMatrix(double[,] returns, CorrelationType correlationType = CorrelationType.Pearson)
        {
            int bots = returns.GetLength(0);

            double[,] result = new double[bots, bots];

            for (int i = 0; i < bots; i++)
            {
                double[] rowI = GetRow(returns, i);

                result[i, i] = 1;

                for (int j = i + 1; j < bots; j++)
                {
                    double[] rowJ = GetRow(returns, j);

                    double corr = correlationType == CorrelationType.Spearman
                        ? Spearman(rowI, rowJ)
                        : Pearson(rowI, rowJ);

                    result[i, j] = corr;
                    result[j, i] = corr;
                }
            }

            return result;
        }

        /// <summary>
        /// Ковариационная матрица [бот, бот].
        /// </summary>
        public static double[,] CovarianceMatrix(double[,] returns, CovarianceMethod covarianceMethod = CovarianceMethod.Sample)
        {
            if (covarianceMethod == CovarianceMethod.Shrinkage)
            {
                return ShrinkageCovariance(returns);
            }

            int bots = returns.GetLength(0);
            int periods = returns.GetLength(1);

            double[,] result = new double[bots, bots];

            if (periods < 2)
            {
                return result;
            }

            double[] means = new double[bots];

            for (int i = 0; i < bots; i++)
            {
                double sum = 0;

                for (int t = 0; t < periods; t++)
                {
                    sum += returns[i, t];
                }

                means[i] = sum / periods;
            }

            double divisor = periods - 1;

            for (int i = 0; i < bots; i++)
            {
                for (int j = i; j < bots; j++)
                {
                    double sum = 0;

                    for (int t = 0; t < periods; t++)
                    {
                        sum += (returns[i, t] - means[i]) * (returns[j, t] - means[j]);
                    }

                    double cov = sum / divisor;

                    result[i, j] = cov;
                    result[j, i] = cov;
                }
            }

            return result;
        }

        private static double[,] ShrinkageCovariance(double[,] returns)
        {
            int bots = returns.GetLength(0);
            int periods = returns.GetLength(1);

            double[,] result = new double[bots, bots];

            if (periods < 2)
            {
                return result;
            }

            double[,] sample = CovarianceMatrix(returns);

            double[] means = new double[bots];

            for (int i = 0; i < bots; i++)
            {
                double sum = 0;

                for (int t = 0; t < periods; t++)
                {
                    sum += returns[i, t];
                }

                means[i] = sum / periods;
            }

            // цель — масштабированная единичная матрица: t_ij = mu * delta_ij, mu = trace(S) / p
            double mu = 0;

            for (int i = 0; i < bots; i++)
            {
                mu += sample[i, i];
            }

            mu = mu / bots;

            double sumSquaredDistance = 0;
            double sumSquaredVariance = 0;

            for (int i = 0; i < bots; i++)
            {
                for (int j = 0; j < bots; j++)
                {
                    double target = (i == j) ? mu : 0;
                    double s = sample[i, j];

                    double distance = s - target;
                    sumSquaredDistance += distance * distance;

                    double varianceEstimate = 0;

                    for (int t = 0; t < periods; t++)
                    {
                        double term = (returns[i, t] - means[i]) * (returns[j, t] - means[j]) - s;
                        varianceEstimate += term * term;
                    }

                    sumSquaredVariance += varianceEstimate;
                }
            }

            double shrinkage = sumSquaredDistance > 0
                ? sumSquaredVariance / (periods * periods) / sumSquaredDistance
                : 0;

            if (shrinkage > 1)
            {
                shrinkage = 1;
            }

            if (shrinkage < 0)
            {
                shrinkage = 0;
            }

            for (int i = 0; i < bots; i++)
            {
                for (int j = 0; j < bots; j++)
                {
                    double target = (i == j) ? mu : 0;

                    result[i, j] = shrinkage * target + (1 - shrinkage) * sample[i, j];
                }
            }

            return result;
        }

        private static double[] Rank(double[] values)
        {
            int n = values.Length;

            int[] indices = new int[n];

            for (int i = 0; i < n; i++)
            {
                indices[i] = i;
            }

            Array.Sort(indices, delegate (int a, int b) { return values[a].CompareTo(values[b]); });

            double[] ranks = new double[n];

            for (int i = 0; i < n; )
            {
                int j = i;

                while (j < n && values[indices[j]] == values[indices[i]])
                {
                    j++;
                }

                double averageRank = (i + 1 + j) / 2.0;

                for (int k = i; k < j; k++)
                {
                    ranks[indices[k]] = averageRank;
                }

                i = j;
            }

            return ranks;
        }

        private static double[] GetRow(double[,] matrix, int row)
        {
            int cols = matrix.GetLength(1);

            double[] result = new double[cols];

            for (int i = 0; i < cols; i++)
            {
                result[i] = matrix[row, i];
            }

            return result;
        }
    }
}
