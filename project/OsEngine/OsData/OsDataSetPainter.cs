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
        #region static painter part

        private static List<OsDataSetPainter> _painters = new List<OsDataSetPainter>();

        private static string _locker = "painterAreaLocker";

        private static Thread _worker;

        private static void AddPainterInArray(OsDataSetPainter painter)
        {
            lock(_locker)
            {
                _painters.Add(painter);

                if(_worker == null)
                {
                    _worker = new Thread(PainterThreadArea);
                    _worker.Start();
                }
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
            while(true)
            {
                Thread.Sleep(2000);

                lock (_locker)
                {
                    for (int i = 0; i < _painters.Count; i++)
                    {
                        _painters[i].TryUpdateInterface();
                    }
                }
            }
        }

        #endregion

        public OsDataSetPainter(OsDataSet set)
        {
            _set = set;

            DateTime centuryBegin = new DateTime(2021, 4, 29);
            DateTime currentDate = DateTime.Now;
            UID = currentDate.Ticks - centuryBegin.Ticks;

            AddPainterInArray(this);
        }

        public void Delete()
        {
            DeletePainterFromArray(this);
        }

        public long UID;

        OsDataSet _set;

        DataGridView _dataGrid;

        WindowsFormsHost _host;

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

            _labelSetName.Content = "";
            _labelTimeStart.Content = "";
            _labelTimeEnd.Content = "";

            _host.Child = null;

            _host = null;
            _labelSetName = null;
            _labelTimeStart = null;
            _labelTimeEnd = null;
            _bar.Maximum = 100;
            _bar.Value = 0;
            _bar = null;
        }

        System.Windows.Controls.Label _labelSetName;

        System.Windows.Controls.Label _labelTimeStart;

        System.Windows.Controls.Label _labelTimeEnd;

        System.Windows.Controls.ProgressBar _bar;

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

            DataGridViewButtonColumn colum08 = new DataGridViewButtonColumn();
            //colum08.CellTemplate = cell0;
            //colum08.HeaderText = "Chart";
            colum08.ReadOnly = true;
            colum08.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum08);

            DataGridViewButtonColumn colum09 = new DataGridViewButtonColumn();
            //colum09.CellTemplate = cell0;
            //colum09.HeaderText = "Delete";
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dataGrid.Columns.Add(colum09);

            _dataGrid.DataError += _dataGrid_DataError;

            _dataGrid.Click += _dataGrid_Click;

            /*
            _grid.CellBeginEdit += _grid_CellBeginEdit;
            _grid.MouseLeave += _grid_MouseLeave;*/
        }

        private void _dataGrid_Click(object sender, EventArgs e)
        {
            if (_dataGrid.SelectedCells.Count == 0)
            {
                return;
            }

            int coluIndex = _dataGrid.SelectedCells[0].ColumnIndex;
            int rowIndex = _dataGrid.SelectedCells[0].RowIndex;

             if (coluIndex == 9)
            { // chart or раскрыть/скрыть бумаги внутри

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

                if(isClickOnShowHideSecs)
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
                else if(isClickOnShowChartBtn)
                {// показать чарт
                    int numSecurity = -1;

                    for(int i = rowIndex; i > -1; i--)
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

                    if(numSecurity == -1)
                    {
                        return;
                    }

                    string TfInSecurity = _dataGrid.Rows[rowIndex].Cells[6].Value.ToString();

                    SecurityToLoad sec = _set.SecuritiesLoad[numSecurity-1];

                    SecurityTfLoader loader = null;

                    for(int i = 0;i < sec.SecLoaders.Count;i++)
                    {
                        if(sec.SecLoaders[i].TimeFrame.ToString() == TfInSecurity)
                        {
                            loader = sec.SecLoaders[i];
                            break;
                        }
                    }

                    if(loader == null)
                    {
                        return;
                    }

                    List<Candle> candles = loader.GetCandlesAllHistory();

                    if(candles == null ||
                        candles.Count == 0)
                    {
                        return;
                    }

                    CandleChartUi ui = new CandleChartUi(loader.SecName + " " + loader.TimeFrame,StartProgram.IsOsData);
                    ui.Show();
                    ui.ProcessCandles(candles);
                }
            }
            else if (coluIndex == 10)
            { // delete

                if(_dataGrid.Rows[rowIndex].Cells[0].Value == null
                   || _dataGrid.Rows[rowIndex].Cells[0].Value.ToString() == "")
                {
                    return;
                }

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

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                _set.DeleteSecurity(realNum-1);

                RePaintInterface();
            }

        }

        private void _dataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.Exception.ToString(), LogMessageType.Error);
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
            if (_dataGrid.InvokeRequired)
            {
                _dataGrid.Invoke(new Action(RePaintLabels));
                return;
            }

            _labelSetName.Content = _set.SetName;
            _labelTimeStart.Content = _set.BaseSettings.TimeStart.ToShortDateString();
            _labelTimeEnd.Content = _set.BaseSettings.TimeEnd.ToShortDateString();

        }

        private void RePaintGrid()
        {
            if(_host == null ||
                _dataGrid == null)
            {
                return;
            }

            if (_dataGrid.InvokeRequired)
            {
                _dataGrid.Invoke(new Action(RePaintGrid));
                return;
            }

            _dataGrid.Rows.Clear();

            List<SecurityToLoad> secs = _set.SecuritiesLoad;

            for(int i = 0; secs != null && i < secs.Count;i++)
            {
                List<DataGridViewRow> secRows = GetRowsFromSecurity(secs[i], i+1);

                for(int i2 = 0;i2< secRows.Count;i2++)
                {
                    _dataGrid.Rows.Add(secRows[i2]);
                }
            }
        }

        private void UpDateGrid()
        {
            if (_host == null ||
                _dataGrid == null)
            {
                return;
            }

            if (_dataGrid.InvokeRequired)
            {
                _dataGrid.Invoke(new Action(UpDateGrid));
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

            if(_dataGrid.Rows.Count != allRows.Count)
            {
                return;
            }

            for(int i = 0;i < _dataGrid.Rows.Count;i++)
            {
                DataGridViewRow rowInGrid = _dataGrid.Rows[i];
                DataGridViewRow rowInNewArray = allRows[i];
                CompairCellsInRow(rowInGrid, rowInNewArray);
            }

        }

        private void CompairCellsInRow(DataGridViewRow rowInGrid, DataGridViewRow rowInNewArray)
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

colum10.HeaderText = "Chart";    9
colum11.HeaderText = "Delete";   10

*/
            if(rowInGrid.Cells[4].Value != null &&
                rowInGrid.Cells[4].Value.ToString() != rowInNewArray.Cells[4].Value.ToString())
            {
                rowInGrid.Cells[4].Value = rowInNewArray.Cells[4].Value;
            }
            if (rowInGrid.Cells[5].Value  != null &&
                rowInGrid.Cells[5].Value.ToString() != rowInNewArray.Cells[5].Value.ToString())
            {
                rowInGrid.Cells[5].Value = rowInNewArray.Cells[5].Value;
            }
            if (rowInGrid.Cells[7].Value != null &&
                rowInGrid.Cells[7].Value.ToString() != rowInNewArray.Cells[7].Value.ToString())
            {
                rowInGrid.Cells[7].Value = rowInNewArray.Cells[7].Value;
            }
            if (rowInGrid.Cells[8].Value != null &&
                rowInGrid.Cells[8].Value.ToString() != rowInNewArray.Cells[8].Value.ToString())
            {
                rowInGrid.Cells[8].Value = rowInNewArray.Cells[8].Value;
            }
        }

        private void RePaintProgressBar()
        {
            if (_host == null ||
                _dataGrid == null)
            {
                return;
            }

            if (_dataGrid.InvokeRequired)
            {
                _dataGrid.Invoke(new Action(RePaintProgressBar));
                return;
            }

            if (_bar.Maximum != 100)
            {
                _bar.Maximum = 100;
            }

            double value = Convert.ToDouble(_set.PercentLoad());

            if(_bar.Value != value)
            {
                _bar.Value = value;
            }
            
        }

        public void TryUpdateInterface()
        {
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

colum10.HeaderText = "Chart";
colum11.HeaderText = "Delete";

*/

            List<DataGridViewRow> result = new List<DataGridViewRow>();

            result.Add(GetPrimeRowToSecurity(SecToLoad, num));

            if(SecToLoad.IsCollapced == false)
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

colum10.HeaderText = "Chart";
colum11.HeaderText = "Delete";

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
            row.Cells[4].Value = loader.TimeStartInReal.Date.ToString("dd.MM.yyyy");

            row.Cells.Add(new DataGridViewTextBoxCell()); // End
            row.Cells[5].Value = loader.TimeEndInReal.Date.ToString("dd.MM.yyyy");

            row.Cells.Add(new DataGridViewTextBoxCell()); // TF
            row.Cells[6].Value = loader.TimeFrame.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell()); // Status
            row.Cells[7].Value = loader.Status;

            row.Cells.Add(new DataGridViewTextBoxCell()); //"% Load";
            row.Cells[8].Value = loader.PercentLoad().ToString();

            row.Cells.Add(new DataGridViewButtonCell()); //"Chart";

            if(loader.TimeFrame != TimeFrame.MarketDepth 
                && loader.TimeFrame != TimeFrame.Tick)
            {
                row.Cells[9].Value = OsLocalization.Data.Label43;
            }
           
            row.Cells.Add(new DataGridViewButtonCell()); //"Delete";
            row.Cells[10].Value = "";

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

colum10.HeaderText = "Chart";
colum11.HeaderText = "Delete";

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

            row.Cells.Add(new DataGridViewButtonCell()); //"Chart";

            if(SecToLoad.IsCollapced == true)
            {
                row.Cells[9].Value = "vvv";
            }
            else if(SecToLoad.IsCollapced == false)
            {
                row.Cells[9].Value = "^^^";
            }

            row.Cells.Add(new DataGridViewButtonCell()); //"Delete";
            row.Cells[10].Value = OsLocalization.Data.Label41;

            return row;
        }

        // messages to log/сообщения в лог 

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
    }
}