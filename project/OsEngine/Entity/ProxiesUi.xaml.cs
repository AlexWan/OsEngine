/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Market.Servers.Kraken;

namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction logic for  ProxyUi.xaml
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

            Title = OsLocalization.Entity.TitleProxiesUi;
            ButtonДобавить.Content = OsLocalization.Entity.ProxiesLabel1;
            ButtonDelete.Content = OsLocalization.Entity.ProxiesLabel2;
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
        /// table for drawing error messages
        /// таблица для прорисовки сообщений с ошибками
        /// </summary>
        private DataGridView _gridErrorLog;

        private void CreateTable()
        {
            if (_gridErrorLog != null)
            {
                return;
            }

            _gridErrorLog = DataGridFactory.GetDataGridProxies();

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