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

            CheckBoxEcqCollectDataIsOn.IsChecked = SystemUsageAnalyzeMaster.EcqCollectDataIsOn;
            CheckBoxEcqCollectDataIsOn.Checked += CheckBoxEcqCollectDataIsOn_Checked;
            CheckBoxEcqCollectDataIsOn.Unchecked += CheckBoxEcqCollectDataIsOn_Checked;


            ComboBoxRamPeriodSavePoint.Items.Add(SavePointPeriod.OneSecond.ToString());
            ComboBoxRamPeriodSavePoint.Items.Add(SavePointPeriod.TenSeconds.ToString());
            ComboBoxRamPeriodSavePoint.Items.Add(SavePointPeriod.Minute.ToString());
            ComboBoxRamPeriodSavePoint.SelectedItem = SystemUsageAnalyzeMaster.RamPeriodSavePoint.ToString();
            ComboBoxRamPeriodSavePoint.SelectionChanged += ComboBoxRamPeriodSavePoint_SelectionChanged;

            ComboBoxCpuPeriodSavePoint.Items.Add(SavePointPeriod.OneSecond.ToString());
            ComboBoxCpuPeriodSavePoint.Items.Add(SavePointPeriod.TenSeconds.ToString());
            ComboBoxCpuPeriodSavePoint.Items.Add(SavePointPeriod.Minute.ToString());
            ComboBoxCpuPeriodSavePoint.SelectedItem = SystemUsageAnalyzeMaster.CpuPeriodSavePoint.ToString();
            ComboBoxCpuPeriodSavePoint.SelectionChanged += ComboBoxCpuPeriodSavePoint_SelectionChanged;

            ComboBoxEcqPeriodSavePoint.Items.Add(SavePointPeriod.OneSecond.ToString());
            ComboBoxEcqPeriodSavePoint.Items.Add(SavePointPeriod.TenSeconds.ToString());
            ComboBoxEcqPeriodSavePoint.Items.Add(SavePointPeriod.Minute.ToString());
            ComboBoxEcqPeriodSavePoint.SelectedItem = SystemUsageAnalyzeMaster.EcqPeriodSavePoint.ToString();
            ComboBoxEcqPeriodSavePoint.SelectionChanged += ComboBoxEcqPeriodSavePoint_SelectionChanged;

            TextBoxRamPointsMax.Text = SystemUsageAnalyzeMaster.RamPointsMax.ToString();
            TextBoxRamPointsMax.TextChanged += TextBoxRamPointsMax_TextChanged;

            TextBoxCpuPointsMax.Text = SystemUsageAnalyzeMaster.CpuPointsMax.ToString();
            TextBoxCpuPointsMax.TextChanged += TextBoxCpuPointsMax_TextChanged;
             
            TextBoxEcqPointsMax.Text = SystemUsageAnalyzeMaster.EcqPointsMax.ToString();
            TextBoxEcqPointsMax.TextChanged += TextBoxEcqPointsMax_TextChanged;

            this.Closed += SystemAnalyzeUi_Closed;

            Title = OsLocalization.Trader.Label556 + " В РАБОТЕ";
            CheckBoxRamCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxCpuCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxEcqCollectDataIsOn.Content = OsLocalization.Trader.Label557;

            LabelRamPeriod.Content = OsLocalization.Trader.Label559;
            LabelCpuPeriod.Content = OsLocalization.Trader.Label559;
            LabelEcqPeriod.Content = OsLocalization.Trader.Label559;

            LabelRamPointsMaxCount.Content = OsLocalization.Trader.Label561;
            LabelCpuPointsMaxCount.Content = OsLocalization.Trader.Label561;
            LabelEcqPointsMaxCount.Content = OsLocalization.Trader.Label561;

            LabelTotalRamOccupied.Content = OsLocalization.Trader.Label562;
            LabelOsEngineRamOccupied.Content = OsLocalization.Trader.Label563;

            CreateRamChart();
            CreateCpuChart();

            RePaintRamValues(SystemUsageAnalyzeMaster.ValuesRam);
            RePaintCpuChart(SystemUsageAnalyzeMaster.ValuesCpu);

            SystemUsageAnalyzeMaster.RamUsageCollectionChange += SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange += SystemUsageAnalyzeMaster_CpuUsageCollectionChange;

            Layout.StickyBorders.Listen(this);
            Layout.StartupLocation.Start_MouseInCentre(this);
        }

        private void SystemAnalyzeUi_Closed(object sender, EventArgs e)
        {
            SystemUsageAnalyzeMaster.RamUsageCollectionChange -= SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange -= SystemUsageAnalyzeMaster_CpuUsageCollectionChange;

            CheckBoxRamCollectDataIsOn.Checked -= CheckBoxRamCollectDataIsOn_Checked;
            CheckBoxRamCollectDataIsOn.Unchecked -= CheckBoxRamCollectDataIsOn_Checked;

            CheckBoxCpuCollectDataIsOn.Checked -= CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked -= CheckBoxCpuCollectDataIsOn_Checked;

            ComboBoxRamPeriodSavePoint.SelectionChanged -= ComboBoxRamPeriodSavePoint_SelectionChanged;
            ComboBoxCpuPeriodSavePoint.SelectionChanged -= ComboBoxCpuPeriodSavePoint_SelectionChanged;
            ComboBoxEcqPeriodSavePoint.SelectionChanged -= ComboBoxEcqPeriodSavePoint_SelectionChanged;

            TextBoxRamPointsMax.TextChanged -= TextBoxRamPointsMax_TextChanged;
            TextBoxCpuPointsMax.TextChanged -= TextBoxCpuPointsMax_TextChanged;
            TextBoxEcqPointsMax.TextChanged -= TextBoxEcqPointsMax_TextChanged;
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

        private void CheckBoxEcqCollectDataIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.EcqCollectDataIsOn = CheckBoxEcqCollectDataIsOn.IsChecked.Value;
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxRamPeriodSavePoint_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePointPeriod period;

                if (Enum.TryParse(ComboBoxRamPeriodSavePoint.SelectedItem.ToString(), out period))
                {
                    SystemUsageAnalyzeMaster.RamPeriodSavePoint = period;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxCpuPeriodSavePoint_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePointPeriod period;

                if (Enum.TryParse(ComboBoxCpuPeriodSavePoint.SelectedItem.ToString(), out period))
                {
                    SystemUsageAnalyzeMaster.CpuPeriodSavePoint = period;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxEcqPeriodSavePoint_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePointPeriod period;

                if (Enum.TryParse(ComboBoxEcqPeriodSavePoint.SelectedItem.ToString(), out period))
                {
                    SystemUsageAnalyzeMaster.EcqPeriodSavePoint = period;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxRamPointsMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if(string.IsNullOrEmpty(TextBoxRamPointsMax.Text))
                {
                    return;
                }

                int result = Convert.ToInt32(TextBoxRamPointsMax.Text);

                if(result <= 0)
                {
                    result = 10;
                }

                SystemUsageAnalyzeMaster.RamPointsMax = result;
            }
            catch
            {
               // ignore
            }
        }

        private void TextBoxCpuPointsMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxCpuPointsMax.Text))
                {
                    return;
                }

                int result = Convert.ToInt32(TextBoxCpuPointsMax.Text);

                if (result <= 0)
                {
                    result = 10;
                }

                SystemUsageAnalyzeMaster.CpuPointsMax = result;
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxEcqPointsMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxEcqPointsMax.Text))
                {
                    return;
                }

                int result = Convert.ToInt32(TextBoxEcqPointsMax.Text);

                if (result <= 0)
                {
                    result = 10;
                }

                SystemUsageAnalyzeMaster.EcqPointsMax = result;
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
            seriesTotalRam.Color = Color.Green;
            seriesTotalRam.YAxisType = AxisType.Secondary;
            seriesTotalRam.ChartArea = "ChartAreaSystemValues";
            seriesTotalRam.ShadowOffset = 2;
            _chartReport.Series.Add(seriesTotalRam);

            // 3 series free ram

            Series seriesFreeRam = new Series("SeriesFreeRam");
            seriesFreeRam.ChartType = SeriesChartType.Column;
            seriesFreeRam.Color = Color.Red;
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

        private void RePaintRamValues(List<SystemUsagePointRam> values)
        {
            if (_chartReport.InvokeRequired)
            {
                _chartReport.Invoke(new Action<List<SystemUsagePointRam>>(RePaintRamValues),values);
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
                    SystemUsagePointRam usagePoint = values[i];

                    _chartReport.Series[0].Points.AddXY(i, 100);
                    _chartReport.Series[0].Points[^1].ToolTip = OsLocalization.Trader.Label564 + ": " + (100 - usagePoint.SystemUsedPercent) + "%";

                    _chartReport.Series[1].Points.AddXY(i, usagePoint.SystemUsedPercent);
                    _chartReport.Series[1].Points[^1].ToolTip = OsLocalization.Trader.Label565 + ": " + usagePoint.SystemUsedPercent + "%";

                    _chartReport.Series[2].Points.AddXY(i, usagePoint.ProgramUsedPercent);
                    _chartReport.Series[2].Points[^1].ToolTip = "OsEngine: " + usagePoint.ProgramUsedPercent + "%";
                }

                SystemUsagePointRam lastPoint = values[^1];

                TextBoxTotalRamOccupied.Text = lastPoint.SystemUsedPercent.ToString() + "%";
                TextBoxOsEngineRamOccupied.Text = lastPoint.ProgramUsedPercent.ToString() + "%";
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }
        }

        private void SystemUsageAnalyzeMaster_RamUsageCollectionChange(List<SystemUsagePointRam> values)
        {
            RePaintRamValues(values);
        }

        #endregion

        #region CPU

        private void CreateCpuChart()
        {



        }

        private void RePaintCpuChart(List<SystemUsagePointCpu> values)
        {

        }

        private void SystemUsageAnalyzeMaster_CpuUsageCollectionChange(List<SystemUsagePointCpu> values)
        {
            RePaintCpuChart(values);
        }

        #endregion

        #region ECQ. Emergency clearing of queues in servers 







        #endregion

    }
}
