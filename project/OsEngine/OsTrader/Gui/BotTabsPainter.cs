/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Language;
using System.Threading;
using System.Collections.Generic;
using OsEngine.Journal;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;

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
            _master.UserClickOnPositionShowBotInTableEvent += _master_UserClickOnPositionShowBotInTableEvent;
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

        private OsTraderMaster _master;

        private WindowsFormsHost _host;

        private DataGridView _grid;

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
            colum0.HeaderText = "#"; //"Num";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = OsLocalization.Trader.Label175;//"Name";
            colum01.ReadOnly = false;
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
            colum05.HeaderText = OsLocalization.Trader.Label186;//"Position";
            colum05.ReadOnly = true;
            colum05.Width = 120;
            newGrid.Columns.Add(colum05);

            DataGridViewCheckBoxColumn column06 = new DataGridViewCheckBoxColumn();
            column06.HeaderText = OsLocalization.Trader.Label184; // On/off
            column06.ReadOnly = false;
            column06.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column06.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            newGrid.Columns.Add(column06);

            DataGridViewCheckBoxColumn column07 = new DataGridViewCheckBoxColumn();
            column07.HeaderText = OsLocalization.Trader.Label185; // Emulator on/off
            column07.ReadOnly = false;
            column07.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
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
            _grid.CellEndEdit += _grid_CellEndEdit;
            _grid.MouseLeave += _grid_MouseLeave;
            _grid.CellMouseClick += _grid_CellMouseClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex != 1)
                {
                    return;
                }

                if (_master.PanelsArray == null ||
                    _master.PanelsArray.Count == 0)
                {
                    return;
                }

                int rowIndex = e.RowIndex;

                if (rowIndex >= _grid.Rows.Count)
                {
                    return;
                }

                if (rowIndex >= _master.PanelsArray.Count)
                {
                    return;
                }

                string newName = null;

                if (_grid.Rows[rowIndex].Cells[1].Value != null)
                {
                    newName = _grid.Rows[rowIndex].Cells[1].Value.ToString();
                    newName = newName.Replace("@", "");
                }
                else
                {
                    newName = _master.PanelsArray[rowIndex].NameStrategyUniq;
                    _grid.Rows[rowIndex].Cells[1].Value = newName;
                }

                _master.PanelsArray[rowIndex].PublicName = newName;
                _master.Save();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_MouseLeave(object sender, EventArgs e)
        {
            try
            {
                _grid.ClearSelection();
            }
            catch (Exception ex) 
            {
                _master.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

		private int _prevActiveRow;

        private void _grid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button == MouseButtons.Right)
                {
                    _mouseXPos = mouse.X;
                    _mouseYPos = mouse.Y;
                    return;
                }

                if (_grid.SelectedCells.Count == 0)
                {
                    return;
                }

                int coluIndex = _grid.SelectedCells[0].ColumnIndex;

                int rowIndex = _grid.SelectedCells[0].RowIndex;

                if(coluIndex < 3)
                {
                    return;
                }

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

                if (rowIndex < botsCount)
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
                    bot.ShowParameterDialog();
                }
                else if (coluIndex == 9 &&
        rowIndex < botsCount)
                { // вызываем окно удаление робота

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    _master.DeleteRobotByNum(rowIndex);
                }

                if(rowIndex == botsCount + 1)
                { // последняя строка

                    if (_master._startProgram == StartProgram.IsOsTrader
                       && coluIndex == 5)
                    {
                        ServerMaster.ShowApiDialog();
                    }
                    else if (_master._startProgram == StartProgram.IsOsTrader
                       && coluIndex == 6)
                    {
                        ServerMaster.ShowClientManagerDialog();
                    }
                    if (_master._startProgram == StartProgram.IsOsTrader
                        && coluIndex == 7)
                    {
                        ServerMaster.ShowCopyMasterDialog();
                    }
                    else if (coluIndex == 8 &&
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

                if (_grid.Rows.Count <= _prevActiveRow)
                {
                    _prevActiveRow = rowIndex;
                    return;
                }

                _grid.Rows[_prevActiveRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(154, 156, 158);
                _grid.Rows[rowIndex].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255);
                _prevActiveRow = rowIndex;
            }
            catch(Exception error)
            {
                _master.SendNewLogMessage(error.ToString(),Logging.LogMessageType.Error);
            }
        }

        #region Pop-up menu

        private int _mouseXPos;

        private int _mouseYPos;

        private BotPanel _lastSelectedBot;

        private void _grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Right)
                {
                    if (_grid.ContextMenuStrip != null)
                    {
                        _grid.ContextMenuStrip = null;
                    }

                    return;
                }

                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;

                if(rowIndex >= _master.PanelsArray.Count
                    || rowIndex < 0)
                {
                    return;
                }

                _lastSelectedBot = _master.PanelsArray[rowIndex];

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem(_lastSelectedBot.GetNameStrategyType() + "  " + _lastSelectedBot.NameStrategyUniq));
                items[0].Enabled = false;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label172));
                items[1].Click += BotTabsPainter_Chart_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label45));
                items[2].Click += BotTabsPainter_Parameters_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label40));
                items[3].Click += BotTabsPainter_Journal_Click;

                if(_lastSelectedBot.OnOffEventsInTabs == true)
                {
                    items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label412));
                }
                else //if (selectedBot.OnOffEventsInTabs == false)
                {
                    items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label413));
                }
                items[4].Click += BotTabsPainter_OnOffEvents_Click;

                if (_lastSelectedBot.OnOffEmulatorsInTabs == true)
                {
                    items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label414));
                }
                else //if (selectedBot.OnOffEventsInTabs == false)
                {
                   items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label415));
                }
                if(_master._startProgram == StartProgram.IsTester)
                {
                    items[5].Enabled = false;
                }
                items[5].Click += BotTabsPainter_OnOffEmulator_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label416));
                items[6].Click += BotTabsPainter_MoveUp_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label417));
                items[7].Click += BotTabsPainter_MoveDown_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label39));
                items[8].Click += BotTabsPainter_Delete_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                _grid.ContextMenuStrip = menu;
                _grid.ContextMenuStrip.Show(_grid, new System.Drawing.Point(_mouseXPos, _mouseYPos));
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_Chart_Click(object sender, EventArgs e)
        {
            try
            {
                _lastSelectedBot.ShowChartDialog();
            }
            catch(Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_Parameters_Click(object sender, EventArgs e)
        {
            try
            {
                _lastSelectedBot.ShowParameterDialog();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_Journal_Click(object sender, EventArgs e)
        {
            try
            {
                string journalName = 
                    "Journal2Ui_" + _lastSelectedBot.NameStrategyUniq + _master._startProgram.ToString();

                for(int i = 0;i < _journalUi.Count;i++)
                {
                    if (_journalUi[i].JournalName == journalName)
                    {
                        _journalUi[i].Activate();
                        return;
                    }
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                List<Journal.Journal> journals = _lastSelectedBot.GetJournals();


                BotPanelJournal botPanel = new BotPanelJournal();
                botPanel.BotName = _lastSelectedBot.NameStrategyUniq;
                botPanel.BotClass = _lastSelectedBot.GetNameStrategyType();

                botPanel._Tabs = new List<BotTabJournal>();

                for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                {
                    BotTabJournal botTabJournal = new BotTabJournal();
                    botTabJournal.TabNum = i2;
                    botTabJournal.Journal = journals[i2];
                    botPanel._Tabs.Add(botTabJournal);
                }

                panelsJournal.Add(botPanel);

                _journalUi.Add(new JournalUi2(panelsJournal, _lastSelectedBot.StartProgram));
                _journalUi[_journalUi.Count-1].Closed += _journalUi_Closed;
                _journalUi[_journalUi.Count - 1].LogMessageEvent += _journalUi_LogMessageEvent;
                _journalUi[_journalUi.Count - 1].Show();
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<JournalUi2> _journalUi = new List<JournalUi2>();

        private void _journalUi_LogMessageEvent(string message, LogMessageType type)
        {
            if (_master == null)
            {
                return;
            }
            _master.SendNewLogMessage(message, type);
        }

        private void _journalUi_Closed(object sender, EventArgs e)
        {
            try
            {
                JournalUi2 myJournal = (JournalUi2)sender;

                for (int i = 0; i < _journalUi.Count; i++)
                {
                    if (_journalUi[i].JournalName == myJournal.JournalName)
                    {
                        _journalUi[i].Closed -= _journalUi_Closed;
                        _journalUi[i].LogMessageEvent -= _journalUi_LogMessageEvent;
                        _journalUi[i].IsErase = true;
                        _journalUi.RemoveAt(i);
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BotTabsPainter_OnOffEvents_Click(object sender, EventArgs e)
        {
            try
            {
                if(_lastSelectedBot.OnOffEventsInTabs == true)
                {
                    _lastSelectedBot.OnOffEventsInTabs = false;
                }
                else
                {
                    _lastSelectedBot.OnOffEventsInTabs = true;
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_OnOffEmulator_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastSelectedBot.OnOffEmulatorsInTabs == true)
                {
                    _lastSelectedBot.OnOffEmulatorsInTabs = false;
                }
                else
                {
                    _lastSelectedBot.OnOffEmulatorsInTabs = true;
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_MoveUp_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 1; i < _master.PanelsArray.Count; i++)
                {
                    if (_master.PanelsArray[i].NameStrategyUniq == _lastSelectedBot.NameStrategyUniq)
                    {
                        BotPanel panel = _master.PanelsArray[i];
                        _master.PanelsArray[i] = _master.PanelsArray[i - 1];
                        _master.PanelsArray[i - 1] = panel;
                        _master.Save();
                        RePaintTable();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void BotTabsPainter_MoveDown_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < _master.PanelsArray.Count-1; i++)
                {
                    if (_master.PanelsArray[i].NameStrategyUniq == _lastSelectedBot.NameStrategyUniq)
                    {
                        BotPanel panel = _master.PanelsArray[i];
                        _master.PanelsArray[i] = _master.PanelsArray[i + 1];
                        _master.PanelsArray[i + 1] = panel;
                        _master.Save();
                        RePaintTable();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

        }

        private void BotTabsPainter_Delete_Click(object sender, EventArgs e)
        {
            try
            {
                int rowIndex = -1;

                for(int i = 0;i < _master.PanelsArray.Count;i++)
                {
                    if (_master.PanelsArray[i].NameStrategyUniq == _lastSelectedBot.NameStrategyUniq)
                    {
                        rowIndex = i;
                        break;
                    }
                }

                if(rowIndex == -1)
                {
                    return;
                }


                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                _master.DeleteRobotByNum(rowIndex);
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region работа с чек-боксами включений и отключений

        private int _lastChangeRow;

        private int _lastChangeColumn;

        private void _grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            try
            {
                if(e.ColumnIndex < 3)
                {
                    return;
                }

                if (_lastTimeClick.AddMilliseconds(500) > DateTime.Now)
                {
                    return;
                }
                _lastTimeClick = DateTime.Now;

                _lastChangeRow = e.RowIndex;
                _lastChangeColumn = e.ColumnIndex;

                Task.Run(ChangeOnOffAwait);
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DateTime _lastTimeClick = DateTime.MinValue;

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
            if(_master.PanelsArray == null)
            {
                return;
            }
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
            if (_master.PanelsArray == null)
            {
                return;
            }
            for (int i = 0; i < _master.PanelsArray.Count; i++)
            {
                BotPanel bot = _master.PanelsArray[i];
                bot.OnOffEmulatorsInTabs = value;
            }
        }

        #endregion

        private void RePaintTable()
        {
            try
            {
                int lastShowRowIndex = _grid.FirstDisplayedScrollingRowIndex;

                _grid.Rows.Clear();

                for (int i = 0; _master.PanelsArray != null && i < _master.PanelsArray.Count; i++)
                {
                    BotPanel bot = _master.PanelsArray[i];

                    if(bot == null)
                    {
                        continue;
                    }

                    _grid.Rows.Add(GetRow(bot, i + 1));
                }

                _grid.Rows.Add(GetNullRow());

                _grid.Rows.Add(GetAddRow());

                if (lastShowRowIndex > 0 &&
                    lastShowRowIndex < _grid.Rows.Count)
                {
                    _grid.FirstDisplayedScrollingRowIndex = lastShowRowIndex;
                    _grid.Rows[lastShowRowIndex].Selected = true;

                    if (_grid.Rows[lastShowRowIndex].Cells != null
                        && _grid.Rows[lastShowRowIndex].Cells[0] != null)
                    {
                        _grid.Rows[lastShowRowIndex].Cells[0].Selected = true;
                    }
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
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

            if(string.IsNullOrEmpty(bot.PublicName) == false)
            {
                row.Cells[1].Value = bot.PublicName;
            }
            else
            {
                row.Cells[1].Value = bot.NameStrategyUniq;
            }
           
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = bot.GetType().Name;

            row.Cells.Add(new DataGridViewTextBoxCell());

            if(bot.TabsSimple.Count != 0 &&
                bot.TabsSimple[0].Security != null)
            {
                row.Cells[3].Value = bot.TabsSimple[0].Security.Name;
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].Value = bot.PositionsCount.ToString() + "/" + bot.AllPositionsCount.ToString();

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
            row.Cells[6].ReadOnly = true;
            
            row.Cells.Add(new DataGridViewButtonCell());

            if(_master._startProgram == StartProgram.IsOsTrader)
            {
                row.Cells[7].Value = OsLocalization.Trader.Label570; //"Copy trading";
            }

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
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(UpdateTable));
                    return;
                }

                if (_master.PanelsArray == null)
                {
                    return;
                }

                for (int i = 0; i < _master.PanelsArray.Count && i < _grid.Rows.Count; i++)
                {
                    if (_lastTimeClick.AddSeconds(2) > DateTime.Now)
                    {
                        return;
                    }

                    DataGridViewRow row = _grid.Rows[i];

                    BotPanel bot = _master.PanelsArray[i];

                    if(bot == null)
                    {
                        continue;
                    }

                    if (bot.TabsSimple.Count != 0 &&
                        bot.TabsSimple[0].Security != null)
                    {
                        if (row.Cells[3].Value == null
                            ||
                            (row.Cells[3].Value != null
                            && row.Cells[3].Value.ToString() != bot.TabsSimple[0].Security.Name))
                        {
                            row.Cells[3].Value = bot.TabsSimple[0].Security.Name;
                        }
                    }

                    if (row.Cells[4].Value == null || (row.Cells[4].Value != null && row.Cells[4].Value.ToString() != bot.PositionsCount.ToString() + "/" + bot.AllPositionsCount.ToString()))
                    {
                        row.Cells[4].Value = bot.PositionsCount.ToString() + "/" + bot.AllPositionsCount.ToString();
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

        #region подсветка робота по клику по позиции

        private void _master_UserClickOnPositionShowBotInTableEvent(string botTabName)
        {
            try
            {
                if (_rowToPaintInOpenPoses != -1)
                {
                    return;
                }

                int botNum = 0;

                bool findTheBot = false;

                for (int i = 0; i < _master.PanelsArray.Count; i++)
                {
                    BotPanel curRobot = _master.PanelsArray[i];

                    if (curRobot.TabsSimple != null)
                    {
                        for (int i2 = 0; i2 < curRobot.TabsSimple.Count; i2++)
                        {
                            if (curRobot.TabsSimple[i2].TabName == botTabName)
                            {
                                botNum = i;
                                findTheBot = true;
                                break;
                            }
                        }
                    }

                    if (curRobot.TabsScreener != null)
                    {
                        for (int i2 = 0; i2 < curRobot.TabsScreener.Count; i2++)
                        {
                            BotTabScreener screener = curRobot.TabsScreener[i2];

                            for (int j = 0; j < screener.Tabs.Count; j++)
                            {
                                if (screener.Tabs[j].TabName == botTabName)
                                {
                                    botNum = i;
                                    findTheBot = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (curRobot.TabsPair != null)
                    {
                        for (int i2 = 0; i2 < curRobot.TabsPair.Count; i2++)
                        {
                            BotTabPair pair = curRobot.TabsPair[i2];

                            for (int j = 0; j < pair.Pairs.Count; j++)
                            {
                                if (pair.Pairs[j].Tab1.TabName == botTabName)
                                {
                                    botNum = i;
                                    findTheBot = true;
                                    break;
                                }
                                if (pair.Pairs[j].Tab2.TabName == botTabName)
                                {
                                    botNum = i;
                                    findTheBot = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (findTheBot)
                    {
                        break;
                    }
                }

                if (findTheBot)
                {
                    _rowToPaintInOpenPoses = botNum;
                    Task.Run(PaintPos);
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private int _rowToPaintInOpenPoses = -1;

        System.Drawing.Color _lastBackColor;

        private async void PaintPos()
        {
            try
            {
                await Task.Delay(200);
                ColoredRow(System.Drawing.Color.LightSlateGray);
                await Task.Delay(600);
                ColoredRow(_lastBackColor);
                _rowToPaintInOpenPoses = -1;
            }
            catch
            {
               // ignore
            }
        }

        private void ColoredRow(System.Drawing.Color color)
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action<System.Drawing.Color>(ColoredRow), color);
                    return;
                }

                _lastBackColor = _grid.Rows[_rowToPaintInOpenPoses].Cells[0].Style.BackColor;

                for (int i = 0; i < 7; i++)
                {
                    _grid.Rows[_rowToPaintInOpenPoses].Cells[i].Style.BackColor = color;
                }
            }
            catch
            {
                return;
            }
        }

        #endregion

    }
}