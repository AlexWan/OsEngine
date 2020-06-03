using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Tester;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.OsOptimizer
{

    /// <summary>
    /// Interaction logic for TabIndexOptimizerUi.xaml
    /// Логика взаимодействия для TabIndexOptimizerUi.xaml
    /// </summary>
    public partial class TabIndexOptimizerUi
    {
        public TabIndexOptimizerUi(List<SecurityTester> securities, TabIndexEndTimeFrame index)
        {
            InitializeComponent();

            _securities = securities;
            CreateTable();

            List<string> timeFrame = new List<string>();

            if (securities[0].DataType == SecurityTesterDataType.Candle)
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    if (timeFrame.Find(n => n == securities[i].TimeFrame.ToString()) == null)
                    {
                        timeFrame.Add(securities[i].TimeFrame.ToString());
                    }
                }
            }
            else
            {
                timeFrame.Add(TimeFrame.Sec2.ToString());
                timeFrame.Add(TimeFrame.Sec5.ToString());
                timeFrame.Add(TimeFrame.Sec10.ToString());
                timeFrame.Add(TimeFrame.Sec15.ToString());
                timeFrame.Add(TimeFrame.Sec20.ToString());
                timeFrame.Add(TimeFrame.Sec30.ToString());
                timeFrame.Add(TimeFrame.Min1.ToString());
                timeFrame.Add(TimeFrame.Min2.ToString());
                timeFrame.Add(TimeFrame.Min5.ToString());
                timeFrame.Add(TimeFrame.Min10.ToString());
                timeFrame.Add(TimeFrame.Min15.ToString());
                timeFrame.Add(TimeFrame.Min20.ToString());
                timeFrame.Add(TimeFrame.Min30.ToString());
                timeFrame.Add(TimeFrame.Hour1.ToString());
                timeFrame.Add(TimeFrame.Hour2.ToString());
                timeFrame.Add(TimeFrame.Hour4.ToString());
            }
            for (int i = 0; i < timeFrame.Count; i++)
            {
                ComboBoxTimeFrame.Items.Add(timeFrame[i]);
            }
            
            if (index != null)
            {
                Index = new TabIndexEndTimeFrame();
                Index.Formula = index.Formula;
                Index.TimeFrame = index.TimeFrame;
                Index.NamesSecurity = new List<string>();

                for (int i = 0; index.NamesSecurity != null && i < index.NamesSecurity.Count; i++)
                {
                    Index.NamesSecurity.Add(index.NamesSecurity[i]);
                }
                TextBoxFormula.Text = Index.Formula;
                ComboBoxTimeFrame.SelectedItem = Index.TimeFrame.ToString();
                PaintTable();
                
            }
            else
            {
                Index = new TabIndexEndTimeFrame();
            }

            Title = OsLocalization.Optimizer.Title1;
            Label1.Content = OsLocalization.Optimizer.Label1;
            Label2.Content = OsLocalization.Optimizer.Label2;
            Label3.Content = OsLocalization.Optimizer.Label3;
            ButtonAddSecurity.Content = OsLocalization.Optimizer.Label4;
            ButtonDeleteSecurity.Content = OsLocalization.Optimizer.Label5;
            ButtonAccept.Content = OsLocalization.Optimizer.Label6;
        }

        private List<SecurityTester> _securities;

        public TabIndexEndTimeFrame Index;

        public bool NeadToSave;

        private DataGridView _securitiesNamesGrid;

        private void CreateTable()
        {
            _securitiesNamesGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _securitiesNamesGrid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Номер";
            column0.ReadOnly = true;
            column0.Width = 100;

            _securitiesNamesGrid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Бумага";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _securitiesNamesGrid.Columns.Add(column1);

            _securitiesNamesGrid.Rows.Add(null, null);

            HostSecuritiesName.Child = _securitiesNamesGrid;
        }

        private void PaintTable()
        {
            if (_securitiesNamesGrid.InvokeRequired)
            {
                _securitiesNamesGrid.Invoke(new Action(PaintTable));
                return;
            }
            _securitiesNamesGrid.CellValueChanged -= _securitiesNamesGrid_CellValueChanged;

            _securitiesNamesGrid.Rows.Clear();

            List<string> strings = Index.NamesSecurity;

            if (strings == null ||
                strings.Count == 0)
            {
                return;
            }
            for (int i = 0; i < strings.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = "A" + i;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                cell.ReadOnly = false;


                for (int i2 = 0; i2 < _securities.Count; i2++)
                {
                    bool isInTheArray = false;
                    for (int i3 = 0; i3 < cell.Items.Count; i3++)
                    {
                        if (cell.Items[i3].ToString() == _securities[i2].Security.Name)
                        {
                            isInTheArray = true;
                            break;
                        }
                    }

                    if (isInTheArray)
                    {
                        continue;
                    }

                    cell.Items.Add(_securities[i2].Security.Name);
                }

                cell.Value = strings[i];
                row.Cells.Add(cell);

                _securitiesNamesGrid.Rows.Add(row);
            }
            _securitiesNamesGrid.CellValueChanged += _securitiesNamesGrid_CellValueChanged;
        }

        void _securitiesNamesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            for (int i = 0; i < _securitiesNamesGrid.Rows.Count; i++)
            {
                if (_securitiesNamesGrid.Rows[i].Cells[0].Value == null ||
                    _securitiesNamesGrid.Rows[i].Cells[1].Value == null)
                {
                    return;
                }
            }

            for (int i = 0; i < _securitiesNamesGrid.Rows.Count; i++)
            {
                Index.NamesSecurity[i] = _securitiesNamesGrid.Rows[i].Cells[1].Value.ToString();
            }
        }

        private void ButtonAddSecurity_Click(object sender, RoutedEventArgs e)
        {
            Index.NamesSecurity.Add("");
            PaintTable();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxFormula.Text))
            {
                MessageBox.Show("Формула не назначена. Сохранение невозможно.");
                return;
            }

            for (int i = 0; Index.NamesSecurity != null && i < Index.NamesSecurity.Count; i++)
            {
                if (string.IsNullOrEmpty(Index.NamesSecurity[i]))
                {
                    Index.NamesSecurity.RemoveAt(i);
                    i--;
                }
            }

            if (Index.NamesSecurity == null || Index.NamesSecurity.Count == 0)
            {
                MessageBox.Show("Ни одна бумага не назначена. Сохранение невозможно.");
                return;
            }

            if (ComboBoxTimeFrame.SelectedItem == null)
            {
                MessageBox.Show("ТаймФрейм не назначен. Сохранение невозможно.");
                return;
            }

            Enum.TryParse(ComboBoxTimeFrame.SelectedItem.ToString(), out Index.TimeFrame); 
            Index.Formula = TextBoxFormula.Text;

            NeadToSave = true;
            Close();
        }

        private void ButtonDeleteSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_securitiesNamesGrid.SelectedCells == null ||
                _securitiesNamesGrid.SelectedCells[0] == null)
            {
                return;
            }

            int rowNum = _securitiesNamesGrid.SelectedCells[0].RowIndex;

            if (rowNum >= Index.NamesSecurity.Count ||
                rowNum < 0)
            {
                return;
            }

            Index.NamesSecurity.RemoveAt(rowNum);

            PaintTable();
        }
    }
}
