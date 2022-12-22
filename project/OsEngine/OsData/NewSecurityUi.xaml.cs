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
using System.Windows;

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
        public List<Security> SelectedSecurity = new List<Security>();

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
            CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
            CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;

            this.Activate();
            this.Focus();
        }

        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                _grid.Rows[i].Cells[2].Value = isCheck;
            }
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

            DataGridViewCheckBoxColumn colum6 = new DataGridViewCheckBoxColumn();
            //colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label171;
            colum6.ReadOnly = false;
            colum6.Width = 50;
            _grid.Columns.Add(colum6);

            HostSecurity.Child = _grid;


            TextBoxSearchSec.Text = OsLocalization.Trader.Label174;
            TextBoxSearchSec.TextChanged += TextBoxSearchSec_TextChanged;
        }

        // поиск бумаги

        private void TextBoxSearchSec_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TextBoxSearchSec.Text.Contains(OsLocalization.Trader.Label174))
            {
                TextBoxSearchSec.Text = TextBoxSearchSec.Text.Replace(OsLocalization.Trader.Label174, "");
            }

            string str = TextBoxSearchSec.Text;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                DataGridViewRow row = _grid.Rows[i];

                if (row.Cells.Count < 2 ||
                    row.Cells[1].Value == null)
                {
                    continue;
                }

                string secName = row.Cells[0].Value.ToString();

                if (secName.StartsWith(str))
                {
                    row.Selected = true;
                    _grid.FirstDisplayedScrollingRowIndex = i;
                    break;
                }

                string sec2Name = row.Cells[1].Value.ToString();

                if (sec2Name.StartsWith(str))
                {
                    row.Selected = true;
                    _grid.FirstDisplayedScrollingRowIndex = i;
                    break;
                }
            }

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

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Cells[2].Value != null &&
                    _grid.Rows[i].Cells[2].Value.ToString() == "True")
                {
                    Security Selected = _securitiesInBox.Find(
                    security => security.Name == _grid.Rows[i].Cells[0].Value.ToString());
                    SelectedSecurity.Add(Selected);
                }
            }


            Close();
        }
    }
}
