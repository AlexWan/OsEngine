/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Drawing.Point;

namespace OsEngine.OsData
{

    /// <summary>
    /// Set Storage Wizard/мастер хранения сетов 
    /// </summary>
    public class OsDataMasterPainter
    {
        OsDataMaster _master;

        /// <summary>
        /// constructor/конструктор
        /// </summary>
        /// <param name="hostChart">chart host/хост для чарта</param>
        /// <param name="hostLog">log host/хост для лога</param>
        /// <param name="hostSource">source host/хост для источников</param>
        /// <param name="hostSets">host for sets/хост для сетов</param>
        /// <param name="comboBoxSecurity">paper selection menu/меню выбора бумаг</param>
        /// <param name="comboBoxTimeFrame">time frame selection menu/меню выбора таймфрейма</param>
        /// <param name="rectangle">square for substrate/квадрат для подложки</param>
        public OsDataMasterPainter(OsDataMaster master,
            WindowsFormsHost hostChart, 
            WindowsFormsHost hostLog,
            WindowsFormsHost hostSource, 
            WindowsFormsHost hostSets, 
            System.Windows.Shapes.Rectangle rectangle, 
            Grid greedChartPanel,
            System.Windows.Controls.Label setName,
            System.Windows.Controls.Label labelTimeStart,
            System.Windows.Controls.Label labelTimeEnd,
            System.Windows.Controls.ProgressBar bar)
        {
            _master = master;
            _hostChart = hostChart;
            _hostSource = hostSource;
            _hostSets = hostSets;
            _rectangle = rectangle;
            _greedChartPanel = greedChartPanel;
            _setName = setName;
            _labelTimeStart = labelTimeStart;
            _labelTimeEnd = labelTimeEnd;
            _bar = bar;

            CreateSetGrid();
            RePaintSetGrid();

            CreateSourceGrid();
            RePaintSourceGrid();

            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
            master.NewLogMessageEvent += SendNewLogMessage;
            master.NeadUpDateTableEvent += Master_NeadUpDateTableEvent;

            Log myLog = new Log("OsDataLog", StartProgram.IsOsData);
            myLog.StartPaint(hostLog);
            myLog.Listen(this);
        }

