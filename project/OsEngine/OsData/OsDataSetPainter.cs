/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using System.Threading;
using OsEngine.Charts.CandleChart;

namespace OsEngine.OsData
{
    public class OsDataSetPainter
    {
        #region Static painter part

        private static List<OsDataSetPainter> _painters = new List<OsDataSetPainter>();

        private static string _locker = "painterAreaLocker";

        private static Thread _worker;

        private static void AddPainterInArray(OsDataSetPainter painter)
        {
            _painters.Add(painter);

            if (_worker == null)
            {
                _worker = new Thread(PainterThreadArea);
                _worker.Start();
            }
        }

        private static void DeletePainterFromArray(OsDataSetPainter painter)
        {
            for (int i = 0; i < _painters.Count; i++)
            {
                if (_painters[i].UID == painter.UID)
                {
                    _painters.RemoveAt(i);
                    break;
                }

            }
        }

        private static void PainterThreadArea()
        {
            while (true)
            {
                Thread.Sleep(5000);

                try
                {
                    lock (_locker)
                    {
                        for (int i = 0; i < _painters.Count; i++)
                        {
                            _painters[i].TryUpdateInterface();
                        }
                    }
                }
                catch(Exception ex)
                {
                    if(_painters != null &&
                        _painters.Count > 0)
                    {
                        _painters[0].SendNewLogMessage(ex.ToString(),LogMessageType.Error);
                    }

                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region Object service part

        public OsDataSetPainter(OsDataMasterPainter master)
        {
            _set = master.Master.SelectedSet;
            _masterPainter = master;

            DateTime centuryBegin = new DateTime(2021, 4, 29);
            DateTime currentDate = DateTime.Now;
            UID = currentDate.Ticks - centuryBegin.Ticks;

            AddPainterInArray(this);
        }

        public long UID;

        public string NameSet
        {
            get
            {
                return _set.SetName;
            }
        }

        private OsDataSet _set;

        private OsDataMasterPainter _masterPainter;

        #endregion

        #region Managment

        public void StartPaint(WindowsFormsHost host,
            System.Windows.Controls.Label setName,
            System.Windows.Controls.Label labelTimeStart,
            System.Windows.Controls.Label labelTimeEnd,
            System.Windows.Controls.ProgressBar bar)
        {
            try
            {
                _host = host;
                _labelSetName = setName;
                _labelTimeStart = labelTimeStart;
                _labelTimeEnd = labelTimeEnd;
                _bar = bar;

                if (_dataGrid == null)
                {
                    CreateGrid();
                }

                _host.Child = _dataGrid;

                RePaintInterface();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            try
            {
                if (_host == null ||
                    _dataGrid == null)
                {
                    return;
                }

                if (_dataGrid.InvokeRequired)
                {
                    _dataGrid.Invoke(new Action(StopPaint));
                    return;
                }

                if (_labelSetName != null)
                {
                    _labelSetName.Content = "";
                }

                if (_labelTimeStart != null)
                {
                    _labelTimeStart.Content = "";
                }

                if (_labelTimeEnd != null)
                {
                    _labelTimeEnd.Content = "";
                }

                if (_host != null)
                {
                    _host.Child = null;
                }

                _host = null;
                _labelSetName = null;
                _labelTimeStart = null;
                _labelTimeEnd = null;

                if (_bar != null)
                {
                    _bar.Maximum = 100;
                    _bar.Value = 0;
                }

                _bar = null;
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void Delete()
        {
            DeletePainterFromArray(this);

            StopPaint();

            if (_dataGrid != null)
            {
                _dataGrid.DataError -= _dataGrid_DataError;
                _dataGrid.Click -= _dataGrid_Click;
                _dataGrid.Rows.Clear();
                DataGridFactory.ClearLinks(_dataGrid);
                _dataGrid = null;
            }
        }

        private System.Windows.Controls.Label _labelSetName;

        private System.Windows.Controls.Label _labelTimeStart;

        private System.Windows.Controls.Label _labelTimeEnd;

        private System.Windows.Controls.ProgressBar _bar;

        #endregion

        #region Paint interface

        private DataGridView _dataGrid;

        private WindowsFormsHost _host;

        private void CreateGrid()
        {
            _dataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);

            _dataGrid.ScrollBars = ScrollBars.Vertical;

            _dataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _dataGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label35; //"Num";
            colum0.ReadOnly = true;
            colum0.Width = 30;
            _dataGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Data.Label36;//"Security";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum01);

            DataGridViewColumn colum11 = new DataGridViewColumn();
            colum11.CellTemplate = cell0;
            colum11.HeaderText = OsLocalization.Data.Label39;//"Class";
            colum11.ReadOnly = true;
            colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum11);

            DataGridViewColumn colum12 = new DataGridViewColumn();
            colum12.CellTemplate = cell0;
            colum12.HeaderText = OsLocalization.Data.Label40;//"Exchange";
            colum12.ReadOnly = true;
            colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum12);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = OsLocalization.Data.Label18;//"Start time";
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum04);

            DataGridViewColumn colum05 = new DataGridViewColumn();
            colum05.CellTemplate = cell0;
            colum05.HeaderText = OsLocalization.Data.Label19;//"End time";
            colum05.ReadOnly = true;
            colum05.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum05);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = OsLocalization.Data.Label37;//"TF";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum02);

            DataGridViewColumn colum06 = new DataGridViewColumn();
            colum06.CellTemplate = cell0;
            colum06.HeaderText = OsLocalization.Data.Label33;//"Status";
            colum06.ReadOnly = true;
            colum06.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum06);

            DataGridViewColumn colum07 = new DataGridViewColumn();
            colum07.CellTemplate = cell0;
            colum07.HeaderText = OsLocalization.Data.Label32;//"Load %";
            colum07.ReadOnly = true;
            colum07.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum07);

