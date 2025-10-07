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
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Drawing.Point;

namespace OsEngine.OsData
{
    public class OsDataMasterPainter
    {
        #region Service

        private OsDataMaster _master;

        public OsDataMasterPainter(OsDataMaster master,
            WindowsFormsHost hostSetGrid, 
            WindowsFormsHost hostLog,
            WindowsFormsHost hostSource, 
            WindowsFormsHost hostSets, 
            System.Windows.Controls.Label setName,
            System.Windows.Controls.Label labelTimeStart,
            System.Windows.Controls.Label labelTimeEnd,
            System.Windows.Controls.ProgressBar bar,
            System.Windows.Controls.TextBox textBox)
        {
            _master = master;
            _hostSetGrid = hostSetGrid;
            _hostSource = hostSource;
            _hostSets = hostSets;
            _setName = setName;
            _labelTimeStart = labelTimeStart;
            _labelTimeEnd = labelTimeEnd;
            _bar = bar;
            _textBoxSource = textBox;

            if (_textBoxSource != null) 
            { 
                _textBoxSource.TextChanged += FilterTextBox_TextChanged;
                _textBoxSource.GotFocus += FilterTextBox_Enter;
                _textBoxSource.LostFocus += FilterTextBox_Leave;
                _textBoxSource.Foreground = System.Windows.Media.Brushes.Gray;
            }

            CreateSetGrid();
            RePaintSetGrid();

            CreateSourceGrid();
            RePaintSourceGrid();

            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
            ServerMaster.ServerDeleteEvent += ServerMaster_ServerDeleteEvent;
            master.NewLogMessageEvent += SendNewLogMessage;
            master.NeedUpDateTableEvent += Master_NeedUpDateTableEvent;

            Log myLog = new Log("OsDataLog", StartProgram.IsOsData);
            myLog.StartPaint(hostLog);
            myLog.Listen(this);
        }

        private void Master_NeedUpDateTableEvent()
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