        private void Master_NeadUpDateTableEvent()
        {
            try
            {
                RePaintSetGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void ServerMaster_ServerCreateEvent(IServer server)
        {
            try
            {
                List<IServer> servers = ServerMaster.GetServers();

                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerType == ServerType.Optimizer)
                    {
                        continue;
                    }
                    servers[i].ConnectStatusChangeEvent -= ServerStatusChangeEvent;
                    servers[i].LogMessageEvent -= OsDataMaster_LogMessageEvent;

                    servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
                    servers[i].LogMessageEvent += OsDataMaster_LogMessageEvent;

                }
                RePaintSourceGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaintActiveSet()
        {
            try
            {
                if (_master.SelectSet == null)
                {
                    return;
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectSet.SetName)
                    {
                        curPainter = _painters[i];
                        break;
                    }
                }

                if (curPainter == null)
                {
                    curPainter = new OsDataSetPainter(_master.SelectSet);
                    curPainter.NewLogMessageEvent += SendNewLogMessage;
                    _painters.Add(curPainter);
                }

                curPainter.StopPaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StartPaintActiveSet()
        {
            try
            {
                if (_master.Sets == null ||
                    _master.Sets.Count == 0)
                {
                    return;
                }

                if (_master.SelectSet == null)
                {
                    _master.SelectSet = _master.Sets[0];
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectSet.SetName)
                    {
                        curPainter = _painters[i];
                        break;
                    }
                }

                if (curPainter == null)
                {
                    curPainter = new OsDataSetPainter(_master.SelectSet);
                    curPainter.NewLogMessageEvent += SendNewLogMessage;
                    _painters.Add(curPainter);
                }

                curPainter.StartPaint(_hostChart, _setName, _labelTimeStart, _labelTimeEnd, _bar);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void RefreshActiveSet()
        {
            try
            {
                if (_master.SelectSet == null)
                {
                    return;
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectSet.SetName)
                    {
                        curPainter = _painters[i];
                        break;
                    }
                }

                if (curPainter == null)
                {
                    return;
                }

                curPainter.RePaintInterface();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        List<OsDataSetPainter> _painters = new List<OsDataSetPainter>();

        void OsDataMaster_LogMessageEvent(string message, LogMessageType type)
        {
            SendNewLogMessage(message, type);
        }

        System.Windows.Controls.Label _setName;

        System.Windows.Controls.Label _labelTimeStart;

        System.Windows.Controls.Label _labelTimeEnd;

        System.Windows.Controls.ProgressBar _bar;

        /// <summary>
        /// chart host/хост для чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// server host/хост для серверов
        /// </summary>
        private WindowsFormsHost _hostSource;

        /// <summary>
        /// host for sets/хост для сетов
        /// </summary>
        private WindowsFormsHost _hostSets;

        /// <summary>
        /// дата грид для других видов чарта
        /// </summary>
        private Grid _greedChartPanel;

        /// <summary>
        /// rectangle for the substrate/прямоугольник для подложки
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectangle;

        /// <summary>
        /// send new message to log/выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// new message event to log/событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

        /// <summary>
        /// source table/таблица источников
        /// </summary>
        private DataGridView _gridSources;

        /// <summary>
        /// create source table/создать таблицу источников
        /// </summary>
        private void CreateSourceGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, 
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label4;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Data.Label5;
            colu.ReadOnly = true;
            colu.Width = 130;
            
            newGrid.Columns.Add(colu);

            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            _hostSource.Child = _gridSources;
            _hostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// redraw the source table/перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGrid()
        {
            try
            {
                if (_gridSources.InvokeRequired)
                {
                    _gridSources.Invoke(new Action(RePaintSourceGrid));
                    return;
                }

                _gridSources.Rows.Clear();

                List<ServerType> servers = ServerMaster.ServersTypesToOsData;

                List<IServer> serversCreate = ServerMaster.GetServers();

                if (serversCreate == null)
                {
                    serversCreate = new List<IServer>();
                }

                for (int i = 0; i < servers.Count; i++)
                {
                    DataGridViewRow row1 = new DataGridViewRow();
                    row1.Cells.Add(new DataGridViewTextBoxCell());
                    row1.Cells[0].Value = servers[i];
                    row1.Cells.Add(new DataGridViewTextBoxCell());

                    IServer server = serversCreate.Find(s => s.ServerType == servers[i]);

                    if (server == null)
                    {
                        row1.Cells[1].Value = "Disabled";
                    }
                    else if (server != null && server.ServerStatus == ServerConnectStatus.Connect)
                    {
                        row1.Cells[1].Value = "Connect";
                        DataGridViewCellStyle style = new DataGridViewCellStyle();
                        style.BackColor = Color.MediumSeaGreen;
                        style.SelectionBackColor = Color.Green;
                        style.ForeColor = Color.Black;
                        style.SelectionForeColor = Color.Black;
                        row1.Cells[1].Style = style;
                        row1.Cells[0].Style = style;
                    }
                    else
                    {
                        row1.Cells[1].Value = "Disconnect";
                        DataGridViewCellStyle style = new DataGridViewCellStyle();
                        style.BackColor = Color.Coral;
                        style.SelectionBackColor = Color.Chocolate;
                        style.ForeColor = Color.Black;
                        style.SelectionForeColor = Color.Black;
                        row1.Cells[1].Style = style;
                        row1.Cells[0].Style = style;
                    }

                    _gridSources.Rows.Add(row1);
                }
                _gridSources[1, 0].Selected = true; // Select an invisible line to remove the default selection from the grid./Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
                _gridSources.ClearSelection();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// double click event on the source table/событие двойного клика на таблицу источников
        /// </summary>
        void _gridSources_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_gridSources.CurrentCell.RowIndex <= -1)
                {
                    return;
                }

                ServerType type;
                Enum.TryParse(_gridSources.Rows[_gridSources.CurrentCell.RowIndex].Cells[0].Value.ToString(), out type);

                if (ServerMaster.GetServers() == null)
                {
                    ServerMaster.CreateServer(type, false);
                }

                IServer server = ServerMaster.GetServers().Find(s => s.ServerType == type);

                if (server == null)
                {
                    ServerMaster.CreateServer(type, false);
                    server = ServerMaster.GetServers().Find(s => s.ServerType == type);
                }

                server.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// server status change event/событие изменения статуса сервера
        /// </summary>
        /// <param name="newState"></param>
        void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

        // work with the grid/работа с гридом

        /// <summary>
        /// table for sets/таблица для сетов
        /// </summary>
        private DataGridView _gridset;

        /// <summary>
        /// create table for sets/создать таблицу для сетов
        /// </summary>
        private void CreateSetGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            newGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label3;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Data.Label5;
            colu.ReadOnly = true;
            colu.Width = 100;
          
            newGrid.Columns.Add(colu);

            _gridset = newGrid;
            _gridset.Click += _gridset_Click;
            _gridset.DoubleClick += _gridset_DoubleClick;
            _hostSets.Child = _gridset;
        }

        /// <summary>
        /// redraw the set table/перерисовать таблицу сетов
        /// </summary>
        private void RePaintSetGrid()
        {
            try
            {
                if (_gridset.InvokeRequired)
                {
                    _gridset.Invoke(new Action(RePaintSetGrid));
                    return;
                }

                _master.SortSets();

                _gridset.Rows.Clear();

                for (int i = 0; _master.Sets != null && i < _master.Sets.Count; i++)
                {
                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = _master.Sets[i].SetName;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _master.Sets[i].BaseSettings.Regime;

                    if (_master.Sets[i].BaseSettings.Regime == DataSetState.On)
                    {
                        DataGridViewCellStyle style = new DataGridViewCellStyle();
                        style.BackColor = Color.MediumSeaGreen;
                        style.SelectionBackColor = Color.Green;
                        style.ForeColor = Color.Black;
                        style.SelectionForeColor = Color.Black;
                        nRow.Cells[1].Style = style;
                        nRow.Cells[0].Style = style;
                    }

                    _gridset.Rows.Add(nRow);
                }
                if (_gridset.Rows.Count != 0)
                {
                    _gridset[0, 0].Selected = true; // Select an invisible line to remove the default selection from the grid./Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
                    _gridset.ClearSelection();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// double-click event on the table of sets/событие двойного клика по таблицу сетов
        /// </summary>
        private void _gridset_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_gridset.CurrentCell == null ||
                _gridset.CurrentCell.RowIndex <= -1)
                {
                    return;
                }
                int _rowIndex = _gridset.CurrentCell.RowIndex;
                RedactThisSet(_rowIndex);
                RePaintSetGrid();
                _gridset.Rows[_rowIndex].Selected = true; // Return focus to the line you edited./Вернуть фокус на строку, которую редактировал.
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// single click on the table of sets/одиночный клик по таблице сетов
        /// </summary>
        void _gridset_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    if (_gridset.CurrentCell != null)
                    {
                        ChangeActivSet(_gridset.CurrentCell.RowIndex);
                    }

                    return;
                }

                // creating a context menu/cоздание контекстного меню

                MenuItem[] items = new MenuItem[3];

                items[0] = new MenuItem();
                items[0].Text = OsLocalization.Data.Label6;
                items[0].Click += AddSet_Click;

                items[1] = new MenuItem() { Text = OsLocalization.Data.Label7 };
                items[1].Click += RedactSet_Click;

                items[2] = new MenuItem() { Text = OsLocalization.Data.Label8 };
                items[2].Click += DeleteSet_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridset.ContextMenu = menu;
                _gridset.ContextMenu.Show(_gridset, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create new set/создать новый сет
        /// </summary>
        private void AddSet_Click(object sender, EventArgs e)
        {
            try
            {
                CreateNewSetDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// edit set/редактировать сет
        /// </summary>
        void RedactSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gridset.CurrentCell == null ||
                    _gridset.CurrentCell.RowIndex <= -1)
                {
                    return;
                }
                RedactThisSet(_gridset.CurrentCell.RowIndex);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete set/удалить сет
        /// </summary>
        void DeleteSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gridset.CurrentCell == null ||
                    _gridset.CurrentCell.RowIndex <= -1)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label9);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }
                DeleteThisSet(_gridset.CurrentCell.RowIndex);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// change active set/сменить активный сет
        /// </summary>
        /// <param name="index">new index/индекс нового</param>
        private void ChangeActivSet(int index)
        {
            try
            {
                if (_master.Sets == null ||
                    _master.Sets.Count == 0)
                {
                    return;
                }

                if (index > _master.Sets.Count)
                {
                    return;
                }

                OsDataSet currentSet = _master.Sets[index];

                if (_master.SelectSet != null &&
                    currentSet.SetName == _master.SelectSet.SetName)
                {
                    return;
                }

                if (_master.SelectSet != null &&
                    currentSet.SetName != _master.SelectSet.SetName)
                {
                    StopPaintActiveSet();
                }

                _master.SelectSet = currentSet;

                StartPaintActiveSet();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }



        // management/управление        

        /// <summary>
        /// create new set/создать новый сет
        /// </summary>
        public void CreateNewSetDialog()
        {
            try
            {
                if (_master.Sets == null)
                {
                    _master.Sets = new List<OsDataSet>();
                }
                OsDataSet set = new OsDataSet("Set_");
                set.NewLogMessageEvent += SendNewLogMessage;

                OsDataSetUi ui = new OsDataSetUi(set);
                ui.ShowDialog();

                if (!ui.IsSaved)
                { // the user did not press the accept button in the form/пользователь не нажал на кнопку принять в форме
                    set.BaseSettings.Regime = DataSetState.Off;
                    set.Delete();
                    return;
                }

                if (set.SetName == "Set_")
                {
                    set.BaseSettings.Regime = DataSetState.Off;
                    set.Delete();
                    MessageBox.Show(OsLocalization.Data.Label10);
                    return;
                }

                if (_master.Sets.Find(dataSet => dataSet.SetName == set.SetName) != null)
                {
                    MessageBox.Show(OsLocalization.Data.Label11);
                    set.BaseSettings.Regime = DataSetState.Off;
                    set.Delete();
                    return;
                }

                _master.Sets.Add(set);
                set.Save();
                ChangeActivSet(_master.Sets.Count - 1);
                RePaintSetGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete set by index/удалить сет по индексу
        /// </summary>
        public void DeleteThisSet(int num)
        {
            try
            {
                if (_master.Sets == null)
                {
                    _master.Sets = new List<OsDataSet>();
                }

                if (num >= _master.Sets.Count)
                {
                    return;
                }
                _master.Sets[num].Delete();
                _master.Sets[num].BaseSettings.Regime = DataSetState.Off;

                if (_master.SelectSet != null
                    && _master.Sets[num].SetName == _master.SelectSet.SetName)
                {
                    StopPaintActiveSet();
                    _master.SelectSet = null;
                }

                _master.Sets.RemoveAt(num);

                RePaintSetGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// edit set by index/редактировать сет по индексу
        /// </summary>
        public void RedactThisSet(int num)
        {
            try
            {
                if (num >= _master.Sets.Count)
                {
                    return;
                }

                OsDataSet set = _master.Sets[num];

                OsDataSetUi ui = new OsDataSetUi(set);
                ui.ShowDialog();

                if (ui.IsSaved)
                {
                    set.Save();
                    RefreshActiveSet();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}