/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OsEngine.Language;
using System.IO;

namespace OsEngine.Entity
{
    public class DataGridFactory
    {
        public static DataGridView GetDataGridView(DataGridViewSelectionMode selectionMode, DataGridViewAutoSizeRowsMode rowsSizeMode, bool createSaveMenu = false)
        {
            DataGridView grid = new DoubleBufferedDataGridView();
            
            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeRows = true;
            grid.AutoSizeRowsMode = rowsSizeMode;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = selectionMode;
            grid.MultiSelect = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
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

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            headerStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            headerStyle.WrapMode = DataGridViewTriState.True;
            headerStyle.BackColor = Color.FromArgb(21, 26, 30);
            headerStyle.SelectionBackColor = Color.FromArgb(21, 26, 30);
            headerStyle.ForeColor = Color.FromArgb(154, 156, 158);


            grid.ColumnHeadersDefaultCellStyle = headerStyle;

            grid.MouseLeave += GridMouseLeaveEvent;

            grid.MouseWheel += GridMouseWheelEvent;

            if (createSaveMenu)
            {
                grid.Click += GridClickMenuEvent;
            }

            return grid;
        }

        class DoubleBufferedDataGridView : DataGridView
        {
            protected override bool DoubleBuffered { get => true; }
        }

        private static void GridMouseWheelEvent(object sender, MouseEventArgs args)
        {
            try
            {
                DataGridView grid = (DataGridView)sender;

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
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static void GridMouseLeaveEvent(Object sender, EventArgs e)
        {
            try
            {
                DataGridView grid = (DataGridView)sender;
                grid.EndEdit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static void GridClickMenuEvent(Object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                DataGridView grid = (DataGridView)sender;

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem(OsLocalization.Entity.TableSaveMenu1));

                items[items.Count - 1].Click += delegate (Object sender, EventArgs e)
                {
                    try
                    {
                        if (grid.Rows.Count == 0)
                        {
                            return;
                        }

                        SaveFileDialog myDialog = new SaveFileDialog();
                        myDialog.Filter = "*.txt|";
                        myDialog.ShowDialog();

                        if (string.IsNullOrEmpty(myDialog.FileName))
                        {
                            MessageBox.Show(OsLocalization.Journal.Message1);
                            return;
                        }

                        string fileName = myDialog.FileName;
                        if (fileName.Split('.').Length == 1)
                        {
                            fileName = fileName + ".txt";
                        }

                        string saveStr = "";

                        for (int i = 0; i < grid.Columns.Count; i++)
                        {
                            saveStr += grid.Columns[i].HeaderText + ";";
                        }

                        saveStr += "\r\n";

                        for (int i = 0; i < grid.Rows.Count; i++)
                        {
                            saveStr += grid.Rows[i].ToFormatString() + "\r\n";
                        }


                        StreamWriter writer = new StreamWriter(fileName);
                        writer.Write(saveStr);
                        writer.Close();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(error.ToString());
                    }
                };

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                grid.ContextMenuStrip = menu;
                grid.ContextMenuStrip.Show(grid, new Point(mouse.X, mouse.Y));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public static void ClearLinks(DataGridView grid)
        {
            grid.MouseLeave -= GridMouseLeaveEvent;
            grid.MouseWheel -= GridMouseWheelEvent;
            grid.Click -= GridClickMenuEvent;
        }

        public static DataGridView GetDataGridBuyAtStopPositions()
        {
            DataGridView newGrid = GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
    DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            /*  
positionOpener.Number
positionOpener.TimeCreate
positionOpener.TabName
positionOpener.Securit
positionOpener.Volume = volume;
positionOpener.Side
positionOpener.ActivateType
positionOpener.PriceRedLine
positionOpener.PriceOrder
positionOpener.ExpiresBars
positionOpener.LifeTimeType

*/

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn1; // Number
            colum0.ReadOnly = true;
            colum0.Width = 70;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn2; // Time Open
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn3; // Tab name
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn4; // Security
            colum03.ReadOnly = true;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn5; // Volume
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum04);

            DataGridViewColumn colum05 = new DataGridViewColumn();
            colum05.CellTemplate = cell0;
            colum05.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn6; // Side
            colum05.ReadOnly = true;
            colum05.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum05);

            DataGridViewColumn colum06 = new DataGridViewColumn();
            colum06.CellTemplate = cell0;
            colum06.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn7; // ActivateType
            colum06.ReadOnly = true;
            colum06.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum06);

            DataGridViewColumn colum07 = new DataGridViewColumn();
            colum07.CellTemplate = cell0;
            colum07.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn8; // PriceRedLine
            colum07.ReadOnly = true;
            colum07.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum07);

            DataGridViewColumn colum08 = new DataGridViewColumn();
            colum08.CellTemplate = cell0;
            colum08.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn9; // PriceOrder
            colum08.ReadOnly = true;
            colum08.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum08);

