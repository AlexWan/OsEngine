/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Market.Servers;

namespace OsEngine.Market
{
    /// <summary>
    /// Логика взаимодействия для ServerPrimeUi.xaml
    /// </summary>
    public partial class ServerMasterUi
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="isTester">вызывается ли метод из тестера</param>
        public ServerMasterUi(bool isTester)
        {
            InitializeComponent();

            List<IServer> servers = ServerMaster.GetServers();

            if (isTester)
            {
                servers = ServerMaster.GetServers();

                if (servers == null ||
                    servers.Find(s => s.ServerType == ServerType.Tester) == null)
                {
                    ServerMaster.CreateServer(ServerType.Tester, false);
                }

                Close();
                
                servers = ServerMaster.GetServers();
                servers.Find(s => s.ServerType == ServerType.Tester).ShowDialog();
            }

            CreateSourceGrid();
            RePaintSourceGrid();

            CheckBoxServerAutoOpen.IsChecked = ServerMaster.NeadToConnectAuto;
            CheckBoxServerAutoOpen.Click += CheckBoxServerAutoOpen_Click;
            CheckBoxServerAutoOpen.ToolTip = "При включении, мастер серверов будет пытаться автоматически развернуть " +
                                             "\r" +
                                             "сервера которые у него запрашивают роботы(панели). По одному разу на каждый сервер. ";

            ServerMaster.Log.StartPaint(HostLog);

            for (int i = 0; servers != null && i < servers.Count; i++)
            {
                servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
            }
        }

