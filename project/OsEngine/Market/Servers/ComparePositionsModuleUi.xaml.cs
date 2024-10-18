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
using System.Threading;
using OsEngine.Market.Servers.MoexAlgopack.Entity;

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

            LabelConnectionName.Content = OsLocalization.Market.Label136;
            LabelConnectionName.Content += " " + _comparePositionsModule.Server.ServerType.ToString();
            Title = OsLocalization.Market.Label137;
            CheckBoxAutoLogMessageOnError.Content = OsLocalization.Market.Label138;
            LabelVerificationPeriod.Content = OsLocalization.Market.Label139;


            bool isInArray = false;

            for (int i = 0; i < _comparePositionsModule.PortfoliosToWatch.Count; i++)
            {
                if (_comparePositionsModule.PortfoliosToWatch[i] == PortfolioName)
                {
                    isInArray = true;
                    break;
                }
            }

            if(isInArray == true)
            {
                CheckBoxAutoLogMessageOnError.IsChecked = true;
            }
            else
            {
                CheckBoxAutoLogMessageOnError.IsChecked = false;
            }
         
            CheckBoxAutoLogMessageOnError.Click += CheckBoxAutoLogMessageOnError_Click;

            ComboBoxVerificationPeriod.Items.Add(ComparePositionsVerificationPeriod.Min1.ToString());
            ComboBoxVerificationPeriod.Items.Add(ComparePositionsVerificationPeriod.Min5.ToString());
            ComboBoxVerificationPeriod.Items.Add(ComparePositionsVerificationPeriod.Min10.ToString());
            ComboBoxVerificationPeriod.Items.Add(ComparePositionsVerificationPeriod.Min30.ToString());
            ComboBoxVerificationPeriod.SelectedItem = comparePositionsModule.VerificationPeriod.ToString();
            ComboBoxVerificationPeriod.SelectionChanged += ComboBoxVerificationPeriod_SelectionChanged;

            RePaintGrids();

            Thread worker = new Thread(RePainterThread);
            worker.Start();
        }

        private void CheckBoxAutoLogMessageOnError_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(CheckBoxAutoLogMessageOnError.IsChecked.Value == true)
                {
                    bool isInArray = false;

                    for(int i =0;i < _comparePositionsModule.PortfoliosToWatch.Count;i++)
                    {
                        if (_comparePositionsModule.PortfoliosToWatch[i] == PortfolioName)
                        {
                            isInArray = true; 
                            break;    
                        }
                    }

                    if(isInArray == false)
                    {
                        _comparePositionsModule.PortfoliosToWatch.Add(PortfolioName);
                        _comparePositionsModule.Save();
                    }
                }
                else
                {
                    for (int i = 0; i < _comparePositionsModule.PortfoliosToWatch.Count; i++)
                    {
                        if (_comparePositionsModule.PortfoliosToWatch[i] == PortfolioName)
                        {
                            _comparePositionsModule.PortfoliosToWatch.RemoveAt(i);
                            _comparePositionsModule.Save();
                            break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _comparePositionsModule.Server.Log.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxVerificationPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComparePositionsVerificationPeriod curPeriod;

                if (Enum.TryParse(ComboBoxVerificationPeriod.SelectedItem.ToString(), out curPeriod))
                {
                    _comparePositionsModule.VerificationPeriod = curPeriod;
                }
            }
            catch (Exception ex)
            {
                _comparePositionsModule.Server.Log.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

        }

        private void ComparePositionsModuleUi_Closed(object sender, EventArgs e)
        {
            _isClosed = true;

            if (GuiClosed != null)
            {
                GuiClosed(PortfolioName);
            }

            _comparePositionsModule = null;
        }

        public event Action<string> GuiClosed;

        private bool _isClosed;

        #region Grid

        DataGridView _grid;

        public void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                         DataGridViewAutoSizeRowsMode.AllCells);

            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            // Portfolio
            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Market.Label140;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum0);

            // Security
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Market.Message14;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum1);

            // State
            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Market.Label141;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum2);

            // Robots Long
            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Market.Label142;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum3);

            // Robots Short
            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Market.Label143;
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum4);

            // Robots Common
            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Market.Label144;
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum5);

            // Portfolio Long
            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Market.Label145;
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum6);

            // Portfolio Short
            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = OsLocalization.Market.Label146;
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum7);

            // Portfolio Common
            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = OsLocalization.Market.Label147;
            colum8.ReadOnly = true;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            _grid.Columns.Add(colum8);

            Host.Child = _grid;
        }

        private void RePainterThread()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_isClosed == true)
                    {
                        return;
                    }

                    RePaintGrids();
                }
                catch(Exception ex)
                {
                    _comparePositionsModule.Server.Log.ProcessMessage(ex.ToString(),Logging.LogMessageType.Error);
                }
            }
        }

        public void RePaintGrids()
        {
            try
            {
                if (!Host.CheckAccess())
                {
                    Host.Dispatcher.Invoke(RePaintGrids);
                    return;
                }

                List<ComparePositionsPortfolio> portfolioCompare = _comparePositionsModule.UpdateCompareData();

                if(portfolioCompare == null)
                {
                    _grid.Rows.Clear();
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                for(int i = 0;i < portfolioCompare.Count;i++)
                {
                    if (portfolioCompare[i].PortfolioName != PortfolioName)
                    {
                        continue;
                    }

                    List<DataGridViewRow> rowsCurrentPortfolio = GetPortfolioRows(portfolioCompare[i]);

                    rows.AddRange(rowsCurrentPortfolio);
                }

                if(rows.Count != _grid.Rows.Count)
                { // переписываем полностью. Изменилось кол-во строк
                    Host.Child = null;

                    _grid.Rows.Clear();
                    _grid.ClearSelection();

                    if (rows.Count > 0)
                    {
                        _grid.Rows.AddRange(rows.ToArray());
                    }

                    Host.Child = _grid;
                }
                else
                {
                    for(int i = 1; i < _grid.Rows.Count;i++)
                    {
                        TryRePaintRow(_grid.Rows[i], rows[i]);
                    }
                }

            }
            catch (Exception ex)
            {
                _comparePositionsModule.Server.Log.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TryRePaintRow(DataGridViewRow oldRow, DataGridViewRow newRow)
        {
            for(int i = 1; i < oldRow.Cells.Count ; i++)
            {
                if (oldRow.Cells[i].Value != newRow.Cells[i].Value)
                {
                    oldRow.Cells[i].Value = newRow.Cells[i].Value;
                }
                if (oldRow.Cells[i].Style.ForeColor != newRow.Cells[i].Style.ForeColor)
                {
                    oldRow.Cells[i].Style.ForeColor = newRow.Cells[i].Style.ForeColor;
                }
            }
        }

        public List<DataGridViewRow> GetPortfolioRows(ComparePositionsPortfolio portfolio)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            DataGridViewRow firstRow = new DataGridViewRow();
            firstRow.Cells.Add(new DataGridViewTextBoxCell());
            firstRow.Cells[0].Value = portfolio.PortfolioName;
            rows.Add(firstRow);

            for (int i = 0; i < portfolio.CompareSecurities.Count; i++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = portfolio.CompareSecurities[i].Security;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = portfolio.CompareSecurities[i].Status.ToString();

                if(portfolio.CompareSecurities[i].Status == ComparePositionsStatus.Normal)
                {
                    nRow.Cells[2].Style.ForeColor = System.Drawing.Color.Green;
                }
                else
                {
                    nRow.Cells[2].Style.ForeColor = System.Drawing.Color.Red;
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = portfolio.CompareSecurities[i].RobotsLong.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = portfolio.CompareSecurities[i].RobotsShort.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = portfolio.CompareSecurities[i].RobotsCommon.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = portfolio.CompareSecurities[i].PortfolioLong.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = portfolio.CompareSecurities[i].PortfolioShort.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = portfolio.CompareSecurities[i].PortfolioCommon.ToString();

                rows.Add(nRow);
            }

            return rows;
        }


        #endregion

    }
}