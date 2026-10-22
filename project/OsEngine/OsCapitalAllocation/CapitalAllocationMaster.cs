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
        /// Событие изменения состояния (боты/результат) — для перерисовки UI,
        /// когда состояние меняет не человек, а MCP API.
        /// </summary>
        public event Action StateChangedEvent;

        /// <summary>
        /// Загруженные боты (стратегии).
        /// </summary>
        public List<BotFile> Bots = new List<BotFile>();

        private ReturnPeriodType _periodType = ReturnPeriodType.Week;

        /// <summary>
        /// Точность агрегации доходности (неделя/месяц).
        /// </summary>
        public ReturnPeriodType PeriodType
        {
            get { return _periodType; }
            set { if (_periodType == value) return; _periodType = value; NotifyChanged(); }
        }

        private DateTime? _startTime;

        /// <summary>
        /// Начало диапазона периодов (null — без ограничения).
        /// </summary>
        public DateTime? StartTime
        {
            get { return _startTime; }
            set { if (_startTime == value) return; _startTime = value; NotifyChanged(); }
        }

        private DateTime? _endTime;

        /// <summary>
        /// Конец диапазона периодов (null — без ограничения).
        /// </summary>
        public DateTime? EndTime
        {
            get { return _endTime; }
            set { if (_endTime == value) return; _endTime = value; NotifyChanged(); }
        }

        /// <summary>
        /// Последний рассчитанный алгоритм.
        /// </summary>
        public CapitalAllocationAlgorithmType Algorithm = CapitalAllocationAlgorithmType.CorrelationClustering;

        private double _minCorrelation = 0.4;

        /// <summary>
        /// Порог корреляции для CorrelationClustering.
        /// </summary>
        public double MinCorrelation
        {
            get { return _minCorrelation; }
            set { if (_minCorrelation == value) return; _minCorrelation = value; NotifyChanged(); }
        }

        private LinkageMethod _linkageMethod = LinkageMethod.Single;

        /// <summary>
        /// Метод линковки для Hierarchical Risk Parity.
        /// </summary>
        public LinkageMethod LinkageMethod
        {
            get { return _linkageMethod; }
            set { if (_linkageMethod == value) return; _linkageMethod = value; NotifyChanged(); }
        }

        private CovarianceMethod _covarianceMethod = CovarianceMethod.Sample;

        /// <summary>
        /// Способ оценки ковариации для Hierarchical Risk Parity.
        /// </summary>
        public CovarianceMethod CovarianceMethod
        {
            get { return _covarianceMethod; }
            set { if (_covarianceMethod == value) return; _covarianceMethod = value; NotifyChanged(); }
        }

        private CorrelationType _correlationType = CorrelationType.Pearson;

        /// <summary>
        /// Тип корреляции для Hierarchical Risk Parity.
        /// </summary>
        public CorrelationType CorrelationType
        {
            get { return _correlationType; }
            set { if (_correlationType == value) return; _correlationType = value; NotifyChanged(); }
        }

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

            NotifyChanged();
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

            NotifyChanged();
        }

        /// <summary>
        /// Очистить список ботов.
        /// </summary>
        public void ClearBots()
        {
            Bots.Clear();
            ResetResult();
            NotifyChanged();
        }

        /// <summary>
        /// Сбросить результат расчёта (после смены ботов или неудачного расчёта),
        /// чтобы result_get/result_save не отдавали устаревший результат прошлого прогона.
        /// </summary>
        private void ResetResult()
        {
            Matrix = null;
            Weights = null;
            Clusters = null;
            Hrp = null;
        }

        /// <summary>
        /// Оповестить UI об изменении состояния. Сбой подписчика не должен ронять расчёт.
        /// </summary>
        private void NotifyChanged()
        {
            try
            {
                if (StateChangedEvent != null)
                {
                    StateChangedEvent();
                }
            }
            catch
            {
                // подписчик (UI) упал — состояние уже изменено, расчёт продолжается
            }
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
                ResetResult();
                NotifyChanged();
                return;
            }

            CorrelationClusteringAlgorithm clustering = new CorrelationClusteringAlgorithm(MinCorrelation);

            Weights = clustering.CalculateWeights(matrix);
            Clusters = clustering.Clusters;
            Hrp = null;
            Algorithm = CapitalAllocationAlgorithmType.CorrelationClustering;
            NotifyChanged();
        }

        /// <summary>
        /// Рассчитать Hierarchical Risk Parity.
        /// </summary>
        public void CalculateHierarchicalRiskParity()
        {
            ReturnsMatrix matrix = BuildMatrix();

            if (matrix == null)
            {
                ResetResult();
                NotifyChanged();
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
            NotifyChanged();
        }
    }
}
