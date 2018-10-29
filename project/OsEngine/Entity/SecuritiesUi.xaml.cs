/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OsEngine.Market.Servers;

namespace OsEngine.Entity
{
    /// <summary>
    /// Окно настроек бумаг сервера
    /// </summary>
    public partial class SecuritiesUi
    {
        /// <summary>
        /// сервер которому принадлежат бумаги
        /// </summary>
        private IServer _server;

        public SecuritiesUi(IServer server)
        {
            InitializeComponent();
            CreateTable();
            PaintSecurities(server.Securities);
            server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;
            _server = server;
        }

        /// <summary>
        /// таблица для прорисовки бумаг
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// создать таблицу бумаг
        /// </summary>
        private void CreateTable()
        {
            _grid = new DataGridView();
            HostSecurities.Child = _grid;

            _grid.AllowUserToOrderColumns = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Название";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Тип";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Лот";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = @"Шаг цены";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = @"Цена шага цены";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column4);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = "";
            column8.ReadOnly = true;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column8);

            HostSecurities.Child.Show();
            HostSecurities.Child.Refresh();
            _grid.CellValueChanged += _grid_CellValueChanged;

        }

        /// <summary>
        /// изменилось значение в таблице
        /// </summary>
        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveFromTable();
        }

        /// <summary>
        /// в сервере изменились бумаги
        /// </summary>
        /// <param name="securities">бумаги</param>
        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintSecurities(securities);
        }

        /// <summary>
        /// прорисовать бумаги на графике
        /// </summary>
        /// <param name="securities">бумаги</param>
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
        /// сохранить бумаги из таблицы
        /// </summary>
        private void SaveFromTable()
        {
            List<Security> securities = _server.Securities;

            if (securities == null)
            {

            }

            // не реализовано
        }
    }
}
