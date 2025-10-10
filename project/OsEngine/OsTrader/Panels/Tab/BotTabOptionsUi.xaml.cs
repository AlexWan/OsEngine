/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabOptionsUi : Window
    {
        private BotTabOptions _tab;
        private List<IServer> _servers;
        private DataGridView _gridSecurities;

        public BotTabOptionsUi(BotTabOptions tab)
        {
            InitializeComponent();
            _tab = tab;

            _servers = ServerMaster.GetServers();
            if (_servers == null || _servers.Count == 0)
            {
                Close();
                return;
            }

            // Initialize controls
            ComboBoxTypeServer.ItemsSource = _servers.Select(s => s.ServerNameAndPrefix);
            if (_servers.Count > 0)
            {
                ComboBoxTypeServer.SelectedItem = _servers[0].ServerNameAndPrefix;
            }

            CreateGrid();
            LoadPortfolioOnBox();
            LoadClassOnBox();
            LoadSecurityOnBox();

            // Add event handlers
            ComboBoxTypeServer.SelectionChanged += (sender, args) =>
            {
                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();
            };
            ComboBoxClass.SelectionChanged += (sender, args) => LoadSecurityOnBox();

            ButtonAccept.Click += ButtonAccept_Click;
            this.Loaded += BotTabOptionsUi_Loaded;
            this.Closed += BotTabOptionsUi_Closed;
        }

        private void BotTabOptionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                this.Closed -= BotTabOptionsUi_Closed;
                ButtonAccept.Click -= ButtonAccept_Click;
                this.Loaded -= BotTabOptionsUi_Loaded;

                if (HostUnderlyingAssets != null)
                {
                    HostUnderlyingAssets.Child = null;
                }

                if (_gridSecurities != null)
                {
                    _gridSecurities.DataError -= _gridSecurities_DataError;
                    DataGridFactory.ClearLinks(_gridSecurities);
                    _gridSecurities = null;
                }
            }
            catch
            {

            }
        }

        private void BotTabOptionsUi_Loaded(object sender, RoutedEventArgs e)
        {
            HostUnderlyingAssets.Child = _gridSecurities;
        }

        private void CreateGrid()
        {
            _gridSecurities = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridSecurities.MultiSelect = true;
            _gridSecurities.DataError += _gridSecurities_DataError;

            _gridSecurities.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "On" });
            _gridSecurities.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", ReadOnly = true });
            _gridSecurities.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Class", ReadOnly = true });

            HostUnderlyingAssets.Child = _gridSecurities;
        }

        private void _gridSecurities_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void LoadPortfolioOnBox()
        {
            var selectedServer = GetSelectedServer();
            if (selectedServer == null || selectedServer.Portfolios == null) return;

            ComboBoxPortfolio.ItemsSource = selectedServer.Portfolios.Select(p => p.Number);
            if (selectedServer.Portfolios.Count > 0)
            {
                ComboBoxPortfolio.SelectedItem = selectedServer.Portfolios[0].Number;
            }
        }

        private void LoadClassOnBox()
        {
            var selectedServer = GetSelectedServer();
            if (selectedServer == null) return;

            var securities = selectedServer.Securities;
            if (securities == null) return;

            var classes = new HashSet<string> { "All" };
            foreach (var security in securities)
            {
                if (!string.IsNullOrEmpty(security.NameClass))
                {
                    classes.Add(security.NameClass);
                }
            }
            ComboBoxClass.ItemsSource = classes;
            ComboBoxClass.SelectedItem = "All";
        }

        private void LoadSecurityOnBox()
        {
            var selectedServer = GetSelectedServer();
            if (selectedServer == null) return;

            var allSecurities = selectedServer.Securities;
            if (allSecurities == null) return;

            // Default to "Options mode": show only underlying assets of options
            var options = allSecurities.Where(s => s.SecurityType == SecurityType.Option && !string.IsNullOrEmpty(s.UnderlyingAsset)).ToList();
            var underlyingAssetNames = new HashSet<string>(options.Select(o => o.UnderlyingAsset));
            var securitiesToDisplay = allSecurities.Where(s => underlyingAssetNames.Contains(s.Name)).ToList();

            // Further filter by class if a class is selected
            if (ComboBoxClass.SelectedItem != null && ComboBoxClass.SelectedItem.ToString() != "All")
            {
                securitiesToDisplay = securitiesToDisplay.Where(s => s.NameClass == ComboBoxClass.SelectedItem.ToString()).ToList();
            }

            UpdateGrid(securitiesToDisplay);
        }

        private void UpdateGrid(List<Security> securities)
        {
            _gridSecurities.Rows.Clear();

            foreach (var security in securities)
            {
                var row = new DataGridViewRow();

                bool isChecked = _tab.UnderlyingAssets != null && _tab.UnderlyingAssets.Contains(security.Name);

                var checkCell = new DataGridViewCheckBoxCell { Value = isChecked };

                row.Cells.Add(checkCell);
                row.Cells.Add(new DataGridViewTextBoxCell { Value = security.Name });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = security.NameClass });
                row.Tag = security;
                _gridSecurities.Rows.Add(row);
            }
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _gridSecurities.EndEdit();

            var selectedUnderlyingAssets = new List<String>();
            foreach (DataGridViewRow row in _gridSecurities.Rows)
            {
                if (row.Cells[0].Value != null && Convert.ToBoolean(row.Cells[0].Value) == true)
                {
                    var security = (Security)row.Tag;
                    selectedUnderlyingAssets.Add(security.Name);
                }
            }

            // _tab.SetUnderlyingAssets(selectedUnderlyingAssets);
            var selectedServer = GetSelectedServer();
            if (selectedServer == null)
            {
                return;
            }

            if (ComboBoxPortfolio.SelectedItem == null)
            {
                AlertMessageSimpleUi uiMessage = new AlertMessageSimpleUi("Portfolio not selected");
                uiMessage.Show();

                return;
            }

            string portfolioName = ComboBoxPortfolio.SelectedItem.ToString();

            _tab.SetUnderlyingAssetsAndStart(selectedUnderlyingAssets, portfolioName, selectedServer);

            Close();
        }

        private IServer GetSelectedServer()
        {
            if (ComboBoxTypeServer.SelectedItem == null) return null;
            return _servers.FirstOrDefault(s => s.ServerNameAndPrefix == ComboBoxTypeServer.SelectedItem.ToString());
        }
    }
}