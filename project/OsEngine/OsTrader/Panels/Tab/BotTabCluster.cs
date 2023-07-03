/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.ClusterChart;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Tab creating and drawing cluster graph
    /// </summary>
    public class BotTabCluster : IIBotTab
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">bot name</param>
        /// <param name="startProgram">class creating program</param>
        public BotTabCluster(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            CandleConnector = new ConnectorCandles(name, _startProgram, false);
            CandleConnector.SaveTradesInCandles = true;
            CandleConnector.LastCandlesChangeEvent += Tab_LastCandlesChangeEvent;
            CandleConnector.SecuritySubscribeEvent += CandleConnector_SecuritySubscribeEvent;
            CandleConnector.LogMessageEvent += SendNewLogMessage;

            _horizontalVolume = new HorizontalVolume(name);

            _horizontalVolume.MaxSummClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MaxSummClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxBuyClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MaxBuyClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxSellClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MaxSellClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxDeltaClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MaxDeltaClusterChangeEvent?.Invoke(line);
                }
            };

            _horizontalVolume.MinSummClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MinSummClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinBuyClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MinBuyClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinSellClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MinSellClusterChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinDeltaClusterChangeEvent += delegate (HorizontalVolumeCluster line)
            {
                if (EventsIsOn)
                {
                    MinDeltaClusterChangeEvent?.Invoke(line);
                }
            };


            _horizontalVolume.MaxSummLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MaxSummLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxBuyLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MaxBuyLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxSellLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MaxSellLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MaxDeltaLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MaxDeltaLineChangeEvent?.Invoke(line);
                }
            };

            _horizontalVolume.MinSummLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MinSummLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinBuyLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MinBuyLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinSellLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MinSellLineChangeEvent?.Invoke(line);
                }
            };
            _horizontalVolume.MinDeltaLineChangeEvent += delegate (HorizontalVolumeLine line)
            {
                if (EventsIsOn)
                {
                    MinDeltaLineChangeEvent?.Invoke(line);
                }
            };

            _chartMaster = new ChartClusterMaster(name, startProgram, _horizontalVolume);
            _chartMaster.LogMessageEvent += SendNewLogMessage;

            Load();
        }

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Cluster;
            }
        }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
                Save();
            }
        }

        private bool _eventsIsOn = true;

        /// <summary>
        /// Save settings to a file
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"ClusterOnOffSet.txt", false))
                {
                    writer.WriteLine(EventsIsOn);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Load()
        {
            if (!File.Exists(@"Engine\" + TabName + @"ClusterOnOffSet.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"ClusterOnOffSet.txt"))
                {
                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                _eventsIsOn = true;
                // ignore
            }
        }

        /// <summary>
        /// The connector is connected to a new instrument
        /// </summary>
        private void CandleConnector_SecuritySubscribeEvent(Security newSecurity)
        {
            _horizontalVolume.Security = CandleConnector.Security;
        }

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// custom name robot
        /// пользовательское имя робота
        /// </summary>
        public string NameStrategy
        {
            get
            {
                if (TabName.Contains("tab"))
                {
                    return TabName.Remove(TabName.LastIndexOf("tab"), TabName.Length - TabName.LastIndexOf("tab"));
                }
                return "";
            }
        }

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn { get; set; }

        /// <summary>
        /// Line step value
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
        /// Chart type
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
        /// Class creating program
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// Security on which we build clusters
        /// </summary>
        public ConnectorCandles CandleConnector;

        /// <summary>
        /// Chart
        /// </summary>
        private ChartClusterMaster _chartMaster;

        /// <summary>
        /// Horizontal volumes
        /// </summary>
        private HorizontalVolume _horizontalVolume;

        /// <summary>
        /// Remove tab and all child structures
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();
            _horizontalVolume.Delete();
            CandleConnector.Delete();

            if(TabDeletedEvent != null)
            {
                TabDeletedEvent();
            }
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            _horizontalVolume.Clear();
            _chartMaster.Clear();
        }

        // control

        /// <summary>
        /// Settings gui
        /// </summary>
        public void ShowDialog()
        {
            BotTabClusterUi ui = new BotTabClusterUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// Call the window to connect candles
        /// </summary>
        public void ShowCandlesDialog()
        {
            CandleConnector.ShowDialog(false);
        }

        /// <summary>
        /// Stop drawing this robot
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// Start drawing this robot
        /// </summary> 
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(host, rectangle);
        }

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// The last candle has changed
        /// </summary>
        private void Tab_LastCandlesChangeEvent(List<Candle> candles)
        {
            LastTimeCandleUpdate = CandleConnector.MarketTime;
            _horizontalVolume.Process(candles);
            _chartMaster.Process(_horizontalVolume);
        }

        // data request

        /// <summary>
        /// Volume columns
        /// </summary>
        public List<HorizontalVolumeCluster> VolumeClusters
        {
            get { return _horizontalVolume.VolumeClusters; }
        }

        /// <summary>
        /// Volume column with the maximum volume of all transactions
        /// </summary>
        public HorizontalVolumeCluster MaxSummVolumeCluster
        {
            get { return _horizontalVolume.MaxSummVolumeCluster; }
        }

        /// <summary>
        /// Volume column with the minimum volume of all transactions
        /// </summary>
        public HorizontalVolumeCluster MinSummVolumeCluster
        {
            get { return _horizontalVolume.MinSummVolumeCluster; }
        }

        /// <summary>
        /// Volume column with the maximum amount of buy
        /// </summary>
        public HorizontalVolumeCluster MaxBuyVolumeCluster
        {
            get { return _horizontalVolume.MaxBuyVolumeCluster; }
        }

        /// <summary>
        /// Volume column with a minimum amount of buy
        /// </summary>
        public HorizontalVolumeCluster MinBuyVolumeCluster
        {
            get { return _horizontalVolume.MinBuyVolumeCluster; }
        }

        /// <summary>
        /// Volume column with maximum sales
        /// </summary>
        public HorizontalVolumeCluster MaxSellVolumeCluster
        {
            get { return _horizontalVolume.MaxSellVolumeCluster; }
        }

        /// <summary>
        /// Volume column with minimum sales
        /// </summary>
        public HorizontalVolumeCluster MinSellVolumeCluster
        {
            get { return _horizontalVolume.MinSellVolumeCluster; }
        }

        /// <summary>
        /// Volume column with the maximum delta volume (purchases minus sales)
        /// </summary>
        public HorizontalVolumeCluster MaxDeltaVolumeCluster
        {
            get { return _horizontalVolume.MaxDeltaVolumeCluster; }
        }

        /// <summary>
        /// Minimum volume delta volume column (purchases minus sales)
        /// </summary>
        public HorizontalVolumeCluster MinDeltaVolumeCluster
        {
            get { return _horizontalVolume.MinDeltaVolumeCluster; }
        }

        // data access methods

        /// <summary>
        /// Find cluster with maximum volume
        /// </summary>
        /// <param name="startIndex">start index</param>
        /// <param name="endIndex">end index</param>
        /// <param name="typeCluster">type cluster</param>
        public HorizontalVolumeCluster FindMaxVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMaxVolumeCluster(startIndex, endIndex, typeCluster);
        }

        /// <summary>
        /// Find cluster with minimum volume
        /// </summary>
        /// <param name="startIndex">start index</param>
        /// <param name="endIndex">end index</param>
        /// <param name="typeCluster">type cluster</param>
        public HorizontalVolumeCluster FindMinVolumeCluster(int startIndex, int endIndex, ClusterType typeCluster)
        {
            return _horizontalVolume.FindMinVolumeCluster(startIndex, endIndex, typeCluster);
        }

        // outgoing events

        /// <summary>
        /// The cluster has changed with the maximum total volume
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSummClusterChangeEvent;

        /// <summary>
        /// The cluster with the maximum total amount of buy has changed
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxBuyClusterChangeEvent;

        /// <summary>
        /// The cluster with the maximum total sales volume has changed
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxSellClusterChangeEvent;

        /// <summary>
        /// The cluster with the maximum total volume by delta has changed (purchases - sales)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MaxDeltaClusterChangeEvent;

        /// <summary>
        /// The cluster has changed with the minimum total volume
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSummClusterChangeEvent;

        /// <summary>
        /// The cluster has changed with the minimum total amount of buy
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinBuyClusterChangeEvent;

        /// <summary>
        /// The cluster has changed with a minimum total sales volume 
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinSellClusterChangeEvent;

        /// <summary>
        /// The cluster has changed with the minimum total volume by delta (purchases - sales)
        /// </summary>
        public event Action<HorizontalVolumeCluster> MinDeltaClusterChangeEvent;

        /// <summary>
        /// Volume line with maximum total volume changed
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSummLineChangeEvent;

        /// <summary>
        /// The volume line with the maximum total volume of buy has changed
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxBuyLineChangeEvent;

        /// <summary>
        /// Volume line has changed with the maximum total sales volume
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxSellLineChangeEvent;

        /// <summary>
        /// Volume line changed with the maximum total volume of the delta (buy - sale)
        /// </summary>
        public event Action<HorizontalVolumeLine> MaxDeltaLineChangeEvent;

        /// <summary>
        /// The volume line has changed with the minimum total volume
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSummLineChangeEvent;

        /// <summary>
        /// The volume line was changed by the minimum total amount of buy
        /// </summary>
        public event Action<HorizontalVolumeLine> MinBuyLineChangeEvent;

        /// <summary>
        /// The volume line has changed with a minimum total sales volume
        /// </summary>
        public event Action<HorizontalVolumeLine> MinSellLineChangeEvent;

        /// <summary>
        /// The volume line with the minimum total volume of the delta has changed (purchases - sales)
        /// </summary>
        public event Action<HorizontalVolumeLine> MinDeltaLineChangeEvent;

        // log

        /// <summary>
        /// Send new log message
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action TabDeletedEvent;

        /// <summary>
        /// Get chart
        /// </summary>
        public Chart GetChart()
        {
            return _chartMaster.GetChart();
        }
    }

    /// <summary>
    /// Cluster display type
    /// </summary>
    public enum ClusterType
    {
        /// <summary>
        /// By total volume
        /// </summary>
        SummVolume,

        /// <summary>
        /// Buy volume
        /// </summary>
        BuyVolume,

        /// <summary>
        /// Sell volume
        /// </summary>
        SellVolume,

        /// <summary>
        /// By delta volume (purchase - sale)
        /// </summary>
        DeltaVolume
    }
}