            DataGridViewColumn colum08 = new DataGridViewColumn();
            colum08.CellTemplate = cell0;
            colum08.HeaderText = OsLocalization.Data.Label51;//"Objects";
            colum08.ReadOnly = true;
            colum08.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum08);

            DataGridViewButtonColumn colum09 = new DataGridViewButtonColumn();
            //colum08.CellTemplate = cell0;
            //colum08.HeaderText = "Chart";
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum09);

            DataGridViewButtonColumn colum10 = new DataGridViewButtonColumn();
            //colum09.CellTemplate = cell0;
            //colum09.HeaderText = "Delete";
            colum10.ReadOnly = true;
            colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum10);

            _dataGrid.DataError += _dataGrid_DataError;

            _dataGrid.Click += _dataGrid_Click;

            /*
            _grid.CellBeginEdit += _grid_CellBeginEdit;
            _grid.MouseLeave += _grid_MouseLeave;*/
        }

        private int _previousActiveRow;

        private void _dataGrid_Click(object sender, EventArgs e)
        {
            try
            {
                if (_dataGrid.SelectedCells.Count == 0)
                {
                    return;
                }

                int columnIndex = _dataGrid.SelectedCells[0].ColumnIndex;
                int rowIndex = _dataGrid.SelectedCells[0].RowIndex;

                if (_previousActiveRow < _dataGrid.Rows.Count)
                {
                    _dataGrid.Rows[_previousActiveRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(154, 156, 158);
                }

                _dataGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255);

                _previousActiveRow = rowIndex;

                bool isRowWithManageButtons = ((_dataGrid.Rows[rowIndex].Cells[1].Value == null || _dataGrid.Rows[rowIndex].Cells[1].Value.ToString() == "")
                                             && (_dataGrid.Rows[rowIndex].Cells[4].Value == null || _dataGrid.Rows[rowIndex].Cells[4].Value.ToString() == "")
                                             && _dataGrid.SelectedCells[0] is DataGridViewButtonCell);

                if (columnIndex == 6)
                { // get set settings window

                    if (isRowWithManageButtons)
                    {

                    }
                }
                else if (columnIndex == 7)
                { // delete

                    if (isRowWithManageButtons)
                    {

                    }
                }
                else if (columnIndex == 8)
                { // cut
                    if (isRowWithManageButtons)
                    {
                        // временно кнопка настройки
                        try
                        {
                            int rowGridSetsIndex = _masterPainter.GridSets.CurrentCell.RowIndex;

                            OsDataSetUi ui = new OsDataSetUi(_set);
                            ui.ShowDialog();

                            if (ui.IsSaved)
                            {
                                _set.Save();
                                RePaintInterface();
                            }

                            _masterPainter.RefreshActiveSet();
                            _masterPainter.RePaintSetGrid();
                            _masterPainter.GridSets.Rows[rowGridSetsIndex].Selected = true;
                        }
                        catch (Exception error)
                        {
                            SendNewLogMessage(error.ToString(), LogMessageType.Error);
                        }
                    }
                }
                else if (columnIndex == 9)
                { // copying
                    if (isRowWithManageButtons)
                    {
                        // временно кнопка удалить
                        try
                        {
                            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label9);
                            ui.ShowDialog();

                            if (ui.UserAcceptAction == false)
                            {
                                return;
                            }

                            _set.Delete();
                            _set.BaseSettings.Regime = DataSetState.Off;

                            if (_set != null)
                            {
                                _masterPainter.StopPaintActiveSet();
                                _masterPainter.Master.SelectedSet = null;
                            }

                            _masterPainter.Master.Sets.Remove(_set);
                            _masterPainter.RePaintSetGrid();

                            RePaintInterface();
                        }
                        catch (Exception error)
                        {
                            SendNewLogMessage(error.ToString(), LogMessageType.Error);
                        }

                    }
                }
                else if (columnIndex == 10)
                { // chart or раскрыть/скрыть бумаги внутри, update set

                    _dataGrid.Rows[rowIndex].Cells[0].Selected = true;

                    bool isClickOnShowChartBtn = false;
                    bool isClickOnShowHideSecs = false;

                    if (_dataGrid.Rows[rowIndex].Cells[0].Value == null
                        || _dataGrid.Rows[rowIndex].Cells[0].Value.ToString() == "")
                    {
                        isClickOnShowChartBtn = true;
                    }
                    else
                    {
                        isClickOnShowHideSecs = true;
                    }

                    if (isRowWithManageButtons)
                    { // update set

                        // временно кнопка обрезать

                        if (_set.SecuritiesLoad.Count > 0)
                        {
                            DataPrunerUi ui = new DataPrunerUi(_set, this);
                            ui.Show();
                            return;
                        }
                    }
                    else if (isClickOnShowHideSecs)
                    { // скрыть/раскрыть бумаги
                        int realNum = 0;

                        try
                        {
                            realNum = Convert.ToInt32(_dataGrid.Rows[rowIndex].Cells[0].Value.ToString());
                        }
                        catch
                        {
                            return;
                        }

                        _set.ChangeCollapsedStateBySecurity(realNum - 1);
                        RePaintInterface();
                    }
                    else if (isClickOnShowChartBtn)
                    {// показать чарт
                        int numSecurity = -1;

                        for (int i = rowIndex; i > -1; i--)
                        {
                            try
                            {
                                numSecurity = Convert.ToInt32(_dataGrid.Rows[i].Cells[0].Value.ToString());
                                break;
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        if (numSecurity == -1)
                        {
                            return;
                        }

                        string TfInSecurity = _dataGrid.Rows[rowIndex].Cells[6].Value.ToString();

                        SecurityToLoad sec = _set.SecuritiesLoad[numSecurity - 1];

                        SecurityTfLoader loader = null;

                        for (int i = 0; i < sec.SecLoaders.Count; i++)
                        {
                            if (sec.SecLoaders[i].TimeFrame.ToString() == TfInSecurity)
                            {
                                loader = sec.SecLoaders[i];
                                break;
                            }
                        }

                        if (loader == null)
                        {
                            return;
                        }

                        List<Candle> candles = null;

                        if (loader.TimeFrame == TimeFrame.Sec1
                        || loader.TimeFrame == TimeFrame.Sec2
                        || loader.TimeFrame == TimeFrame.Sec5
                        || loader.TimeFrame == TimeFrame.Sec10
                        || loader.TimeFrame == TimeFrame.Sec15
                        || loader.TimeFrame == TimeFrame.Sec20
                        || loader.TimeFrame == TimeFrame.Sec30)
                        {
                            candles = loader.GetExtCandlesFromTrades();
                        }
                        else if (loader.TimeFrame == TimeFrame.Tick)
                        {
                            return;
                        }
                        else
                        {
                            candles = loader.GetCandlesAllHistory();
                        }

                        if (candles == null ||
                            candles.Count == 0)
                        {
                            return;
                        }

                        CandleChartUi ui = new CandleChartUi(loader.SecName + " " + loader.TimeFrame, StartProgram.IsOsData);
                        ui.Show();
                        ui.ProcessCandles(candles);
                    }
                }
                else if (columnIndex == 11)
                { // delete or detail / LQDT

                    _dataGrid.Rows[rowIndex].Cells[0].Selected = true;

                    if (isRowWithManageButtons) // add LQDT
                    {
                        LqdtDataUi ui = new LqdtDataUi(_set, this);
                        ui.Show();
                    }
                    else if (_dataGrid.Rows[rowIndex].Cells[0].Value == null
                       || _dataGrid.Rows[rowIndex].Cells[0].Value.ToString() == "")
                    {
                        // detail
                        int numSecurity = -1;

                        for (int i = rowIndex; i > -1; i--)
                        {
                            try
                            {
                                numSecurity = Convert.ToInt32(_dataGrid.Rows[i].Cells[0].Value.ToString());
                                break;
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        if (numSecurity == -1)
                        {
                            return;
                        }

                        string TfInSecurity = _dataGrid.Rows[rowIndex].Cells[6].Value.ToString();

                        SecurityToLoad sec = _set.SecuritiesLoad[numSecurity - 1];

                        SecurityTfLoader loader = null;

                        for (int i = 0; i < sec.SecLoaders.Count; i++)
                        {
                            if (sec.SecLoaders[i].TimeFrame.ToString() == TfInSecurity)
                            {
                                loader = sec.SecLoaders[i];
                                break;
                            }
                        }

                        if (loader == null)
                        {
                            return;
                        }

                        if (loader.TimeFrame == TimeFrame.MarketDepth)
                        {
                            return;
                        }

                        OsDataSetDetailUi detailUi = new OsDataSetDetailUi(loader);
                        detailUi.ShowDialog();
                        loader.CheckTimeInSets();
                        RePaintInterface();

                        return;
                    }
                    else
                    {
                        int realNum = 0;

                        try
                        {
                            realNum = Convert.ToInt32(_dataGrid.Rows[rowIndex].Cells[0].Value.ToString());
                        }
                        catch
                        {
                            return;
                        }

                        string secName = "";

                        try
                        {
                            secName = _dataGrid.Rows[rowIndex].Cells[1].Value.ToString();
                        }
                        catch
                        {
                            return;
                        }

                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label42 + "  " + secName);
                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        _set.DeleteSecurity(realNum - 1);

                        RePaintInterface();
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void RePaintInterface()
        {
            if (_host == null ||
                _dataGrid == null)
            {
                return;
            }

            RePaintGrid();
            RePaintLabels();
        }

        private void RePaintLabels()
        {
            try
            {
                if (_dataGrid.InvokeRequired)
                {
                    _dataGrid.Invoke(new Action(RePaintLabels));
                    return;
                }

                _labelSetName.Content = _set.SetName;

                _labelTimeStart.Content = _set.BaseSettings.TimeStart.ToString(OsLocalization.ShortDateFormatString);
                _labelTimeEnd.Content = _set.BaseSettings.TimeEnd.ToString(OsLocalization.ShortDateFormatString);
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void RePaintGrid()
        {
            try
            {
                if (_host == null ||
                _dataGrid == null)
                {
                    return;
                }

                if (_dataGrid.InvokeRequired)
                {
                    _dataGrid.Invoke(new Action(RePaintGrid));
                    return;
                }

                int lastShowRow = _dataGrid.FirstDisplayedScrollingRowIndex;

                _dataGrid.Rows.Clear();

                List<SecurityToLoad> secs = _set.SecuritiesLoad;

                for (int i = 0; secs != null && i < secs.Count; i++)
                {
                    List<DataGridViewRow> secRows = GetRowsFromSecurity(secs[i], i + 1);

                    for (int i2 = 0; i2 < secRows.Count; i2++)
                    {
                        _dataGrid.Rows.Add(secRows[i2]);
                    }
                }

                _dataGrid.Rows.AddRange(GetRowWithManageButtons());

                if (lastShowRow != -1 &&
                    lastShowRow < _dataGrid.Rows.Count)
                {
                    _dataGrid.FirstDisplayedScrollingRowIndex = lastShowRow;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpDateGrid()
        {
            try
            {
                if (_host == null ||
                    _dataGrid == null)
                {
                    return;
                }

                List<SecurityToLoad> secs = _set.SecuritiesLoad;

                List<DataGridViewRow> allRows = new List<DataGridViewRow>();

                for (int i = 0; secs != null && i < secs.Count; i++)
                {
                    List<DataGridViewRow> secRows = GetRowsFromSecurity(secs[i], i + 1);

                    for (int i2 = 0; i2 < secRows.Count; i2++)
                    {
                        allRows.Add(secRows[i2]);
                    }
                }

                if (_dataGrid.Rows.Count - 2 != allRows.Count)
                {
                    return;
                }

                for (int i = 0; i < allRows.Count; i++)
                {
                    DataGridViewRow rowInGrid = _dataGrid.Rows[i];
                    DataGridViewRow rowInNewArray = allRows[i];
                    CompareCellsInRow(rowInGrid, rowInNewArray);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow[] GetRowWithManageButtons()
        {
            DataGridViewRow[] rows = [new DataGridViewRow(), new DataGridViewRow()];

            for (int i = 0; i < 12; i++)
            {
                rows[0].Cells.Add(new DataGridViewTextBoxCell() { Value = "" });
            }

            for (int i = 0; i <= 7; i++)
            {
                rows[1].Cells.Add(new DataGridViewTextBoxCell() { Value = "" });
            }

            for (int i = 8; i <= 11; i++)
            {
                DataGridViewButtonCell buttonCell = new DataGridViewButtonCell();

                switch (i)
                {
                    case 8:
                        buttonCell.Value = OsLocalization.Data.Label61; break; //Настройки
                    case 9:
                        buttonCell.Value = OsLocalization.Data.Label8; break; // удалить
                    case 10:
                        buttonCell.Value = OsLocalization.Data.Label73; break; // "Обрезать"
                    //case 9:
                    //    buttonCell.Value = "Дублировать"; break;
                    //case 10:
                    //    buttonCell.Value = "Обновление"; break;
                    case 11:
                        buttonCell.Value = "+LQDT"; break;
                }

                buttonCell.Style.ForeColor = System.Drawing.Color.FromArgb(250, 250, 250);
                buttonCell.Style.BackColor = System.Drawing.Color.FromArgb(17, 18, 23);

                rows[1].Cells.Add(buttonCell);
            }

            return rows;
        }

        private void CompareCellsInRow(DataGridViewRow rowInGrid, DataGridViewRow rowInNewArray)
        {
            /*
colum0.HeaderText = "Num";       0
colum01.HeaderText = "Security"; 1
colum02.HeaderText = "Class";    2
colum03.HeaderText = "Exchange"; 3
colum06.HeaderText = "Start";    4
colum07.HeaderText = "End";      5

colum04.HeaderText = "TF";       6
colum08.HeaderText = "Status";   7
colum09.HeaderText = "% Load";   8
colum10.HeaderText = "Objects";   9

colum11.HeaderText = "Chart";    10
colum12.HeaderText = "Delete";   11

*/
            if (rowInGrid.Cells[4].Value != null &&
                rowInGrid.Cells[4].Value.ToString() != rowInNewArray.Cells[4].Value.ToString())
            {
                UpDateCell(rowInGrid.Cells[4], rowInNewArray.Cells[4].Value.ToString());
            }
            if (rowInGrid.Cells[5].Value != null &&
                rowInGrid.Cells[5].Value.ToString() != rowInNewArray.Cells[5].Value.ToString())
            {
                UpDateCell(rowInGrid.Cells[5], rowInNewArray.Cells[5].Value.ToString());
            }
            if (rowInGrid.Cells[7].Value != null &&
                rowInGrid.Cells[7].Value.ToString() != rowInNewArray.Cells[7].Value.ToString())
            {
                UpDateCell(rowInGrid.Cells[7], rowInNewArray.Cells[7].Value.ToString());
            }
            if (rowInGrid.Cells[8].Value != null &&
                rowInGrid.Cells[8].Value.ToString() != rowInNewArray.Cells[8].Value.ToString())
            {
                UpDateCell(rowInGrid.Cells[8], rowInNewArray.Cells[8].Value.ToString());
            }
            if (rowInGrid.Cells[9].Value != null &&
                rowInGrid.Cells[9].Value.ToString() != rowInNewArray.Cells[9].Value.ToString())
            {
                UpDateCell(rowInGrid.Cells[9], rowInNewArray.Cells[9].Value.ToString());
            }
        }

        private void UpDateCell(DataGridViewCell cell, string newValue)
        {
            try
            {
                if (_dataGrid.InvokeRequired)
                {
                    _dataGrid.Invoke(new Action<DataGridViewCell, string>(UpDateCell), cell, newValue);
                    return;
                }

                cell.Value = newValue;
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private void RePaintProgressBar()
        {
            try
            {
                if (_host == null ||
                    _dataGrid == null)
                {
                    return;
                }

                if (_bar.Dispatcher.CheckAccess() == false)
                {
                    _bar.Dispatcher.Invoke(new Action(RePaintProgressBar));
                    return;
                }

                if (_bar.Maximum != 100)
                {
                    _bar.Maximum = 100;
                }

                double value = Convert.ToDouble(_set.PercentLoad());

                if (_bar.Value != value)
                {
                    _bar.Value = value;
                }
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void TryUpdateInterface()
         {
            if (_host == null ||
                _dataGrid == null)
            {
                return;
            }

            RePaintProgressBar();
            UpDateGrid();
        }

        private List<DataGridViewRow> GetRowsFromSecurity(SecurityToLoad SecToLoad, int num)
        {
            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Security";
colum02.HeaderText = "Class";
colum03.HeaderText = "Exchange";
colum06.HeaderText = "Start";
colum07.HeaderText = "End";

colum04.HeaderText = "TF";
colum08.HeaderText = "Status";
colum09.HeaderText = "% Load";
colum10.HeaderText = "Objects";

colum11.HeaderText = "Chart";
colum12.HeaderText = "Delete";

*/

            List<DataGridViewRow> result = new List<DataGridViewRow>();

            result.Add(GetPrimeRowToSecurity(SecToLoad, num));

            if (SecToLoad.IsCollapsed == false)
            {
                for (int i = 0; i < SecToLoad.SecLoaders.Count; i++)
                {
                    SecurityTfLoader loader = SecToLoad.SecLoaders[i];

                    result.Add(GetLoaderToTimeFrame(loader));
                }
            }

            return result;
        }

        private DataGridViewRow GetLoaderToTimeFrame(SecurityTfLoader loader)
        {
            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Security";
colum02.HeaderText = "Class";
colum03.HeaderText = "Exchange";
colum06.HeaderText = "Start";
colum07.HeaderText = "End";

colum04.HeaderText = "TF";
colum08.HeaderText = "Status";
colum09.HeaderText = "% Load";
colum10.HeaderText = "Objects";

colum11.HeaderText = "Chart";
colum12.HeaderText = "Delete";

            */

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell()); // Num
            row.Cells[0].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); // Security Name
            row.Cells[1].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); // Class
            row.Cells[2].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); // Exchange
            row.Cells[3].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); // Start
            row.Cells[4].Value = loader.TimeStartInReal.Date.ToString(OsLocalization.ShortDateFormatString);

            row.Cells.Add(new DataGridViewTextBoxCell()); // End
            row.Cells[5].Value = loader.TimeEndInReal.Date.ToString(OsLocalization.ShortDateFormatString);

            row.Cells.Add(new DataGridViewTextBoxCell()); // TF
            row.Cells[6].Value = loader.TimeFrame.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell()); // Status
            row.Cells[7].Value = loader.Status;

            row.Cells.Add(new DataGridViewTextBoxCell()); //"% Load";
            row.Cells[8].Value = loader.PercentLoad().ToString();

            row.Cells.Add(new DataGridViewTextBoxCell()); //"Objects";
            row.Cells[9].Value = loader.Objects().ToString();

            row.Cells.Add(new DataGridViewButtonCell()); //"Chart";

            if (loader.TimeFrame != TimeFrame.MarketDepth
                && loader.TimeFrame != TimeFrame.Tick)
            {
                row.Cells[10].Value = OsLocalization.Data.Label43;
            }

            row.Cells.Add(new DataGridViewButtonCell()); //"Delete";

            if (loader.TimeFrame != TimeFrame.MarketDepth)
            {
                row.Cells[11].Value = OsLocalization.Data.Label47;
            }


            return row;
        }

        private DataGridViewRow GetPrimeRowToSecurity(SecurityToLoad SecToLoad, int num)
        {
            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Security";
colum02.HeaderText = "Class";
colum03.HeaderText = "Exchange";
colum06.HeaderText = "Start";
colum07.HeaderText = "End";

colum04.HeaderText = "TF";
colum08.HeaderText = "Status";
colum09.HeaderText = "% Load";
colum10.HeaderText = "Objects";

colum11.HeaderText = "Chart";
colum12.HeaderText = "Delete";

            */

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell()); // Num
            row.Cells[0].Value = num.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell()); // Security Name
            row.Cells[1].Value = SecToLoad.SecName;

            row.Cells.Add(new DataGridViewTextBoxCell()); // Class
            row.Cells[2].Value = SecToLoad.SecClass;

            row.Cells.Add(new DataGridViewTextBoxCell()); // Exchange
            row.Cells[3].Value = SecToLoad.SecExchange;

            row.Cells.Add(new DataGridViewTextBoxCell()); // Start
            //row.Cells[4].Value = SecToLoad.SettingsToLoadSecurities.TimeStart.Date.ToString("dd.MM.yyyy");

            row.Cells.Add(new DataGridViewTextBoxCell()); // End
            //row.Cells[5].Value = SecToLoad.SettingsToLoadSecurities.TimeEnd.Date.ToString("dd.MM.yyyy");

            row.Cells.Add(new DataGridViewTextBoxCell()); // TF
            row.Cells[6].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); // Status
            row.Cells[7].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); //"% Load";
            row.Cells[8].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell()); //"% Load";
            row.Cells[9].Value = "";

            row.Cells.Add(new DataGridViewButtonCell()); //"Chart";

            if (SecToLoad.IsCollapsed == true)
            {
                row.Cells[10].Value = "vvv";
            }
            else if (SecToLoad.IsCollapsed == false)
            {
                row.Cells[10].Value = "^^^";
            }

            row.Cells.Add(new DataGridViewButtonCell()); //"Delete";
            row.Cells[11].Value = OsLocalization.Data.Label41;

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].Style.BackColor = System.Drawing.Color.FromArgb(9, 11, 13);
            }

            return row;
        }

        #endregion

        #region Logging 

        private void _dataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.Exception.ToString(), LogMessageType.Error);
        }

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }
}