            DataGridViewColumn colum09 = new DataGridViewColumn();
            colum09.CellTemplate = cell0;
            colum09.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn10; // ExpiresBars
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum09);

            DataGridViewColumn colum10 = new DataGridViewColumn();
            colum10.CellTemplate = cell0;
            colum10.HeaderText = OsLocalization.Entity.PositionBuyAtStopColumn11; // ExpiresBars
            colum10.ReadOnly = true;
            colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum10);

            return newGrid;
        }

        public static DataGridView GetDataGridPosition(bool readOnly = true)
        {
            DataGridView newGrid = GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Entity.PositionColumn1;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Entity.PositionColumn2;
            colum01.ReadOnly = true;
            colum01.Width = 100;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = OsLocalization.Entity.PositionColumn3;
            colum02.ReadOnly = true;
            colum02.Width = 100;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Entity.PositionColumn4;
            colu.ReadOnly = readOnly;
            colu.Width = 70;

            newGrid.Columns.Add(colu);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Entity.PositionColumn5;
            colum1.ReadOnly = readOnly;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum1);

            // position SIDE
            if(readOnly == true)
            {
                
                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = OsLocalization.Entity.PositionColumn6;
                colum2.ReadOnly = readOnly;
                colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum2);
            }
            else
            {
                DataGridViewComboBoxColumn dirColumn = new DataGridViewComboBoxColumn();
                dirColumn.HeaderText = OsLocalization.Entity.PositionColumn6;
                dirColumn.ReadOnly = readOnly;
                dirColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(dirColumn);
            }

            // position STATE
            if (readOnly == true)
            {
                DataGridViewColumn colum3 = new DataGridViewColumn();
                colum3.CellTemplate = cell0;
                colum3.HeaderText = OsLocalization.Entity.PositionColumn7;
                colum3.ReadOnly = readOnly;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                newGrid.Columns.Add(colum3);
            }
            else
            {
                DataGridViewComboBoxColumn stateColumn = new DataGridViewComboBoxColumn();
                stateColumn.HeaderText = OsLocalization.Entity.PositionColumn7;
                stateColumn.ReadOnly = readOnly;
                stateColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                newGrid.Columns.Add(stateColumn);
            }

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Entity.PositionColumn8;
            colum4.ReadOnly = true;
            colum4.Width = 60;

            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum45 = new DataGridViewColumn();
            colum45.CellTemplate = cell0;
            colum45.HeaderText = OsLocalization.Entity.PositionColumn9;
            colum45.ReadOnly = true;
            colum45.Width = 60;

            newGrid.Columns.Add(colum45);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Entity.PositionColumn10;
            colum5.ReadOnly = true;
            colum5.Width = 60;

            newGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Entity.PositionColumn11;
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum6);

            DataGridViewColumn colum61 = new DataGridViewColumn();
            colum61.CellTemplate = cell0;
            colum61.HeaderText = OsLocalization.Entity.PositionColumn12;
            colum61.ReadOnly = true;
            colum61.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum61);

            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = OsLocalization.Entity.PositionColumn13;
            colum8.ReadOnly = true;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum8);

            DataGridViewColumn colum9 = new DataGridViewColumn();
            colum9.CellTemplate = cell0;
            colum9.HeaderText = OsLocalization.Entity.PositionColumn14;
            colum9.ReadOnly = readOnly;
            colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum9);

            DataGridViewColumn colum10 = new DataGridViewColumn();
            colum10.CellTemplate = cell0;
            colum10.HeaderText = OsLocalization.Entity.PositionColumn15;
            colum10.ReadOnly = readOnly;
            colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum10);

            DataGridViewColumn colum11 = new DataGridViewColumn();
            colum11.CellTemplate = cell0;
            colum11.HeaderText = OsLocalization.Entity.PositionColumn16;
            colum11.ReadOnly = readOnly;
            colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum11);

            DataGridViewColumn colum12 = new DataGridViewColumn();
            colum12.CellTemplate = cell0;
            colum12.HeaderText = OsLocalization.Entity.PositionColumn17;
            colum12.ReadOnly = readOnly;
            colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum12);

            DataGridViewColumn colum13 = new DataGridViewColumn();
            colum13.CellTemplate = cell0;
            colum13.HeaderText = OsLocalization.Entity.PositionColumn18;
            colum13.ReadOnly = readOnly;
            colum13.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum13);

            DataGridViewColumn colum14 = new DataGridViewColumn();
            colum14.CellTemplate = cell0;
            colum14.HeaderText = OsLocalization.Entity.PositionColumn19;
            colum14.ReadOnly = readOnly;
            colum14.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum14.Width = 60;
            newGrid.Columns.Add(colum14);

            return newGrid;
        }

        public static DataGridView GetDataGridOrder(bool readOnly = true)
        {
            DataGridView newGrid = GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            // User ID
            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Entity.OrderColumn1;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            newGrid.Columns.Add(colum0);

            // Market ID

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Entity.OrderColumn2;
            colum01.ReadOnly = readOnly;
            colum01.Width = 60;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            // Time Create
            if(readOnly)
            {
                DataGridViewColumn colum02 = new DataGridViewColumn();
                colum02.CellTemplate = cell0;
                colum02.HeaderText = OsLocalization.Entity.OrderColumn3;
                colum02.ReadOnly = true;
                colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum02);
            }
            else
            {
                DataGridViewButtonColumn colum02 = new DataGridViewButtonColumn();
                colum02.HeaderText = OsLocalization.Entity.OrderColumn3;
                colum02.ReadOnly = true;
                colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum02);
            }

            // Security
            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Entity.OrderColumn4;
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            // Portfolio
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Entity.OrderColumn5;
            colum1.ReadOnly = readOnly;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            // Direction
            if(readOnly)
            {
                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = OsLocalization.Entity.OrderColumn6;
                colum2.ReadOnly = true;
                colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum2);
            }
            else
            {
                DataGridViewComboBoxColumn dirColumn = new DataGridViewComboBoxColumn();
                dirColumn.HeaderText = OsLocalization.Entity.OrderColumn6;
                dirColumn.ReadOnly = readOnly;
                dirColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(dirColumn);
            }

            // State
            if (readOnly)
            {
                DataGridViewColumn colum3 = new DataGridViewColumn();
                colum3.CellTemplate = cell0;
                colum3.HeaderText = OsLocalization.Entity.OrderColumn7;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum3);
            }
            else
            {
                DataGridViewComboBoxColumn stateColumn = new DataGridViewComboBoxColumn();
                stateColumn.HeaderText = OsLocalization.Entity.OrderColumn7;
                stateColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                stateColumn.Width = 100;
                newGrid.Columns.Add(stateColumn);
            }

            // Price
            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Entity.OrderColumn8;
            colum4.ReadOnly = readOnly;
            colum4.Width = 60;
            newGrid.Columns.Add(colum4);

            // Execution price
            DataGridViewColumn colum45 = new DataGridViewColumn();
            colum45.CellTemplate = cell0;
            colum45.HeaderText = OsLocalization.Entity.OrderColumn9;
            colum45.ReadOnly = true;
            colum45.Width = 60;
            newGrid.Columns.Add(colum45);

            // Volume
            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Entity.OrderColumn10;
            colum5.ReadOnly = readOnly;
            colum5.Width = 60;
            newGrid.Columns.Add(colum5);

            // Type
            if(readOnly)
            {
                DataGridViewColumn colum6 = new DataGridViewColumn();
                colum6.CellTemplate = cell0;
                colum6.HeaderText = OsLocalization.Entity.OrderColumn11;
                colum6.ReadOnly = true;
                colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum6);
            }
            else
            {
                DataGridViewComboBoxColumn typeColumn = new DataGridViewComboBoxColumn();
                typeColumn.HeaderText = OsLocalization.Entity.OrderColumn11;
                typeColumn.ReadOnly = readOnly;
                typeColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(typeColumn);
            }

            // RoundTrip
            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = OsLocalization.Entity.OrderColumn12;
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            newGrid.Columns.Add(colum7);

            return newGrid;
        }

        public static DataGridView GetDataGridMyTrade(bool readOnly = true)
        {
            DataGridView newGrid = GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);
            newGrid.ScrollBars = ScrollBars.Vertical;
            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            // 0 Id

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Entity.TradeColumn1;
            colum0.Width = 60;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            // 1 Order Id

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = OsLocalization.Entity.TradeColumn2;
            colum03.ReadOnly = true;
            colum03.Width = 60;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            // 2 Security

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Entity.TradeColumn3;
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            // 3 Time
            if (readOnly)
            {
                DataGridViewColumn colum02 = new DataGridViewColumn();
                colum02.CellTemplate = cell0;
                colum02.HeaderText = OsLocalization.Entity.TradeColumn4;
                colum02.ReadOnly = true;
                colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum02);
            }
            else
            {
                DataGridViewButtonColumn colum02 = new DataGridViewButtonColumn();
                colum02.HeaderText = OsLocalization.Entity.TradeColumn4;
                colum02.ReadOnly = true;
                colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum02);
            }

            // 4 Price

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Entity.TradeColumn5;
            colu.ReadOnly = readOnly;
            colu.Width = 70;
            newGrid.Columns.Add(colu);

            // 5 Volume

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Entity.TradeColumn6;
            colum1.ReadOnly = readOnly;
            colum1.Width = 70;
            newGrid.Columns.Add(colum1);

            // 6 Direction

            if (readOnly)
            {
                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = OsLocalization.Entity.TradeColumn7;
                colum2.ReadOnly = true;
                colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(colum2);
            }
            else
            {
                DataGridViewComboBoxColumn dirColumn = new DataGridViewComboBoxColumn();
                dirColumn.HeaderText = OsLocalization.Entity.TradeColumn7;
                dirColumn.ReadOnly = readOnly;
                dirColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                newGrid.Columns.Add(dirColumn);
            }

            return newGrid;
        }

        public static DataGridView GetDataGridProxies()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.ProxiesColumn1;
            column0.ReadOnly = true;
            column0.Width = 170;

            newGrid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.ProxiesColumn2;
            column1.ReadOnly = true;
            column1.Width = 100;

            newGrid.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Entity.ProxiesColumn3;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(column);

            return newGrid;
        }

        public static DataGridView GetDataGridPortfolios()
        {
            DataGridView _gridPosition = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            _gridPosition.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridPosition.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.ColumnPortfolio0;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridPosition.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.ColumnPortfolio1;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridPosition.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Entity.ColumnPortfolio2;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            

            _gridPosition.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Entity.ColumnPortfolio3;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;

            _gridPosition.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Entity.ColumnPortfolio4;
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridPosition.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Entity.ColumnPortfolio5;
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridPosition.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = OsLocalization.Entity.ColumnPortfolio6;
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridPosition.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = OsLocalization.Entity.ColumnPortfolio7;
            column7.ReadOnly = true;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;

            _gridPosition.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = OsLocalization.Entity.ColumnPortfolio8;
            column8.ReadOnly = true;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;

            _gridPosition.Columns.Add(column8);

            DataGridViewColumn column9 = new DataGridViewColumn();
            column9.CellTemplate = cell0;
            column9.HeaderText = OsLocalization.Entity.ColumnPortfolio9;
            column9.ReadOnly = true;
            column9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridPosition.Columns.Add(column9);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.CellTemplate = cell0;
            column10.HeaderText = OsLocalization.Entity.ColumnPortfolio5;
            column10.ReadOnly = true;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridPosition.Columns.Add(column10);

            DataGridViewColumn column11 = new DataGridViewColumn();
            column11.CellTemplate = cell0;
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _gridPosition.Columns.Add(column11);

            return _gridPosition;
        }

        public static DataGridView GetDataGridServers()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Entity.ColumnServers1;
            colum1.ReadOnly = true;
            colum1.Width = 150;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Entity.ColumnServers2;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = "";
            colum3.ReadOnly = true;
            colum3.Width = 30;
            newGrid.Columns.Add(colum3);

            return newGrid;
        }

        public static DataGridView GetDataGridDataSource()
        {
            DataGridView myGridView = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = myGridView.DefaultCellStyle;

            myGridView.ScrollBars = ScrollBars.Vertical;

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Entity.ColumnDataSource1;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column2);

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.ColumnDataSource2;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Entity.ColumnDataSource3;
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.ColumnDataSource4;
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Entity.ColumnDataSource5;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Entity.ColumnDataSource6;
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            myGridView.Columns.Add(column4);

            return myGridView;
        }

    }
}