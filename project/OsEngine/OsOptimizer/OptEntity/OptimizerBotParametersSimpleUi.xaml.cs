using System.Collections.Generic;
using System.Windows;
using OsEngine.Entity;
using System.Windows.Forms.Integration;
using System.Windows.Forms;

namespace OsEngine.OsOptimizer.OptEntity
{
    /// <summary>
    /// Логика взаимодействия для OptimizerBotParametersSimpleUi.xaml
    /// </summary>
    public partial class OptimizerBotParametersSimpleUi : Window
    {
        public OptimizerBotParametersSimpleUi(OptimizerReport report, OptimazerFazeReport faze, string botType)
        {
            InitializeComponent();
            _report = report;
            _faze = faze;

            string label = " StrategyType: " + botType;

            label += " Faze Start :" + _faze.Faze.TimeStart.Date.ToShortDateString();

            label += " Faze End :" + _faze.Faze.TimeEnd.Date.ToShortDateString();

            label += " Aver profit % in faze " + report.AverageProfitPercent;

            Title = Title + label;

            List<IIStrategyParameter> parameters = _report.GetParameters();

            if (parameters.Count < 8)
            {
                PaintParams(parameters, HostParams1);
                return;
            }

            List<IIStrategyParameter> parameters2 = new List<IIStrategyParameter>();

            int breakNum = parameters.Count / 2;

            for (int i = breakNum; i < parameters.Count;)
            {
                parameters2.Add(parameters[i]);
                parameters.RemoveAt(i);
            }

            PaintParams(parameters, HostParams1);
            PaintParams(parameters2, HostParams2);

            this.Activate();
            this.Focus();
        }

        OptimizerReport _report;

        OptimazerFazeReport _faze;

        private void PaintParams(List<IIStrategyParameter> parameters, WindowsFormsHost host)
        {
            DataGridView grid = GetGrid();

            for (int i = 0; i < parameters.Count; i++)
            {
                if(parameters[i].Type == StrategyParameterType.Button ||
                    parameters[i].Type == StrategyParameterType.Label)
                {
                    continue;
                }
                DataGridViewRow row = GetRow(parameters[i]);
                grid.Rows.Add(row);
            }

            host.Child = grid;
        }

        private DataGridViewRow GetRow(IIStrategyParameter parameter)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = parameter.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (parameter.Type == StrategyParameterType.Bool)
            {
                nRow.Cells[1].Value = ((StrategyParameterBool)parameter).ValueBool;
            }
            if (parameter.Type == StrategyParameterType.Decimal)
            {
                nRow.Cells[1].Value = ((StrategyParameterDecimal)parameter).ValueDecimal;
            }
            if (parameter.Type == StrategyParameterType.Int)
            {
                nRow.Cells[1].Value = ((StrategyParameterInt)parameter).ValueInt;
            }
            if (parameter.Type == StrategyParameterType.String)
            {
                nRow.Cells[1].Value = ((StrategyParameterString)parameter).ValueString;
            }
            if (parameter.Type == StrategyParameterType.TimeOfDay)
            {
                nRow.Cells[1].Value = ((StrategyParameterTimeOfDay)parameter).Value.ToString();
            }

            return nRow;
        }

        private DataGridView GetGrid()
        {
            DataGridView grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewCellStyle style = grid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "Param name";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = "Value";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(colum01);

            return grid;
        }
    }
}