        void CheckBoxServerAutoOpen_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxServerAutoOpen.IsChecked.HasValue)
            {
                ServerMaster.NeadToConnectAuto = CheckBoxServerAutoOpen.IsChecked.Value;
            }
            ServerMaster.Save();
        }

        /// <summary>
        /// таблица источников
        /// </summary>
        private DataGridView _gridSources;

        /// <summary>
        /// сохдать таблицу источников
        /// </summary>
        private void CreateSourceGrid()
        {
            DataGridView newGrid = new DataGridView();

            newGrid.AllowUserToOrderColumns = false;
            newGrid.AllowUserToResizeRows = false;
            newGrid.AllowUserToDeleteRows = false;
            newGrid.AllowUserToAddRows = false;
            newGrid.RowHeadersVisible = false;
            newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            newGrid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = @"Источник";
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Статус";
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            HostSource.Child = _gridSources;
            HostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGrid()
        {
            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(RePaintSourceGrid));
                return;
            }

            _gridSources.Rows.Clear();

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; servers != null && i < servers.Count; i++)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = servers[i].ServerType;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = servers[i].ServerStatus;
                _gridSources.Rows.Add(row1);
            }

            bool bitfinex = false;
            bool bitMexIsOn = false;
            bool quikIsOn = false;
            bool smartcomIsOn = false;
            bool plazaIsOn = false;
            bool ibIsOn = false;
            bool astsIsOn = false;
            bool quikLuaIsOn = false;
            bool oandaIsOn = false;
            bool krakenIsOn = false;
            bool bitStampIsOn = false;
            bool binanceIsOn = false;
            bool ninjaIsOn = false;

            for (int i = 0; i < _gridSources.Rows.Count; i++)
            {
                DataGridViewRow row1 =_gridSources.Rows[i];

                ServerType type;

                Enum.TryParse(row1.Cells[0].Value.ToString(), out type);

                if (type == ServerType.Bitfinex)
                {
                    bitfinex = true;
                }

                if (type == ServerType.Binance)
                {
                    binanceIsOn = true;
                }

                if (type == ServerType.NinjaTrader)
                {
                    ninjaIsOn = true;
                }

                if (type == ServerType.BitStamp)
                {
                    bitStampIsOn = true;
                }

                if (type == ServerType.BitMex)
                {
                    bitMexIsOn = true;
                }

                if (type == ServerType.Kraken)
                {
                    krakenIsOn = true;
                }

                if (type == ServerType.Oanda)
                {
                    oandaIsOn = true;
                }

                if (type == ServerType.QuikLua)
                {
                    quikLuaIsOn = true;
                }

                if (type == ServerType.QuikDde)
                {
                    quikIsOn = true;
                }
                if (type == ServerType.SmartCom)
                {
                    smartcomIsOn = true;
                }
                if (type == ServerType.Plaza)
                {
                    plazaIsOn = true;
                }
                if(type == ServerType.InteractivBrokers)
                {
                    ibIsOn = true;
                }
                if (type == ServerType.AstsBridge)
                {
                    astsIsOn = true;
                }
            }


            if (quikIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.QuikDde;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (smartcomIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.SmartCom;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (ninjaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.NinjaTrader;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (ibIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.InteractivBrokers;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }

            if (binanceIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Binance;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitMexIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.BitMex;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitfinex == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Bitfinex;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (krakenIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Kraken;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitStampIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.BitStamp;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (plazaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Plaza;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (quikLuaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.QuikLua;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (oandaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Oanda;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (astsIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.AstsBridge;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
        }

        /// <summary>
        /// событие двойного клика на таблицу источников
        /// </summary>
        void _gridSources_DoubleClick(object sender, EventArgs e)
        {
            if (_gridSources.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            ServerType type;
            Enum.TryParse(_gridSources.SelectedRows[0].Cells[0].Value.ToString(),out type);

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null ||
                servers.Find(serv => serv.ServerType == type) == null)
            {
                // нужно впервые создать сервер
                ServerMaster.CreateServer(type, true);

                servers = ServerMaster.GetServers();

                if (servers == null)
                { // чтото пошло не так
                    return;
                }
                else
                { // подписываемся на событие изменения статуса
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
        /// событие измениня статуса сервера
        /// </summary>
        /// <param name="newState"></param>
        void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

        /// <summary>
        /// сохдать таблицу источников
        /// </summary>
        private void CreateSourceGridTestsBackGround()
        {
            DataGridView newGrid = new DataGridView();

            newGrid.AllowUserToOrderColumns = false;
            newGrid.AllowUserToResizeRows = false;
            newGrid.AllowUserToDeleteRows = false;
            newGrid.AllowUserToAddRows = false;
            newGrid.RowHeadersVisible = false;
            newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            newGrid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = @"Источник";
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Статус";
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            HostSource.Child = _gridSources;
            HostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGridTestsBackGround()
        {
            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(RePaintSourceGridTestsBackGround));
                return;
            }

            _gridSources.Rows.Clear();

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; servers != null && i < servers.Count; i++)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = servers[i].ServerType;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = servers[i].ServerStatus;
                _gridSources.Rows.Add(row1);
            }

            bool bitfinex = false;
            bool bitMexIsOn = false;
            bool quikIsOn = false;
            bool smartcomIsOn = false;
            bool plazaIsOn = false;
            bool ibIsOn = false;
            bool astsIsOn = false;
            bool quikLuaIsOn = false;
            bool oandaIsOn = false;
            bool krakenIsOn = false;
            bool bitStampIsOn = false;
            bool binanceIsOn = false;
            bool ninjaIsOn = false;

            for (int i = 0; i < _gridSources.Rows.Count; i++)
            {
                DataGridViewRow row1 = _gridSources.Rows[i];

                ServerType type;

                Enum.TryParse(row1.Cells[0].Value.ToString(), out type);

                if (type == ServerType.Bitfinex)
                {
                    bitfinex = true;
                }

                if (type == ServerType.Binance)
                {
                    binanceIsOn = true;
                }

                if (type == ServerType.NinjaTrader)
                {
                    ninjaIsOn = true;
                }

                if (type == ServerType.BitStamp)
                {
                    bitStampIsOn = true;
                }

                if (type == ServerType.BitMex)
                {
                    bitMexIsOn = true;
                }

                if (type == ServerType.Kraken)
                {
                    krakenIsOn = true;
                }

                if (type == ServerType.Oanda)
                {
                    oandaIsOn = true;
                }

                if (type == ServerType.QuikLua)
                {
                    quikLuaIsOn = true;
                }

                if (type == ServerType.QuikDde)
                {
                    quikIsOn = true;
                }
                if (type == ServerType.SmartCom)
                {
                    smartcomIsOn = true;
                }
                if (type == ServerType.Plaza)
                {
                    plazaIsOn = true;
                }
                if (type == ServerType.InteractivBrokers)
                {
                    ibIsOn = true;
                }
                if (type == ServerType.AstsBridge)
                {
                    astsIsOn = true;
                }
            }


            if (quikIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.QuikDde;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (smartcomIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.SmartCom;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (ninjaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.NinjaTrader;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (ibIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.InteractivBrokers;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }

            if (binanceIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Binance;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitMexIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.BitMex;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitfinex == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Bitfinex;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (krakenIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Kraken;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (bitStampIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.BitStamp;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (plazaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Plaza;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (quikLuaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.QuikLua;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (oandaIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.Oanda;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
            if (astsIsOn == false)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = ServerType.AstsBridge;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = "Disconnect";
                _gridSources.Rows.Add(row1);
            }
        }

    }
}
