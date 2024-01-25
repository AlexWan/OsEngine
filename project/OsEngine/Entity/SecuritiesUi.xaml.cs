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
            server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;
            _server = server;

            Title = OsLocalization.Entity.TitleSecuritiesUi;

            this.Activate();
            this.Focus();
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
            _grid = DataGridFactory.GetDataGridSecurities();

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

            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<List<Security>>(PaintSecurities), securities);
                return;
            }

            bool isInArray;
            for (int indexSecuriti = 0; indexSecuriti < securities.Count; indexSecuriti++)
            {
                isInArray = false;

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (_grid.Rows[i].Cells[0].Value.ToString() == securities[indexSecuriti].Name)
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
                nRow.Cells[0].Value = securities[indexSecuriti].Name;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = securities[indexSecuriti].SecurityType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = securities[indexSecuriti].Lot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecuriti].PriceStep;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = securities[indexSecuriti].PriceStepCost;

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
