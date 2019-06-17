/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Entity
{
    /// <summary>
    /// horizontal volumes
    /// горизонтальные объёмы
    /// </summary>
    public class HorizontalVolume
    {
        public HorizontalVolume(string name)
        {
            _name = name;
            Load();
        }

        private string _name;

        /// <summary>
        /// save the settings to a file
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"HorizontalVolumeSet.txt", false))
                {
                    writer.WriteLine(_lineStep);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// upload the settings from the file
        /// загрузить настройки из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"HorizontalVolumeSet.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"HorizontalVolumeSet.txt"))
                {
                    _lineStep = reader.ReadLine().ToDecimal();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// Delete the settings from the file
        /// удалить настройки из файла
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + _name + @"HorizontalVolumeSet.txt"))
            {
                File.Delete(@"Engine\" + _name + @"HorizontalVolumeSet.txt");
            }
        }
        // cluster calculation
        // расчёт кластеров

        /// <summary>
        /// compute the clusters
        /// просчитать кластеры
        /// </summary>
        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (_myCandles != null &&
                candles.Count < _myCandles.Count)
            {
                Clear();
            }

            if (candles.Count == 1 && candles[0].Trades.Count == 0)
            {
                return;
            }



            _myCandles = candles;

            int index = _lastCandlesCount - 2;

            if (index < 0)
            {
                index = 0;
            }

            for (int i = index; i < candles.Count; i++)
            {
                if (candles[i].Trades.Count == 0)
                {
                    continue;
                }

                // take the cluster from the storage, if not, then create a new
                // берём кластер из хранилища, если нет, то создаём новый

                if (candles[i].Trades == null ||
                    candles[i].Trades.Count == 0)
                {
                    continue;
                }

                HorizontalVolumeCluster cluster = VolumeClusters.Find(c => c.Time == candles[i].TimeStart);

                if (cluster == null)
                {
                    cluster = new HorizontalVolumeCluster(_lineStep, Security);
                    VolumeClusters.Add(cluster);
                    cluster.NumCluster = VolumeClusters.Count - 1;

                    cluster.Time = candles[i].TimeStart;
                    cluster.LogMessageEvent += SendNewLogMessage;
                    cluster.MaxSummLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MaxSummLineChangeEvent?.Invoke(line);
                    };
                    cluster.MaxBuyLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MaxBuyLineChangeEvent?.Invoke(line);
                    };
                    cluster.MaxSellLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MaxSellLineChangeEvent?.Invoke(line);
                    };
                    cluster.MaxDeltaLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MaxDeltaLineChangeEvent?.Invoke(line);
                    };

                    cluster.MinSummLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MinSummLineChangeEvent?.Invoke(line);
                    };
                    cluster.MinBuyLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MinBuyLineChangeEvent?.Invoke(line);
                    };
                    cluster.MinSellLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MinSellLineChangeEvent?.Invoke(line);
                    };
                    cluster.MinDeltaLineChangeEvent += delegate(HorizontalVolumeLine line)
                    {
                        MinDeltaLineChangeEvent?.Invoke(line);
                    };

                    cluster.NewLineCreateEvent += delegate(HorizontalVolumeLine line) { VolumeClusterLines.Add(line); };
                }
                // upload the cluster with data
                // прогружаем кластер данными

                cluster.Process(candles[i].Trades);
                // calculate the maximum / minimum clusters
                // рассчитываем максимальные / минимальные кластеры

                if (MaxSummVolumeCluster == null ||
                    cluster.MaxSummVolumeLine.VolumeSumm > MaxSummVolumeCluster.MaxSummVolumeLine.VolumeSumm)
                {
                    MaxSummVolumeCluster = cluster;
                    MaxSummClusterChangeEvent?.Invoke(cluster);
                }
                if (MinSummVolumeCluster == null ||
                    cluster.MinSummVolumeLine.VolumeSumm < MinSummVolumeCluster.MinSummVolumeLine.VolumeSumm)
                {
                    MinSummVolumeCluster = cluster;
                    MinSummClusterChangeEvent?.Invoke(cluster);
                }
                //
                if (MaxBuyVolumeCluster == null ||
                    cluster.MaxBuyVolumeLine.VolumeBuy > MaxBuyVolumeCluster.MaxBuyVolumeLine.VolumeBuy)
                {
                    MaxBuyVolumeCluster = cluster;
                    MaxBuyClusterChangeEvent?.Invoke(cluster);
                }
                if (MinBuyVolumeCluster == null ||
                    cluster.MinBuyVolumeLine.VolumeBuy < MinBuyVolumeCluster.MinBuyVolumeLine.VolumeBuy)
                {
                    MinBuyVolumeCluster = cluster;
                    MinBuyClusterChangeEvent?.Invoke(cluster);
                }
                //
                if (MaxSellVolumeCluster == null ||
                    cluster.MaxSellVolumeLine.VolumeSell > MaxSellVolumeCluster.MaxSellVolumeLine.VolumeSell)
                {
                    MaxSellVolumeCluster = cluster;
                    MaxSellClusterChangeEvent?.Invoke(cluster);
                }
                if (MinSellVolumeCluster == null ||
                    cluster.MinSellVolumeLine.VolumeSell < MinSellVolumeCluster.MinSellVolumeLine.VolumeSell)
                {
                    MinSellVolumeCluster = cluster;
                    MinSellClusterChangeEvent?.Invoke(cluster);
                }
                //
                if (MaxDeltaVolumeCluster == null ||
                    cluster.MaxDeltaVolumeLine.VolumeDelta > MaxDeltaVolumeCluster.MaxDeltaVolumeLine.VolumeDelta)
                {
                    MaxDeltaVolumeCluster = cluster;
                    MaxDeltaClusterChangeEvent?.Invoke(cluster);
                }
                if (MinDeltaVolumeCluster == null ||
                    cluster.MinDeltaVolumeLine.VolumeDelta < MinDeltaVolumeCluster.MinDeltaVolumeLine.VolumeDelta)
                {
                    MinDeltaVolumeCluster = cluster;
                    MinDeltaClusterChangeEvent?.Invoke(cluster);
                }
                //
                if (MaxPriceCluster == null ||
                    cluster.MaxPriceLine.Price > MaxPriceCluster.MaxPriceLine.Price)
                {
                    MaxPriceCluster = cluster;
                }
                if (MinPriceCluster == null ||
                    cluster.MinPriceLine.Price < MinPriceCluster.MinPriceLine.Price)
                {
                    MinPriceCluster = cluster;
                }
            }

            _lastCandlesCount = candles.Count;
        }

        /// <summary>
        /// the last number of candles processed
        /// последнее количество обработанных свечек
        /// </summary>
        private int _lastCandlesCount = 0;

        /// <summary>
        /// candlesticks that were used to calculate the volumes
        /// свечи по которым рассчитывались объёмы
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        ///   clear all data
        /// очистить все данные
        /// </summary>
        public void Clear()
        {
            VolumeClusters = new List<HorizontalVolumeCluster>();
            VolumeClusterLines = new List<HorizontalVolumeLine>();
            MaxSummVolumeCluster = null;
            MinSummVolumeCluster = null;
            MaxBuyVolumeCluster = null;
            MinBuyVolumeCluster = null;
            MaxSellVolumeCluster = null;
            MinSellVolumeCluster = null;
            MaxDeltaVolumeCluster = null;
            MinDeltaVolumeCluster = null;
            _lastCandlesCount = 0;
        }

        /// <summary>
        ///  volume update
        /// обновить объёмы
        /// </summary>
        public void Refresh()
        {
            Clear();
            Process(_myCandles);
        }

        /// <summary>
        /// line pitch in cluster
        /// шаг линии в кластере
        /// </summary>
        public decimal StepLine
        {
            get { return _lineStep; }
            set
            {
                if (_lineStep == value)
                {
                    return;
                }
                _lineStep = value;
                Refresh();
                Save();
            }
        }

        private decimal _lineStep;

        /// <summary>
        /// the paper on which we calculate the clusters
        /// бумага по которой рассчитываем кластеры
        /// </summary>
        public Security Security
        {
            get { return _security;}
            set
            {
                if (value == null)
                {
                    return;
                }
                if (_security != null &&
                    _security.Name == value.Name)
                {
                    return;
                }
                _security = value;
                Refresh();
            }
        }
        private Security _security;

        // Data storage
        // хранение данных

        /// <summary>
        /// volume clusters
        /// кластеры(колонки) объёмов
        /// </summary>
        public List<HorizontalVolumeCluster> VolumeClusters = new List<HorizontalVolumeCluster>();

        /// <summary>
        /// lines in volume clusters
        /// линии в кластерах обьёма
        /// </summary>
        public List<HorizontalVolumeLine> VolumeClusterLines = new List<HorizontalVolumeLine>();

        /// <summary>
        /// volume column with the maximum volume of all transactions
        /// столбец объёма с максимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MaxSummVolumeCluster;

        /// <summary>
        /// volume column with minimum volume of all transactions
        /// столбец объёма с минимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MinSummVolumeCluster;

        /// <summary>
        /// volume column with the maximum amount of purchases
        /// столбец объёма с максимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MaxBuyVolumeCluster;

        /// <summary>
        /// volume column with a minimum amount of purchases
        /// столбец объёма с минимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MinBuyVolumeCluster;

        /// <summary>
        /// volume column with maximum sales
        /// столбец объёма с максимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MaxSellVolumeCluster;

        /// <summary>
        /// volume column with minimum sales
        /// столбец объёма с минимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MinSellVolumeCluster;

        /// <summary>
        /// volume column with the maximum delta volume (purchases minus sales)
        /// столбец объёма с максимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MaxDeltaVolumeCluster;

        /// <summary>
        /// minimum volume delta volume column (purchases minus sales)
        /// столбец объёма с минимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MinDeltaVolumeCluster;

        /// <summary>
        /// maximum price slob
        /// слобец объёма с максимальной ценой
        /// </summary>
        public HorizontalVolumeCluster MaxPriceCluster;

        /// <summary>
        /// volume column with minimum price
        /// столбец объёма с минимальной ценой
        /// </summary>
        public HorizontalVolumeCluster MinPriceCluster;
        // data access methods
        // методы доступа к данным

        /// <summary>
        /// find cluster with maximum volume
        /// найти кластер с максимальным объёмом
        /// </summary>
        /// <param name="startIndex">starting index/стартовый индекс</param>
        /// <param name="endIndex">final index /конечный индекс</param>
        /// <param name="typeCluster">type of volume in the cluster/тип объёма в кластере</param>
        /// <returns></returns>
        public HorizontalVolumeCluster FindMaxVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            if (VolumeClusters == null ||
                VolumeClusters.Count == 0)
            {
                return null;
            }
            HorizontalVolumeCluster cluster = null;

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            for(int i = startIndex; VolumeClusters != null && i < endIndex && i < VolumeClusters.Count; i++)
            {
                if (VolumeClusters[i].MaxSummVolumeLine == null)
                {
                    continue;
                }
                if (typeCluster == ClusterType.SummVolume)
                {
                    if (cluster == null ||
                        cluster.MaxSummVolumeLine.VolumeSumm < VolumeClusters[i].MaxSummVolumeLine.VolumeSumm)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.BuyVolume)
                {
                    if (cluster == null ||
                        cluster.MaxBuyVolumeLine.VolumeBuy < VolumeClusters[i].MaxBuyVolumeLine.VolumeBuy)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.SellVolume)
                {
                    if (cluster == null ||
                        cluster.MaxSellVolumeLine.VolumeSell < VolumeClusters[i].MaxSellVolumeLine.VolumeSell)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.DeltaVolume)
                {
                    if (cluster == null ||
                        cluster.MaxDeltaVolumeLine.VolumeDelta < VolumeClusters[i].MaxDeltaVolumeLine.VolumeDelta)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
            }

            return cluster;
        }

        /// <summary>
        ///  найти кластер с минимальный объёмом
        /// найти кластер с минимальный объёмом
        /// </summary>
        /// <param name="startIndex">starting index/стартовый индекс</param>
        /// <param name="endIndex">final index /конечный индекс</param>
        /// <param name="typeCluster">type of volume in the cluster/тип объёма в кластере</param>
        /// <returns></returns>
        public HorizontalVolumeCluster FindMinVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            if (VolumeClusters == null ||
                VolumeClusters.Count == 0)
            {
                return null;
            }
            HorizontalVolumeCluster cluster = null;

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            for (int i = startIndex; VolumeClusters != null && i < endIndex && i < VolumeClusters.Count; i++)
            {
                if (VolumeClusters[i].MinSummVolumeLine == null)
                {
                    continue;
                }
                if (typeCluster == ClusterType.SummVolume)
                {
                    if (cluster == null ||
                        cluster.MinSummVolumeLine.VolumeSumm > VolumeClusters[i].MinSummVolumeLine.VolumeSumm)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.BuyVolume)
                {
                    if (cluster == null ||
                        cluster.MinBuyVolumeLine.VolumeBuy > VolumeClusters[i].MinBuyVolumeLine.VolumeBuy)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.SellVolume)
                {
                    if (cluster == null ||
                        cluster.MinSellVolumeLine.VolumeSell > VolumeClusters[i].MinSellVolumeLine.VolumeSell)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
                else if (typeCluster == ClusterType.DeltaVolume)
                {
                    if (cluster == null ||
                        cluster.MinDeltaVolumeLine.VolumeDelta > VolumeClusters[i].MinDeltaVolumeLine.VolumeDelta)
                    {
                        cluster = VolumeClusters[i];
                    }
                }
            }

            return cluster;
        }
        // outgoing events
        // исходящие события

        /// <summary>
        /// the cluster has changed with the maximum total volume
        /// изменился кластер с максимальным суммарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSummClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total amount of purchases has changed
        /// изменился кластер с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxBuyClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total sales volume has changed
        /// изменился кластер с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSellClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total volume by delta has changed (purchases - sales)
        /// изменился кластер с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxDeltaClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with minimal total volume
        /// изменился кластер с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSummClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with minimal total purchases
        /// изменился кластер с минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinBuyClusterChangeEvent;

        /// <summary>
        /// cluster change with minimal total sales
        /// изменился кластер с минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSellClusterChangeEvent;

        /// <summary>
        /// the cluster with the minimum total volume of purchases and sales in the delta has changed
        /// изменился кластер с минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinDeltaClusterChangeEvent;

        /// <summary>
        /// the volume line with the maximum total volume has changed
        /// изменилась линия объёма с максимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSummLineChangeEvent;

        /// <summary>
        /// the volume line with the maximum total volume of purchases has changed
        /// изменилась линия объёма с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxBuyLineChangeEvent;

        /// <summary>
        /// The volume line with the maximum total sales volume has changed
        /// изменилась линия объёма с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSellLineChangeEvent;

        /// <summary>
        /// the volume line with the maximum total volume in the delta (buying/selling) has changed
        /// изменилась линия объёма с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxDeltaLineChangeEvent;

        /// <summary>
        /// the volume line has changed with minimal total volume
        /// изменилась линия объёма с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSummLineChangeEvent;

        /// <summary>
        /// the line of volume has changed with the minimum total volume of purchases
        /// изменилась линия объёма  минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MinBuyLineChangeEvent;

        /// <summary>
        /// the minimum total sales line changed
        /// изменилась линия объёма  минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSellLineChangeEvent;

        /// <summary>.
        /// the volume line with the minimum total volume of the delta has changed (purchases - sales)
        /// изменилась линия объёма  минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MinDeltaLineChangeEvent;
        // logging
        // логирование

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// message for log
        /// сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// horizontal volume column
    /// столбец в горизонтальных обьёмах
    /// </summary>
    public class HorizontalVolumeCluster
    {
        public HorizontalVolumeCluster(decimal stepLines,Security security)
        {
            StepLines = stepLines;
            Decimals = security.Decimals;

            if (security.Decimals > 0)
            {

                StepSecurity = 0.1m;

                for (int i = 1; i < security.Decimals; i++)
                {
                    StepSecurity = StepSecurity * 0.1m;
                }
            }
            else
            {
                StepSecurity = 1;
            }
        }

        /// <summary>
        /// line spacing
        /// шаг между линиями
        /// </summary>
        public decimal StepLines;

        /// <summary>
        /// minimum price step of the instrument
        /// минимальный шаг цены инструмента
        /// </summary>
        public decimal StepSecurity;

        /// <summary>
        /// a number of simbols after comma
        /// количество знаков после запятой
        /// </summary>
        public int Decimals;

        /// <summary>
        /// horizontal volume lines. 0 - with a minimum price
        /// линии горизонтального объёма. 0 - с минимальной ценой
        /// </summary>
        public List<HorizontalVolumeLine> Lines
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _lines;
            }
        }
        List<HorizontalVolumeLine> _lines = new List<HorizontalVolumeLine>();

        /// <summary>
        /// last updated line
        /// последняя обновлённая линия
        /// </summary>
        public HorizontalVolumeLine LastUpdateLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _lastUpdateLine;
            }
        }
        private HorizontalVolumeLine _lastUpdateLine;

        /// <summary>
        /// volume line with the maximum volume of all transactions
        /// линия объёма с максимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeLine MaxSummVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _maxVolumeSummLine;
            }
        }
        private HorizontalVolumeLine _maxVolumeSummLine;

        /// <summary>
        /// volume line with the minimum volume of all transactions
        /// линия объёма с минимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeLine MinSummVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _minVolumeSummLine;
            }
        }
        private HorizontalVolumeLine _minVolumeSummLine;

        /// <summary>
        /// volume line with the maximum amount of purchases
        /// линия объёма с максимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeLine MaxBuyVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _maxVolumeBuyLine;
            }
        }
        private HorizontalVolumeLine _maxVolumeBuyLine;

        /// <summary>
        /// volume line with a minimum amount of purchases
        /// линия объёма с минимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeLine MinBuyVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _minVolumeBuyLine;
            }
        }
        private HorizontalVolumeLine _minVolumeBuyLine;

        /// <summary>
        /// volume line with maximum sales
        /// линия объёма с максимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeLine MaxSellVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _maxVolumeSellLine;
            }
        }
        private HorizontalVolumeLine _maxVolumeSellLine;

        /// <summary>
        /// volume line with minimum sales
        /// линия объёма с минимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeLine MinSellVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _minVolumeSellLine;
            }
        }
        private HorizontalVolumeLine _minVolumeSellLine;

        /// <summary>
        /// volume line with maximum delta volume (purchases minus sales)
        /// линия объёма с максимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeLine MaxDeltaVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _maxVolumeDeltaLine;
            }
        }
        private HorizontalVolumeLine _maxVolumeDeltaLine;

        /// <summary>
        /// volume line with a minimum delta volume (purchases minus sales)
        /// линия объёма с минимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeLine MinDeltaVolumeLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _minVolumeDeltaLine;
            }
        }
        private HorizontalVolumeLine _minVolumeDeltaLine;

        /// <summary>
        /// volume line with maximum price
        /// линия объёма с максимальной ценой
        /// </summary>
        public HorizontalVolumeLine MaxPriceLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _maxPriceLine;
            }
        }
        private HorizontalVolumeLine _maxPriceLine;

        /// <summary>
        /// volume line with minimum price
        /// линия объёма с минимальной ценой
        /// </summary>
        public HorizontalVolumeLine MinPriceLine
        {
            get
            {
                if (_neadToRebuidVolume)
                {
                    ReloadLines();
                }
                return _minPriceLine;
            }
        }
        private HorizontalVolumeLine _minPriceLine;

        /// <summary>
        /// time of the candle by which the volume was built
        /// время свечи, по которой строился объём
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// cluster number in the storage of horizontal volumes
        /// номер кластера в хранилище горизонтальных объёмов
        /// </summary>
        public int NumCluster;
        // calculation of volumes
        // расчёт объёмов

        /// <summary>
        /// multi-threaded trade array blocker for updating
        /// блокировщик многопоточного доступа к массиву трейдов для обновления
        /// </summary>
        public object _tradesArrayLocker = new object();

        /// <summary>
        /// update trades by volume
        /// обновить трейды по объёмам
        /// </summary>
        public void Process(List<Trade> trades)
        {
            if (_tradesCount == trades.Count)
            {
                return;
            }

            if (trades.Count < _tradesCount)
            {
                return;
            }

            lock (_tradesArrayLocker)
            {
                _tradesNew.AddRange(trades.GetRange(_tradesCount, trades.Count - _tradesCount));
                _tradesCount = trades.Count;
            }

            _neadToRebuidVolume = true;
        }

        /// <summary>
        /// flag. Do I need to rebuild obmy before sending data to the top
        /// флаг. Нужно ли перестраивать объмы перед отправкой данных на верх
        /// </summary>
        private bool _neadToRebuidVolume;

        /// <summary>
        /// number of trades already accounted for
        /// количество трейдов уже учтённых в объёмах
        /// </summary>
        private int _tradesCount = 0;

        /// <summary>
        /// trades not accounted for in volumes
        /// не учтёные в объёмах трейды
        /// </summary>
        private List<Trade> _tradesNew = new List<Trade>();

        /// <summary>
        /// to align the price of the trade in a cluster step
        /// выровнять цену трейда по шагу кластера
        /// </summary>
        private decimal Round(decimal price)
        {
            if (StepLines == 0 || StepSecurity == 0)
            {
                return price;
            }

            while (price % StepLines != 0)
            {
                price -= StepSecurity;
            }

            return price;
        }

        /// <summary>
        /// volume updating method not considered trades
        /// метод обновляющий объёмы не учтёными трейдами
        /// </summary>
        private void ReloadLines()
        {
            _neadToRebuidVolume = false;

            lock (_tradesArrayLocker)
            {
                try
                {
                    for (int i = 0; i < _tradesNew.Count; i++)
                    {
                        HorizontalVolumeLine curLine = null;

                        decimal linePrice = Round(_tradesNew[i].Price);

                        curLine = _lines.Find(l => l.Price == linePrice);

                        if (curLine == null)
                        {
                            curLine = new HorizontalVolumeLine();
                            curLine.TimeFirstTrade = _tradesNew[i].Time;
                            curLine.Price = linePrice;
                            curLine.TimeCandle = Time;
                            curLine.NumCluster = NumCluster;

                            for (int i2 = 0; i2 < _lines.Count; i2++)
                            {
                                if (curLine.Price < _lines[i2].Price)
                                {
                                    _lines.Insert(i2, curLine);
                                    break;
                                }
                                if (i2 + 1 == _lines.Count)
                                {
                                    _lines.Add(curLine);
                                    break;
                                }
                            }

                            if (_lines.Count == 0)
                            {
                                _lines.Add(curLine);
                            }

                            if (NewLineCreateEvent != null)
                            {
                                NewLineCreateEvent(curLine);
                            }
                        }

                        curLine.TimeLastTrade = _tradesNew[i].Time;
                        curLine.Trades.Add(_tradesNew[i]);
                        curLine.VolumeSumm += _tradesNew[i].Volume;

                        _lastUpdateLine = curLine;

                        if (_tradesNew[i].Side == Side.Buy)
                        {
                            curLine.VolumeBuy += _tradesNew[i].Volume;
                            curLine.VolumeDelta += _tradesNew[i].Volume;
                        }
                        else if (_tradesNew[i].Side == Side.Sell)
                        {
                            curLine.VolumeSell += _tradesNew[i].Volume;
                            curLine.VolumeDelta -= _tradesNew[i].Volume;
                        }

                        //
                        if (_maxVolumeSummLine == null ||
                            curLine.VolumeSumm > _maxVolumeSummLine.VolumeSumm)
                        {
                            _maxVolumeSummLine = curLine;

                            MaxSummLineChangeEvent?.Invoke(_maxVolumeSummLine);
                        }

                        if (_minVolumeSummLine == null ||
                            curLine.VolumeSumm < _minVolumeSummLine.VolumeSumm)
                        {
                            _minVolumeSummLine = curLine;
                            MinSummLineChangeEvent?.Invoke(_minVolumeSummLine);
                        }
                        //
                        if (_maxVolumeBuyLine == null ||
                            curLine.VolumeBuy > _maxVolumeBuyLine.VolumeBuy)
                        {
                            _maxVolumeBuyLine = curLine;
                            MaxBuyLineChangeEvent?.Invoke(_maxVolumeBuyLine);
                        }

                        if (_minVolumeBuyLine == null ||
                            curLine.VolumeBuy < _minVolumeBuyLine.VolumeBuy)
                        {
                            _minVolumeBuyLine = curLine;
                            MinBuyLineChangeEvent?.Invoke(_minVolumeBuyLine);
                        }
                        //
                        if (_maxVolumeSellLine == null ||
                            curLine.VolumeSell > _maxVolumeSellLine.VolumeSell)
                        {
                            _maxVolumeSellLine = curLine;
                            MaxSellLineChangeEvent?.Invoke(_maxVolumeSellLine);
                        }
                        if (_minVolumeSellLine == null ||
                            curLine.VolumeSell < _minVolumeSellLine.VolumeSell)
                        {
                            _minVolumeSellLine = curLine;
                            MinSellLineChangeEvent?.Invoke(_minVolumeSellLine);
                        }
                        //
                        if (_maxVolumeDeltaLine == null ||
                            curLine.VolumeDelta > _maxVolumeDeltaLine.VolumeDelta)
                        {
                            _maxVolumeDeltaLine = curLine;
                            MaxDeltaLineChangeEvent?.Invoke(_maxVolumeDeltaLine);
                        }
                        if (_minVolumeDeltaLine == null ||
                            curLine.VolumeDelta < _minVolumeDeltaLine.VolumeDelta)
                        {
                            _minVolumeDeltaLine = curLine;
                            MinDeltaLineChangeEvent?.Invoke(_minVolumeDeltaLine);
                        }
                        //
                        if (_maxPriceLine == null ||
                            curLine.Price > _maxPriceLine.Price)
                        {
                            _maxPriceLine = curLine;
                        }
                        if (_minPriceLine == null ||
                            curLine.Price < _minPriceLine.Price)
                        {
                            _minPriceLine = curLine;
                        }
                    }
                    _tradesNew.Clear();
                }
                catch (Exception e)
                {
                    SendNewLogMessage(e.ToString(),LogMessageType.Error);
                }
              
            }
        }
        // outgoing events
        // исходящие события

        public event Action<HorizontalVolumeLine> NewLineCreateEvent;

        /// <summary>
        /// the volume line was replaced with the maximum total volume
        /// изменилась линия объёма с максимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSummLineChangeEvent;

        /// <summary>
        /// the volume line with the maximum total volume of purchases has changed
        /// изменилась линия объёма с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxBuyLineChangeEvent;

        /// <summary>
        /// volume line has changed with the maximum total sales volume
        /// изменилась линия объёма с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSellLineChangeEvent;

        /// <summary>
        /// the volume line with the maximum total sales volume changed the volume line with the maximum total volume by delta changed
        /// изменилась линия объёма с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxDeltaLineChangeEvent;

        /// <summary>
        /// the volume line has changed with the minimum total volume
        /// изменилась линия объёма с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSummLineChangeEvent;

        /// <summary>
        /// the volume line was changed by the minimum total amount of purchases
        /// изменилась линия объёма  минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MinBuyLineChangeEvent;

        /// <summary>
        /// the volume line has changed with a minimum total sales volume
        /// изменилась линия объёма  минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSellLineChangeEvent;

        /// <summary>
        /// the volume line with the minimum total volume of the delta has changed (purchases - sales)
        /// изменилась линия объёма  минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MinDeltaLineChangeEvent;
        // logging
        // логирование

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// message for log
        /// сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// one line of horizontal volume
    /// одна линия горизонтального объёма
    /// </summary>
    public class HorizontalVolumeLine
    {
        /// <summary>
        /// volume Total amount of trades taken
        /// объём. Общая сумма учтёных трейдов
        /// </summary>
        public decimal VolumeSumm;

        /// <summary>
        /// volume The amount of purchases in the recorded trades
        /// объём. Сумма покупок в учтёных трейдах
        /// </summary>
        public decimal VolumeBuy;

        /// <summary>
        /// volume Amount of sales in the recorded trades
        /// объём. Сумма продаж в учтёных трейдах
        /// </summary>
        public decimal VolumeSell;

        /// <summary>
        /// volume Delta (amount of purchases minus the amount of sales)
        /// объём. Дельта (сумма объёма покупок минус сумма объёма продаж)
        /// </summary>
        public decimal VolumeDelta;

        /// <summary>
        /// take the volume according to its type
        /// взять объём по его типу 
        /// </summary>
        /// <param name="clusterType"></param>
        /// <returns>volume type/тип объёма</returns>
        public decimal GetVolume(ClusterType clusterType)
        {
            if (clusterType == ClusterType.SummVolume)
            {
                return VolumeSumm;
            }
            else if (clusterType == ClusterType.BuyVolume)
            {
                return VolumeBuy;
            }
            else if (clusterType == ClusterType.SellVolume)
            {
                return VolumeSell;
            }
            else //if (clusterType == ClusterType.DeltaVolume)
            {
                return VolumeDelta;
            }
        }

        /// <summary>
        /// price level
        /// цена уровня
        /// </summary>
        public decimal Price;

        /// <summary>
        /// line trades
        /// трейды линии
        /// </summary>
        public List<Trade> Trades = new List<Trade>();

        /// <summary>
        /// candle time
        /// время свечи
        /// </summary>
        public DateTime TimeCandle;

        /// <summary>
        /// time of the first trade
        /// время первого трейда
        /// </summary>
        public DateTime TimeFirstTrade;

        /// <summary>
        /// time of the last trade
        /// время последнего трейда
        /// </summary>
        public DateTime TimeLastTrade;

        /// <summary>
        /// take a line with captions
        /// взять строку с подписями
        /// </summary>
        public string ToolTip
        {
            //Date - 20131001 Time - 100000 
            // Open - 97.8000000 High - 97.9900000 Low - 97.7500000 Close - 97.9000000
            get
            {

                StringBuilder result = new StringBuilder();

                if (TimeCandle.Day > 9)
                {
                    result.Append(TimeCandle.Day);
                }
                else
                {
                    result.Append("0" + TimeCandle.Day);
                }

                result.Append(".");

                if (TimeCandle.Month > 9)
                {
                    result.Append(TimeCandle.Month);
                }
                else
                {
                    result.Append("0" + TimeCandle.Month);
                }

                result.Append(".");
                result.Append(TimeCandle.Year.ToString());

                result.Append(" ");

                if (TimeCandle.Hour > 9)
                {
                    result.Append(TimeCandle.Hour.ToString());
                }
                else
                {
                    result.Append("0" + TimeCandle.Hour);
                }

                result.Append(":");

                if (TimeCandle.Minute > 9)
                {
                    result.Append(TimeCandle.Minute.ToString());
                }
                else
                {
                    result.Append("0" + TimeCandle.Minute);
                }

                result.Append(":");

                if (TimeCandle.Second > 9)
                {
                    result.Append(TimeCandle.Second.ToString());
                }
                else
                {
                    result.Append("0" + TimeCandle.Second);
                }

                result.Append("  \r\n");
                result.Append(" Price: ");
                result.Append(Price.ToString(new CultureInfo("ru-RU")));
                result.Append("  \r\n");

                result.Append(" Summ: ");
                result.Append(VolumeSumm.ToString(new CultureInfo("ru-RU")));
                result.Append(" Delta: ");
                result.Append(VolumeDelta.ToString(new CultureInfo("ru-RU")));
                result.Append("  \r\n");

                result.Append(" Buy: ");
                result.Append(VolumeBuy.ToString(new CultureInfo("ru-RU")));
                result.Append(" Sell: ");
                result.Append(VolumeSell.ToString(new CultureInfo("ru-RU")));

                return result.ToString();
            }
        }

        /// <summary>
        /// cluster number in which the line is located
        /// номер кластера в котором находится линия
        /// </summary>
        public int NumCluster;
    }
}
