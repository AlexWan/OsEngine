using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsData
{
    public partial class OsDataSetDetailUi : Window
    {
        public OsDataSetDetailUi(SecurityTfLoader loader)
        {
            InitializeComponent();
            _loader = loader;
            _curCulture = OsLocalization.CurCulture;

            Title = OsLocalization.Data.Label50;
            LabelStatus.Content = OsLocalization.Data.Label33;
            LabelExchange.Content = OsLocalization.Data.Label40;
            LabelSecurity.Content = OsLocalization.Data.Label36;
            LabelClass.Content = OsLocalization.Data.Label39;
            LabelTimeFrame.Content = OsLocalization.Data.Label37;
            LabelTimeStart.Content = OsLocalization.Data.Label18;
            LabelTimeEnd.Content = OsLocalization.Data.Label19;
            LabelTimeStartReal.Content = OsLocalization.Data.Label48;
            LabelTimeEndReal.Content = OsLocalization.Data.Label49;
            LabelCache.Content = OsLocalization.Data.Label57;

            CreateTable();

            RePaintAll();

            Closed += OsDataSetDetailUi_Closed;

            Task.Run(PainterThreadArea);
        }

        private void OsDataSetDetailUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _isDeleted = true;
                _loader = null;

                if (HostDataPiesDetails != null)
                {
                    HostDataPiesDetails.Child = null;
                }

                if (_grid != null)
                {
                    _grid.CellClick -= _grid_CellClick;
                    _grid.DataError -= _grid_DataError;
                    DataGridFactory.ClearLinks(_grid);
                    _grid = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private async void PainterThreadArea()
        {
            while (true)
            {
                await Task.Delay(10000);

                if (_isDeleted)
                {
                    return;
                }

                RePaintAll();
            }
        }

        bool _isDeleted;

        private void RePaintAll()
        {
            if (_grid == null)
            {
                return;
            }

            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(RePaintAll));
                return;
            }
            try
            {
                LabelStatusValue.Content = _loader.Status.ToString();
                LabelExchangeValue.Content = _loader.Exchange;
                LabelSecurityValue.Content = _loader.SecName;
                LabelClassValue.Content = _loader.SecClass;
                LabelTimeFrameValue.Content = _loader.TimeFrame.ToString();
                LabelTimeStartValue.Content = _loader.TimeStart.ToString(OsLocalization.ShortDateFormatString);
                LabelTimeEndValue.Content = _loader.TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                LabelTimeStartRealValue.Content = _loader.TimeStartInReal.ToString(OsLocalization.ShortDateFormatString);
                LabelTimeEndRealValue.Content = _loader.TimeEndInReal.ToString(OsLocalization.ShortDateFormatString);

                PaintTable();
            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.Message);
            }
        }

        SecurityTfLoader _loader;

        DataGridView _grid;

        CultureInfo _curCulture;

        public void CreateTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ScrollBars = ScrollBars.Vertical;

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            // Объектов
            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label51;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum0.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum0);

            // Start
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Data.Label18;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum1.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum1);

            // End
            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Data.Label19;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum2.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum2);

            // Start Fact
            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Data.Label52;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum3.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum3);

            // End Fact
            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Data.Label53;
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum4);

            // Open File
            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum5.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum5);

            // Open in Folder
            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum6.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum6);

            // Clear
            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum7.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum7);

            _grid = newGrid;

            HostDataPiesDetails.Child = _grid;
            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _loader.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int col = e.ColumnIndex;

                if (col != 5 && col != 6 && col != 7)
                {
                    return;
                }

                if (row >= _loader.DataPies.Count)
                {
                    return;
                }

                DataPie pie = _loader.DataPies[row];

                if (col == 5)
                { // показать файл файлом

                    string tempFile = Environment.CurrentDirectory + "\\" + pie._pathMyTempPieInTfFolder + "\\" + pie.TempFileName;

                    if (File.Exists(tempFile) == false)
                    {
                        return;
                    }

                    Process.Start("explorer.exe", tempFile);

                }

                if (col == 6)
                { // открыть папку

                    string tempFile = Environment.CurrentDirectory + "\\" + pie._pathMyTempPieInTfFolder;

                    if (Directory.Exists(tempFile) == false)
                    {
                        return;
                    }

                    Process.Start("explorer.exe", tempFile);
                }
                else if (col == 7)
                { // очистить данные. С ПОДТВЕРЖДЕНИЕМ!

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label58);
                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    if (row >= _loader.DataPies.Count)
                    {
                        return;
                    }

                    _loader.DataPies[row].Clear();

                    _loader.Status = SecurityLoadStatus.Loading;
                    _loader.CheckTimeInSets();

                    RePaintAll();
                }
            }
            catch (Exception ex)
            {
                _loader.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public void PaintTable()
        {
            if (_grid == null)
            {
                return;
            }

            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(PaintTable));
                return;
            }

            if (_grid.Rows.Count != _loader.DataPies.Count)
            {
                _grid.Rows.Clear();

                for (int i = 0; i < _loader.DataPies.Count; i++)
                {
                    _grid.Rows.Add(GetRow(_loader.DataPies[i]));
                }
            }
            else
            {
                for (int i = 0; i < _loader.DataPies.Count; i++)
                {
                    UpDateRow(_grid.Rows[i], _loader.DataPies[i]);
                }
            }
        }

        private void UpDateRow(DataGridViewRow nRow, DataPie pie)
        {
            if (nRow.Cells[0].Value.ToString() != pie.ObjectCount.ToString())
            {
                nRow.Cells[0].Value = pie.ObjectCount.ToString();
            }

            if (nRow.Cells[1].Value.ToString() != pie.Start.ToString(_curCulture))
            {
                nRow.Cells[1].Value = pie.Start.ToString(_curCulture);
            }

            if (nRow.Cells[2].Value.ToString() != pie.End.ToString(_curCulture))
            {
                nRow.Cells[2].Value = pie.End.ToString(_curCulture);
            }

            if (nRow.Cells[3].Value.ToString() != pie.StartFact.ToString(_curCulture))
            {
                nRow.Cells[3].Value = pie.StartFact.ToString(_curCulture);
            }

            if (nRow.Cells[4].Value.ToString() != pie.EndFact.ToString(_curCulture))
            {
                nRow.Cells[4].Value = pie.EndFact.ToString(_curCulture);
            }
        }

        private DataGridViewRow GetRow(DataPie pie)
        {
            // Объектов
            // Start
            // End
            // StartFact
            // EndFact
            // Show File
            // Show Folder
            // Clear

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = pie.ObjectCount.ToString();
            nRow.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = pie.Start.ToString(_curCulture);
            nRow.Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = pie.End.ToString(_curCulture);
            nRow.Cells[2].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = pie.StartFact.ToString(_curCulture);
            nRow.Cells[3].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = pie.EndFact.ToString(_curCulture);
            nRow.Cells[4].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[5].Value = OsLocalization.Data.Label56;
            nRow.Cells[5].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[6].Value = OsLocalization.Data.Label54;
            nRow.Cells[6].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[7].Value = OsLocalization.Data.Label55;
            nRow.Cells[7].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            //nRow.Height = nRow.Height + 5;

            return nRow;
        }
    }
}