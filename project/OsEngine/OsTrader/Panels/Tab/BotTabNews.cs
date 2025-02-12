/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/


using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabNews : IIBotTab
    {
        #region Static part

        /// <summary>
        /// Activate grid drawing
        /// </summary>
        private static void StaticThreadActivation()
        {
            lock (_staticThreadLocker)
            {
                if (_staticThread != null)
                {
                    return;
                }

                _staticThread = new Thread(StaticThreadArea);
                _staticThread.Start();
            }
        }

        /// <summary>
        /// Screener tabs
        /// </summary>
        private static List<BotTabNews> _newsSources = new List<BotTabNews>();

        private static string _newsListLocker = "scrLocker";

        /// <summary>
        /// Add a new tracking tab
        /// </summary>
        /// <param name="tab">screener tab</param>
        private static void AddNewTabToWatch(BotTabNews tab)
        {
            lock (_newsListLocker)
            {
                _newsSources.Add(tab);
            }
        }

        /// <summary>
        /// Remove a tab from being followed
        /// </summary>
        /// <param name="tab">screener tab</param>
        private static void RemoveTabFromWatch(BotTabNews tab)
        {
            lock (_newsListLocker)
            {
                for (int i = 0; i < _newsSources.Count; i++)
                {
                    if (_newsSources[i].TabName == tab.TabName)
                    {
                        _newsSources.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Blocker of multi-threaded access to the activation of the rendering of screeners
        /// </summary>
        private static object _staticThreadLocker = new object();

        /// <summary>
        /// Thread rendering screeners
        /// </summary>
        private static Thread _staticThread;

        /// <summary>
        /// Place of work for the thread that draws screeners
        /// </summary>
        private static void StaticThreadArea()
        {
            Thread.Sleep(3000);

            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                Thread.Sleep(500);

                try
                {
                    for (int i = 0; _newsSources != null &&
                        _newsSources.Count != 0 &&
                        i < _newsSources.Count; i++)
                    {
                        BotTabNews curScreener = _newsSources[i];

                        if (curScreener.Host != null)
                        {
                            curScreener.TryRepaintGrid();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    // do nothin
                }
            }
        }

        #endregion

        #region Service. Constructor. Override for the interface

        public BotTabNews(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;

            _connector = new ConnectorNews(name, startProgram);
            _connector.LogMessageEvent += SendNewLogMessage;
            _connector.NewsEvent += _connector_NewsEvent;

            CreateGrid();

            StaticThreadActivation();

            AddNewTabToWatch(this);
        }

        public BotTabType TabType { get { return BotTabType.News; } }

        public string TabName { get; set; }

        public StartProgram StartProgram { get; set; }

        public int TabNum { get; set; }

        public bool EventsIsOn 
        { 
            get 
            {
                if (_connector == null)
                {
                    return true;
                }
                return _connector.EventsIsOn; 
            }
            set
            {
                if(_connector == null)
                {
                    return;
                }
                _connector.EventsIsOn = value;
            }
        }

        public bool EmulatorIsOn { get; set; }

        public DateTime LastTimeCandleUpdate { get; set; }

        public void Clear()
        {
            ClearGrid();
        }

        public void Delete()
        {
            if (_connector != null)
            {
                _connector.Delete();
                _connector.LogMessageEvent -= SendNewLogMessage;
                _connector.NewsEvent -= _connector_NewsEvent;
                _connector = null;
            }

            DeleteGridsToPaint();

            RemoveTabFromWatch(this);

            if (TabDeletedEvent != null)
            {
                TabDeletedEvent();
            }
        }

        public event Action TabDeletedEvent;

        #endregion

        #region Outgoing events

        private ConnectorNews _connector;

        private void _connector_NewsEvent(News news)
        {
            if(NewsEvent != null)
            {
                NewsEvent(news);
            }
        }

        /// <summary>
        /// the news has come out
        /// </summary>
        public event Action<News> NewsEvent;

        #endregion

        #region Drawing the source table

        public void StopPaint()
        {


        }

        private DataGridView _grid;

        public WindowsFormsHost Host;

        private void CreateGrid()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Time";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column0.MinimumWidth = 100;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Source";
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column1.MinimumWidth = 100;

            _grid.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = "News";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column);

            _grid.Rows.Add(null, null);

        }

        private void ClearGrid()
        {

        }

        private void DeleteGridsToPaint()
        {


        }

        private void TryRepaintGrid()
        {



        }


        #endregion

        #region Logging

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

        #endregion
    }
}