/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace OsEngine.Charts.ClusterChart
{
    public class ChartClusterMaster
    {
        public ChartClusterMaster(string name, StartProgram startProgram, HorizontalVolume volume)
        {
            _name = name;
            _chart = new ChartClusterPainter(name, startProgram, volume);

            Load();
        }

        private string _name;

        /// <summary>
        /// save settings to file
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"ClusterChartMasterSet.txt", false))
                {
                    writer.WriteLine(_chartType);

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
        /// upload settings from file
        /// загрузить настройки из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"ClusterChartMasterSet.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"ClusterChartMasterSet.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out _chartType);
                    _chart.ChartType =_chartType;
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
        /// delete settings from file
        /// удалить настройки из файла
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + _name + @"ClusterChartMasterSet.txt"))
            {
                File.Delete(@"Engine\" + _name + @"ClusterChartMasterSet.txt");
            }
        }


        public ClusterType ChartType
        {
            get { return _chartType; }
            set
            {
                if (_chartType == value)
                {
                    return;
                }
                _chartType = value;
                _chart.ChartType = value;
                Save();
                Refresh();
            }
        }

        private ClusterType _chartType;

        private ChartClusterPainter _chart;

        public void Process(HorizontalVolume volume)
        {
            _cluster = volume;
            _chart.ProcessCluster(_cluster.VolumeClusterLines);
        }

        private HorizontalVolume _cluster;

        /// <summary>
        /// to start drawing this chart on the window
        /// начать прорисовывать данный чарт на окне
        /// </summary>
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            try
            {
                _chart.StartPaintPrimeChart(host, rectangle);

                if (_cluster != null && _cluster.VolumeClusterLines != null)
                {
                    _chart.ProcessCluster(_cluster.VolumeClusterLines);
                }
            }
            catch (Exception error)
            {
                SendErrorMessage(error);
            }
        }

        /// <summary>
        /// stop drawing this chart on the window
        /// прекратить прорисовывать данный чарт на окне
        /// </summary>
        public void StopPaint()
        {
            _chart.StopPaint();

        }

        /// <summary>
        /// clear chart
        /// очистить чарт
        /// </summary>
        public void Clear()
        {
            _chart.ClearDataPointsAndSizeValue();
            _chart.ClearSeries();
        }

        /// <summary>
        /// redraw chart
        /// перерисовать чарт
        /// </summary>
        public void Refresh()
        {
            _chart.ClearDataPointsAndSizeValue();
            _chart.ClearSeries();
            if (_cluster != null)
            {
                _chart.ProcessCluster(_cluster.VolumeClusterLines);
            }
        }
        // work with log
        // работа с логом

        /// <summary>
        /// send an error message upstairs
        /// выслать наверх сообщение об ошибке
        /// </summary>
        private void SendErrorMessage(Exception error)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(error.ToString(), LogMessageType.Error);
            }
            else
            {
                // if no one's subscribed to us and there's a mistake
                // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// an incoming event from class where chart drawn
        /// входящее событие из класса в котором прорисовывается чарт
        /// </summary>
        void NewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                // if no one's subscribed to us and there's a mistake
                // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
 
        /// <summary>
        /// get chart
        /// взять чарт
        /// </summary>
        public Chart GetChart()
        {
            return _chart.GetChart();
        }
    }


}
