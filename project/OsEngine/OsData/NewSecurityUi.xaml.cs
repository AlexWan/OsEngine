﻿/*
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
using System.Windows.Input;

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
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _securities = securities;

            GetClasses();
            CreateTable();
            ReloadSecurityTable();
            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

            Title = OsLocalization.Data.TitleNewSecurity;
            Label1.Content = OsLocalization.Data.Label1;
            TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            ButtonAccept.Content = OsLocalization.Data.ButtonAccept;
            CheckBoxSelectAllCheckBox.Content = OsLocalization.Trader.Label173;
            CheckBoxSelectAllCheckBox.Click += CheckBoxSelectAllCheckBox_Click;

            ButtonRightInSearchResults.Visibility = Visibility.Hidden;
            ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
            LabelCurrentResultShow.Visibility = Visibility.Hidden;
            LabelCommasResultShow.Visibility = Visibility.Hidden;
            LabelCountResultsShow.Visibility = Visibility.Hidden;
            TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
            ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
            TextBoxSearchSecurity.KeyDown += TextBoxSearchSecurity_KeyDown;			

            this.Activate();
            this.Focus();
        }

        private void CheckBoxSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isCheck = CheckBoxSelectAllCheckBox.IsChecked.Value;

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                _gridSecurities.Rows[i].Cells[2].Value = isCheck;
            }
        }

        /// <summary>
        /// paper table/таблица для бумаг
        /// </summary>
        private DataGridView _gridSecurities;

        /// <summary>
        /// create a table for papers/создать таблицу для бумаг
        /// </summary>
        private void CreateTable()
        {
            _gridSecurities = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            _gridSecurities.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSecurities.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label2;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Data.Label3;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column1);

            DataGridViewCheckBoxColumn colum6 = new DataGridViewCheckBoxColumn();
            //colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label171;
            colum6.ReadOnly = false;
            colum6.Width = 50;
            _gridSecurities.Columns.Add(colum6);

            HostSecurity.Child = _gridSecurities;
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
            _gridSecurities.Rows.Clear();

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

            _gridSecurities.Rows.AddRange(rows.ToArray());
        }

        /// <summary>
        /// the selected item in the class selection menu has changed/изменился выбранный элемент в меню выбора классов
        /// </summary>
        void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ReloadSecurityTable();
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        /// <summary>
        /// "Accept" button pressed/нажата кнопка "Принять"
        /// </summary>
        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_gridSecurities.SelectedCells[0] == null ||
                string.IsNullOrWhiteSpace(_gridSecurities.SelectedCells[0].ToString()))
            {
                return;
            }

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                if (_gridSecurities.Rows[i].Cells[2].Value != null &&
                    _gridSecurities.Rows[i].Cells[2].Value.ToString() == "True")
                {
                    Security Selected = _securitiesInBox.Find(
                    security => security.Name == _gridSecurities.Rows[i].Cells[0].Value.ToString());
                    SelectedSecurity.Add(Selected);
                }
            }


            Close();
        }

        #region поиск по таблице бумаг

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == ""
                && TextBoxSearchSecurity.IsKeyboardFocused == false)
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
            {
                TextBoxSearchSecurity.Text = "";
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == "")
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            _searchResults.Clear();

            string key = TextBoxSearchSecurity.Text;

            if (key == "")
            {
                UpdateSearchPanel();
                return;
            }

            key = key.ToLower();

            for (int i = 0; i < _gridSecurities.Rows.Count; i++)
            {
                string security = "";
                string secSecond = "";

                if (_gridSecurities.Rows[i].Cells[0].Value != null)
                {
                    security = _gridSecurities.Rows[i].Cells[0].Value.ToString();
                }

                if (_gridSecurities.Rows[i].Cells[1].Value != null)
                {
                    secSecond = _gridSecurities.Rows[i].Cells[1].Value.ToString();
                }

                security = security.ToLower();
                secSecond = secSecond.ToLower();

                if (security.Contains(key) ||
                    secSecond.Contains(key))
                {
                    _searchResults.Add(i);
                }
            }
        }

        private void UpdateSearchPanel()
        {
            if (_searchResults.Count == 0)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            int firstRow = _searchResults[0];

            _gridSecurities.Rows[firstRow].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = firstRow;

            if (_searchResults.Count < 2)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            LabelCurrentResultShow.Content = 1.ToString();
            LabelCountResultsShow.Content = (_searchResults.Count).ToString();

            ButtonRightInSearchResults.Visibility = Visibility.Visible;
            ButtonLeftInSearchResults.Visibility = Visibility.Visible;
            LabelCurrentResultShow.Visibility = Visibility.Visible;
            LabelCommasResultShow.Visibility = Visibility.Visible;
            LabelCountResultsShow.Visibility = Visibility.Visible;
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow <= 0)
            {
                indexRow = maxRowIndex;
                LabelCurrentResultShow.Content = maxRowIndex.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow).ToString();
            }

            int realInd = _searchResults[indexRow - 1];

            _gridSecurities.Rows[realInd].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1 + 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow >= maxRowIndex)
            {
                indexRow = 0;
                LabelCurrentResultShow.Content = 1.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow + 1).ToString();
            }

            int realInd = _searchResults[indexRow];

            _gridSecurities.Rows[realInd].Selected = true;
            _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void TextBoxSearchSecurity_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    int rowIndex = 0;
                    for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                    {
                        if (_gridSecurities.Rows[i].Selected == true)
                        {
                            rowIndex = i;
                            break;
                        }
                        if (i == _gridSecurities.Rows.Count - 1)
                        {
                            return;
                        }
                    }

                    DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[rowIndex].Cells[2];
                    if (Convert.ToBoolean(checkBox.Value) == false)
                    {
                        checkBox.Value = true;
                        TextBoxSearchSecurity.Text = "";
                    }
                    else
                    {
                        checkBox.Value = false;
                        TextBoxSearchSecurity.Text = "";
                    }
                }
            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }		
        }

        #endregion
    }
}
