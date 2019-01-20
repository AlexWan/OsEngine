/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsTrader.Panels.Tab
{

    /// <summary>
    /// Логика взаимодействия для BotTabIndexUi.xaml
    /// </summary>
    public partial class BotTabIndexUi
    {

        public BotTabIndexUi(BotTabIndex spread)
        {
            InitializeComponent();
            CreateTable();
            _spread = spread;
            ReloadSecurityTable();
            TextboxUserFormula.Text = _spread.UserFormula;
        }

        private BotTabIndex _spread;

        private DataGridView _grid1;

        private void CreateTable()
        {
            //создание первой табицы
            _grid1 = new DataGridView();
            _grid1.CellContentDoubleClick += Grid1CellValueChangeClick;

            _grid1.AllowUserToOrderColumns = false;
            _grid1.AllowUserToResizeColumns = false;
            _grid1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid1.AllowUserToDeleteRows = false;
            _grid1.AllowUserToAddRows = false;
            _grid1.RowHeadersVisible = false;
            _grid1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid1.MultiSelect = false;

            DataGridViewCellStyle style1 = new DataGridViewCellStyle();
            style1.Alignment = DataGridViewContentAlignment.TopLeft;
            style1.WrapMode = DataGridViewTriState.True;
            _grid1.DefaultCellStyle = style1;

            DataGridViewTextBoxCell fcell0 = new DataGridViewTextBoxCell();
            fcell0.Style = style1;

            DataGridViewColumn fcolumn0 = new DataGridViewColumn();
            fcolumn0.CellTemplate = fcell0;
            fcolumn0.HeaderText = @"Номер индекса";
            fcolumn0.ReadOnly = true;
            fcolumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid1.Columns.Add(fcolumn0);

            DataGridViewColumn fcolumn1 = new DataGridViewColumn();
            fcolumn1.CellTemplate = fcell0;
            fcolumn1.HeaderText = @"Код бумаги";
            fcolumn1.ReadOnly = true;
            fcolumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid1.Columns.Add(fcolumn1);

            HostSecurity1.Child = _grid1;

        }

        void Grid1CellValueChangeClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = _grid1.CurrentCell.RowIndex;
            _spread.ShowIndexConnectorIndexDialog(index);
            ReloadSecurityTable();
        }

        private void ReloadSecurityTable()
        {
            if (_spread.Tabs == null)
            {
                return;
            }

            _grid1.Rows.Clear();

            for (int i = 0; i < _spread.Tabs.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add((new DataGridViewTextBoxCell()));
                row.Cells[0].Value = "A"+i;


                row.Cells.Add(new DataGridViewTextBoxCell());
                if (string.IsNullOrWhiteSpace(_spread.Tabs[i].NamePaper))
                {
                    row.Cells[1].Value = "Пусто";

                }
                else
                {

                    row.Cells[1].Value = _spread.Tabs[i].NamePaper;

                }
                _grid1.Rows.Add(row);
            }
        }

        private void ButtonAddSecurity_Click(object sender, RoutedEventArgs e)
        {
            _spread.ShowNewSecurityDialog();
            ReloadSecurityTable();
        }

        private void ButtonDeleteSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_grid1.CurrentCell == null)
            {
                return;
            }

            _spread.DeleteSecurityTab(_grid1.CurrentCell.RowIndex);
            ReloadSecurityTable();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _spread.UserFormula = TextboxUserFormula.Text;
            Close();
        }
    }
}
