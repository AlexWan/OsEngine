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

namespace OsEngine.Market
{
    public class ServerMasterSourcesPainter
    {
        public ServerMasterSourcesPainter(WindowsFormsHost hostServers, 
            WindowsFormsHost hostLog, 
            System.Windows.Controls.CheckBox boxCreateServerАutomatically)
        {
            _boxCreateServerАutomatically = boxCreateServerАutomatically;
             
            _hostServers = hostServers;

            _hostLog = hostLog;

            _boxCreateServerАutomatically.IsChecked = ServerMaster.NeadToConnectAuto;
            _boxCreateServerАutomatically.Click += CheckBoxServerAutoOpen_Click;

            ServerMaster.Log.StartPaint(_hostLog);

            _servers = ServerMaster.GetServers();

            for (int i = 0; _servers != null && i < _servers.Count; i++)
            {
                _servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
            }

            ServerMaster.ServerCreateEvent += ServerMasterOnServerCreateEvent;

            LoadAttachedServers();

            CreateSourceGrid();
            RePaintSourceGrid();
        }

        public void Dispose()
        {
            try
            {
                ServerMaster.ServerCreateEvent -= ServerMasterOnServerCreateEvent;

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

                if (_boxCreateServerАutomatically != null)
                {
                    _boxCreateServerАutomatically.Click -= CheckBoxServerAutoOpen_Click;
                    _boxCreateServerАutomatically = null;
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

        List<IServer> _servers;

        System.Windows.Controls.CheckBox _boxCreateServerАutomatically;

        WindowsFormsHost _hostServers;

        WindowsFormsHost _hostLog;

        /// <summary>
        /// source table
        /// таблица источников
        /// </summary>
        private DataGridView _gridSources;

        /// <summary>
        /// create source table
        /// сохдать таблицу источников
        /// </summary>
        private void CreateSourceGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridServers();
            newGrid.ScrollBars = ScrollBars.Vertical;
            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            _gridSources.Click += _gridSources_Click;
            _gridSources.CellMouseClick += _gridSources_CellMouseClick;
            _hostServers.Child = _gridSources;
            _hostServers.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// redraw source table
        /// перерисовать таблицу источников
        /// </summary>
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
                    row1.Cells[0].Value = servers[i].ServerType;
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
            catch(Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// double click evet on the source table
        /// событие двойного клика на таблицу источников
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
                Enum.TryParse(_gridSources.SelectedRows[0].Cells[0].Value.ToString(), out type);

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

                IServer myServer = servers.Find(serv => serv.ServerType == type);

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

        /// <summary>
        /// change status event for server
        /// событие измениня статуса сервера
        /// </summary>
        /// <param name="newState"></param>
        void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
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
                if (_boxCreateServerАutomatically.IsChecked.HasValue)
                {
                    ServerMaster.NeadToConnectAuto = _boxCreateServerАutomatically.IsChecked.Value;
                }
                ServerMaster.Save();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Attaching servers on top

        List<ServerType> _attachedServers = new List<ServerType>();

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

                List<MenuItem> items = new List<MenuItem>();

                items.Add(new MenuItem(serverType.ToString()));
                items[0].Enabled = false;

                items.Add(new MenuItem(OsLocalization.Market.Label119));
                items[1].Click += _gridSources_ShowSettingsWindow_Click;

                if (isPin == false)
                {
                    items.Add(new MenuItem(OsLocalization.Market.Label117));
                    items[2].Click += _gridSources_AttachServer_Click;
                }
                else if (isPin == true)
                {
                    items.Add(new MenuItem(OsLocalization.Market.Label118));
                    items[2].Click += _gridSources_DetachServer_Click;
                }

                items.Add(new MenuItem(OsLocalization.Market.ButtonConnect));
                items[3].Click += _gridSources_Connect_Click;

                items.Add(new MenuItem(OsLocalization.Market.ButtonDisconnect));
                items[4].Click += _gridSources_Disconnect_Click;

                ContextMenu menu = new ContextMenu(items.ToArray());

                _gridSources.ContextMenu = menu;
                _gridSources.ContextMenu.Show(_gridSources, new System.Drawing.Point(_mouseXPos, _mouseYPos));
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
                        break;
                    }
                }

                SaveAttachedServers();
                RePaintSourceGrid();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }
    }
}