/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.Entity;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// Interaction logic for ComparePositionsModuleUi.xaml
    /// </summary>
    public partial class ComparePositionsModuleUi : Window
    {
        ComparePositionsModule _comparePositionsModule;

        public string PortfolioName;

        public ComparePositionsModuleUi(ComparePositionsModule comparePositionsModule, string portfolioName)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _comparePositionsModule = comparePositionsModule;
            PortfolioName = portfolioName;
            CreateTable();
            this.Closed += ComparePositionsModuleUi_Closed;

            LabelPortfolioName.Content += portfolioName;
            LabelConnectionName.Content += _comparePositionsModule.Server.ServerType.ToString();
        }

        private void ComparePositionsModuleUi_Closed(object sender, EventArgs e)
        {
           if(GuiClosed != null)
            {
                GuiClosed(PortfolioName);
            }
        }

        public event Action<string> GuiClosed;

        #region Grid

        DataGridView _grid;

        public void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                         DataGridViewAutoSizeRowsMode.AllCells);

            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            // Security
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = "Security";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum1);

            // State
            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = "State";
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum2);

            // Robots Long
            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = "Robots Long";
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum3);

            // Robots Short
            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = "Robots Long";
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum4);

            // Robots Common
            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = "Robots Common";
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum5);

            // Portfolio Long
            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = "Portfolio Long";
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum6);

            // Portfolio Short
            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = "Portfolio Short";
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum7);

            // Portfolio Common
            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = "Portfolio Common";
            colum8.ReadOnly = true;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum8);

            Host.Child = _grid;
        }


        #endregion

    }
}
