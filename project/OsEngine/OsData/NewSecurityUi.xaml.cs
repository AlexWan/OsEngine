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
        /// <summary>
        /// papers that are in the server/бумаги которые есть в сервере
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// selected paper/выбранная бумага
        /// </summary>
        public Security SelectedSecurity;

        /// <summary>
        /// constructor/конструктор
        /// </summary>
        /// <param name="securities">papers available for selection/бумаги доступные к выбору</param>
        public NewSecurityUi(List<Security> securities)
        {
            InitializeComponent();
            _securities = securities;

            GetClasses();
            CreateTable();
            ReloadSecurityTable();
            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

            Title = OsLocalization.Data.TitleNewSecurity;
            Label1.Content = OsLocalization.Data.Label1;
            ButtonAccept.Content = OsLocalization.Data.ButtonAccept;

        }

        /// <summary>
        /// paper table/таблица для бумаг
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// create a table for papers/создать таблицу для бумаг
        /// </summary>
        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label2;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Data.Label3;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            _grid.KeyPress += SearchSecurity;

            HostSecurity.Child = _grid;
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

        /// <summary>
        /// refresh search label/обновить строку поиска
        /// </summary>
        private void RefreshSearchLabel(int freshnessTime)
        {
            LabelSearchString.Content = "🔍 " + _searchString;

            // clear search label after freshnessTime + 1 (seconds)
            // очистить строку поиска через freshnessTime + 1 (секунд)
            Task t = new Task(async () => {

                await Task.Delay((freshnessTime+1)*1000);

                if (DateTime.Now.Subtract(_startSearch).Seconds > freshnessTime)
                {
                    LabelSearchString.Dispatcher.Invoke(() =>
                    {
                        LabelSearchString.Content = "";
                    });
                }
            });
            t.Start();
        }

        /// <summary>
        /// unload all available classes in the class selection menu/выгрузить все доступные классы в меню выбора классов
        /// </summary>
        private void GetClasses()
        {
            // order securities by class / упорядочить бумаги по классу
            List<Security> orderedSecurities = _securities.OrderBy(s => s.NameClass).ToList();
            List<string> classes = new List<string>();
            for (int i = 0; i < orderedSecurities.Count; i++)
            {
                if (classes.Find(s => s == orderedSecurities[i].NameClass) == null && 
                    !IsSecurityEmpty(orderedSecurities[i]))
                {
                    if (orderedSecurities[i].NameClass == null)
                    {
                        continue;
                    }
                    classes.Add(orderedSecurities[i].NameClass);
                    ComboBoxClass.Items.Add(orderedSecurities[i].NameClass);
                }
            }

            ComboBoxClass.Items.Add("All");

            if (classes.Find(clas => clas == "МосБиржа топ") != null)
            {
                ComboBoxClass.SelectedItem = "МосБиржа топ";
            }
            else
            {
                ComboBoxClass.SelectedItem = "All";
            }
        }

        /// <summary>
        /// security doesn't contain enough info/бумага не содержит достаточно информации
        /// </summary>
        private bool IsSecurityEmpty(Security security)
        {
            return string.IsNullOrEmpty(security.Name) || 
                   string.IsNullOrEmpty(security.NameFull);
        }

        /// <summary>
        /// currently displayed papers/отображаемые на текущий момент бумаги
        /// </summary>
        private List<Security> _securitiesInBox = new List<Security>();

        /// <summary>
        /// reload tool selection menu/перезагрузить меню выбора инструментов
        /// </summary>
        private void ReloadSecurityTable()
        {
            if (ComboBoxClass.SelectedItem == null)
            {
                return;
            }

            _securitiesInBox = new List<Security>();
            _grid.Rows.Clear();

            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            for (int i = 0; _securities != null && i < _securities.Count; i++)
            {
                if (ComboBoxClass.SelectedItem.ToString() != "All" && _securities[i].NameClass != ComboBoxClass.SelectedItem.ToString())
                {
                    continue;
                }

                if (IsSecurityEmpty(_securities[i]))
                {
                    continue;
                }

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _securities[i].Name;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = _securities[i].NameFull;

                rows.Add(row);

                _securitiesInBox.Add(_securities[i]);
            }

            _grid.Rows.AddRange(rows.ToArray());
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
            if (_grid.SelectedCells[0] == null ||
                string.IsNullOrWhiteSpace(_grid.SelectedCells[0].ToString()))
            {
                return;
            }

            SelectedSecurity = _securitiesInBox.Find(
                security => security.Name == _grid.SelectedCells[0].Value.ToString());
            Close();
        }
    }
}
