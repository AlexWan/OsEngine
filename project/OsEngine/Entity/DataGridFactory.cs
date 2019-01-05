using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    public class DataGridFactory
    {
        public static DataGridView GetDataGridView(DataGridViewSelectionMode selectionMode, DataGridViewAutoSizeRowsMode rowsSizeMode)
        {
            DataGridView grid = new DataGridView();

            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeRows = true;
            grid.AutoSizeRowsMode = rowsSizeMode;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = selectionMode;
            grid.MultiSelect = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ScrollBars = ScrollBars.None;
            grid.BackColor = Color.FromArgb(21, 26, 30);
            grid.BackgroundColor = Color.FromArgb(21, 26, 30);

            grid.GridColor = Color.FromArgb(17, 18, 23);
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.BorderStyle = BorderStyle.None;
            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            style.BackColor =  Color.FromArgb(21, 26, 30);
            style.SelectionBackColor = Color.FromArgb(17, 18, 23);
            style.ForeColor = Color.FromArgb(154, 156, 158);


            grid.DefaultCellStyle = style;
            grid.ColumnHeadersDefaultCellStyle = style;

            grid.MouseEnter += delegate(object sender, EventArgs args)
            {
                grid.Focus();
            };

            grid.MouseLeave += delegate(object sender, EventArgs args)
            {
                grid.EndEdit();
            };

            grid.MouseWheel += delegate(object sender, MouseEventArgs args)
            {
                if (grid.SelectedCells.Count == 0)
                {
                    return;
                }
                int rowInd = grid.SelectedCells[0].RowIndex;
                if (args.Delta < 0)
                {
                    rowInd++;
                }
                else if (args.Delta > 0)
                {
                    rowInd--;
                }

                if (rowInd < 0)
                {
                    rowInd = 0;
                }

                if (rowInd >= grid.Rows.Count)
                {
                    rowInd = grid.Rows.Count - 1;
                }

                grid.Rows[rowInd].Selected = true;
                grid.Rows[rowInd].Cells[grid.SelectedCells[0].ColumnIndex].Selected = true;

                if (grid.FirstDisplayedScrollingRowIndex > rowInd)
                {
                    grid.FirstDisplayedScrollingRowIndex = rowInd;
                }
                

            };



            return grid;
        }

    }
}
