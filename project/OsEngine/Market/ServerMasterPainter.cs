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

namespace OsEngine.Market
{
    public class ServerMasterPainter
    {
        public ServerMasterPainter(WindowsFormsHost hostServers, 
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

            CreateSourceGrid();
            RePaintSourceGrid();
        }

        public void Dispose()
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

        private void ClearControls()
        {
            if(_gridSources == null)
            {
                return;
            }

            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(ClearControls));
                return;
            }

            if(_boxCreateServerАutomatically != null)
            {
                _boxCreateServerАutomatically.Click -= CheckBoxServerAutoOpen_Click;
                _boxCreateServerАutomatically = null;
            }

            if(_hostServers != null)
            {
                _hostServers.Child = null;
                _hostServers = null;
            }

            if(_hostLog != null)
            {
                _hostLog.Child = null;
                _hostLog = null;
            }
            
            if(_gridSources != null)
            {
                _gridSources.DoubleClick -= _gridSources_DoubleClick;
                _gridSources.Rows.Clear();
                _gridSources.Columns.Clear();
                DataGridFactory.ClearLinks(_gridSources);
                _gridSources = null;
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
            _hostServers.Child = _gridSources;
            _hostServers.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// redraw source table
        /// перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGrid()
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
                _gridSources.Rows.Add(row1);
            }
        }

        /// <summary>
        /// double click evet on the source table
        /// событие двойного клика на таблицу источников
        /// </summary>
        void _gridSources_DoubleClick(object sender, EventArgs e)
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
            if(newServer.ServerType == ServerType.Optimizer)
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

        void CheckBoxServerAutoOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_boxCreateServerАutomatically.IsChecked.HasValue)
            {
                ServerMaster.NeadToConnectAuto = _boxCreateServerАutomatically.IsChecked.Value;
            }
            ServerMaster.Save();
        }
    }
}
