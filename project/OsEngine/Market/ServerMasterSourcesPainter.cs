/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using System.Windows.Forms.Integration;
using System.IO;
using System.Threading;
using OsEngine.Language;
using OsEngine.Logging;
using System.Windows.Input;

namespace OsEngine.Market
{
    public class ServerMasterSourcesPainter
    {
        public ServerMasterSourcesPainter(WindowsFormsHost hostServers,
            WindowsFormsHost hostLog,
            System.Windows.Controls.CheckBox boxCreateServerAutomatic
            , System.Windows.Controls.TextBox textBoxSearchSource
            , System.Windows.Controls.Button buttonRightInSearchResults
            , System.Windows.Controls.Button buttonLeftInSearchResults
            , System.Windows.Controls.Label labelCurrentResultShow
            , System.Windows.Controls.Label labelCommasResultShow
            , System.Windows.Controls.Label labelCountResultsShow)
        {
            _boxCreateServerAutomatic = boxCreateServerAutomatic;

            _hostServers = hostServers;

            _hostLog = hostLog;

            _boxCreateServerAutomatic.IsChecked = ServerMaster.NeedToConnectAuto;
            _boxCreateServerAutomatic.Click += CheckBoxServerAutoOpen_Click;

            ServerMaster.Log.StartPaint(_hostLog);

            _servers = ServerMaster.GetServers();

            for (int i = 0; _servers != null && i < _servers.Count; i++)
            {
                _servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
            }

            ServerMaster.ServerCreateEvent += ServerMasterOnServerCreateEvent;
            ServerMaster.ServerDeleteEvent += ServerMaster_ServerDeleteEvent;

            LoadAttachedServers();

            CreateSourceGrid();
            RePaintSourceGrid();

            _textBoxSearchSecurity = textBoxSearchSource;
            _buttonRightInSearchResults = buttonRightInSearchResults;
            _buttonLeftInSearchResults = buttonLeftInSearchResults;
            _labelCurrentResultShow = labelCurrentResultShow;
            _labelCommasResultShow = labelCommasResultShow;
            _labelCountResultsShow = labelCountResultsShow;

            _buttonRightInSearchResults.Visibility = Visibility.Hidden;
            _buttonLeftInSearchResults.Visibility = Visibility.Hidden;
            _labelCurrentResultShow.Visibility = Visibility.Hidden;
            _labelCommasResultShow.Visibility = Visibility.Hidden;
            _labelCountResultsShow.Visibility = Visibility.Hidden;
            _textBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
            _textBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
            _textBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
            _textBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
            _buttonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
            _buttonRightInSearchResults.Click += ButtonRightInSearchResults_Click;

            SearchCheckLocalization();
        }

