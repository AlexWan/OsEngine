using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Market.Servers.Kraken;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для ProxyUi.xaml
    /// </summary>
    public partial class ProxiesUi
    {
        public ProxiesUi(List<ProxyHolder> proxies, KrakenServer server)
        {
            InitializeComponent();

            _proxies = proxies;
            _server = server;
            CreateTable();
            ReloadTable();
        }

        private KrakenServer _server;
        private List<ProxyHolder> _proxies;

        private void ButtonДобавить_Click(object sender, RoutedEventArgs e)
        {
            ProxyHolderAddUi ui = new ProxyHolderAddUi();
            ui.ShowDialog();

            if (ui.Proxy != null)
            {
                _proxies.Add(ui.Proxy);
                ReloadTable();
                _server.SaveProxies(_proxies);
            }
        }

        /// <summary>
        /// таблица для прорисовки сообщений с ошибками
        /// </summary>
        private DataGridView _gridErrorLog;

        private void CreateTable()
        {
            if (_gridErrorLog != null)
            {
                return;
            }
            _gridErrorLog = new DataGridView();

            _gridErrorLog = new DataGridView();

            _gridErrorLog.AllowUserToOrderColumns = true;
            _gridErrorLog.AllowUserToResizeRows = true;
            _gridErrorLog.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _gridErrorLog.AllowUserToDeleteRows = false;
            _gridErrorLog.AllowUserToAddRows = false;
            _gridErrorLog.RowHeadersVisible = false;
            _gridErrorLog.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridErrorLog.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _gridErrorLog.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"IP";
            column0.ReadOnly = true;
            column0.Width = 170;

            _gridErrorLog.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Имя";
            column1.ReadOnly = true;
            column1.Width = 100;

            _gridErrorLog.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Пароль";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridErrorLog.Columns.Add(column);

            Host.Child = _gridErrorLog;
        }

        private void ReloadTable()
        {
            _gridErrorLog.Rows.Clear();

            for (int i = 0; i < _proxies.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _proxies[i].Ip;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = _proxies[i].UserName;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[2].Value = _proxies[i].UserPassword;

                _gridErrorLog.Rows.Add(row);
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            int num = -1;
            for (int i = 0; i < _gridErrorLog.Rows.Count; i++)
            {
                if (_gridErrorLog.Rows[i].Selected)
                {
                    num = i;
                    break;
                }
            }

            if (num == -1 || num >= _gridErrorLog.Rows.Count)
            {
                return;
            }

            _proxies.RemoveAt(num);
            ReloadTable();

            _server.SaveProxies(_proxies);
        }

    }
}