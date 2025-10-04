/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction logic for ColorCustomDialog.xaml
    /// </summary>
    public partial class ColorCustomDialog : Window
    {
        public ColorCustomDialog()
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.Entity.CustomColorDialogUiTitle;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

            CreateTableToColors();
            PaintColorsGrid();

            Closed += CustomColorDialog_Closed;
        }

        private void CustomColorDialog_Closed(object sender, EventArgs e)
        {
            if (_isAccepted == false)
            {
                _color = _colorSetUser;
            }

            _grid.CellClick -= _grid_CellClick;
            _grid.DataError -= _grid_DataError;
            DataGridFactory.ClearLinks(_grid);
            _grid.Rows.Clear();
            _grid = null;

            HostTableColors.Child = null;
        }

        private DataGridView _grid;

        private void CreateTableToColors()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            _grid.ScrollBars = ScrollBars.Vertical;

            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            // Color

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum0);

            // Check Box

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = "";
            colum01.Width = 50;

            colum01.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(colum01);

            HostTableColors.Child = _grid;

            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int columnInd = e.ColumnIndex;

            int rowInd = e.RowIndex;

            if (columnInd == 0)
            {
                return;
            }
            else if (columnInd == 1)
            {

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (rowInd == i)
                    {
                        continue;
                    }

                    DataGridViewCheckBoxCell checkBox = (DataGridViewCheckBoxCell)_grid.Rows[i].Cells[1];

                    if (checkBox.Value == null)
                    {
                        continue;
                    }

                    if (Convert.ToBoolean(checkBox.Value.ToString()) == true)
                    {
                        checkBox.Value = false;
                        break;
                    }
                }
            }
        }

        private void PaintColorsGrid()
        {
            string[] colors = System.Enum.GetNames(typeof(System.Drawing.KnownColor));

            for (int i = 0; i < colors.Length; i++)
            {
                System.Drawing.KnownColor curKnownColor;

                if (Enum.TryParse(colors[i], out curKnownColor) == false)
                {
                    continue;
                }

                System.Drawing.Color curColor = System.Drawing.Color.FromKnownColor(curKnownColor);

                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = "";
                nRow.Cells[0].Style.BackColor = curColor;
                nRow.Cells[0].Style.SelectionBackColor = curColor;

                DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cell.Value = false;

                nRow.Cells.Add(cell);

                _grid.Rows.Add(nRow);
            }
        }

        private void SetSelectedColorOnGrid()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(SetSelectedColorOnGrid));
                return;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (Convert.ToBoolean(_grid.Rows[i].Cells[1].Value) == true)
                {
                    _grid.Rows[i].Cells[1].Value = false;
                }
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                System.Drawing.Color curColor = _grid.Rows[i].Cells[0].Style.BackColor;

                if (curColor == _color)
                {
                    _grid.Rows[i].Cells[1].Value = true;
                    break;
                }
            }
        }

        private void GetSelectedColorFromGrid()
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (Convert.ToBoolean(_grid.Rows[i].Cells[1].Value) != true)
                {
                    continue;
                }

                System.Drawing.Color curColor = _grid.Rows[i].Cells[0].Style.BackColor;
                _color = curColor;
                return;
            }
        }

        public System.Drawing.Color Color
        {
            get
            {
                return _color;
            }
            set
            {
                if (value == _color)
                {
                    return;
                }

                _colorSetUser = value;
                _color = value;
                SetSelectedColorOnGrid();
            }
        }

        private System.Drawing.Color _color = System.Drawing.Color.Azure;

        private System.Drawing.Color _colorSetUser = System.Drawing.Color.Azure;

        private bool _isAccepted;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            GetSelectedColorFromGrid();
            _isAccepted = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            _color = _colorSetUser;
            Close();
        }
    }
}