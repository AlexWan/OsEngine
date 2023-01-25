using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Language;
using System.Threading;
using System.Windows;

namespace OsEngine.OsTrader.Gui
{
    public class BotTabsPainter
    {
        public BotTabsPainter(OsTraderMaster master, WindowsFormsHost host)
        {
            _master = master;
            _host = host;

            CreateTable(master._startProgram);
            RePaintTable(); 
            _master.BotCreateEvent += _master_NewBotCreateEvent;
            _master.BotDeleteEvent += _master_BotDeleteEvent;

            Thread painterThread = new Thread(UpdaterThreadArea);
            painterThread.Start();
        }

        private void _master_BotDeleteEvent(Panels.BotPanel obj)
        {
            RePaintTable();
        }

        private void _master_NewBotCreateEvent(Panels.BotPanel obj)
        {
            RePaintTable();
        }

        OsTraderMaster _master;

        WindowsFormsHost _host;

        DataGridView _grid;

        private void CreateTable(StartProgram startProgram)
        {
            DataGridView newGrid =
             DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
             DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ScrollBars = ScrollBars.Vertical;

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label165; //"Num";
            colum0.ReadOnly = true;
            colum0.Width = 70;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Trader.Label175;//"Name";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = OsLocalization.Trader.Label167;//"Type";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = OsLocalization.Trader.Label176;//"First Security";
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum04);

            DataGridViewColumn colum05 = new DataGridViewColumn();
            colum05.CellTemplate = cell0;
            colum05.HeaderText = OsLocalization.Trader.Label20;//"Position";
            colum05.ReadOnly = true;
            colum05.Width = 120;
            newGrid.Columns.Add(colum05);

            DataGridViewCheckBoxColumn column06 = new DataGridViewCheckBoxColumn();
            column06.HeaderText = OsLocalization.Trader.Label184; // On/off
            column06.ReadOnly = false;
            column06.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column06.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(column06);

            DataGridViewCheckBoxColumn column07 = new DataGridViewCheckBoxColumn();
            column07.HeaderText = OsLocalization.Trader.Label185; // Emulator on/off
            column07.ReadOnly = false;
            column07.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column07.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(column07);

            if(startProgram != StartProgram.IsOsTrader)
            {
                column07.ReadOnly = true;
            }

            DataGridViewButtonColumn colum08 = new DataGridViewButtonColumn();
            //colum06.CellTemplate = cell0;
            //colum06.HeaderText = "Chart";
            colum08.ReadOnly = true;
            colum08.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum08);

