/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;

namespace OsEngine.Market.AutoFollow
{
    public partial class AddSlavePortfolioUi : Window
    {
        public CopyTrader CopyTraderInstance;

        public AddSlavePortfolioUi(CopyTrader copyTrader)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            CopyTraderInstance = copyTrader;

            CreateSlaveGrid();
            UpdateGridSlave();

            this.Closed += AddSlavePortfolioUi_Closed;

            Title = OsLocalization.Market.Label225;

            Thread worker = new Thread(PainterThreadArea);
            worker.Start();
        }

        private void AddSlavePortfolioUi_Closed(object sender, EventArgs e)
        {
            _windowIsClosed = true;

            _gridSlave.DataError -= _gridSlave_DataError;
            _gridSlave.CellClick -= _gridSlave_CellClick;
            HostSlaves.Child = null;
            _gridSlave.Rows.Clear();
            DataGridFactory.ClearLinks(_gridSlave);

            CopyTraderInstance = null;
        }

        #region Painter thread

        private bool _windowIsClosed;

        private void PainterThreadArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_windowIsClosed == true)
                    {
                        return;
                    }

                    UpdateGridSlave();
                }
                catch (Exception ex)
                {
                    CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Slave grid

        private DataGridView _gridSlave;

        private void CreateSlaveGrid()
        {
            _gridSlave = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
  DataGridViewAutoSizeRowsMode.AllCells);
            _gridSlave.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSlave.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#"; // num
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridSlave.Columns.Add(column0);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label164; // Name
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSlave.Columns.Add(column2);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            //column4.HeaderText = OsLocalization.Market.Label140; // Portfolio
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridSlave.Columns.Add(column4);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = OsLocalization.Market.Label215; // Select
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column6.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridSlave.Columns.Add(column6);

            HostSlaves.Child = _gridSlave;
            _gridSlave.DataError += _gridSlave_DataError;
            _gridSlave.CellClick += _gridSlave_CellClick;
        }

        private void UpdateGridSlave()
        {
            try
            {
                if (_gridSlave.InvokeRequired)
                {
                    _gridSlave.Invoke(new Action(UpdateGridSlave));
                    return;
                }

                // 0 num
                // 1 Name
                // 2 Portfolio
                // 3 Is On
                // 4 Volume type / Copy type
                // 5 Mult
                // 6 Master asset / Order type
                // 7 Slave asset  / Icebert count

                List<AServer> connectors = ServerMaster.GetAServers();
                List<DataGridViewRow> rowsNow = new List<DataGridViewRow>();

                for (int i = 0; i < connectors.Count; i++)
                {
                    List<DataGridViewRow> serverRows = GetRowsByServer(connectors[i], i + 1);

                    if (serverRows != null &&
                        serverRows.Count > 0)
                    {
                        rowsNow.AddRange(serverRows);
                        rowsNow.Add(GetNullRow());
                    }
                }

                if (rowsNow.Count != _gridSlave.Rows.Count)
                { // 1 перерисовываем целиком
                    _gridSlave.Rows.Clear();

                    for (int i = 0; i < rowsNow.Count; i++)
                    {
                        _gridSlave.Rows.Add(rowsNow[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetNullRow()
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].ReadOnly = true;
            }

            return row;
        }

        private List<DataGridViewRow> GetRowsByServer(AServer server, int number)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            // 0 num
            // 1 Name
            // 2 Portfolio
            // 3 Select
            
            // 1 формируем первую строку с названием сервера

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = server.ServerNameUnique;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].Style.BackColor = Color.Black;
                row.Cells[i].ReadOnly = true;
            }

            rows.Add(row);

            // 2 формируем портфели по этому серверу

            List<Portfolio> portfolios = server.Portfolios;

            if (portfolios != null && portfolios.Count != 0)
            {
                rows.Add(GetNullRow());
            }

            for (int i = 0; portfolios != null && i < portfolios.Count; i++)
            {
                List<DataGridViewRow> rowsPortfolio = GetRowsByPortfolio(server, portfolios[i]);

                if (rowsPortfolio != null &&
                    rowsPortfolio.Count != 0)
                {
                    rows.AddRange(rowsPortfolio);
                    if (i + 1 != portfolios.Count)
                    {
                        rows.Add(GetNullRow());
                    }
                }
            }

            return rows;
        }

        private List<DataGridViewRow> GetRowsByPortfolio(AServer server, Portfolio portfolio)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            // 0 num
            // 1 Name
            // 2 Portfolio
            // 3 Select

            // 1 формируем хедеры

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Market.Label140; // Portfolio

            row.Cells.Add(new DataGridViewTextBoxCell());

            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i > 1)
                {
                    row.Cells[i].Style.BackColor = System.Drawing.Color.FromArgb(9, 11, 13);
                }

                row.Cells[i].ReadOnly = true;
            }

            rows.Add(row);

            // 3 формируем первую строку 

            DataGridViewRow row2 = new DataGridViewRow();

            row2.Cells.Add(new DataGridViewTextBoxCell());
            row2.Cells.Add(new DataGridViewTextBoxCell());

            row2.Cells.Add(new DataGridViewTextBoxCell());
            row2.Cells[row2.Cells.Count - 1].Value = portfolio.Number; // Portfolio

            row2.Cells.Add(new DataGridViewButtonCell());
            row2.Cells[row2.Cells.Count - 1].Value = OsLocalization.Market.Label224; //"Select";
            row2.Cells[row2.Cells.Count - 1].ToolTipText = server.ServerNameUnique + "~" + portfolio.Number;
            rows.Add(row2);

            return rows;
        }

        private void _gridSlave_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            CopyTraderInstance.SendLogMessage("_gridSlave_DataError \n"
              + e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _gridSlave_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int col = e.ColumnIndex;

                if (row >= _gridSlave.Rows.Count)
                {
                    return;
                }

                if (_gridSlave.Rows[row].Cells[col] == null
                    || _gridSlave.Rows[row].Cells[col].Value == null
                    || _gridSlave.Rows[row].Cells[col].ToolTipText == null)
                {
                    return;
                }

                if (_gridSlave.Rows[row].Cells[col].ToolTipText.Split('~').Length != 2)
                {
                    return;
                }

                string server = _gridSlave.Rows[row].Cells[col].ToolTipText.Split('~')[0];
                string portfolio = _gridSlave.Rows[row].Cells[col].ToolTipText.Split('~')[1];

                PortfolioToCopy portfolioToCopy =
                        CopyTraderInstance.GetPortfolioByName(server, portfolio);

                Close();
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion
    }
}