        public void Dispose()
        {
            try
            {
                _textBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                _textBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                _textBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                _textBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                _buttonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
                _buttonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;

                ServerMaster.ServerCreateEvent -= ServerMasterOnServerCreateEvent;
                ServerMaster.ServerDeleteEvent -= ServerMaster_ServerDeleteEvent;

                for (int i = 0; _servers != null && i < _servers.Count; i++)
                {
                    IServer serv = _servers[i];

                    serv.ConnectStatusChangeEvent -= ServerStatusChangeEvent;

                    if (serv == null)
                    {
                        _servers.RemoveAt(i);
                        i--;
                        continue;
                    }
                }

                ServerMaster.Log.StopPaint();

                _servers = null;

                ClearControls();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ClearControls()
        {
            try
            {
                if (_gridSources == null)
                {
                    return;
                }

                if (_gridSources.InvokeRequired)
                {
                    _gridSources.Invoke(new Action(ClearControls));
                    return;
                }

                if (_boxCreateServerAutomatic != null)
                {
                    _boxCreateServerAutomatic.Click -= CheckBoxServerAutoOpen_Click;
                    _boxCreateServerAutomatic = null;
                }

                if (_hostServers != null)
                {
                    _hostServers.Child = null;
                    _hostServers = null;
                }

                if (_hostLog != null)
                {
                    _hostLog.Child = null;
                    _hostLog = null;
                }

                if (_gridSources != null)
                {
                    _gridSources.DoubleClick -= _gridSources_DoubleClick;
                    _gridSources.DataError -= _gridSources_DataError;
                    _gridSources.Rows.Clear();
                    _gridSources.Columns.Clear();
                    DataGridFactory.ClearLinks(_gridSources);
                    _gridSources = null;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private List<IServer> _servers;

        private System.Windows.Controls.CheckBox _boxCreateServerAutomatic;

        private WindowsFormsHost _hostServers;

        private WindowsFormsHost _hostLog;

        private DataGridView _gridSources;

        private void CreateSourceGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridServers();
            newGrid.ScrollBars = ScrollBars.Vertical;
            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            _gridSources.Click += _gridSources_Click;
            _gridSources.CellMouseClick += _gridSources_CellMouseClick;
            _gridSources.DataError += _gridSources_DataError;
            _hostServers.Child = _gridSources;
            _hostServers.VerticalAlignment = VerticalAlignment.Top;
        }

        private void _gridSources_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.Log?.ProcessMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void RePaintSourceGrid()
        {
            try
            {
                if (_gridSources == null)
                {
                    return;
                }
                if (_gridSources.InvokeRequired)
                {
                    _gridSources.Invoke(new Action(RePaintSourceGrid));
                    return;
                }

                _gridSources.Rows.Clear();

                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null)
                {
                    servers = servers.FindAll(s => s != null && s.ServerType != ServerType.Optimizer);
                }

                List<ServerType> serverTypes = ServerMaster.ServersTypes;

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    DataGridViewRow row1 = new DataGridViewRow();
                    row1.Cells.Add(new DataGridViewTextBoxCell());

                    if (servers[i].GetType().BaseType.Name == "AServer")
                    {
                        AServer serv = (AServer)servers[i];
                        row1.Cells[0].Value = serv.ServerNameAndPrefix;
                    }
                    else
                    {
                        row1.Cells[0].Value = servers[i].ServerType;
                    }

                    row1.Cells.Add(new DataGridViewTextBoxCell());
                    row1.Cells[1].Value = servers[i].ServerStatus;

                    bool isAttached = false;

                    for (int j = 0; j < _attachedServers.Count; j++)
                    {
                        if (servers[i].ServerType == _attachedServers[j])
                        {
                            isAttached = true;
                            break;
                        }
                    }

                    if (isAttached == true)
                    {
                        DataGridViewImageCell imageCell = new DataGridViewImageCell();
                        Bitmap bmp = new Bitmap(System.Windows.Forms.Application.StartupPath + "\\Images\\pinBar.png");
                        imageCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        imageCell.Value = bmp;
                        row1.Cells.Add(imageCell);
                    }

                    _gridSources.Rows.Add(row1);

                    serverTypes.Remove(serverTypes.Find(s => s == servers[i].ServerType));

                    if (servers[i].ServerStatus == ServerConnectStatus.Connect)
                    {
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
                        DataGridViewCellStyle style = new DataGridViewCellStyle();
                        style.BackColor = Color.Coral;
                        style.SelectionBackColor = Color.Chocolate;
                        style.ForeColor = Color.Black;
                        style.SelectionForeColor = Color.Black;
                        row1.Cells[1].Style = style;
                        row1.Cells[0].Style = style;
                    }
                }

                for (int i = 0; i < serverTypes.Count; i++)
                {
                    DataGridViewRow row1 = new DataGridViewRow();
                    row1.Cells.Add(new DataGridViewTextBoxCell());
                    row1.Cells[0].Value = serverTypes[i].ToString();
                    row1.Cells.Add(new DataGridViewTextBoxCell());
                    row1.Cells[1].Value = "Disabled";

                    bool isAttached = false;

                    for (int j = 0; j < _attachedServers.Count; j++)
                    {
                        if (serverTypes[i] == _attachedServers[j])
                        {
                            isAttached = true;
                            break;
                        }
                    }

                    if (isAttached == false)
                    {
                        _gridSources.Rows.Add(row1);
                    }
                    else if (isAttached == true)
                    {
                        DataGridViewImageCell imageCell = new DataGridViewImageCell();
                        Bitmap bmp = new Bitmap(System.Windows.Forms.Application.StartupPath + "\\Images\\pinBar.png");
                        imageCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        imageCell.Value = bmp;
                        row1.Cells.Add(imageCell);

                        _gridSources.Rows.Insert(0, row1);
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_gridSources.CurrentCell.RowIndex <= -1)
                {
                    return;
                }

                ServerType type;

                string serverNameFull = _gridSources.SelectedRows[0].Cells[0].Value.ToString();

                if (serverNameFull.Split('_').Length == 3)
                {
                    serverNameFull = serverNameFull.Split('_')[0] + "_" + serverNameFull.Split('_')[1];
                }

                Enum.TryParse(serverNameFull.Split('_')[0], out type);

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null ||
                    servers.Find(serv => serv.ServerType == type) == null)
                {
                    // need to create a server for the first time 
                    // нужно впервые создать сервер
                    ServerMaster.CreateServer(type, true);

                    servers = ServerMaster.GetServers();

                    if (servers == null)
                    { // something went wrong / что-то пошло не так
                        return;
                    }
                    else
                    { // subscribe to the change status event / подписываемся на событие изменения статуса
                        IServer myServ = servers.Find(serv => serv.ServerType == type);

                        if (myServ != null)
                        {
                            myServ.ConnectStatusChangeEvent += ServerStatusChangeEvent;
                        }
                    }
                }

                List<IServer> myServers = servers.FindAll(serv => serv.ServerType == type);

                if (myServers == null
                    || myServers.Count == 0)
                {
                    return;
                }

                int myServerNum = 0;

                for (int i = 0; i < myServers.Count; i++)
                {
                    if (myServers[i].GetType().BaseType.Name == "AServer")
                    {
                        AServer aServer = (AServer)myServers[i];

                        if (aServer.ServerNameUnique == serverNameFull)
                        {
                            myServerNum = aServer.ServerNum;
                            break;
                        }
                    }
                }

                myServers[0].ShowDialog(myServerNum);
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

        private void ServerMaster_ServerDeleteEvent(IServer server)
        {
            try
            {
                server.ConnectStatusChangeEvent -= ServerStatusChangeEvent;

                RePaintSourceGrid();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ServerMasterOnServerCreateEvent(IServer newServer)
        {
            try
            {
                if (newServer.ServerType == ServerType.Optimizer)
                {
                    return;
                }

                List<IServer> servers = ServerMaster.GetServers();

                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerType == ServerType.Optimizer)
                    {
                        continue;
                    }
                    servers[i].ConnectStatusChangeEvent -= ServerStatusChangeEvent;
                    servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
                }
                RePaintSourceGrid();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        void CheckBoxServerAutoOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_boxCreateServerAutomatic.IsChecked.HasValue)
                {
                    ServerMaster.NeedToConnectAuto = _boxCreateServerAutomatic.IsChecked.Value;
                }
                ServerMaster.Save();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MouseEventArgs mouse = (System.Windows.Forms.MouseEventArgs)e;

            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            _mouseXPos = mouse.X;
            _mouseYPos = mouse.Y;
        }

        private int _mouseXPos;

        private int _mouseYPos;

        private ServerType _lastClickServer;

        private void _gridSources_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Right)
                {
                    if (_gridSources.ContextMenuStrip != null)
                    {
                        _gridSources.ContextMenuStrip = null;
                    }
                    return;
                }

                int row = e.RowIndex;

                if (_gridSources.Rows[row].Selected == false)
                {
                    _gridSources.Rows[row].Selected = true;
                }

                ServerType serverType = ServerType.None;

                if (Enum.TryParse(_gridSources.Rows[row].Cells[0].Value.ToString(), out serverType) == false)
                {
                    return;
                }

                _lastClickServer = serverType;

                bool isPin = false;

                for (int i = 0; i < _attachedServers.Count; i++)
                {
                    if (_attachedServers[i] == serverType)
                    {
                        isPin = true;
                        break;
                    }
                }

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem(serverType.ToString()));
                items[0].Enabled = false;

                items.Add(new ToolStripMenuItem(OsLocalization.Market.Label119));
                items[1].Click += _gridSources_ShowSettingsWindow_Click;

                if (isPin == false)
                {
                    items.Add(new ToolStripMenuItem(OsLocalization.Market.Label117));
                    items[2].Click += _gridSources_AttachServer_Click;
                }
                else if (isPin == true)
                {
                    items.Add(new ToolStripMenuItem(OsLocalization.Market.Label118));
                    items[2].Click += _gridSources_DetachServer_Click;
                }

                items.Add(new ToolStripMenuItem(OsLocalization.Market.ButtonConnect));
                items[3].Click += _gridSources_Connect_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Market.ButtonDisconnect));
                items[4].Click += _gridSources_Disconnect_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                _gridSources.ContextMenuStrip = menu;
                _gridSources.ContextMenuStrip.Show(_gridSources, new System.Drawing.Point(_mouseXPos, _mouseYPos));
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_ShowSettingsWindow_Click(object sender, EventArgs e)
        {
            try
            {
                List<IServer> servers = ServerMaster.GetServers();

                IServer myServer = null;

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == _lastClickServer)
                    {
                        myServer = servers[i];
                        break;
                    }
                }

