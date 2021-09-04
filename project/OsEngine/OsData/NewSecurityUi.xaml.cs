/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.OsData
{

    /// <summary>
    /// Interaction Logic for NewSecurityDialog.xaml/Логика взаимодействия для NewSecurityDialog.xaml
    /// </summary>
    public partial class NewSecurityUi
    {
        private const string ComboBoxClassNameAll = "All";
        private const string ComboBoxClassNameMoexTop = "МосБиржа топ";

        /// <summary>
        /// papers that are in the server/бумаги которые есть в сервере
        /// </summary>
        private readonly List<Security> _securities;
        private readonly Dictionary<string, List<Security>> _securityGroupsDictionary;

        /// <summary>
        /// paper table/таблица для бумаг
        /// </summary>
        private readonly DataGridView _grid;

        /// <summary>
        /// selected paper/выбранная бумага
        /// </summary>
        public Security SelectedSecurity { get; private set; }

        /// <summary>
        /// constructor/конструктор
        /// </summary>
        /// <param name="securities">papers available for selection/бумаги доступные к выбору</param>
        public NewSecurityUi(List<Security> securities)
        {
            InitializeComponent();
            _securities = securities;
            _securityGroupsDictionary = BuildSecurityGroupsDictionary(_securities);

            InitializeClassesComboBox();
            _grid = CreateTable();
            ReloadSecurityTable();

            Title = OsLocalization.Data.TitleNewSecurity;
            Label1.Content = OsLocalization.Data.Label1;
            ButtonAccept.Content = OsLocalization.Data.ButtonAccept;
        }

        private Dictionary<string, List<Security>> BuildSecurityGroupsDictionary(List<Security> securities)
        {
            var dictionary = securities
                .Where(s => !IsSecurityEmpty(s)
                    && !string.IsNullOrEmpty(s.NameClass))
                .GroupBy(s => s.NameClass)
                .ToDictionary(s => s.Key, s => s.ToList());

            dictionary[ComboBoxClassNameAll] = securities;

            return dictionary;
        }

        /// <summary>
        /// create a table for papers/создать таблицу для бумаг
        /// </summary>
        private DataGridView CreateTable()
        {
            var grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label2;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Data.Label3;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column1);

            grid.KeyPress += SearchSecurity;
            FilterTextBox.TextChanged += FilterTextBox_TextChanged;

            HostSecurity.Child = grid;

            return grid;
        }

        /// <summary>
        /// search string/строка поиска
        /// </summary>
        private string _searchString;

        /// <summary>
        /// when a key was pressed/когда была нажата кнопка
        /// </summary>
        private DateTime _startSearch;

        /// <summary>
        /// search security in table/поиск бумаги в таблице 
        /// </summary>
        private void SearchSecurity(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Back)
            {
                _startSearch = DateTime.Now;
                _searchString = "";
                LabelSearchString.Content = "";
                return;
            }

            if (!char.IsLetter(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                return;
            }

            int freshnessTime = 3; // seconds

            if (_startSearch == null || DateTime.Now.Subtract(_startSearch).Seconds > freshnessTime)
            {
                _startSearch = DateTime.Now;
                _searchString = e.KeyChar.ToString();
                RefreshSearchLabel(freshnessTime);
            }
            else
            {
                _searchString += e.KeyChar.ToString();
                RefreshSearchLabel(freshnessTime);
            }

            char[] charsToTrim = { '*', ' ', '\'', '\"', '+', '=', '-', '!', '#', '%', '.', ',' };

            for (int c = 0; c < _grid.Columns.Count; c++)
            {
                for (int r = 0; r < _grid.Rows.Count; r++)
                {
                    if (_grid.Rows[r].Cells[c].Value.ToString().Trim(charsToTrim)
                        .StartsWith(_searchString, true, CultureInfo.InvariantCulture))
                    {
                        _grid.Rows[r].Cells[c].Selected = true;
                        return; // stop looping
                    }
                }
            }
        }

        private DateTime _filterTextBoxChangedDateTime;
        private static readonly TimeSpan _filterTextBoxSearchDelay = TimeSpan.FromSeconds(1);

        private void FilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _filterTextBoxChangedDateTime = DateTime.Now;

            Task.Run(async () => {

                while (true)
                {
                    await Task.Delay(100);

                    if (DateTime.Now >= _filterTextBoxChangedDateTime.Add(_filterTextBoxSearchDelay))
                    {
                        FilterTextBox.Dispatcher.Invoke(() =>
                        {
                            ReloadSecurityTable();
                        });
                        return;
                    }
                }
            });
        }

        /// <summary>
        /// refresh search label/обновить строку поиска
        /// </summary>
        private void RefreshSearchLabel(int freshnessTime)
        {
            LabelSearchString.Content = "🔍 " + _searchString;

            // clear search label after freshnessTime + 1 (seconds)
            // очистить строку поиска через freshnessTime + 1 (секунд)
            Task.Run(async () => {

                await Task.Delay((freshnessTime + 1) * 1000);

                if (DateTime.Now.Subtract(_startSearch).Seconds > freshnessTime)
                {
                    LabelSearchString.Dispatcher.Invoke(() =>
                    {
                        LabelSearchString.Content = "";
                    });
                }
            });
        }

        /// <summary>
        /// unload all available classes in the class selection menu/выгрузить все доступные классы в меню выбора классов
        /// </summary>
        private void InitializeClassesComboBox()
        {
            // order securities by class / упорядочить бумаги по классу
            var hasMoexTop = false;
            foreach (var nameClass in _securityGroupsDictionary.Keys.OrderBy(n => n))
            {
                if (nameClass == ComboBoxClassNameMoexTop)
                {
                    hasMoexTop = true;
                }

                ComboBoxClass.Items.Add(nameClass);
            }

            if (hasMoexTop)
            {
                ComboBoxClass.SelectedItem = ComboBoxClassNameMoexTop;
            }
            else
            {
                ComboBoxClass.SelectedItem = ComboBoxClassNameAll;
            }

            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;
        }

        /// <summary>
        /// security doesn't contain enough info/бумага не содержит достаточно информации
        /// </summary>
        private bool IsSecurityEmpty(Security security)
        {
            return security == null ||
                string.IsNullOrEmpty(security.Name) || 
                string.IsNullOrEmpty(security.NameFull);
        }

        /// <summary>
        /// reload tool selection menu/перезагрузить меню выбора инструментов
        /// </summary>
        private void ReloadSecurityTable()
        {
            if (ComboBoxClass.SelectedItem == null)
            {
                return;
            }

            var selectedNameClass = ComboBoxClass.SelectedItem.ToString();
            var filterText = FilterTextBox.Text.Trim();

            var securityGroup = _securityGroupsDictionary[selectedNameClass];

            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            foreach (var security in securityGroup)
            {
                if (!string.IsNullOrEmpty(filterText) && !ContainsText(security, filterText))
                {
                    continue;
                }

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = security.Name;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = security.NameFull;
                row.Tag = security;

                rows.Add(row);
            }

            _grid.Rows.Clear();
            _grid.Rows.AddRange(rows.ToArray());
        }

        private bool ContainsText(Security security, string filterText)
        {
            return security.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0
                || security.NameFull.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// the selected item in the class selection menu has changed/изменился выбранный элемент в меню выбора классов
        /// </summary>
        void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ReloadSecurityTable();
        }

        /// <summary>
        /// "Accept" button pressed/нажата кнопка "Принять"
        /// </summary>
        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_grid.SelectedRows.Count <= 0)
            {
                return;
            }

            SelectedSecurity = _grid.SelectedRows[0].Tag as Security;

            if (SelectedSecurity == null)
            {
                return;
            }

            Close();
        }
    }
}
