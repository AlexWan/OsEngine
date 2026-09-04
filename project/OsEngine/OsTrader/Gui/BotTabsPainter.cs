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
using System.Drawing;
using System.IO;
using OsEngine.Instructions;

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

        // Tags for rows in the table: BotPanel - bot row, string - group header row, _nullRowTag / _addRowTag - service rows

        private static readonly object _nullRowTag = new object();

        private static readonly object _addRowTag = new object();

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

            if (startProgram != StartProgram.IsOsTrader)
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

            DataGridViewButtonColumn colum12 = new DataGridViewButtonColumn();
            // colum09.CellTemplate = cell0;
            //colum09.HeaderText = "Action";
            colum12.ReadOnly = true;
            colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum12);

            _grid = newGrid;
            _host.Child = _grid;

            _grid.Click += _grid_Click;
            _grid.CellContentClick += _grid_CellContentClick;
            _grid.CurrentCellDirtyStateChanged += _grid_CurrentCellDirtyStateChanged;
            _grid.CellEndEdit += _grid_CellEndEdit;
            _grid.MouseLeave += _grid_MouseLeave;
            _grid.CellMouseClick += _grid_CellMouseClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            string exception = "null";

            if (e.Exception != null)
            {
                exception = e.Exception.GetType().Name + ": " + e.Exception.Message;
            }

            _master.SendNewLogMessage(
                "DataGridView error. Row: " + e.RowIndex + " Column: " + e.ColumnIndex
                + " Context: " + e.Context + " Exception: " + exception,
                Logging.LogMessageType.Error);
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

                if ((_grid.Rows[rowIndex].Tag is BotPanel) == false)
                {
                    return;
                }

                BotPanel bot = (BotPanel)_grid.Rows[rowIndex].Tag;

                string newName = null;

                if (_grid.Rows[rowIndex].Cells[1].Value != null)
                {
                    newName = _grid.Rows[rowIndex].Cells[1].Value.ToString();
                    newName = newName.Replace("@", "");
                }
                else
                {
                    newName = bot.NameStrategyUniq;
                    _grid.Rows[rowIndex].Cells[1].Value = newName;
                }

                bot.PublicName = newName;
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
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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

                //if (coluIndex < 3)
                //{
                //    return;
                //}

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

                if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                {
                    return;
                }

                object rowTag = _grid.Rows[rowIndex].Tag;

                if (rowTag is string groupName)
                { // строка-заголовок группы - свернуть / развернуть
                    _master.SetGroupCollapsed(groupName, !_master.IsGroupCollapsed(groupName));
                    RePaintTable();
                    return;
                }

                if (rowTag is BotPanel bot)
                {
                    if (coluIndex == 7)
                    { // вызываем чарт робота
                        bot.ShowChartDialog();
                    }
                    else if (coluIndex == 8)
                    { // вызываем параметры
                        bot.ShowParameterDialog();
                    }
                    else if (coluIndex == 9)
                    { // вызываем окно удаление робота

                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        _master.DeleteRobotByInstance(bot);
                    }
                    else if (coluIndex == 10)
                    { // вызываем журнал конкретного робота
                        bot.ShowJournalDialog();
                        return;
                    }
                }

                if (ReferenceEquals(rowTag, _addRowTag))
                { // последняя строка

                    if (coluIndex == 1)
                    {
                        if (_master._startProgram == StartProgram.IsOsTrader)
                        {
                            ShowInstructionsForTheBotStation();
                        }
                        else if (_master._startProgram == StartProgram.IsTester)
                        {
                            ShowInstructionsForTheTester();
                        }
                    }

                    if ((_master._startProgram == StartProgram.IsOsTrader
                        || _master._startProgram == StartProgram.IsTester)
                       && coluIndex == 4)
                    {
                        ServerMaster.ShowMatrixManagerDialog();
                    }
                    else if (_master._startProgram == StartProgram.IsOsTrader
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
                    else if (coluIndex == 8)
                    { // вызываем общий журнал
                        _master.ShowCommunityJournal(2, 0, 0);
                    }
                    else if (coluIndex == 9)
                    { // вызываем добавление нового бота
                        _master.CreateNewBot();
                    }
                    else if (coluIndex == 10)
                    { // окно миграции роботов
                        if (_migrationUi != null)
                        {
                            _migrationUi.Activate();
                            return;
                        }

                        _migrationUi = new BotsMigrationUi(_master);
                        _migrationUi.Closed += _migrationUi_Closed;
                        _migrationUi.Show();
                    }
                }

                if (_grid.Rows.Count <= _prevActiveRow)
                {
                    _prevActiveRow = rowIndex;
                    return;
                }

                _grid.Rows[_prevActiveRow].DefaultCellStyle.ForeColor = Themes.ThemeManager.GetColorWinForms("GridTextColor");
                _grid.Rows[rowIndex].DefaultCellStyle.ForeColor = Themes.ThemeManager.GetColorWinForms("GridSelectionForeColor");
                _prevActiveRow = rowIndex;
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private BotsMigrationUi _migrationUi;

        private void _migrationUi_Closed(object sender, EventArgs e)
        {
            _migrationUi.Closed -= _migrationUi_Closed;
            _migrationUi = null;
        }

        #region Pop-up menu

        private int _mouseXPos;

        private int _mouseYPos;

        private BotPanel _lastSelectedBot;

        private string _lastSelectedGroup;

        private void _grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Right)
                {
                    return;
                }

                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;

                if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                {
                    return;
                }

                object rowTag = _grid.Rows[rowIndex].Tag;

                if (rowTag is string groupName)
                { // меню для заголовка группы
                    if (groupName == BotPanel.BaseGroupName)
                    {
                        return;
                    }

                    _lastSelectedGroup = groupName;

                    ContextMenuStrip groupMenu = new ContextMenuStrip();

                    ToolStripMenuItem deleteGroupItem = new ToolStripMenuItem(OsLocalization.Trader.Label779);
                    deleteGroupItem.Click += BotTabsPainter_DeleteGroup_Click;
                    groupMenu.Items.Add(deleteGroupItem);

                    groupMenu.Show(_grid, new System.Drawing.Point(_mouseXPos, _mouseYPos));
                    return;
                }

                if ((rowTag is BotPanel) == false)
                {
                    return;
                }

                _lastSelectedBot = (BotPanel)rowTag;

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem(_lastSelectedBot.GetNameStrategyType() + "  " + _lastSelectedBot.NameStrategyUniq));
                items[0].Enabled = false;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label172));
                items[1].Click += BotTabsPainter_Chart_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label45));
                items[2].Click += BotTabsPainter_Parameters_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label40));
                items[3].Click += BotTabsPainter_Journal_Click;

                if (_lastSelectedBot.OnOffEventsInTabs == true)
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
                if (_master._startProgram == StartProgram.IsTester)
                {
                    items[5].Enabled = false;
                }
                items[5].Click += BotTabsPainter_OnOffEmulator_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label416));
                items[6].Click += BotTabsPainter_MoveUp_Click;

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label417));
                items[7].Click += BotTabsPainter_MoveDown_Click;

                ToolStripMenuItem moveToGroupItem = new ToolStripMenuItem(OsLocalization.Trader.Label776);
                items.Add(moveToGroupItem);

                items.Add(new ToolStripMenuItem(OsLocalization.Trader.Label39));
                items[9].Click += BotTabsPainter_Delete_Click;

                // подменю перемещения робота в группу

                List<string> botsGroups = _master.GetBotsGroups();

                for (int i = 0; i < botsGroups.Count; i++)
                {
                    if (botsGroups[i] == _lastSelectedBot.BotGroup)
                    {
                        continue;
                    }

                    ToolStripMenuItem groupItem = new ToolStripMenuItem(botsGroups[i]);
                    groupItem.Click += BotTabsPainter_MoveToGroup_Click;
                    moveToGroupItem.DropDownItems.Add(groupItem);
                }

                if (_lastSelectedBot.BotGroup != BotPanel.BaseGroupName)
                {
                    ToolStripMenuItem baseGroupItem = new ToolStripMenuItem(OsLocalization.Trader.Label778);
                    baseGroupItem.Click += BotTabsPainter_MoveToBaseGroup_Click;
                    moveToGroupItem.DropDownItems.Add(baseGroupItem);
                }

                if (moveToGroupItem.DropDownItems.Count > 0)
                {
                    moveToGroupItem.DropDownItems.Add(new ToolStripSeparator());
                }

                ToolStripMenuItem newGroupItem = new ToolStripMenuItem(OsLocalization.Trader.Label777);
                newGroupItem.Click += BotTabsPainter_MoveToNewGroup_Click;
                moveToGroupItem.DropDownItems.Add(newGroupItem);

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                menu.Show(_grid, new System.Drawing.Point(_mouseXPos, _mouseYPos));
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
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                _lastSelectedBot.ShowJournalDialog();
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
                if (_lastSelectedBot.OnOffEventsInTabs == true)
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
                        int swapIndex = -1;

                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (_master.PanelsArray[j].BotGroup == _lastSelectedBot.BotGroup)
                            {
                                swapIndex = j;
                                break;
                            }
                        }

                        if (swapIndex == -1)
                        {
                            break;
                        }

                        BotPanel panel = _master.PanelsArray[i];
                        _master.PanelsArray[i] = _master.PanelsArray[swapIndex];
                        _master.PanelsArray[swapIndex] = panel;
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
                for (int i = 0; i < _master.PanelsArray.Count - 1; i++)
                {
                    if (_master.PanelsArray[i].NameStrategyUniq == _lastSelectedBot.NameStrategyUniq)
                    {
                        int swapIndex = -1;

                        for (int j = i + 1; j < _master.PanelsArray.Count; j++)
                        {
                            if (_master.PanelsArray[j].BotGroup == _lastSelectedBot.BotGroup)
                            {
                                swapIndex = j;
                                break;
                            }
                        }

                        if (swapIndex == -1)
                        {
                            break;
                        }

                        BotPanel panel = _master.PanelsArray[i];
                        _master.PanelsArray[i] = _master.PanelsArray[swapIndex];
                        _master.PanelsArray[swapIndex] = panel;
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
                if (_lastSelectedBot == null)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                _master.DeleteRobotByInstance(_lastSelectedBot);
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_MoveToGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastSelectedBot == null)
                {
                    return;
                }

                string groupName = ((ToolStripMenuItem)sender).Text;

                _master.MoveBotToGroup(_lastSelectedBot, groupName);
                RePaintTable();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_MoveToBaseGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastSelectedBot == null)
                {
                    return;
                }

                _master.MoveBotToGroup(_lastSelectedBot, BotPanel.BaseGroupName);
                RePaintTable();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_MoveToNewGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastSelectedBot == null)
                {
                    return;
                }

                List<string> oldGroupNames = _master.GetBotsGroups();
                oldGroupNames.Add(BotPanel.BaseGroupName);
                oldGroupNames.Add(OsLocalization.Trader.Label778);

                NewGroupAddInJournalUi ui = new NewGroupAddInJournalUi(oldGroupNames);
                ui.ShowDialog();

                if (ui.IsAccepted == false
                    || string.IsNullOrEmpty(ui.NewGroupName))
                {
                    return;
                }

                string newGroup = ui.NewGroupName.Replace("@", "").Trim();

                if (string.IsNullOrEmpty(newGroup)
                    || newGroup == BotPanel.BaseGroupName
                    || newGroup == OsLocalization.Trader.Label778
                    || _master.GetBotsGroups().Contains(newGroup))
                {
                    return;
                }

                _master.AddNewGroup(newGroup);
                _master.MoveBotToGroup(_lastSelectedBot, newGroup);
                RePaintTable();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void BotTabsPainter_DeleteGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastSelectedGroup))
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(string.Format(OsLocalization.Trader.Label780, _lastSelectedGroup));
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                _master.DeleteGroup(_lastSelectedGroup);
                RePaintTable();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region работа с чек-боксами включений и отключений

        private DateTime _lastTimeClick = DateTime.MinValue;

        private void _grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            try
            {
                if (_grid.IsCurrentCellDirty
                    && _grid.CurrentCell is DataGridViewCheckBoxCell)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                if (columnIndex != 5 && columnIndex != 6)
                {
                    return;
                }

                if (rowIndex < 0)
                {
                    return;
                }

                _lastTimeClick = DateTime.Now;

                if (rowIndex >= _grid.Rows.Count)
                {
                    return;
                }

                object cellValue = _grid.Rows[rowIndex].Cells[columnIndex].Value;

                if (cellValue == null)
                {
                    return;
                }

                bool isOn = Convert.ToBoolean(cellValue);

                object rowTag = _grid.Rows[rowIndex].Tag;

                if (rowTag is BotPanel bot)
                {
                    if (columnIndex == 5)
                    {
                        OnOffBot(bot, isOn);
                    }
                    else if (columnIndex == 6)
                    {
                        OnOffEmulatorBot(bot, isOn);
                    }
                }
                else if (ReferenceEquals(rowTag, _nullRowTag))
                {
                    if (columnIndex == 5)
                    {
                        OnOffAll(isOn);
                    }
                    else if (columnIndex == 6)
                    {
                        OnOffEmulatorAll(isOn);
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void OnOffBot(BotPanel bot, bool value)
        {
            bot.OnOffEventsInTabs = value;
        }

        private void OnOffAll(bool value)
        {
            if (_master.PanelsArray == null)
            {
                return;
            }
            for (int i = 0; i < _master.PanelsArray.Count; i++)
            {
                BotPanel bot = _master.PanelsArray[i];
                bot.OnOffEventsInTabs = value;
            }
        }

        private void OnOffEmulatorBot(BotPanel bot, bool value)
        {
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

                // сначала базовая группа, затем пользовательские в порядке создания

                List<string> groups = new List<string>();
                groups.Add(BotPanel.BaseGroupName);
                groups.AddRange(_master.GetBotsGroups());

                for (int i = 0; i < groups.Count; i++)
                {
                    string groupName = groups[i];

                    List<BotPanel> botsInGroup = new List<BotPanel>();

                    for (int j = 0; _master.PanelsArray != null && j < _master.PanelsArray.Count; j++)
                    {
                        BotPanel bot = _master.PanelsArray[j];

                        if (bot == null)
                        {
                            continue;
                        }

                        string botGroup = bot.BotGroup;

                        if (string.IsNullOrEmpty(botGroup))
                        {
                            botGroup = BotPanel.BaseGroupName;
                        }

                        if (botGroup == groupName)
                        {
                            botsInGroup.Add(bot);
                        }
                    }

                    if (groupName == BotPanel.BaseGroupName
                        && botsInGroup.Count == 0)
                    { // пустую базовую группу не показываем
                        continue;
                    }

                    bool isCollapsed = _master.IsGroupCollapsed(groupName);

                    _grid.Rows.Add(GetGroupRow(groupName, botsInGroup.Count, isCollapsed));

                    if (isCollapsed == true)
                    {
                        continue;
                    }

                    for (int j = 0; j < botsInGroup.Count; j++)
                    {
                        _grid.Rows.Add(GetRow(botsInGroup[j], j + 1));
                    }
                }

                DataGridViewRow nullRow = GetNullRow();
                nullRow.Tag = _nullRowTag;
                _grid.Rows.Add(nullRow);

                DataGridViewRow addRow = GetAddRow();
                addRow.Tag = _addRowTag;
                _grid.Rows.Add(addRow);

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

        private DataGridViewRow GetGroupRow(string groupName, int botsCount, bool isCollapsed)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = isCollapsed ? "+" : "–";

            row.Cells.Add(new DataGridViewTextBoxCell());

            if (groupName == BotPanel.BaseGroupName)
            {
                row.Cells[1].Value = OsLocalization.Trader.Label778 + " (" + botsCount + ")";
            }
            else
            {
                row.Cells[1].Value = groupName + " (" + botsCount + ")";
            }

            for (int i = 2; i <= 10; i++)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
            }

            // пустая строка в ячейках чек-боксов, чтобы DataGridView не вызывал DataError (FormatException) при форматировании null

            row.Cells[5].Value = "";
            row.Cells[6].Value = "";

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].Style.BackColor = Themes.ThemeManager.GetColorWinForms("GridButtonBackColor");
            }

            row.ReadOnly = true;
            row.Tag = groupName;

            return row;
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
colum9.HeaderText = "Journal common";
colum10.HeaderText = "Action";
*/
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());

            if (string.IsNullOrEmpty(bot.PublicName) == false)
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

            if (bot.TabsSimple.Count != 0 &&
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
            row.Cells[7].Value = OsLocalization.Trader.Label172;//"Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[8].Value = OsLocalization.Trader.Label45;//"Parameters";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[9].Value = OsLocalization.Trader.Label39;//"Delete";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[10].Value = OsLocalization.Trader.Label40; //"Journal";

            if (num % 2 == 0)
            {
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    row.Cells[i].Style.BackColor = Themes.ThemeManager.GetColorWinForms("GridRowAltColor");
                }
            }

            row.Tag = bot;

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
            row.Cells.Add(new DataGridViewButtonCell());
            return row;
        }

        private DataGridViewRow GetAddRow()
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());

            if (_master._startProgram == StartProgram.IsOsTrader)
            {
                if (InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass != null
                    && InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass.Count > 0)
                {
                    AddImageToRow(row);
                }
                else
                {
                    row.Cells.Add(new DataGridViewTextBoxCell());
                }
            }
            else
            {
                if (InteractiveInstructions.TesterLightPosts.AllInstructionsInClass != null
                    && InteractiveInstructions.TesterLightPosts.AllInstructionsInClass.Count > 0)
                {
                    AddImageToRow(row);
                }
                else
                {
                    row.Cells.Add(new DataGridViewTextBoxCell());
                }
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].Value = "";

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].Value = "";
            row.Cells[6].ReadOnly = true;

            row.Cells.Add(new DataGridViewButtonCell());

            if (_master._startProgram == StartProgram.IsOsTrader)
            {
                row.Cells[7].Value = OsLocalization.Trader.Label570; //"Copy trading";
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[8].Value = OsLocalization.Trader.Label747; //"Journal common";
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[9].Value = OsLocalization.Trader.Label38; //"Add New...";
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[10].Value = OsLocalization.Trader.Label762;  // "Migration";

            return row;
        }

        private void AddImageToRow(DataGridViewRow row)
        {
            try
            {
                DataGridViewImageCell imageCell = new DataGridViewImageCell();
                imageCell.ImageLayout = DataGridViewImageCellLayout.Normal;
                row.Cells.Add(imageCell);
                imageCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                string altPath = Path.Combine(Application.StartupPath, @"Images\InstructionPosts\GreenPostCollection.png");

                if (File.Exists(altPath))
                {
                    using (FileStream fs = new FileStream(altPath, FileMode.Open, FileAccess.Read))
                    {
                        Image originalImage = Image.FromStream(fs);
                        Image resizedImage = new Bitmap(originalImage, new Size(25, 20));
                        row.Cells[1].Value = resizedImage;
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);

                if (row.Cells.Count < 2)
                {
                    row.Cells.Add(new DataGridViewTextBoxCell());
                }
            }
        }

        private void UpdaterThreadArea()
        {
            while (true)
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

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (_lastTimeClick.AddSeconds(2) > DateTime.Now)
                    {
                        return;
                    }

                    DataGridViewRow row = _grid.Rows[i];

                    if ((row.Tag is BotPanel) == false)
                    {
                        continue;
                    }

                    BotPanel bot = (BotPanel)row.Tag;

                    if (bot == null)
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

                bool findTheBot = false;

                BotPanel foundBot = null;

                for (int i = 0; i < _master.PanelsArray.Count; i++)
                {
                    BotPanel curRobot = _master.PanelsArray[i];

                    if (curRobot.TabsSimple != null)
                    {
                        for (int i2 = 0; i2 < curRobot.TabsSimple.Count; i2++)
                        {
                            if (curRobot.TabsSimple[i2].TabName == botTabName)
                            {
                                foundBot = curRobot;
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
                                    foundBot = curRobot;
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
                                    foundBot = curRobot;
                                    findTheBot = true;
                                    break;
                                }
                                if (pair.Pairs[j].Tab2.TabName == botTabName)
                                {
                                    foundBot = curRobot;
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
                    int rowNum = GetGridRowIndexByBot(foundBot);

                    if (rowNum == -1)
                    { // робот в свёрнутой группе - не подсвечиваем
                        return;
                    }

                    _rowToPaintInOpenPoses = rowNum;
                    Task.Run(PaintPos);
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private int _rowToPaintInOpenPoses = -1;

        private int GetGridRowIndexByBot(BotPanel bot)
        {
            if (_grid.InvokeRequired)
            {
                return (int)_grid.Invoke(new Func<BotPanel, int>(GetGridRowIndexByBot), bot);
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (ReferenceEquals(_grid.Rows[i].Tag, bot))
                {
                    return i;
                }
            }

            return -1;
        }

        System.Drawing.Color _lastBackColor;

        private async void PaintPos()
        {
            try
            {
                await Task.Delay(200);
                ColoredRow(Themes.ThemeManager.GetColorWinForms("GridFlashColor"));
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

        #region Posts collection

        private InstructionsUi _instructionsUi;

        private void ShowInstructionsForTheTester()
        {
            if (InteractiveInstructions.TesterLightPosts.AllInstructionsInClass == null
                    || InteractiveInstructions.TesterLightPosts.AllInstructionsInClass.Count == 0)
            {
                return;
            }

            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.TesterLightPosts.AllInstructionsInClass, InteractiveInstructions.TesterLightPosts.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = System.Windows.WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ShowInstructionsForTheBotStation()
        {
            if (InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass == null
                    || InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass.Count == 0)
            {
                return;
            }

            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass, InteractiveInstructions.BotStationLightPosts.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = System.Windows.WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _instructionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _instructionsUi.Closed -= _instructionsUi_Closed;
                _instructionsUi = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

    }
}