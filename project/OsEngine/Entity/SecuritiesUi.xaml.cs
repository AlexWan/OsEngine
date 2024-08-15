/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Market.Servers;

namespace OsEngine.Entity
{
    /// <summary>
    /// server securities settings window
    /// </summary>
    public partial class SecuritiesUi
    {
        /// <summary>
        /// the server that owns the securities
        /// </summary>
        private IServer _server;

        public SecuritiesUi(IServer server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            CreateTable();
            PaintSecurities(server.Securities);

            _server = server;
            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            Title = OsLocalization.Entity.TitleSecuritiesUi + " " + _server.ServerType;

            this.Activate();
            this.Focus();

            this.Closed += SecuritiesUi_Closed;
        }

        private void SecuritiesUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
                _server = null;
             
                _grid.CellValueChanged -= _grid_CellValueChanged;
                DataGridFactory.ClearLinks(_grid);
                _grid = null;
                HostSecurities.Child = null;
            }
            catch
            {

            }
        }

        /// <summary>
        /// spreadsheet for drawing securities
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// create a table of securities
        /// </summary>
        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
    DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#";
            column0.ReadOnly = true;
            column0.Width = 70;
            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.SecuritiesColumn1;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            DataGridViewColumn column11 = new DataGridViewColumn();
            column11.CellTemplate = cell0;
            column11.HeaderText = "Full name";
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column11);

            DataGridViewColumn column12 = new DataGridViewColumn();
            column12.CellTemplate = cell0;
            column12.HeaderText = "Name ID";
            column12.ReadOnly = true;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column12);

            DataGridViewColumn column13 = new DataGridViewColumn();
            column13.CellTemplate = cell0;
            column13.HeaderText = "Class";
            column13.ReadOnly = true;
            column13.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column13);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Entity.SecuritiesColumn2;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Entity.SecuritiesColumn3;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Entity.SecuritiesColumn4;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Entity.SecuritiesColumn5;
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column5);

            HostSecurities.Child = _grid;
            HostSecurities.Child.Show();
            HostSecurities.Child.Refresh();
            _grid.CellValueChanged += _grid_CellValueChanged;

        }

        /// <summary>
        /// changed value in the table
        /// </summary>
        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveFromTable();
        }

        /// <summary>
        /// securities has changed in the server
        /// </summary>
        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintSecurities(securities);
        }

        /// <summary>
        /// draw securities on the chart
        /// </summary>
        private void PaintSecurities(List<Security> securities)
        {
            if(securities == null)
            {
                return;
            }

            if(_grid == null)
            {
                return;
            }

            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<List<Security>>(PaintSecurities), securities);
                return;
            }

            bool isInArray;
            for (int indexSecurity = 0; indexSecurity < securities.Count; indexSecurity++)
            {
                isInArray = false;

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (_grid.Rows[i].Cells[0].Value.ToString() == securities[indexSecurity].Name)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray)
                {
                    continue;
                }

                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = indexSecurity;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecurity].Name;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecurity].NameFull;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecurity].NameId;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = securities[indexSecurity].NameClass;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = securities[indexSecurity].SecurityType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = securities[indexSecurity].Lot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = securities[indexSecurity].PriceStep;

                _grid.Rows.Add(nRow);

            }
        }

        /// <summary>
        /// save securities from table
        /// </summary>
        private void SaveFromTable()
        {
            List<Security> securities = _server.Securities;

            if (securities == null)
            {

            }
            // not implemented
            // не реализовано
        }
    }
}
