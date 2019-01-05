﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace OsEngine.Market.Servers.InteractivBrokers
{
    /// <summary>
    /// Логика взаимодействия для IbContractStorageUi.xaml
    /// </summary>
    public partial class IbContractStorageUi 
    {
        private DataGridView _grid;

        public IbContractStorageUi(List<SecurityIb> secToSubscrible)
        {
            InitializeComponent();
            SecToSubscrible = secToSubscrible;

            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Базовый актив";
            column0.ReadOnly = false;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
           // column0.Width = 150;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Биржа";
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
           // column.Width = 150;
            _grid.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column1.ReadOnly = false;
           // column1.Width = 150;
            column1.HeaderText = @"Тип Инструмента";
            _grid.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column3.ReadOnly = false;
            // column1.Width = 150;
            column3.HeaderText = @"Символ";
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column4.ReadOnly = false;
            // column1.Width = 150;
            column4.HeaderText = @"Биржа основная";
            _grid.Columns.Add(column4);


            _grid.Rows.Add(null, null);
            _grid.Click += _grid_Click;
            _grid.CellValueChanged += _grid_CellValueChanged;
            Host.Child = _grid;
            LoadSecOnTable();
        }

        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveSecFromTable();
        }

        public List<SecurityIb> SecToSubscrible;

        private void LoadSecOnTable()
        {
            if (!Host.CheckAccess())
            {
                Host.Dispatcher.Invoke(new Action(LoadSecOnTable));
                return;
            }

            _grid.Rows.Clear();

            if (SecToSubscrible == null ||
                SecToSubscrible.Count == 0)
            {
                return;
            }

            for (int i = 0; SecToSubscrible != null && SecToSubscrible.Count != 0 && i < SecToSubscrible.Count; i++)
            {

                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = SecToSubscrible[i].Symbol;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = SecToSubscrible[i].Exchange;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = SecToSubscrible[i].SecType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = SecToSubscrible[i].LocalSymbol;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = SecToSubscrible[i].PrimaryExch;

                _grid.Rows.Add(nRow);
            }
        }

        private void SaveSecFromTable()
        {
            if (SecToSubscrible == null ||
                SecToSubscrible.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                SecurityIb security = SecToSubscrible[i];

                security.Symbol = Convert.ToString(_grid.Rows[i].Cells[0].Value);
                security.Exchange = Convert.ToString(_grid.Rows[i].Cells[1].Value);
                security.SecType = Convert.ToString(_grid.Rows[i].Cells[2].Value);
                security.LocalSymbol = Convert.ToString(_grid.Rows[i].Cells[3].Value);
                security.PrimaryExch = Convert.ToString(_grid.Rows[i].Cells[4].Value);
            }
        }

        private void ButtonAsk_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Поскольку у IB очень много инструментов, Вам нужно указать какие именно типы инструментов Вы хотите подгрузить."+
                " Типы Инструментов: FUT STK CASH BOND CFD FOP WAR FWD BAG IND BILL FUND FIXED SLB CMDTY BSK ICU ICS");
        }

        void _grid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            // cоздание контекстного меню

            MenuItem[] items = new MenuItem[2];

            items[0] = new MenuItem();
            items[0].Text = @"Удалить";
            items[0].Click += AlertDelete_Click;

            items[1] = new MenuItem() { Text = @"Добавить" };
            items[1].Click += AlertCreate_Click;

            ContextMenu menu = new ContextMenu(items);
            _grid.ContextMenu = menu;
            _grid.ContextMenu.Show(_grid, new System.Drawing.Point(mouse.X, mouse.Y));
        }

        void AlertDelete_Click(object sender, EventArgs e)
        {
            if (_grid.CurrentCell == null || _grid.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            SecToSubscrible.RemoveAt(_grid.CurrentCell.RowIndex);
            LoadSecOnTable();
        }

        void AlertCreate_Click(object sender, EventArgs e)
        {
            if (SecToSubscrible == null)
            {
                SecToSubscrible = new List<SecurityIb>();
            }
            SecToSubscrible.Add(new SecurityIb());
            LoadSecOnTable();
        }
    }
}
