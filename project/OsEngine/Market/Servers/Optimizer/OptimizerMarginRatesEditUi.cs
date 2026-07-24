/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OsEngine.Market.Servers.Optimizer
{
    public class OptimizerMarginRatesEditUi : Window
    {
        private WindowsFormsHost _host;
        private DataGridView _dgv;
        private List<ListTableSumm> _listTableSumm;
        private int _year;
        private System.Windows.Controls.Button _createButton;
        private OptimizerDataStorage _server;
        private StackPanel _mainPanel;

        public OptimizerMarginRatesEditUi(OptimizerDataStorage server, List<ListTableSumm> list, int year)
        {
            try
            {
                _year = year;
                _listTableSumm = list;
                _server = server;

                Title = OsLocalization.ConvertToLocString($"Eng:Margin rates {year} year_Ru:Ставки маржи {year} год_");
                Width = 460;
                Height = 350;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Topmost = false;
                Style = (Style)FindResource("WindowStyleNoResize");
                Icon = GetIcon();

                _mainPanel = new StackPanel();
                _mainPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                _mainPanel.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 21, 26, 30));

                _host = new WindowsFormsHost();
                _host.Child = GetTable();
                FillTableSumm();

                _createButton = new System.Windows.Controls.Button();
                _createButton.Content = OsLocalization.ConvertToLocString("Eng:Accept_Ru:Принять_");
                _createButton.Width = 120;
                _createButton.Height = 30;
                _createButton.Margin = new Thickness(300, 0, 0, 0);
                _createButton.Click += CreateButton_Click;

                _mainPanel.Children.Add(_host);
                _mainPanel.Children.Add(_createButton);

                Content = _mainPanel;

                Closed += OptimizerMarginRatesEditUi_Closed;

                Activate();
                Focus();
                Show();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _server.SetMarginTableSumm(_year, _listTableSumm);
                Close();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void OptimizerMarginRatesEditUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= OptimizerMarginRatesEditUi_Closed;
                _createButton.Click -= CreateButton_Click;
                _createButton = null;
                _dgv = null;
                _host = null;
                _mainPanel = null;
                _server = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private ImageSource GetIcon()
        {
            try
            {
                return new BitmapImage(new Uri("pack://application:,,,/OsEngine;component/Images/OsLogo.ico"));
            }
            catch
            {
                return null;
            }
        }

        private DataGridView GetTable()
        {
            try
            {
                _dgv = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

                _dgv.Dock = DockStyle.Fill;
                _dgv.ScrollBars = ScrollBars.Vertical;
                _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

                _dgv.ColumnCount = 3;
                _dgv.RowCount = 0;

                _dgv.Columns[0].HeaderText = OsLocalization.ConvertToLocString("Eng:Amount margin_" + "Ru:Сумма непокрытой позиции_");
                _dgv.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Type rate_" + "Ru:Вид ставки_");
                _dgv.Columns[2].HeaderText = OsLocalization.ConvertToLocString("Eng:Rate_" + "Ru:Ставка_");

                _dgv.Columns[0].ReadOnly = true;
                _dgv.Columns[1].ReadOnly = true;

                _dgv.Columns[0].Width = 250;
                _dgv.Columns[1].Width = 100;
                _dgv.Columns[2].Width = 100;

                foreach (DataGridViewColumn column in _dgv.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }

                _dgv.CellValueChanged += _dgv_CellValueChanged;
                _dgv.DataError += _dgv_DataError;

                return _dgv;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void _dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void _dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0
                    || e.RowIndex >= _listTableSumm.Count)
                {
                    return;
                }

                if (e.ColumnIndex == 2)
                {
                    ListTableSumm list = new ListTableSumm();

                    list.Summ = _listTableSumm[e.RowIndex].Summ;
                    list.TypeValue = _dgv.Rows[e.RowIndex].Cells[1].Value?.ToString() == TypeValueTableSumm.Absolute.ToString() ? TypeValueTableSumm.Absolute : TypeValueTableSumm.Percent;

                    decimal rate = 0;
                    decimal.TryParse(_dgv.Rows[e.RowIndex].Cells[2].Value?.ToString(), out rate);
                    list.Rate = rate;

                    _listTableSumm[e.RowIndex] = list;
                    _server.SetMarginTableSumm(_year, _listTableSumm);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void FillTableSumm()
        {
            try
            {
                for (int i = 0; i < _listTableSumm.Count; i++)
                {
                    DataGridViewRow row = new DataGridViewRow();

                    if (i == _listTableSumm.Count - 1)
                    {
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"более {_listTableSumm[i - 1].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                    }
                    else
                    {
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"до {_listTableSumm[i].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                    }

                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].TypeValue });
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].Rate });

                    _dgv.Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
    }
}
