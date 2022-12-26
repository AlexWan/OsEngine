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

namespace OsEngine.OsTrader.Gui
{
    public class BotTabsPainter
    {
        public BotTabsPainter(OsTraderMaster master, WindowsFormsHost host)
        {
            _master = master;
            _host = host;

            CreateTable();
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

        private void CreateTable()
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

            DataGridViewButtonColumn colum06 = new DataGridViewButtonColumn();
            //colum06.CellTemplate = cell0;
            //colum06.HeaderText = "Chart";
            colum06.ReadOnly = true;
            colum06.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum06);

            DataGridViewButtonColumn colum07 = new DataGridViewButtonColumn();
            //colum07.CellTemplate = cell0;
            //colum07.HeaderText = "Parameters";
            colum07.ReadOnly = true;
            colum07.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum07);

            DataGridViewButtonColumn colum09 = new DataGridViewButtonColumn();
            // colum09.CellTemplate = cell0;
            //colum09.HeaderText = "Action";
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum09);

            _grid = newGrid;
            _host.Child = _grid;

            _grid.Click += _grid_Click;
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
colum0.HeaderText = "Num";               0
colum01.HeaderText = "Name";             1
colum02.HeaderText = "Type";             2
colum04.HeaderText = "First Security";   3
colum05.HeaderText = "Position";         4
colum06.HeaderText = "Chart";            5
colum07.HeaderText = "Parameters";       6
colum09.HeaderText = "Action";           7
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


            if(coluIndex == 5 &&
                rowIndex < botsCount)
            { // вызываем чарт робота
                bot.ShowChartDialog();
            }
            else if (coluIndex == 6 &&
   rowIndex < botsCount)
            { // вызываем параметры
                bot.ShowParametrDialog();
            }
            else if (coluIndex == 7 &&
    rowIndex < botsCount)
            { // вызываем окно удаление робота
                _master.DeleteByNum(rowIndex);
            }

            if (coluIndex == 6 &&
     rowIndex == botsCount + 1)
            { // вызываем общий журнал
                _master.ShowCommunityJournal(2, 0, 0);
            }
            else if (coluIndex == 7 &&
    rowIndex == botsCount + 1)
            { // вызываем добавление нового бота
                _master.CreateNewBot();
            }
        }

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
colum04.HeaderText = "First Security";
colum05.HeaderText = "Position";
colum06.HeaderText = "Chart";
colum07.HeaderText = "Parameters";
colum08.HeaderText = "Journal";
colum09.HeaderText = "Action";
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

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[5].Value =  OsLocalization.Trader.Label172;//"Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[6].Value = OsLocalization.Trader.Label45;//"Parameters";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[7].Value = OsLocalization.Trader.Label39;//"Delete";

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
            DataGridViewRow row = new DataGridViewRow();

            for(int i = 0;i < 8;i++)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
            }

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
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[6].Value = OsLocalization.Trader.Label40; //"Journal";
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[7].Value = OsLocalization.Trader.Label38; //"Add New...";

            return row;
        }

        private void UpdaterThreadArea()
        {
            while(true)
            {
                Thread.Sleep(5000);

                if(MainWindow.ProccesIsWorked == false)
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
                    
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }
    }
}