        private void ServerMaster_ServerDeleteEvent(IServer server)
        {
            try
            {
                if(server.ServerType == ServerType.Optimizer)
                {
                    return;
                }

                server.ConnectStatusChangeEvent -= ServerStatusChangeEvent;
                server.LogMessageEvent -= OsDataMaster_LogMessageEvent;

                RePaintSourceGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ServerMaster_ServerCreateEvent(IServer server)
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

        private List<OsDataSetPainter> _painters = new List<OsDataSetPainter>();

        private System.Windows.Controls.Label _setName;

        private System.Windows.Controls.Label _labelTimeStart;

        private System.Windows.Controls.Label _labelTimeEnd;

        private System.Windows.Controls.ProgressBar _bar;

        private System.Windows.Controls.TextBox _textBoxSource;

        private WindowsFormsHost _hostSetGrid;

        #endregion

        #region Management      

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
                ChangeActiveSet(_master.Sets.Count - 1);
                RePaintSetGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

                if (_master.SelectedSet != null
                    && _master.Sets[num].SetName == _master.SelectedSet.SetName)
                {
                    StopPaintActiveSet();
                    _master.SelectedSet = null;
                }

                _master.Sets.RemoveAt(num);

                RePaintSetGrid();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

        public void StopPaintActiveSet()
        {
            try
            {
                if (_master.SelectedSet == null)
                {
                    return;
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectedSet.SetName)
                    {
                        curPainter = _painters[i];
                        break;
                    }
                }

                if (curPainter == null)
                {
                    curPainter = new OsDataSetPainter(_master.SelectedSet);
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

                if (_master.SelectedSet == null)
                {
                    _master.SelectedSet = _master.Sets[0];
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectedSet.SetName)
                    {
                        curPainter = _painters[i];
                        break;
                    }
                }

                if (curPainter == null)
                {
                    curPainter = new OsDataSetPainter(_master.SelectedSet);
                    curPainter.NewLogMessageEvent += SendNewLogMessage;
                    _painters.Add(curPainter);
                }

                curPainter.StartPaint(_hostSetGrid, _setName, _labelTimeStart, _labelTimeEnd, _bar);
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
                if (_master.SelectedSet == null)
                {
                    return;
                }

                OsDataSetPainter curPainter = null;

                for (int i = 0; i < _painters.Count; i++)
                {
                    if (_painters[i].NameSet == _master.SelectedSet.SetName)
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

        #endregion

        #region Work with Source grid

        private WindowsFormsHost _hostSource;

        private DataGridView _gridSources;

        private void CreateSourceGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, 
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label4;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Data.Label5;
            column1.ReadOnly = true;
            column1.Width = 130;
            
            newGrid.Columns.Add(column1);

            _gridSources = newGrid;
            _gridSources.DoubleClick += GridSources_DoubleClick;
            _gridSources.DataError += _gridSources_DataError;
            _hostSource.Child = _gridSources;
            _hostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        private void _gridSources_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void FilterTextBox_TextChanged(object sender, EventArgs e)
        {
            RePaintSourceGrid();
        }

        private void FilterTextBox_Enter(object sender, EventArgs e)
        {
            if (_textBoxSource.Text == OsLocalization.Market.Label64)
            {
                _textBoxSource.Text = "";
                _textBoxSource.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void FilterTextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBoxSource.Text))
            {
                _textBoxSource.Text = OsLocalization.Market.Label64;
                _textBoxSource.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

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

                string filter = "";

                List<ServerType> servers = ServerMaster.ServersTypesToOsData
                .OrderBy(s => s.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();
                List<IServer> serversCreate = ServerMaster.GetServers() ?? new List<IServer>();

                if (_textBoxSource != null
                && !string.IsNullOrWhiteSpace(_textBoxSource.Text)
                && _textBoxSource.Text != OsLocalization.Market.Label64)
                {
                    filter = _textBoxSource.Text.ToLower();
                }

                for (int i = 0; i < servers.Count; i++)
                {
                    if (!string.IsNullOrEmpty(filter) &&
                        !servers[i].ToString().ToLower().Contains(filter))
                    {
                        continue;
                    }

                    DataGridViewRow row1 = new DataGridViewRow();
                    row1.Cells.Add(new DataGridViewTextBoxCell { Value = servers[i] });
                    row1.Cells.Add(new DataGridViewTextBoxCell());

                    IServer server = serversCreate.Find(s => s.ServerType == servers[i]);

                    if (server == null)
                    {
                        row1.Cells[1].Value = "Disabled";
                    }
                    else if (server.ServerStatus == ServerConnectStatus.Connect)
                    {
                        row1.Cells[1].Value = "Connect";
                        var style = new DataGridViewCellStyle
                        {
                            BackColor = Color.MediumSeaGreen,
                            SelectionBackColor = Color.Green,
                            ForeColor = Color.Black,
                            SelectionForeColor = Color.Black
                        };
                        row1.Cells[0].Style = row1.Cells[1].Style = style;
                    }
                    else
                    {
                        row1.Cells[1].Value = "Disconnect";
                        var style = new DataGridViewCellStyle
                        {
                            BackColor = Color.Coral,
                            SelectionBackColor = Color.Chocolate,
                            ForeColor = Color.Black,
                            SelectionForeColor = Color.Black
                        };
                        row1.Cells[0].Style = row1.Cells[1].Style = style;
                    }

                    _gridSources.Rows.Add(row1);
                }

                if (_gridSources.Rows.Count > 0)
                {
                    _gridSources[1, 0].Selected = true;
                    _gridSources.ClearSelection();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void GridSources_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_gridSources.CurrentCell.RowIndex <= -1)
                {
                    return;
                }

                Enum.TryParse(_gridSources.Rows[_gridSources.CurrentCell.RowIndex].Cells[0].Value.ToString(), out ServerType type);

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

        private void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

        #endregion

        #region Work with Sets grid

        private WindowsFormsHost _hostSets;

        private DataGridView _gridSets;

        private void CreateSetGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            newGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label3;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Data.Label5;
            column1.ReadOnly = true;
            column1.Width = 100;
          
            newGrid.Columns.Add(column1);

            _gridSets = newGrid;
            _gridSets.Click += GridSet_Click;
            _gridSets.DoubleClick += GridSet_DoubleClick;
            _gridSets.DataError += _gridSources_DataError;
            _hostSets.Child = _gridSets;
        }

        private void RePaintSetGrid()
        {
            try
            {
                if (_gridSets.InvokeRequired)
                {
                    _gridSets.Invoke(new Action(RePaintSetGrid));
                    return;
                }

                _master.SortSets();

                _gridSets.Rows.Clear();

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

                    _gridSets.Rows.Add(nRow);
                }
                if (_gridSets.Rows.Count != 0)
                {
                    _gridSets[0, 0].Selected = true; // Select an invisible line to remove the default selection from the grid.
                    _gridSets.ClearSelection();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void GridSet_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_gridSets.CurrentCell == null ||
                _gridSets.CurrentCell.RowIndex <= -1)
                {
                    return;
                }
                int _rowIndex = _gridSets.CurrentCell.RowIndex;
                RedactThisSet(_rowIndex);
                RePaintSetGrid();
                _gridSets.Rows[_rowIndex].Selected = true; // Return focus to the line you edited./Вернуть фокус на строку, которую редактировал.
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void GridSet_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    if (_gridSets.CurrentCell != null)
                    {
                        ChangeActiveSet(_gridSets.CurrentCell.RowIndex);
                    }

                    return;
                }

                // creating a context menu

                ToolStripMenuItem[] items = new ToolStripMenuItem[3];

                items[0] = new ToolStripMenuItem();
                items[0].Text = OsLocalization.Data.Label6;
                items[0].Click += AddSet_Click;

                items[1] = new ToolStripMenuItem() { Text = OsLocalization.Data.Label7 };
                items[1].Click += RedactSet_Click;

                items[2] = new ToolStripMenuItem() { Text = OsLocalization.Data.Label8 };
                items[2].Click += DeleteSet_Click;

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.AddRange(items);

                _gridSets.ContextMenuStrip = menu;
                _gridSets.ContextMenuStrip.Show(_gridSets, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

        private void RedactSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gridSets.CurrentCell == null ||
                    _gridSets.CurrentCell.RowIndex <= -1)
                {
                    return;
                }
                RedactThisSet(_gridSets.CurrentCell.RowIndex);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteSet_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gridSets.CurrentCell == null ||
                    _gridSets.CurrentCell.RowIndex <= -1)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label9);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }
                DeleteThisSet(_gridSets.CurrentCell.RowIndex);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ChangeActiveSet(int index)
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

                if (_master.SelectedSet != null &&
                    currentSet.SetName == _master.SelectedSet.SetName)
                {
                    return;
                }

                if (_master.SelectedSet != null &&
                    currentSet.SetName != _master.SelectedSet.SetName)
                {
                    StopPaintActiveSet();
                }

                _master.SelectedSet = currentSet;

                StartPaintActiveSet();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logging

        private void OsDataMaster_LogMessageEvent(string message, LogMessageType type)
        {
            SendNewLogMessage(message, type);
        }

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }
}