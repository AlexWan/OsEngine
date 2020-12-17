/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts.ClusterChart;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// tab creating and drawing cluster graph /
    /// вкладка создающая и прорисовывающая кластерный график
    /// </summary>
    public class BotTabCluster : IIBotTab
    {
        /// <summary>
        /// constructor /
        /// конструктор
        /// </summary>
        /// <param name="name">bot name / имя робота</param>
        /// <param name="startProgram">class creating program / программа создающая класс</param>
        public BotTabCluster(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            CandleConnector = new ConnectorCandles(name, _startProgram);
            CandleConnector.SaveTradesInCandles = true;
            CandleConnector.LastCandlesChangeEvent += Tab_LastCandlesChangeEvent;
            CandleConnector.SecuritySubscribeEvent += CandleConnector_SecuritySubscribeEvent;
            CandleConnector.LogMessageEvent += SendNewLogMessage;

            _horizontalVolume = new HorizontalVolume(name);

            _horizontalVolume.MaxSummClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MaxSummClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxBuyClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MaxBuyClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxSellClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MaxSellClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxDeltaClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MaxDeltaClusterChangeEvent?.Invoke(line);
            };

            _horizontalVolume.MinSummClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MinSummClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinBuyClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MinBuyClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinSellClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MinSellClusterChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinDeltaClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                MinDeltaClusterChangeEvent?.Invoke(line);
            };


            _horizontalVolume.MaxSummLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MaxSummLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxBuyLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MaxBuyLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxSellLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MaxSellLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MaxDeltaLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MaxDeltaLineChangeEvent?.Invoke(line);
            };

            _horizontalVolume.MinSummLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MinSummLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinBuyLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MinBuyLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinSellLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MinSellLineChangeEvent?.Invoke(line);
            };
            _horizontalVolume.MinDeltaLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                MinDeltaLineChangeEvent?.Invoke(line);
            };

            _chartMaster = new ChartClusterMaster(name, startProgram,_horizontalVolume);
            _chartMaster.LogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// The connector is connected to a new instrument /
        /// коннектор подключился к новому инструменту
        /// </summary>
        private void CandleConnector_SecuritySubscribeEvent(Security newSecurity)
        {
            _horizontalVolume.Security = CandleConnector.Security;
        }

        /// <summary>
        /// tab name /
        /// имя вкладки
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// tab number /
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// line step value / 
        /// шаг для линий в кластере
        /// </summary>
        public decimal LineStep
        {
            get { return _horizontalVolume.StepLine; }
            set
            {
                _horizontalVolume.StepLine = value;
                _chartMaster.Refresh();
            }
        }

        /// <summary>
        /// chart type
        /// тип чарта
        /// </summary>
        public ClusterType ChartType
        {
            get { return _chartMaster.ChartType; }
            set
            {
                _chartMaster.ChartType = value;
            }
        }

        /// <summary>
        /// class creating program / 
        /// программа создавшая робота
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// security
        /// Инструмент по которому мы строим кластеры
        /// </summary>
        public ConnectorCandles CandleConnector;

        /// <summary>
        /// chart /
        /// чарт
        /// </summary>
        private ChartClusterMaster _chartMaster;

        /// <summary>
        /// volumes /
        /// горизонтальные объёмы
        /// </summary>
        private HorizontalVolume _horizontalVolume;

        /// <summary>
        /// delete /
        /// удалить
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();
            _horizontalVolume.Delete();
            CandleConnector.Delete();
        }

        /// <summary>
        /// clear /
        /// очистить
        /// </summary>
        public void Clear()
        {
            _horizontalVolume.Clear();
            _chartMaster.Clear();
        }

// control / управление

        /// <summary>
        /// settings gui
        /// вызвать окно управления
        /// </summary>
        public void ShowDialog()
        {
            BotTabClusterUi ui = new BotTabClusterUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        ///  call the window to connect candles /
        /// вызвать окно подключения свечек
        /// </summary>
        public void ShowCandlesDialog()
        {
            CandleConnector.ShowDialog(false);
        }
       
        /// <summary>
        /// stop drawing / 
        /// остановить прорисовку
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// start drawing this robot / 
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(host, rectangle);
        }

        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// the last candle has changed / 
        /// изменилась последняя свеча
        /// </summary>
        private void Tab_LastCandlesChangeEvent(List<Candle> candles)
        {
            LastTimeCandleUpdate = DateTime.Now;
            _horizontalVolume.Process(candles);
            _chartMaster.Process(_horizontalVolume);
        }

