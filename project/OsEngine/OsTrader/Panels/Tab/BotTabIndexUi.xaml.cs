/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Entity;

namespace OsEngine.OsTrader.Panels.Tab
{
    
    public partial class BotTabIndexUi
    {
        
        public BotTabIndexUi(BotTabIndex spread)
        {
            InitializeComponent();
            CreateTable();
            _spread = spread;
            ReloadSecurityTable();
            TextboxUserFormula.Text = _spread.UserFormula;

            Title = OsLocalization.Trader.Label81;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            this.Closed += BotTabIndexUi_Closed;

            this.Activate();
            this.Focus();
        }

        public bool IndexOrSourcesChanged = false;

        private void BotTabIndexUi_Closed(object sender, System.EventArgs e)
        {
            _sourcesGrid.CellDoubleClick -= Grid1CellValueChangeClick;
            DataGridFactory.ClearLinks(_sourcesGrid);
            _sourcesGrid.Rows.Clear();
            _sourcesGrid = null;
            _spread = null;
        }

        private BotTabIndex _spread;

        private DataGridView _sourcesGrid;

        private void CreateTable()
        {
            _sourcesGrid = DataGridFactory.GetDataGridView(
                DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            _sourcesGrid.CellDoubleClick += Grid1CellValueChangeClick;

            DataGridViewTextBoxCell fcell0 = new DataGridViewTextBoxCell();

            DataGridViewColumn fcolumn0 = new DataGridViewColumn();
            fcolumn0.CellTemplate = fcell0;
            fcolumn0.HeaderText = OsLocalization.Trader.Label82;
            fcolumn0.ReadOnly = true;
            fcolumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn0);

            DataGridViewColumn fcolumn1 = new DataGridViewColumn();
            fcolumn1.CellTemplate = fcell0;
            fcolumn1.HeaderText = OsLocalization.Trader.Label83;
            fcolumn1.ReadOnly = true;
            fcolumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn1);

            DataGridViewColumn fcolumn2 = new DataGridViewColumn();
            fcolumn2.CellTemplate = fcell0;
            fcolumn2.HeaderText = OsLocalization.Trader.Label178;
            fcolumn2.ReadOnly = true;
            fcolumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn2);

            DataGridViewColumn fcolumn3 = new DataGridViewColumn();
            fcolumn3.CellTemplate = fcell0;
            fcolumn3.HeaderText = OsLocalization.Trader.Label179;
            fcolumn3.ReadOnly = true;
            fcolumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn3);

            HostSecurity1.Child = _sourcesGrid;
        }

        void Grid1CellValueChangeClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = _sourcesGrid.CurrentCell.RowIndex;
            _spread.ShowIndexConnectorIndexDialog(index);
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void ReloadSecurityTable()
        {
            if (_spread.Tabs == null)
            {
                return;
            }

            _sourcesGrid.Rows.Clear();

            for (int i = 0; i < _spread.Tabs.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add((new DataGridViewTextBoxCell()));
                row.Cells[0].Value = "A"+i;

                row.Cells.Add(new DataGridViewTextBoxCell());
                if (string.IsNullOrWhiteSpace(_spread.Tabs[i].SecurityName))
                {
                    row.Cells[1].Value = OsLocalization.Trader.Label84;

                }
                else
                {
                    row.Cells[1].Value = _spread.Tabs[i].SecurityName;
                }
                
                row.Cells.Add((new DataGridViewTextBoxCell()));
                row.Cells[2].Value = _spread.Tabs[i].ServerType.ToString();

                row.Cells.Add((new DataGridViewTextBoxCell()));
                row.Cells[3].Value = _spread.Tabs[i].TimeFrame.ToString();

                _sourcesGrid.Rows.Add(row);
            }
        }

        private void ButtonAddSecurity_Click(object sender, RoutedEventArgs e)
        {
            _spread.ShowNewSecurityDialog();
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void ButtonDeleteSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_sourcesGrid.CurrentCell == null)
            {
                return;
            }

            _spread.DeleteSecurityTab(_sourcesGrid.CurrentCell.RowIndex);
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if(_spread.UserFormula != TextboxUserFormula.Text 
                || IndexOrSourcesChanged == true)
            {
                _spread.UserFormula = TextboxUserFormula.Text;
            }

            Close();
        }
    }
}