            DataGridViewButtonColumn colum09 = new DataGridViewButtonColumn();
            //colum07.CellTemplate = cell0;
            //colum07.HeaderText = "Parameters";
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum09);

            DataGridViewButtonColumn colum11 = new DataGridViewButtonColumn();
            // colum09.CellTemplate = cell0;
            //colum09.HeaderText = "Action";
            colum11.ReadOnly = true;
            colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum11);

            _grid = newGrid;
            _host.Child = _grid;

            _grid.Click += _grid_Click;
            _grid.CellBeginEdit += _grid_CellBeginEdit;
            _grid.MouseLeave += _grid_MouseLeave;
        }

        private void _grid_MouseLeave(object sender, EventArgs e)
        {
            _grid.ClearSelection();
        }

        private void _grid_Click(object sender, EventArgs e)
        {
            if(_grid.SelectedCells.Count == 0)
            {
                return;
            }
            int coluIndex = _grid.SelectedCells[0].ColumnIndex;

            int rowIndex = _grid.SelectedCells[0].RowIndex;

            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Name";
colum02.HeaderText = "Type";
colum03.HeaderText = "First Security";
colum04.HeaderText = "Position";
colum05.HeaderText = "On/off";
colum06.HeaderText = "Emulator on/off";
colum07.HeaderText = "Chart";
colum08.HeaderText = "Parameters";
colum9.HeaderText = "Journal";
colum10.HeaderText = "Action";
*/

            int botsCount = 0;

            if (_master.PanelsArray != null)
            {
                botsCount = _master.PanelsArray.Count;
            }

            BotPanel bot = null;

            if(rowIndex < botsCount)
            {
                bot = _master.PanelsArray[rowIndex];
            }

            if (coluIndex == 7 &&
                rowIndex < botsCount)
            { // вызываем чарт робота
                bot.ShowChartDialog();
            }
            else if (coluIndex == 8 &&
   rowIndex < botsCount)
            { // вызываем параметры
                bot.ShowParametrDialog();
            }
            else if (coluIndex == 9 &&
    rowIndex < botsCount)
            { // вызываем окно удаление робота
                _master.DeleteByNum(rowIndex);
            }

            if (coluIndex == 8 &&
     rowIndex == botsCount + 1)
            { // вызываем общий журнал
                _master.ShowCommunityJournal(2, 0, 0);
            }
            else if (coluIndex == 9 &&
    rowIndex == botsCount + 1)
            { // вызываем добавление нового бота
                _master.CreateNewBot();
            }
        }

        #region работа с чек-боксами включений и отключений

        int _lastChangeRow;
        int _lastChangeColumn;

        private void _grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if(_lastTimeClick.AddMilliseconds(500) > DateTime.Now)
            {
                return;
            }
            _lastTimeClick = DateTime.Now;

            _lastChangeRow = e.RowIndex;
            _lastChangeColumn = e.ColumnIndex;

            Task.Run(ChangeOnOffAwait);
        }

        DateTime _lastTimeClick = DateTime.MinValue;

        private async void ChangeOnOffAwait()
        {
            try
            {
                await Task.Delay(200);
                ChangeFocus();
                await Task.Delay(200);
                ChangeOnOff();
            }
            catch(Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        private void ChangeFocus()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(ChangeFocus));
                return;
            }

            _grid.Rows[_lastChangeRow].Cells[0].Selected = true;
        }

        private void ChangeOnOff()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(ChangeOnOff));
                return;
            }

            int coluIndex = _lastChangeColumn;
            int rowIndex = _lastChangeRow;

            int botsCount = 0;

            if (_master.PanelsArray != null)
            {
                botsCount = _master.PanelsArray.Count;
            }

            if (coluIndex == 5 &&
                rowIndex < botsCount &&
                _grid.Rows[rowIndex].Cells[5].Value != null)
            {
                string textInCell = _grid.Rows[rowIndex].Cells[5].Value.ToString();
                bool isOn = Convert.ToBoolean(textInCell);

                OnOffBot(rowIndex, isOn);
            }
            if (coluIndex == 5 &&
                rowIndex == botsCount &&
                _grid.Rows[rowIndex].Cells[5].Value != null)
            {
                string textInCell = _grid.Rows[rowIndex].Cells[5].Value.ToString();
                bool isOn = Convert.ToBoolean(textInCell);

                OnOffAll(isOn);
            }

            if (coluIndex == 6 &&
                rowIndex < botsCount &&
                _grid.Rows[rowIndex].Cells[6].Value != null)
            {
                string textInCell = _grid.Rows[rowIndex].Cells[6].Value.ToString();

                bool isOn = Convert.ToBoolean(textInCell);

                OnOffEmulatorBot(rowIndex, isOn);
            }
            if (coluIndex == 6 &&
                rowIndex == botsCount &&
                _grid.Rows[rowIndex].Cells[6].Value != null)
            {
                string textInCell = _grid.Rows[rowIndex].Cells[6].Value.ToString();
                bool isOn = Convert.ToBoolean(textInCell);

                OnOffEmulatorAll(isOn);
            }
        }

        private void OnOffBot(int botNum, bool value)
        {
            BotPanel bot = _master.PanelsArray[botNum];
            bot.OnOffEventsInTabs = value;
        }

        private void OnOffAll(bool value)
        {
            for(int i = 0;i < _master.PanelsArray.Count;i++)
            {
                BotPanel bot = _master.PanelsArray[i];
                bot.OnOffEventsInTabs = value;
            }
        }

        private void OnOffEmulatorBot(int botNum, bool value)
        {
            BotPanel bot = _master.PanelsArray[botNum];
            bot.OnOffEmulatorsInTabs = value;
        }

        private void OnOffEmulatorAll(bool value)
        {
            for (int i = 0; i < _master.PanelsArray.Count; i++)
            {
                BotPanel bot = _master.PanelsArray[i];
                bot.OnOffEmulatorsInTabs = value;
            }
        }

        #endregion

        private void RePaintTable()
        {
            _grid.Rows.Clear();

            for (int i = 0; _master.PanelsArray != null && i < _master.PanelsArray.Count; i++)
            {
                _grid.Rows.Add(GetRow(_master.PanelsArray[i],i+1));
            }

            _grid.Rows.Add(GetNullRow());

            _grid.Rows.Add(GetAddRow());
        }

        private DataGridViewRow GetRow(BotPanel bot, int num)
        {
            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Name";
colum02.HeaderText = "Type";
colum03.HeaderText = "First Security";
colum04.HeaderText = "Position";

colum05.HeaderText = "On/off";
colum06.HeaderText = "Emulator on/off";

colum07.HeaderText = "Chart";
colum08.HeaderText = "Parameters";
colum9.HeaderText = "Journal";
colum10.HeaderText = "Action";
*/
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = bot.NameStrategyUniq;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = bot.GetType().Name;

            row.Cells.Add(new DataGridViewTextBoxCell());
            if(bot.TabsSimple.Count != 0 &&
                bot.TabsSimple[0].Securiti != null)
            {
                row.Cells[3].Value = bot.TabsSimple[0].Securiti.Name;
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].Value = bot.PositionsCount;

            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[5].Value = bot.OnOffEventsInTabs;

            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[6].Value = bot.OnOffEmulatorsInTabs;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[7].Value =  OsLocalization.Trader.Label172;//"Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[8].Value = OsLocalization.Trader.Label45;//"Parameters";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[9].Value = OsLocalization.Trader.Label39;//"Delete";

            if (num % 2 == 0)
            {
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    row.Cells[i].Style.BackColor = System.Drawing.Color.FromArgb(9, 11, 13);
                }
            }

            return row;
        }

        private DataGridViewRow GetNullRow()
        {
            /*
colum0.HeaderText = "Num";
colum01.HeaderText = "Name";
colum02.HeaderText = "Type";
colum03.HeaderText = "First Security";
colum04.HeaderText = "Position";

colum05.HeaderText = "On/off";
colum06.HeaderText = "Emulator on/off";

colum07.HeaderText = "Chart";
colum08.HeaderText = "Parameters";
colum9.HeaderText = "Journal";
*/

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells.Add(new DataGridViewCheckBoxCell());

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells.Add(new DataGridViewButtonCell());

            return row;
        }

        private DataGridViewRow GetAddRow()
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].Value = "";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[8].Value = OsLocalization.Trader.Label40; //"Journal";
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[9].Value = OsLocalization.Trader.Label38; //"Add New...";

            return row;
        }

        private void UpdaterThreadArea()
        {
            while(true)
            {
                Thread.Sleep(2000);

                if (_lastTimeClick.AddSeconds(2) > DateTime.Now)
                {
                    continue;
                }

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                UpdateTable();
            }
        }

        private void UpdateTable()
        {
            if(_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(UpdateTable));
                return;
            }

            if (_master.PanelsArray == null)return;
            try
            {
                for (int i = 0; i < _master.PanelsArray.Count; i++)
                {
                    if (_lastTimeClick.AddSeconds(2) > DateTime.Now)
                    {
                        return;
                    }

                    DataGridViewRow row = _grid.Rows[i];

                    BotPanel bot = _master.PanelsArray[i];

                    if (bot.TabsSimple.Count != 0 &&
                        bot.TabsSimple[0].Securiti != null)
                    {
                        if(row.Cells[3].Value == null 
                            ||
                            (row.Cells[3].Value != null 
                            && row.Cells[3].Value.ToString() != bot.TabsSimple[0].Securiti.Name))
                        {
                            row.Cells[3].Value = bot.TabsSimple[0].Securiti.Name;
                        }
                    }

                    if(row.Cells[4].Value == null ||
                        (row.Cells[4].Value != null 
                        && row.Cells[4].Value.ToString() != bot.PositionsCount.ToString()))
                    {
                        row.Cells[4].Value = bot.PositionsCount;
                    }

                    if (row.Cells[5].Value == null ||
                       (row.Cells[5].Value != null
                       && row.Cells[5].Value.ToString() != bot.OnOffEventsInTabs.ToString()))
                    {
                        row.Cells[5].Value = bot.OnOffEventsInTabs;
                    }

                    if (row.Cells[6].Value == null ||
                       (row.Cells[6].Value != null
                        && row.Cells[6].Value.ToString() != bot.OnOffEmulatorsInTabs.ToString()))
                    {
                        row.Cells[6].Value = bot.OnOffEmulatorsInTabs;
                    }

                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }
    }
}