// data request / запрос данных

        /// <summary>
        /// volume columns
        /// колонки объёмов
        /// </summary>
        public List<HorizontalVolumeCluster> VolumeClusters
        {
            get { return _horizontalVolume.VolumeClusters; }
        }

        /// <summary>
        /// volume column with the maximum volume of all transactions / 
        /// столбец объёма с максимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MaxSummVolumeCluster
        {
            get { return _horizontalVolume.MaxSummVolumeCluster; }
        }

        /// <summary>
        /// volume column with the minimum volume of all transactions / 
        /// столбец объёма с минимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MinSummVolumeCluster
        {
            get { return _horizontalVolume.MinSummVolumeCluster; }
        }

        /// <summary>
        /// volume column with the maximum amount of buy /
        /// столбец объёма с максимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MaxBuyVolumeCluster
        {
            get { return _horizontalVolume.MaxBuyVolumeCluster; }
        }

        /// <summary>
        /// volume column with a minimum amount of buy / 
        /// столбец объёма с минимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MinBuyVolumeCluster
        {
            get { return _horizontalVolume.MinBuyVolumeCluster; }
        }

        /// <summary>
        /// volume column with maximum sales / 
        /// столбец объёма с максимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MaxSellVolumeCluster
        {
            get { return _horizontalVolume.MaxSellVolumeCluster; }
        }

        /// <summary>
        /// volume column with minimum sales / 
        /// столбец объёма с минимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MinSellVolumeCluster
        {
            get { return _horizontalVolume.MinSellVolumeCluster; }
        }

        /// <summary>
        /// volume column with the maximum delta volume (purchases minus sales) / 
        /// столбец объёма с максимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MaxDeltaVolumeCluster
        {
            get { return _horizontalVolume.MaxDeltaVolumeCluster; }
        }

        /// <summary>
        /// minimum volume delta volume column (purchases minus sales) / 
        /// столбец объёма с минимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MinDeltaVolumeCluster
        {
            get { return _horizontalVolume.MinDeltaVolumeCluster; }
        }

// data access methods / методы доступа к данным

        /// <summary>
        /// find cluster with maximum volume / 
        /// найти кластер с максимальным объёмом
        /// </summary>
        /// <param name="startIndex">start index / стартовый индекс</param>
        /// <param name="endIndex">end index / конечный индекс</param>
        /// <param name="typeCluster">type cluster / тип объёма в кластере</param>
        public HorizontalVolumeCluster FindMaxVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMaxVolumeCluster(startIndex, endIndex, typeCluster);
        }

        /// <summary>
        /// find cluster with minimum volume / 
        /// найти кластер с минимальный объёмом
        /// </summary>
        /// <param name="startIndex">start index / стартовый индекс</param>
        /// <param name="endIndex">end index / конечный индекс</param>
        /// <param name="typeCluster">type cluster / тип объёма в кластере</param>
        public HorizontalVolumeCluster FindMinVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMinVolumeCluster(startIndex, endIndex, typeCluster);
        }

// outgoing events / исходящие события

        /// <summary>
        /// the cluster has changed with the maximum total volume / 
        /// изменился кластер с максимальным суммарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSummClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total amount of buy has changed / 
        /// изменился кластер с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxBuyClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total sales volume has changed /
        /// изменился кластер с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSellClusterChangeEvent;

        /// <summary>
        /// the cluster with the maximum total volume by delta has changed (purchases - sales) / 
        /// изменился кластер с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxDeltaClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with the minimum total volume / 
        /// изменился кластер с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSummClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with the minimum total amount of buy /
        /// изменился кластер с минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinBuyClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with a minimum total sales volume / 
        /// изменился кластер с минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSellClusterChangeEvent;

        /// <summary>
        /// the cluster has changed with the minimum total volume by delta (purchases - sales) /
        /// изменился кластер с минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinDeltaClusterChangeEvent;

        /// <summary>
        /// volume line with maximum total volume changed /
        /// изменилась линия объёма с максимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSummLineChangeEvent;

        /// <summary>
        /// the volume line with the maximum total volume of buy has changed / 
        /// изменилась линия объёма с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxBuyLineChangeEvent;

        /// <summary>
        /// volume line has changed with the maximum total sales volume / 
        /// изменилась линия объёма с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSellLineChangeEvent;

        /// <summary>
        /// volume line changed with the maximum total volume of the delta (buy - sale) / 
        /// изменилась линия объёма с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxDeltaLineChangeEvent;

        /// <summary>
        /// the volume line has changed with the minimum total volume / 
        /// изменилась линия объёма с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSummLineChangeEvent;

        /// <summary>
        /// the volume line was changed by the minimum total amount of buy
        /// изменилась линия объёма  минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MinBuyLineChangeEvent;

        /// <summary>
        /// the volume line has changed with a minimum total sales volume
        /// изменилась линия объёма  минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSellLineChangeEvent;

        /// <summary>
        /// the volume line with the minimum total volume of the delta has changed (purchases - sales) / 
        /// изменилась линия объёма  минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MinDeltaLineChangeEvent;

// log / логирование

        /// <summary>
        /// send new log message / 
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
        /// log message
        /// сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// get chart
        /// взять чарт
        /// </summary>
        public Chart GetChart()
        {
            return _chartMaster.GetChart();
        }
    }

    /// <summary>
    ///  cluster display type / 
    /// тип отображения кластеров
    /// </summary>
    public enum ClusterType
    {
        /// <summary>
        /// by total volume /
        /// по суммарному объёму
        /// </summary>
        SummVolume,

        /// <summary>
        /// buy volume /
        /// по объёму покупок
        /// </summary>
        BuyVolume,

        /// <summary>
        /// sell volume /
        /// по объёму продаж
        /// </summary>
        SellVolume,

        /// <summary>
        /// by delta volume (purchase - sale) / 
        /// по объёму дельты (покупка - продажи)
        /// </summary>
        DeltaVolume
    }
}
