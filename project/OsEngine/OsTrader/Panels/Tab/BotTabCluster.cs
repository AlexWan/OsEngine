/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// вкладка создающая и прорисовывающая кластерный график
    /// </summary>
    public class BotTabCluster : IIBotTab
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name">имя робота</param>
        /// <param name="startProgram">программа создающая класс</param>
        public BotTabCluster(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            CandleConnector = new ConnectorCandles(name, _startProgram);
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
        /// коннектор подключился к новому инструменту
        /// </summary>
        private void CandleConnector_SecuritySubscribeEvent(Security newSecurity)
        {
            _horizontalVolume.Security = CandleConnector.Security;
        }

        /// <summary>
        /// имя вкладки
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
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
        /// программа создавшая робота
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// Инструмент по которому мы строим кластеры
        /// </summary>
        public ConnectorCandles CandleConnector;

        /// <summary>
        /// чарт
        /// </summary>
        private ChartClusterMaster _chartMaster;

        /// <summary>
        /// горизонтальные объёмы
        /// </summary>
        private HorizontalVolume _horizontalVolume;

        /// <summary>
        /// удалить
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();
            _horizontalVolume.Delete();
            CandleConnector.Delete();
        }

        /// <summary>
        /// очистить
        /// </summary>
        public void Clear()
        {
            _horizontalVolume.Clear();
            _chartMaster.Clear();
        }

// управление

        /// <summary>
        /// вызвать окно управления
        /// </summary>
        public void ShowDialog()
        {
            BotTabClusterUi ui = new BotTabClusterUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вызвать окно подключения свечек
        /// </summary>
        public void ShowCandlesDialog()
        {
            CandleConnector.ShowDialog();
        }
       
        /// <summary>
        /// остановить прорисовку
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(host, rectangle);
        }

        /// <summary>
        /// изменилась последняя свеча
        /// </summary>
        private void Tab_LastCandlesChangeEvent(List<Candle> candles)
        {
            _horizontalVolume.Process(candles);
            _chartMaster.Process(_horizontalVolume);
        }

// запрос данных

        /// <summary>
        /// колонки объёмов
        /// </summary>
        public List<HorizontalVolumeCluster> VolumeClusters
        {
            get { return _horizontalVolume.VolumeClusters; }
        }

        /// <summary>
        /// столбец объёма с максимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MaxSummVolumeCluster
        {
            get { return _horizontalVolume.MaxSummVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с минимальным объёмом всех сделок
        /// </summary>
        public HorizontalVolumeCluster MinSummVolumeCluster
        {
            get { return _horizontalVolume.MinSummVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с максимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MaxBuyVolumeCluster
        {
            get { return _horizontalVolume.MaxBuyVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с минимальным объёмом покупок
        /// </summary>
        public HorizontalVolumeCluster MinBuyVolumeCluster
        {
            get { return _horizontalVolume.MinBuyVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с максимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MaxSellVolumeCluster
        {
            get { return _horizontalVolume.MaxSellVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с минимальным объёмом продаж
        /// </summary>
        public HorizontalVolumeCluster MinSellVolumeCluster
        {
            get { return _horizontalVolume.MinSellVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с максимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MaxDeltaVolumeCluster
        {
            get { return _horizontalVolume.MaxDeltaVolumeCluster; }
        }

        /// <summary>
        /// столбец объёма с минимальным объёмом по дельте (покупки минус продажи)
        /// </summary>
        public HorizontalVolumeCluster MinDeltaVolumeCluster
        {
            get { return _horizontalVolume.MinDeltaVolumeCluster; }
        }

// методы доступа к данным

        /// <summary>
        /// найти кластер с максимальным объёмом
        /// </summary>
        /// <param name="startIndex">стартовый индекс</param>
        /// <param name="endIndex">конечный индекс</param>
        /// <param name="typeCluster">тип объёма в кластере</param>
        /// <returns></returns>
        public HorizontalVolumeCluster FindMaxVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMaxVolumeCluster(startIndex, endIndex, typeCluster);
        }

        /// <summary>
        /// найти кластер с минимальный объёмом
        /// </summary>
        /// <param name="startIndex">стартовый индекс</param>
        /// <param name="endIndex">конечный индекс</param>
        /// <param name="typeCluster">тип объёма в кластере</param>
        /// <returns></returns>
        public HorizontalVolumeCluster FindMinVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMinVolumeCluster(startIndex, endIndex, typeCluster);
        }

// исходящие события

        /// <summary>
        /// изменился кластер с максимальным суммарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSummClusterChangeEvent;

        /// <summary>
        /// изменился кластер с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxBuyClusterChangeEvent;

        /// <summary>
        /// изменился кластер с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSellClusterChangeEvent;

        /// <summary>
        /// изменился кластер с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxDeltaClusterChangeEvent;

        /// <summary>
        /// изменился кластер с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSummClusterChangeEvent;

        /// <summary>
        /// изменился кластер с минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinBuyClusterChangeEvent;

        /// <summary>
        /// изменился кластер с минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSellClusterChangeEvent;

        /// <summary>
        /// изменился кластер с минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinDeltaClusterChangeEvent;

        /// <summary>
        /// изменилась линия объёма с максимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSummLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма с максимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxBuyLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма с максимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSellLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма с максимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxDeltaLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма с минимальным сумарным объёмом
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSummLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма  минимальным сумарным объёмом покупок
        /// </summary>
        public event Action<HorizontalVolumeLine> MinBuyLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма  минимальным сумарным объёмом продаж
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSellLineChangeEvent;

        /// <summary>
        /// изменилась линия объёма  минимальным сумарным объёмом по дельте (покупки - продажи)
        /// </summary>
        public event Action<HorizontalVolumeLine> MinDeltaLineChangeEvent;

// логирование

        /// <summary>
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
        /// сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// тип отображения кластеров
    /// </summary>
    public enum ClusterType
    {
        /// <summary>
        /// по суммарному объёму
        /// </summary>
        SummVolume,

        /// <summary>
        /// по объёму покупок
        /// </summary>
        BuyVolume,

        /// <summary>
        /// по объёму продаж
        /// </summary>
        SellVolume,

        /// <summary>
        /// по объёму дельты (покупка - продажи)
        /// </summary>
        DeltaVolume
    }
}
