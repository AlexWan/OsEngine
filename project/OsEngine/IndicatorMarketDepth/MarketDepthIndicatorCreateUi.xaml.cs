/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.IndicatorMarketDepth
{
    public partial class MarketDepthIndicatorCreateUi : Window
    {
        private class IndicatorRowInfo
        {
            public string Type;
            public string Source;
            public string Location;
            public string Description;
        }

        private List<string> _existingNames;

        private DataGridView _grid;

        private List<IndicatorRowInfo> _rowsInfo = new List<IndicatorRowInfo>();

        private List<int> _searchResults = new List<int>();

        private int _searchPosition;

        public MarketDepthIndicatorCreateUi(List<string> existingNames)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _existingNames = existingNames != null ? existingNames : new List<string>();

            BuildRowsInfo();
            CreateTable();
            FillTable(_rowsInfo);

            TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            TextBoxName.Text = GetNextIndicatorName();

            Title = OsLocalization.ConvertToLocString("Eng:Create indicator for market depth or trades tape_Ru:Окно создания индикатора для стакана или ленты сделок_");
            LabelName.Content = OsLocalization.ConvertToLocString("Eng:Name_Ru:Имя_");
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

            TextBoxSearchSecurity.GotFocus += TextBoxSearchSecurity_GotFocus;
            TextBoxSearchSecurity.LostFocus += TextBoxSearchSecurity_LostFocus;

            Closed += MarketDepthIndicatorCreateUi_Closed;

            UpdateSearchResults();
            UpdateSearchPanel();

            this.Activate();
            this.Focus();
        }

        public bool IsAccepted;

        public string IndicatorType = "";

        public string IndicatorName = "";

        private void MarketDepthIndicatorCreateUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= MarketDepthIndicatorCreateUi_Closed;

                TextBoxSearchSecurity.GotFocus -= TextBoxSearchSecurity_GotFocus;
                TextBoxSearchSecurity.LostFocus -= TextBoxSearchSecurity_LostFocus;

                _grid.CellClick -= _grid_CellClick;
                _grid.DataError -= _grid_DataError;

                HostIndicators.Child = null;
                DataGridFactory.ClearLinks(_grid);
                _grid.Rows.Clear();
                _grid.Columns.Clear();
                _grid.DataSource = null;
                _grid.Dispose();
                _grid = null;

                HostIndicators.Dispose();

                _rowsInfo.Clear();
                _searchResults.Clear();
                _existingNames = null;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BuildRowsInfo()
        {
            List<string> names = MarketDepthIndicatorsFactory.GetIndicatorsNames();

            for (int i = 0; i < names.Count; i++)
            {
                string type = names[i];
                MarketDepthIndicatorDataSource source = MarketDepthIndicatorsFactory.GetIndicatorDataSource(type);

                IndicatorRowInfo info = new IndicatorRowInfo();
                info.Type = type;
                info.Source = GetSourceText(source);
                info.Location = "Include";
                info.Description = MarketDepthIndicatorsFactory.GetIndicatorDescription(type);
                _rowsInfo.Add(info);
            }
        }

        private string GetSourceText(MarketDepthIndicatorDataSource source)
        {
            if (source == MarketDepthIndicatorDataSource.Trades)
            {
                return OsLocalization.ConvertToLocString("Eng:Trades_Ru:Лента сделок_");
            }

            if (source == MarketDepthIndicatorDataSource.Both)
            {
                return OsLocalization.ConvertToLocString("Eng:MarketDepth + Trades_Ru:Стакан + лента_");
            }

            return OsLocalization.ConvertToLocString("Eng:MarketDepth_Ru:Стакан_");
        }

        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ReadOnly = true;
            _grid.ScrollBars = ScrollBars.Vertical;
            _grid.DefaultCellStyle.SelectionBackColor = Themes.ThemeManager.GetColorWinForms("GridSelectionBackColor");
            _grid.DefaultCellStyle.SelectionForeColor = Themes.ThemeManager.GetColorWinForms("ColorForeground");

            DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
            cell.Style = _grid.DefaultCellStyle;

            DataGridViewColumn number = new DataGridViewColumn();
            number.CellTemplate = cell;
            number.HeaderText = "#";
            number.ReadOnly = true;
            number.Width = 30;
            number.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(number);

            DataGridViewColumn type = new DataGridViewColumn();
            type.CellTemplate = cell;
            type.HeaderText = OsLocalization.ConvertToLocString("Eng:Indicator type_Ru:Тип индикатора_");
            type.ReadOnly = true;
            type.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            type.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(type);

            DataGridViewColumn source = new DataGridViewColumn();
            source.CellTemplate = cell;
            source.HeaderText = OsLocalization.ConvertToLocString("Eng:Source_Ru:Источник_");
            source.ReadOnly = true;
            source.Width = 120;
            source.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(source);

            DataGridViewButtonColumn description = new DataGridViewButtonColumn();
            description.CellTemplate = new DataGridViewButtonCell();
            description.HeaderText = OsLocalization.ConvertToLocString("Eng:Description_Ru:Описание_");
            description.ReadOnly = true;
            description.Width = 90;
            description.SortMode = DataGridViewColumnSortMode.NotSortable;
            description.FlatStyle = FlatStyle.Flat;
            description.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            description.Text = "?";
            description.UseColumnTextForButtonValue = true;
            _grid.Columns.Add(description);

            DataGridViewColumn location = new DataGridViewColumn();
            location.CellTemplate = cell;
            location.HeaderText = OsLocalization.ConvertToLocString("Eng:Location_Ru:Расположение_");
            location.ReadOnly = true;
            location.Width = 120;
            location.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(location);

            HostIndicators.Child = _grid;
            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void FillTable(List<IndicatorRowInfo> rows)
        {
            _grid.Rows.Clear();

            for (int i = 0; i < rows.Count; i++)
            {
                IndicatorRowInfo info = rows[i];
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell { Value = (i + 1).ToString() });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = info.Type });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = info.Source });

                DataGridViewButtonCell descriptionCell = new DataGridViewButtonCell();
                descriptionCell.Value = "?";
                descriptionCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                row.Cells.Add(descriptionCell);

                row.Cells.Add(new DataGridViewTextBoxCell { Value = info.Location });

                _grid.Rows.Add(row);
            }
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex != 3 || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                {
                    return;
                }

                string type = _grid.Rows[e.RowIndex].Cells[1].Value.ToString();
                string description = MarketDepthIndicatorsFactory.GetIndicatorDescription(type);

                if (string.IsNullOrEmpty(description) == false)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(description);
                    ui.ShowDialog();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSearchResults()
        {
            _searchResults.Clear();

            string key = TextBoxSearchSecurity.Text == null ? "" : TextBoxSearchSecurity.Text.Trim().ToLowerInvariant();

            if (key == OsLocalization.Market.Label64.ToLowerInvariant())
            {
                key = "";
            }

            for (int i = 0; i < _rowsInfo.Count; i++)
            {
                IndicatorRowInfo info = _rowsInfo[i];

                if (string.IsNullOrEmpty(key)
                    || info.Type.ToLowerInvariant().Contains(key)
                    || info.Source.ToLowerInvariant().Contains(key)
                    || info.Description.ToLowerInvariant().Contains(key)
                    || info.Location.ToLowerInvariant().Contains(key))
                {
                    _searchResults.Add(i);
                }
            }
        }

        private void UpdateSearchPanel()
        {
            if (_searchResults.Count == 0)
            {
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            if (_searchPosition >= _searchResults.Count)
            {
                _searchPosition = 0;
            }

            int rowIndex = _searchResults[_searchPosition];
            _grid.Rows[rowIndex].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = rowIndex;

            LabelCurrentResultShow.Content = (_searchPosition + 1).ToString();
            LabelCommasResultShow.Content = " / ";
            LabelCountResultsShow.Content = _searchResults.Count.ToString();

            ButtonLeftInSearchResults.Visibility = Visibility.Visible;
            ButtonRightInSearchResults.Visibility = Visibility.Visible;
            LabelCurrentResultShow.Visibility = Visibility.Visible;
            LabelCommasResultShow.Visibility = Visibility.Visible;
            LabelCountResultsShow.Visibility = Visibility.Visible;
        }

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
                {
                    TextBoxSearchSecurity.Clear();
                }
                else
                {
                    TextBoxSearchSecurity.SelectAll();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxSearchSecurity.Text))
                {
                    TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_searchResults.Count == 0)
                {
                    return;
                }

                _searchPosition = (_searchPosition + 1) % _searchResults.Count;
                UpdateSearchPanel();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_searchResults.Count == 0)
                {
                    return;
                }

                _searchPosition = (_searchPosition - 1 + _searchResults.Count) % _searchResults.Count;
                UpdateSearchPanel();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private string GetNextIndicatorName()
        {
            int num = 1;

            while (true)
            {
                string name = "Ind" + num;
                bool exists = false;

                for (int i = 0; i < _existingNames.Count; i++)
                {
                    if (_existingNames[i] == name)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists == false)
                {
                    return name;
                }

                num++;
            }
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_grid.SelectedCells.Count == 0)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                        "Eng:Select an indicator first_Ru:Сначала выберите индикатор_"));
                    ui.ShowDialog();
                    return;
                }

                int row = _grid.SelectedCells[0].RowIndex;
                IndicatorType = _grid.Rows[row].Cells[1].Value.ToString();

                string name = TextBoxName.Text == null ? "" : TextBoxName.Text.Trim();

                if (name == "")
                {
                    name = GetNextIndicatorName();
                }

                bool duplicate = false;

                for (int i = 0; i < _existingNames.Count; i++)
                {
                    if (_existingNames[i] == name)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                        "Eng:An indicator with this name already exists_Ru:Индикатор с таким именем уже существует_"));
                    ui.ShowDialog();
                    return;
                }

                IndicatorName = name;
                IsAccepted = true;
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsAccepted = false;
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
