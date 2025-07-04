/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Forms.DataVisualization.Charting;
using Color = System.Drawing.Color;
using OsEngine.Charts;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.SystemAnalyze
{
    public partial class SystemAnalyzeUi : Window
    {
        #region Service

        public SystemAnalyzeUi()
        {
            InitializeComponent();

            CheckBoxRamCollectDataIsOn.IsChecked = SystemUsageAnalyzeMaster.RamCollectDataIsOn;
            CheckBoxRamCollectDataIsOn.Checked += CheckBoxRamCollectDataIsOn_Checked;
            CheckBoxRamCollectDataIsOn.Unchecked += CheckBoxRamCollectDataIsOn_Checked;

            CheckBoxCpuCollectDataIsOn.IsChecked = SystemUsageAnalyzeMaster.CpuCollectDataIsOn;
            CheckBoxCpuCollectDataIsOn.Checked += CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked += CheckBoxCpuCollectDataIsOn_Checked;

            this.Closed += SystemAnalyzeUi_Closed;

            Title = OsLocalization.Trader.Label556 + " В РАБОТЕ";
            CheckBoxRamCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxCpuCollectDataIsOn.Content = OsLocalization.Trader.Label557;

            CreateRamChart();
            CreateCpuChart();

            RePaintRamChart(SystemUsageAnalyzeMaster.ValuesRam);
            RePaintCpuChart(SystemUsageAnalyzeMaster.ValuesCpu);

            SystemUsageAnalyzeMaster.RamUsageCollectionChange += SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange += SystemUsageAnalyzeMaster_CpuUsageCollectionChange;

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
        }

        private void SystemAnalyzeUi_Closed(object sender, EventArgs e)
        {
            SystemUsageAnalyzeMaster.RamUsageCollectionChange -= SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange -= SystemUsageAnalyzeMaster_CpuUsageCollectionChange;

            CheckBoxRamCollectDataIsOn.Checked -= CheckBoxRamCollectDataIsOn_Checked;
            CheckBoxRamCollectDataIsOn.Unchecked -= CheckBoxRamCollectDataIsOn_Checked;

            CheckBoxCpuCollectDataIsOn.Checked -= CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked -= CheckBoxCpuCollectDataIsOn_Checked;
        }

        private void CheckBoxCpuCollectDataIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.CpuCollectDataIsOn = CheckBoxCpuCollectDataIsOn.IsChecked.Value;
            }
            catch
            {
                // ignore
            }
        } 

        private void CheckBoxRamCollectDataIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.RamCollectDataIsOn = CheckBoxRamCollectDataIsOn.IsChecked.Value;
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region RAM

        private Chart _chartReport;

        private void CreateRamChart()
        {
            _chartReport = new Chart();
            HostRam.Child = _chartReport;
            HostRam.Child.Show();

            _chartReport.Series.Clear();
            _chartReport.ChartAreas.Clear();

            // 1 chart area system values

            ChartArea areaSystemValues = new ChartArea("ChartAreaSystemValues");
            areaSystemValues.Position.Height = 70;
            areaSystemValues.Position.Width = 100;
            areaSystemValues.Position.Y = 0;
            areaSystemValues.CursorX.IsUserSelectionEnabled = false;
            areaSystemValues.CursorX.IsUserEnabled = false;
            areaSystemValues.AxisX.Enabled = AxisEnabled.False;
            _chartReport.ChartAreas.Add(areaSystemValues);

            // 2 series total ram

            Series seriesTotalRam = new Series("SeriesTotalRam");
            seriesTotalRam.ChartType = SeriesChartType.RangeColumn;
            seriesTotalRam.Color = Color.DarkRed;
            seriesTotalRam.YAxisType = AxisType.Secondary;
            seriesTotalRam.ChartArea = "ChartAreaSystemValues";
            seriesTotalRam.ShadowOffset = 2;
            _chartReport.Series.Add(seriesTotalRam);

            // 3 series free ram

            Series seriesFreeRam = new Series("SeriesFreeRam");
            seriesFreeRam.ChartType = SeriesChartType.Column;
            seriesFreeRam.Color = Color.Green;
            seriesFreeRam.YAxisType = AxisType.Secondary;
            seriesFreeRam.ChartArea = "ChartAreaSystemValues";
            seriesFreeRam.ShadowOffset = 2;
            _chartReport.Series.Add(seriesFreeRam);

            // 4 chart area my values

            ChartArea areaMyRam = new ChartArea("ChartAreaMyValues");
            areaMyRam.AlignWithChartArea = "ChartAreaSystemValues";
            areaMyRam.Position.Height = 30;
            areaMyRam.Position.Width = 100;
            areaMyRam.Position.Y = 70;
            areaMyRam.AxisX.Enabled = AxisEnabled.False;
            _chartReport.ChartAreas.Add(areaMyRam);

            // 5 series my ram

            Series seriesMyRam = new Series("seriesMyRam");
            seriesMyRam.ChartType = SeriesChartType.Column;
            seriesMyRam.YAxisType = AxisType.Secondary;
            seriesMyRam.Color = Color.DarkOrange;
            seriesMyRam.ChartArea = "ChartAreaMyValues";
            seriesMyRam.ShadowOffset = 2;
            _chartReport.Series.Add(seriesMyRam);

            // 6 colors

            _chartReport.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartReport.ChartAreas != null && i < _chartReport.ChartAreas.Count; i++)
            {
                _chartReport.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartReport.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartReport.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartReport.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartReport.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartReport.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void RePaintRamChart(List<SystemUsagePoint> values)
        {
            if (_chartReport.InvokeRequired)
            {
                _chartReport.Invoke(new Action<List<SystemUsagePoint>>(RePaintRamChart),values);
                return;
            }

            try
            {
                if (_chartReport == null)
                {
                    return;
                }

                _chartReport.Series[0].Points.ClearFast();
                _chartReport.Series[1].Points.ClearFast();
                _chartReport.Series[2].Points.ClearFast();

                for (int i = 0; i < values.Count; i++)
                {
                    SystemUsagePoint usagePoint = values[i];

                    _chartReport.Series[0].Points.AddXY(i, usagePoint.SystemTotal);
                    _chartReport.Series[0].Points[^1].ToolTip = usagePoint.ToolTip;

                    _chartReport.Series[1].Points.AddXY(i, usagePoint.SystemFree);
                    _chartReport.Series[1].Points[^1].ToolTip = usagePoint.ToolTip;

                    _chartReport.Series[2].Points.AddXY(i, usagePoint.ProgramUsed);
                    _chartReport.Series[2].Points[^1].ToolTip = usagePoint.ToolTip;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }

        }

        private void SystemUsageAnalyzeMaster_RamUsageCollectionChange(List<SystemUsagePoint> values)
        {
            RePaintRamChart(values);
        }

        #endregion

        #region CPU

        private void CreateCpuChart()
        {



        }

        private void RePaintCpuChart(List<SystemUsagePoint> values)
        {

        }

        private void SystemUsageAnalyzeMaster_CpuUsageCollectionChange(List<SystemUsagePoint> values)
        {
            RePaintCpuChart(values);
        }

        #endregion

        #region Emergency clearing of queues in servers





        #endregion

    }
}