                if (myServer == null)
                {
                    ServerMaster.CreateServer(_lastClickServer, false);
                    Thread.Sleep(1000);
                }

                servers = ServerMaster.GetServers();

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == _lastClickServer)
                    {
                        myServer = servers[i];
                        break;
                    }
                }

                if (myServer == null)
                {
                    return;
                }

                myServer.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_Connect_Click(object sender, EventArgs e)
        {
            try
            {
                List<IServer> servers = ServerMaster.GetServers();

                IServer myServer = null;

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == _lastClickServer)
                    {
                        myServer = servers[i];
                        break;
                    }
                }

                if (myServer == null)
                {
                    ServerMaster.CreateServer(_lastClickServer, false);
                    Thread.Sleep(1000);
                }

                servers = ServerMaster.GetServers();

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == _lastClickServer)
                    {
                        myServer = servers[i];
                        break;
                    }
                }

                if (myServer == null)
                {
                    return;
                }

                myServer.StartServer();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_Disconnect_Click(object sender, EventArgs e)
        {
            try
            {
                List<IServer> servers = ServerMaster.GetServers();

                IServer myServer = null;

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == _lastClickServer)
                    {
                        myServer = servers[i];
                        break;
                    }
                }

                if (myServer == null)
                {
                    return;
                }

                myServer.StopServer();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_AttachServer_Click(object sender, EventArgs e)
        {
            try
            {
                _attachedServers.Add(_lastClickServer);
                SaveAttachedServers();
                RePaintSourceGrid();

                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSources_DetachServer_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < _attachedServers.Count; i++)
                {
                    if (_attachedServers[i] == _lastClickServer)
                    {
                        _attachedServers.RemoveAt(i);
                        i--;
                    }
                }

                SaveAttachedServers();
                RePaintSourceGrid();

                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #region Search in grid

        private System.Windows.Controls.TextBox _textBoxSearchSecurity;

        private System.Windows.Controls.Button _buttonRightInSearchResults;

        private System.Windows.Controls.Button _buttonLeftInSearchResults;

        private System.Windows.Controls.Label _labelCurrentResultShow;

        private System.Windows.Controls.Label _labelCommasResultShow;

        private System.Windows.Controls.Label _labelCountResultsShow;

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (_textBoxSearchSecurity.Text == ""
                    && _textBoxSearchSecurity.IsKeyboardFocused == false)
                {
                    _textBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (_textBoxSearchSecurity.Text == OsLocalization.Market.Label64)
                {
                    _textBoxSearchSecurity.Text = "";
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                if (_textBoxSearchSecurity.Text == "")
                {
                    _textBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            try
            {
                _searchResults.Clear();

                string key = _textBoxSearchSecurity.Text;

                if (key == "")
                {
                    UpdateSearchPanel();
                    return;
                }

                key = key.ToLower();

                int indexFirstSec = int.MaxValue;

                for (int i = 0; i < _gridSources.Rows.Count; i++)
                {
                    string security = "";

                    if (_gridSources.Rows[i].Cells[0].Value != null)
                    {
                        security = _gridSources.Rows[i].Cells[0].Value.ToString();
                    }


                    security = security.ToLower();

                    if (security.Contains(key))
                    {
                        if (security.IndexOf(key) == 0)
                        {
                            indexFirstSec = i;
                        }

                        _searchResults.Add(i);
                    }
                }

                if (_searchResults.Count > 1 && _searchResults.Contains(indexFirstSec) && _searchResults.IndexOf(indexFirstSec) != 0)
                {
                    int index = _searchResults.IndexOf(indexFirstSec);
                    _searchResults.RemoveAt(index);
                    _searchResults.Insert(0, indexFirstSec);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSearchPanel()
        {
            try
            {
                if (_searchResults.Count == 0)
                {
                    _buttonRightInSearchResults.Visibility = Visibility.Hidden;
                    _buttonLeftInSearchResults.Visibility = Visibility.Hidden;
                    _labelCurrentResultShow.Visibility = Visibility.Hidden;
                    _labelCommasResultShow.Visibility = Visibility.Hidden;
                    _labelCountResultsShow.Visibility = Visibility.Hidden;
                    return;
                }

                int firstRow = _searchResults[0];

                _gridSources.Rows[firstRow].Selected = true;
                _gridSources.FirstDisplayedScrollingRowIndex = firstRow;

                if (_searchResults.Count < 2)
                {
                    _buttonRightInSearchResults.Visibility = Visibility.Hidden;
                    _buttonLeftInSearchResults.Visibility = Visibility.Hidden;
                    _labelCurrentResultShow.Visibility = Visibility.Hidden;
                    _labelCommasResultShow.Visibility = Visibility.Hidden;
                    _labelCountResultsShow.Visibility = Visibility.Hidden;
                    return;
                }

                _labelCurrentResultShow.Content = 1.ToString();
                _labelCountResultsShow.Content = (_searchResults.Count).ToString();

                _buttonRightInSearchResults.Visibility = Visibility.Visible;
                _buttonLeftInSearchResults.Visibility = Visibility.Visible;
                _labelCurrentResultShow.Visibility = Visibility.Visible;
                _labelCommasResultShow.Visibility = Visibility.Visible;
                _labelCountResultsShow.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int indexRow = Convert.ToInt32(_labelCurrentResultShow.Content) - 1;

                int maxRowIndex = Convert.ToInt32(_labelCountResultsShow.Content);

                if (indexRow <= 0)
                {
                    indexRow = maxRowIndex;
                    _labelCurrentResultShow.Content = maxRowIndex.ToString();
                }
                else
                {
                    _labelCurrentResultShow.Content = (indexRow).ToString();
                }

                int realInd = _searchResults[indexRow - 1];

                _gridSources.Rows[realInd].Selected = true;
                _gridSources.FirstDisplayedScrollingRowIndex = realInd;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int indexRow = Convert.ToInt32(_labelCurrentResultShow.Content) - 1 + 1;

                int maxRowIndex = Convert.ToInt32(_labelCountResultsShow.Content);

                if (indexRow >= maxRowIndex)
                {
                    indexRow = 0;
                    _labelCurrentResultShow.Content = 1.ToString();
                }
                else
                {
                    _labelCurrentResultShow.Content = (indexRow + 1).ToString();
                }

                int realInd = _searchResults[indexRow];

                _gridSources.Rows[realInd].Selected = true;
                _gridSources.FirstDisplayedScrollingRowIndex = realInd;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SearchCheckLocalization()
        {
            try
            {
                if (_textBoxSearchSecurity == null)
                {
                    return;
                }

                if (_textBoxSearchSecurity.Text != OsLocalization.Market.Label64)
                {
                    _textBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Attaching servers on top

        private List<ServerType> _attachedServers = new List<ServerType>();

        private void LoadAttachedServers()
        {
            if (!File.Exists(@"Engine\AttachedServers.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\AttachedServers.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        ServerType type = new ServerType();

                        if (Enum.TryParse(reader.ReadLine(), true, out type))
                        {
                            _attachedServers.Add(type);
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void SaveAttachedServers()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\AttachedServers.txt", false)
                    )
                {
                    for (int i = 0; i < _attachedServers.Count; i++)
                    {
                        writer.WriteLine(_attachedServers[i].ToString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion
    }
}
