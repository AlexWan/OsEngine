/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsTrader.ClientManagement
{
    /// <summary>
    /// Interaction logic for ClientsUi.xaml
    /// </summary>
    public partial class ClientsMasterUi : Window
    {
        ClientManagementMaster _master;

        public ClientsMasterUi()
        {
            InitializeComponent();

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClientMasterUi");

            _master = ClientManagementMaster.Instance;
            _master.Log.StartPaint(HostLog);
            _master.DeleteClientEvent += _master_DeleteClientEvent;
            _master.NewClientEvent += _master_NewClientEvent;
            _master.ClientChangeNameEvent += _master_ClientChangeNameEvent;

            CreateClientsGrid();
            RePaintClientsGrid();

            TabItem1.Header = OsLocalization.Trader.Label584;
            TabItem2.Header = OsLocalization.Trader.Label332;
            this.Title = OsLocalization.Trader.Label591;

            Closed += ClientsUi_Closed;
        }

        private void ClientsUi_Closed(object sender, EventArgs e)
        {
            _master.Log.StopPaint();
            _master.DeleteClientEvent -= _master_DeleteClientEvent;
            _master.NewClientEvent -= _master_NewClientEvent;
            _master.ClientChangeNameEvent -= _master_ClientChangeNameEvent;
            _master = null;

            HostClients.Child = null;

            _clientsGrid.CellClick -= _clientsGrid_CellClick;
            _clientsGrid.DataError -= _clientsGrid_DataError;
            DataGridFactory.ClearLinks(_clientsGrid);
            _clientsGrid = null;
        }

        private void _master_NewClientEvent(TradeClient client)
        {
            RePaintClientsGrid();
        }

        private void _master_DeleteClientEvent(TradeClient client)
        {
            RePaintClientsGrid();
        }

        #region Clients grid

        private DataGridView _clientsGrid;

        private void CreateClientsGrid()
        {
            _clientsGrid =
             DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
             DataGridViewAutoSizeRowsMode.AllCells);

            _clientsGrid.ScrollBars = ScrollBars.Vertical;

            _clientsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _clientsGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "#"; //"Num";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _clientsGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Trader.Label61; //"Name";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(colum01);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.HeaderText = ""; // Settings
            column10.ReadOnly = true;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column10.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(column10);

            DataGridViewColumn column12 = new DataGridViewColumn();
            column12.HeaderText = ""; // Delete
            column12.ReadOnly = true;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column12.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(column12);

            HostClients.Child = _clientsGrid;
            _clientsGrid.CellClick += _clientsGrid_CellClick;
            _clientsGrid.DataError += _clientsGrid_DataError;
        }

        private void _clientsGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendNewLogMessage(e.Exception.ToString(),Logging.LogMessageType.Error);
        } 

        private void RePaintClientsGrid()
        {
            try
            {
                if (_clientsGrid.InvokeRequired)
                {
                    _clientsGrid.Invoke(new Action(RePaintClientsGrid));
                    return;
                }                
                
                //"Num";
                //"Name";
                //"Connectors";
                //"Portfolios";
                //"Robots";
                // Positions
                // Deploy
                // Delete

                int lastShowRowIndex = _clientsGrid.FirstDisplayedScrollingRowIndex;

                _clientsGrid.Rows.Clear();

                for (int i = 0; i < _master.Clients.Count; i++)
                {
                    TradeClient client = _master.Clients[i];

                    if (client == null)
                    {
                        continue;
                    }

                    _clientsGrid.Rows.Add(GetClientRow(client));
                }

                _clientsGrid.Rows.Add(GetAddRow());


                if (lastShowRowIndex > 0 &&
                    lastShowRowIndex < _clientsGrid.Rows.Count)
                {
                    _clientsGrid.FirstDisplayedScrollingRowIndex = lastShowRowIndex;
                    _clientsGrid.Rows[lastShowRowIndex].Selected = true;

                    if (_clientsGrid.Rows[lastShowRowIndex].Cells != null
                        && _clientsGrid.Rows[lastShowRowIndex].Cells[0] != null)
                    {
                        _clientsGrid.Rows[lastShowRowIndex].Cells[0].Selected = true;
                    }
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetAddRow()
        {
            // "Num";
            // "Name";
            // "State";
            // Settings
            // Delete / Add

            DataGridViewRow row = new DataGridViewRow();
            
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label589;

            for (int i = 0;i < row.Cells.Count;i++)
            {
                row.Cells[i].ReadOnly = true;
            }
            
            return row;
        }

        private DataGridViewRow GetClientRow(TradeClient client)
        {
            //"Num";
            //"Name";
            // Settings
            // Delete / Add

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = client.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = client.Name;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label469;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label39;

            return row;
        }

        private void _clientsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //"Num";
                //"Name";
                // Settings
                // Delete / Add

                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                if(rowIndex == -1)
                {
                    return;
                }

                if (rowIndex == _master.Clients.Count
                    && columnIndex == 3)
                { // Add new
                    _master.AddNewClient();
                }
                else if (rowIndex < _master.Clients.Count
                   && columnIndex == 3)
                { // Delete
                    int number = Convert.ToInt32(_clientsGrid.Rows[rowIndex].Cells[0].Value.ToString());

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label590);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    _master.RemoveClientAtNumber(number);
                }
                else if (rowIndex < _master.Clients.Count
                   && columnIndex == 2)
                { // Settings

                    int number = Convert.ToInt32(_clientsGrid.Rows[rowIndex].Cells[0].Value.ToString());

                    _master.ShowDialogClient(number);
                }
            }
            catch(Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void _master_ClientChangeNameEvent()
        {
            RePaintClientsGrid();
        }

        #endregion

    }
}
