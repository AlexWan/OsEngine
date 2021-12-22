using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.Entity;
using System.Windows.Forms;

namespace OsEngine.Journal.Assemblage
{
    /// <summary>
    /// Логика взаимодействия для AssemblageBotsUi.xaml
    /// </summary>
    public partial class AssemblageBotsUi : Window
    {
        public AssemblageBotsUi(AssemblageBotsMaster master)
        {
            InitializeComponent();

            CreateDealsTable();
            LoadDeals(master.AllPositions);
            PaintDealsTable();
        }

        #region

        DataGridView _gridPositions;

        private void CreateDealsTable()
        {
            _gridPositions = DataGridFactory.GetDataGridPosition();
            HostDeals.Child = _gridPositions;
        }

        private void PaintDealsTable()
        {
            _gridPositions.Rows.Clear();

            for (int i = 0; i < _poses.Count; i++)
            {
                _gridPositions.Rows.Add(GetRow(_poses[i]));
            }
        }

        private void LoadDeals(List<Position> poses)
        {
            _poses = poses;
            PaintDealsTable();
        }

        private List<Position> _poses;

        private void CreateMainTable()
        {

        }

        /// <summary>
        /// take a row for the table representing the position

        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">position/позиция</param>
        /// <returns>table row/строка для таблицы</returns>
        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }


            DataGridViewCellStyle styleSide = new DataGridViewCellStyle();

            if (position.Direction == Side.Buy)
            {
                styleSide.BackColor = System.Drawing.Color.DodgerBlue;
                styleSide.SelectionBackColor = System.Drawing.Color.DodgerBlue;
            }
            else
            {
                styleSide.BackColor = System.Drawing.Color.DarkOrange;
                styleSide.SelectionBackColor = System.Drawing.Color.DarkOrange;
            }

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = position.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = position.TimeCreate;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = position.TimeClose;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = position.NameBot;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = position.SecurityName;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = position.Direction;
            nRow.Cells[5].Style = styleSide;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].Value = position.State;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].Value = position.MaxVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = position.OpenVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = position.WaitVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[17].Value = position.SignalTypeOpen;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[18].Value = position.SignalTypeClose;

            return nRow;
        }

        #endregion

    }
}
