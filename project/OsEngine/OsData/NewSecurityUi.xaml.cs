/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
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

            HostSecurity.Child = _grid;
        }

        /// <summary>
        /// unload all available classes in the class selection menu/выгрузить все доступные классы в меню выбора классов
        /// </summary>
        private void GetClasses()
        {
            List<string> classes = new List<string>();
            for (int i = 0; i < _securities.Count; i++)
            {
                if (classes.Find(s => s == _securities[i].NameClass) == null)
                {
                    classes.Add(_securities[i].NameClass);
                    ComboBoxClass.Items.Add(_securities[i].NameClass);
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

                if (_securities[i].NameFull== null ||
                    _securities[i].NameFull[0] == '\'' && _securities[i].NameFull[1] == '\'' &&
                    _securities[i].NameFull.Length == 2)
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
            SelectedSecurity = _securitiesInBox.Find(security => security.NameFull == _grid.SelectedCells[1].Value.ToString());
            Close();
        }

    }
}
