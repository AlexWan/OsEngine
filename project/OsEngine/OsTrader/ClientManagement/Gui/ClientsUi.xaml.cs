/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using System;
using System.Net;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsTrader.ClientManagement
{
    /// <summary>
    /// Interaction logic for ClientsUi.xaml
    /// </summary>
    public partial class ClientsUi : Window
    {
        ClientManagementMaster _master;

        public ClientsUi()
        {
            InitializeComponent();

            _master = ClientManagementMaster.Instance;
            _master.Log.StartPaint(HostLog);
            _master.DeleteClientEvent += _master_DeleteClientEvent;
            _master.NewClientEvent += _master_NewClientEvent;

            CreateClientsGrid();
            RePaintClientsGrid();

            Closed += ClientsUi_Closed;
        }

        private void ClientsUi_Closed(object sender, EventArgs e)
        {
            _master.Log.StopPaint();
            _master.DeleteClientEvent -= _master_DeleteClientEvent;
            _master.NewClientEvent -= _master_NewClientEvent;
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
            colum01.HeaderText = OsLocalization.Trader.Label175;//"Name";
            colum01.ReadOnly = false;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = "";//"Connectors";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(colum02);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = "";//"Portfolios";
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientsGrid.Columns.Add(colum04);

            DataGridViewColumn colum05 = new DataGridViewColumn();
            colum05.CellTemplate = cell0;
            colum05.HeaderText = "";//"Robots";
            colum05.ReadOnly = true;
            colum05.Width = 120;
            _clientsGrid.Columns.Add(colum05);

            DataGridViewColumn column06 = new DataGridViewColumn();
            column06.HeaderText = ""; // Positions
            column06.ReadOnly = true;
            column06.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column06.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _clientsGrid.Columns.Add(column06);

            DataGridViewColumn column07 = new DataGridViewColumn();
            column07.HeaderText = ""; // Deploy
            column07.ReadOnly = true;
            column07.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column07.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _clientsGrid.Columns.Add(column07);

            DataGridViewColumn column08 = new DataGridViewColumn();
            column08.HeaderText = ""; // Delete
            column08.ReadOnly = true;
            column08.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column08.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _clientsGrid.Columns.Add(column08);

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
            //"Num";
            //"Name";
            //"Connectors";
            //"Portfolios";
            //"Robots";
            // Positions
            // Deploy
            // Delete

            DataGridViewRow row = new DataGridViewRow();
            
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Add new";

            for(int i = 0;i < row.Cells.Count;i++)
            {
                row.Cells[i].ReadOnly = true;
            }

            //row.Cells[6].Value = OsLocalization.Trader.Label584;  //"Clients";
            
            return row;
        }

        private DataGridViewRow GetClientRow(TradeClient client)
        {
            //"Num";
            //"Name";
            //"Connectors";
            //"Portfolios";
            //"Robots";
            // Positions
            // Deploy
            // Delete

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = client.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = client.Number;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Connectors";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Portfolios";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Robots";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Positions";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Deploy";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = "Delete";

            //row.Cells[6].Value = OsLocalization.Trader.Label584;  //"Clients";

            return row;
        }

        private void _clientsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int columnIndex = e.ColumnIndex;
            int rowIndex = e.RowIndex;

            if(rowIndex == _master.Clients.Count
                && columnIndex == 7)
            { // Add new
                _master.AddNewClient();
            }


        }


        #endregion

    }
}
