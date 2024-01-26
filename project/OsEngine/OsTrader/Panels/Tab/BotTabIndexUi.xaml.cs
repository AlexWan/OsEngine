/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Entity;
using System;

namespace OsEngine.OsTrader.Panels.Tab
{ 
    public partial class BotTabIndexUi
    {
        
        public BotTabIndexUi(BotTabIndex spread)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            CreateTable();
            _spread = spread;
            ReloadSecurityTable();
            TextboxUserFormula.Text = _spread.UserFormula;

            Title = OsLocalization.Trader.Label81;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            ButtonClearAllSecurities.Content = OsLocalization.Trader.Label369;
            TabControlItem1.Header = OsLocalization.Trader.Label374;
            TabControlItem2.Header = OsLocalization.Trader.Label375;

            this.Closed += BotTabIndexUi_Closed;

            this.Activate();
            this.Focus();
        }

        public bool IndexOrSourcesChanged = false;

        private void BotTabIndexUi_Closed(object sender, System.EventArgs e)
        {
            _sourcesGrid.CellDoubleClick -= Grid1CellValueChangeClick;
            _sourcesGrid.CellClick -= _sourcesGrid_CellClick;
            this.Closed -= BotTabIndexUi_Closed;

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

            _sourcesGrid.ScrollBars = ScrollBars.Vertical;

            _sourcesGrid.CellDoubleClick += Grid1CellValueChangeClick;
            _sourcesGrid.CellClick += _sourcesGrid_CellClick;

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

            DataGridViewColumn fcolumn4 = new DataGridViewColumn();
            fcolumn4.CellTemplate = fcell0;
            fcolumn4.HeaderText = "";
            fcolumn4.ReadOnly = true;
            fcolumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn4);

            HostSecurity1.Child = _sourcesGrid;
        }

        void Grid1CellValueChangeClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = _sourcesGrid.CurrentCell.RowIndex;
            _spread.ShowIndexConnectorIndexDialog(index);
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void _sourcesGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if(e.ColumnIndex != 4)
            {
                return;
            }

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

            int showRow = _sourcesGrid.FirstDisplayedScrollingRowIndex;

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

                DataGridViewButtonCell button = new DataGridViewButtonCell(); 
                button.Value = OsLocalization.Trader.Label235;
                row.Cells.Add(button);

                _sourcesGrid.Rows.Add(row);
            }


            if (showRow > 0 &&
                showRow < _sourcesGrid.Rows.Count)
            {
                _sourcesGrid.FirstDisplayedScrollingRowIndex = showRow;
            }
        }

        private void ButtonAddSecurity_Click(object sender, RoutedEventArgs e)
        {
            _spread.ShowNewSecurityDialog();
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void RepeatButtonDeleteSecurity_Click(object sender, RoutedEventArgs e)
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

        private void ButtonClearAllSecurities_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sourcesGrid.Rows.Count == 0)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label370);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                while (_spread.Tabs.Count > 0)
                {
                    _spread.DeleteSecurityTab(0);
                }

                ReloadSecurityTable();
                IndexOrSourcesChanged = true;
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.Message);
                ui.ShowDialog();
            }
        }
    }
}
