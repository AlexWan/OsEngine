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
                catch
                {
                    Thread.Sleep(5000);
                    // ignore
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
                if (_connector == null)
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

        public void ShowDialog()
        {
            _connector.ShowDialog();
        }

        #endregion

        #region Outgoing events

        private ConnectorNews _connector;

        private void _connector_NewsEvent(News news)
        {
            if (NewsEvent != null)
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

        public void StartPaint(WindowsFormsHost host)
        {
            try
            {
                Host = host;

                if (_grid == null)
                {
                    CreateGrid();
                }

                TryRepaintGrid();

                Host.Child = _grid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            Host = null;
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
            column0.HeaderText = OsLocalization.Trader.Label432;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column0.MinimumWidth = 100;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Trader.Label433;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column1.MinimumWidth = 100;

            _grid.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Trader.Label434;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column);

            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void ClearGrid()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(ClearGrid));
                    return;
                }

                _grid.Rows.Clear();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteGridsToPaint()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(DeleteGridsToPaint));
                    return;
                }

                if (Host != null)
                {
                    Host.Child = null;
                    Host = null;
                }

                if (_grid != null)
                {
                    _grid.Rows.Clear();
                    _grid.DataError -= _grid_DataError;
                    DataGridFactory.ClearLinks(_grid);
                    _grid = null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TryRepaintGrid()
        {
            try
            {
                if (Host == null
               || _grid == null)
                {
                    return;
                }

                List<News> news = _connector.NewsArray;

                if (news.Count == 0 && _grid.Rows.Count == 0)
                {
                    return;
                }

                if (news.Count != 0 &&
                    news.Count == _grid.Rows.Count)
                {
                    if (_grid.Rows[0].Cells[2].Value.ToString() == news[0].Value)
                    {
                        return;
                    }
                }

                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(TryRepaintGrid));
                    return;
                }

                int firstNewsNum = 0;

                if (_grid.Rows.Count > 0)
                {
                    string firstMessage = _grid.Rows[0].Cells[2].Value.ToString();

                    for (int i = 0; i < news.Count; i++)
                    {
                        if (news[i].Value == firstMessage)
                        {
                            firstNewsNum = i + 1;
                            break;
                        }
                    }
                }

                for (int i = firstNewsNum; i < news.Count; i++)
                {
                    _grid.Rows.Insert(0, GetRow(news[i]));
                }

                while (_grid.Rows.Count > _connector.CountNewsToSave)
                {
                    _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(News news)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = news.TimeMessage.ToString(OsLocalization.CurCulture);

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = news.Source;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = news.Value;

            return row;
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