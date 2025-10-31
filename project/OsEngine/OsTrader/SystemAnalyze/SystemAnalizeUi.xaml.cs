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
using OsEngine.Entity;

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

            CheckBoxMoqCollectDataIsOn.IsChecked = SystemUsageAnalyzeMaster.MoqCollectDataIsOn;
            CheckBoxMoqCollectDataIsOn.Checked += CheckBoxMoqCollectDataIsOn_Checked;
            CheckBoxMoqCollectDataIsOn.Unchecked += CheckBoxMoqCollectDataIsOn_Checked;

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

            ComboBoxMoqPeriodSavePoint.Items.Add(SavePointPeriod.OneSecond.ToString());
            ComboBoxMoqPeriodSavePoint.Items.Add(SavePointPeriod.TenSeconds.ToString());
            ComboBoxMoqPeriodSavePoint.Items.Add(SavePointPeriod.Minute.ToString());
            ComboBoxMoqPeriodSavePoint.SelectedItem = SystemUsageAnalyzeMaster.MoqPeriodSavePoint.ToString();
            ComboBoxMoqPeriodSavePoint.SelectionChanged += ComboBoxMoqPeriodSavePoint_SelectionChanged;

            TextBoxRamPointsMax.Text = SystemUsageAnalyzeMaster.RamPointsMax.ToString();
            TextBoxRamPointsMax.TextChanged += TextBoxRamPointsMax_TextChanged;

            TextBoxCpuPointsMax.Text = SystemUsageAnalyzeMaster.CpuPointsMax.ToString();
            TextBoxCpuPointsMax.TextChanged += TextBoxCpuPointsMax_TextChanged;
             
            TextBoxEcqPointsMax.Text = SystemUsageAnalyzeMaster.EcqPointsMax.ToString();
            TextBoxEcqPointsMax.TextChanged += TextBoxEcqPointsMax_TextChanged;

            TextBoxMoqPointsMax.Text = SystemUsageAnalyzeMaster.MoqPointsMax.ToString();
            TextBoxMoqPointsMax.TextChanged += TextBoxMoqPointsMax_TextChanged;

            this.Closed += SystemAnalyzeUi_Closed;

            Title = OsLocalization.Trader.Label556;
            CheckBoxRamCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxCpuCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxEcqCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxMoqCollectDataIsOn.Content = OsLocalization.Trader.Label557;

            LabelRamPeriod.Content = OsLocalization.Trader.Label559;
            LabelCpuPeriod.Content = OsLocalization.Trader.Label559;
            LabelEcqPeriod.Content = OsLocalization.Trader.Label559;
            LabelMoqPeriod.Content = OsLocalization.Trader.Label559;

            LabelRamPointsMaxCount.Content = OsLocalization.Trader.Label561;
            LabelCpuPointsMaxCount.Content = OsLocalization.Trader.Label561;
            LabelEcqPointsMaxCount.Content = OsLocalization.Trader.Label561;
            LabelMoqPointsMaxCount.Content = OsLocalization.Trader.Label561;

            LabelFreeRam.Content = OsLocalization.Trader.Label564;
            LabelTotalRamOccupied.Content = OsLocalization.Trader.Label562;
            LabelOsEngineRamOccupied.Content = OsLocalization.Trader.Label563;

            LabelCpuTotalOccupiedPercent.Content = OsLocalization.Trader.Label562;
            LabelCpuProgramOccupiedPercent.Content = OsLocalization.Trader.Label563;

            LabelMarketDepthClearingCount.Content = OsLocalization.Trader.Label566;
            LabelBidAskClearingCount.Content = OsLocalization.Trader.Label567;

            LabelMoqMaxOrdersInQueue.Content = OsLocalization.Trader.Label573;

            CreateRamChart();
            CreateCpuChart();
            CreateEcqChart();
            CreateMoqChart();

            RePaintRamValues(SystemUsageAnalyzeMaster.ValuesRam);
            RePaintCpuChart(SystemUsageAnalyzeMaster.ValuesCpu);
            RePaintEcqChart(SystemUsageAnalyzeMaster.ValuesEcq);
            RePaintMoqChart(SystemUsageAnalyzeMaster.ValuesMoq);

            SystemUsageAnalyzeMaster.RamUsageCollectionChange += SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange += SystemUsageAnalyzeMaster_CpuUsageCollectionChange;
            SystemUsageAnalyzeMaster.EcqUsageCollectionChange += SystemUsageAnalyzeMaster_EcqUsageCollectionChange;
            SystemUsageAnalyzeMaster.MoqUsageCollectionChange += SystemUsageAnalyzeMaster_MoqUsageCollectionChange;

            Layout.StickyBorders.Listen(this);
            Layout.StartupLocation.Start_MouseInCentre(this);
        }

        private void SystemAnalyzeUi_Closed(object sender, EventArgs e)
        {
            SystemUsageAnalyzeMaster.RamUsageCollectionChange -= SystemUsageAnalyzeMaster_RamUsageCollectionChange;
            SystemUsageAnalyzeMaster.CpuUsageCollectionChange -= SystemUsageAnalyzeMaster_CpuUsageCollectionChange;
            SystemUsageAnalyzeMaster.EcqUsageCollectionChange -= SystemUsageAnalyzeMaster_EcqUsageCollectionChange;
            SystemUsageAnalyzeMaster.MoqUsageCollectionChange -= SystemUsageAnalyzeMaster_MoqUsageCollectionChange;

            CheckBoxRamCollectDataIsOn.Checked -= CheckBoxRamCollectDataIsOn_Checked;
            CheckBoxRamCollectDataIsOn.Unchecked -= CheckBoxRamCollectDataIsOn_Checked;

            CheckBoxCpuCollectDataIsOn.Checked -= CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked -= CheckBoxCpuCollectDataIsOn_Checked;

            CheckBoxEcqCollectDataIsOn.Checked -= CheckBoxEcqCollectDataIsOn_Checked;
            CheckBoxEcqCollectDataIsOn.Unchecked -= CheckBoxEcqCollectDataIsOn_Checked;

            CheckBoxMoqCollectDataIsOn.Checked -= CheckBoxMoqCollectDataIsOn_Checked;
            CheckBoxMoqCollectDataIsOn.Unchecked -= CheckBoxMoqCollectDataIsOn_Checked;

            ComboBoxRamPeriodSavePoint.SelectionChanged -= ComboBoxRamPeriodSavePoint_SelectionChanged;
            ComboBoxCpuPeriodSavePoint.SelectionChanged -= ComboBoxCpuPeriodSavePoint_SelectionChanged;
            ComboBoxEcqPeriodSavePoint.SelectionChanged -= ComboBoxEcqPeriodSavePoint_SelectionChanged;
            ComboBoxMoqPeriodSavePoint.SelectionChanged -= ComboBoxMoqPeriodSavePoint_SelectionChanged;

            TextBoxRamPointsMax.TextChanged -= TextBoxRamPointsMax_TextChanged;
            TextBoxCpuPointsMax.TextChanged -= TextBoxCpuPointsMax_TextChanged;
            TextBoxEcqPointsMax.TextChanged -= TextBoxEcqPointsMax_TextChanged;
            TextBoxMoqPointsMax.TextChanged -= TextBoxMoqPointsMax_TextChanged;

            HostCpu.Child = null;
            HostEcq.Child = null;
            HostMoq.Child = null;
            HostRam.Child = null;

            _chartCpu = null;
            _chartRam = null;
            _chartMoq = null;
            _chartEcq = null;

            this.Closed -= SystemAnalyzeUi_Closed;
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

        private void CheckBoxMoqCollectDataIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.MoqCollectDataIsOn = CheckBoxMoqCollectDataIsOn.IsChecked.Value;
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

        private void ComboBoxMoqPeriodSavePoint_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePointPeriod period;

                if (Enum.TryParse(ComboBoxMoqPeriodSavePoint.SelectedItem.ToString(), out period))
                {
                    SystemUsageAnalyzeMaster.MoqPeriodSavePoint = period;
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

        private void TextBoxMoqPointsMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMoqPointsMax.Text))
                {
                    return;
                }

                int result = Convert.ToInt32(TextBoxMoqPointsMax.Text);

                if (result <= 0)
                {
                    result = 10;
                }

                SystemUsageAnalyzeMaster.MoqPointsMax = result;
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region RAM

        private Chart _chartRam; 

        private void CreateRamChart()
        {
            _chartRam = new Chart();
            HostRam.Child = _chartRam;
            HostRam.Child.Show();

            _chartRam.Series.Clear();
            _chartRam.ChartAreas.Clear();

            // 1 chart area system values

            ChartArea areaSystemValues = new ChartArea("ChartAreaSystemValues");
            areaSystemValues.Position.Height = 70;
            areaSystemValues.Position.Width = 100;
            areaSystemValues.Position.Y = 0;
            areaSystemValues.CursorX.IsUserSelectionEnabled = false;
            areaSystemValues.CursorX.IsUserEnabled = false;
            areaSystemValues.AxisX.Enabled = AxisEnabled.False;
            _chartRam.ChartAreas.Add(areaSystemValues);

            // 2 series total ram

            Series seriesTotalRam = new Series("SeriesTotalRam");
            seriesTotalRam.ChartType = SeriesChartType.RangeColumn;
            seriesTotalRam.Color = Color.Green;
            seriesTotalRam.YAxisType = AxisType.Secondary;
            seriesTotalRam.ChartArea = "ChartAreaSystemValues";
            seriesTotalRam.ShadowOffset = 2;
            _chartRam.Series.Add(seriesTotalRam);

            // 3 series free ram

            Series seriesFreeRam = new Series("SeriesFreeRam");
            seriesFreeRam.ChartType = SeriesChartType.Column;
            seriesFreeRam.Color = Color.Red;
            seriesFreeRam.YAxisType = AxisType.Secondary;
            seriesFreeRam.ChartArea = "ChartAreaSystemValues";
            seriesFreeRam.ShadowOffset = 2;
            _chartRam.Series.Add(seriesFreeRam);

            // 4 chart area my values

            ChartArea areaMyRam = new ChartArea("ChartAreaMyValues");
            areaMyRam.AlignWithChartArea = "ChartAreaSystemValues";
            areaMyRam.Position.Height = 30;
            areaMyRam.Position.Width = 100;
            areaMyRam.Position.Y = 70;
            areaMyRam.AxisX.Enabled = AxisEnabled.False;
            _chartRam.ChartAreas.Add(areaMyRam);

            // 5 series my ram

            Series seriesMyRam = new Series("seriesMyRam");
            seriesMyRam.ChartType = SeriesChartType.Column;
            seriesMyRam.YAxisType = AxisType.Secondary;
            seriesMyRam.Color = Color.DarkOrange;
            seriesMyRam.ChartArea = "ChartAreaMyValues";
            seriesMyRam.ShadowOffset = 2;
            _chartRam.Series.Add(seriesMyRam);

            // 6 colors

            _chartRam.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartRam.ChartAreas != null && i < _chartRam.ChartAreas.Count; i++)
            {
                _chartRam.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartRam.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartRam.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartRam.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartRam.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartRam.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void RePaintRamValues(List<SystemUsagePointRam> values)
        {
            try
            {
                if (_chartRam == null)
                {
                    return;
                }
                
                if (_chartRam.InvokeRequired)
                {
                    _chartRam.Invoke(new Action<List<SystemUsagePointRam>>(RePaintRamValues), values);
                    return;
                }

                _chartRam.Series[0].Points.ClearFast();
                _chartRam.Series[1].Points.ClearFast();
                _chartRam.Series[2].Points.ClearFast();

                int maxPoints = SystemUsageAnalyzeMaster.RamPointsMax;
                if (values.Count > maxPoints)
                {
                    values.RemoveRange(0, values.Count - maxPoints);
                }

                if (values == null 
                    || values.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < values.Count; i++)
                {
                    SystemUsagePointRam usagePoint = values[i];

                    _chartRam.Series[0].Points.AddXY(i, 100);
                    _chartRam.Series[0].Points[^1].ToolTip = OsLocalization.Trader.Label564 + ": " + (100 - usagePoint.SystemUsedPercent) + "%";

                    _chartRam.Series[1].Points.AddXY(i, usagePoint.SystemUsedPercent);
                    _chartRam.Series[1].Points[^1].ToolTip = OsLocalization.Trader.Label565 + ": " + usagePoint.SystemUsedPercent + "%";

                    _chartRam.Series[2].Points.AddXY(i, usagePoint.ProgramUsedPercent);
                    _chartRam.Series[2].Points[^1].ToolTip = "OsEngine: " + usagePoint.ProgramUsedPercent + "%";
                }

                SystemUsagePointRam lastPoint = values[^1];

                TextBoxTotalRamOccupied.Text = lastPoint.SystemUsedPercent.ToString() + "%";
                TextBoxOsEngineRamOccupied.Text = lastPoint.ProgramUsedPercent.ToString() + "%";
                TextBoxFreeRam.Text = (100 - lastPoint.SystemUsedPercent).ToString() + "%";
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

        private Chart _chartCpu;

        private void CreateCpuChart()
        {
            _chartCpu = new Chart();
            HostCpu.Child = _chartCpu;
            HostCpu.Child.Show();

            _chartCpu.Series.Clear();
            _chartCpu.ChartAreas.Clear();

            // 1 chart area system values

            ChartArea areaSystemValues = new ChartArea("ChartAreaSystemValues");
            areaSystemValues.Position.Height = 100;
            areaSystemValues.Position.Width = 100;
            areaSystemValues.Position.Y = 0;
            areaSystemValues.CursorX.IsUserSelectionEnabled = false;
            areaSystemValues.CursorX.IsUserEnabled = false;
            areaSystemValues.AxisX.Enabled = AxisEnabled.False;
            _chartCpu.ChartAreas.Add(areaSystemValues);

            // 2 series total cpu

            Series seriesTotalCpu = new Series("SeriesTotalCpu");
            seriesTotalCpu.ChartType = SeriesChartType.Line;
            seriesTotalCpu.BorderWidth = 3;
            seriesTotalCpu.Color = Color.Green;
            seriesTotalCpu.YAxisType = AxisType.Secondary;
            seriesTotalCpu.ChartArea = "ChartAreaSystemValues";
            seriesTotalCpu.ShadowOffset = 2;
            _chartCpu.Series.Add(seriesTotalCpu);

            // 3 series osEngine cpu

            Series seriesOsEngineCpu = new Series("SeriesFreeCpu");
            seriesOsEngineCpu.ChartType = SeriesChartType.Line;
            seriesOsEngineCpu.BorderWidth = 3;
            seriesOsEngineCpu.Color = Color.Red;
            seriesOsEngineCpu.YAxisType = AxisType.Secondary;
            seriesOsEngineCpu.ChartArea = "ChartAreaSystemValues";
            seriesOsEngineCpu.ShadowOffset = 2;
            _chartCpu.Series.Add(seriesOsEngineCpu);

            _chartCpu.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartCpu.ChartAreas != null && i < _chartCpu.ChartAreas.Count; i++)
            {
                _chartCpu.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartCpu.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartCpu.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartCpu.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartCpu.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartCpu.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void RePaintCpuChart(List<SystemUsagePointCpu> values)
        {
            try
            {
                if (_chartCpu.InvokeRequired)
                {
                    _chartCpu.Invoke(new Action<List<SystemUsagePointCpu>>(RePaintCpuChart), values);
                    return;
                }

                if (_chartCpu == null)
                {
                    return;
                }

                if (values == null
                    || values.Count == 0)
                {
                    return;
                }

                _chartCpu.Series[0].Points.ClearFast();
                _chartCpu.Series[1].Points.ClearFast();

                int maxPoints = SystemUsageAnalyzeMaster.CpuPointsMax;

                if (values.Count > maxPoints)
                {
                    values.RemoveRange(0, values.Count - maxPoints);
                }

                decimal maxValue = 0;

                for (int i = 0; i < values.Count; i++)
                {
                    SystemUsagePointCpu usagePoint = values[i];

                    _chartCpu.Series[0].Points.AddXY(i, usagePoint.TotalOccupiedPercent);
                    _chartCpu.Series[0].Points[^1].ToolTip = OsLocalization.Trader.Label562 + ": " + usagePoint.TotalOccupiedPercent + "%";

                    _chartCpu.Series[1].Points.AddXY(i, usagePoint.ProgramOccupiedPercent);
                    _chartCpu.Series[1].Points[^1].ToolTip = OsLocalization.Trader.Label563 + ": " + usagePoint.ProgramOccupiedPercent + "%";

                    if (usagePoint.TotalOccupiedPercent > maxValue)
                    {
                        maxValue = usagePoint.TotalOccupiedPercent;
                    }
                }

                if (maxValue != 0)
                {
                    _chartCpu.ChartAreas[0].AxisY2.Maximum = (double)maxValue;
                    _chartCpu.ChartAreas[0].AxisY2.Minimum = 0;
                }

                SystemUsagePointCpu lastPoint = values[^1];

                TextBoxCpuTotalOccupiedPercent.Text = lastPoint.TotalOccupiedPercent.ToString() + "%";
                TextBoxCpuProgramOccupiedPercent.Text = lastPoint.ProgramOccupiedPercent.ToString() + "%";
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }
        }

        private void SystemUsageAnalyzeMaster_CpuUsageCollectionChange(List<SystemUsagePointCpu> values)
        {
            RePaintCpuChart(values);
        }

        #endregion

        #region ECQ. Emergency clearing of queues in servers 

        private Chart _chartEcq;

        private void CreateEcqChart()
        {
            _chartEcq = new Chart();
            HostEcq.Child = _chartEcq;
            HostEcq.Child.Show();

            _chartEcq.Series.Clear();
            _chartEcq.ChartAreas.Clear();

            // 1 chart area

            ChartArea areaSystemValues = new ChartArea("ChartAreaSystemValues");
            areaSystemValues.Position.Height = 100;
            areaSystemValues.Position.Width = 100;
            areaSystemValues.Position.Y = 0;
            areaSystemValues.CursorX.IsUserSelectionEnabled = false;
            areaSystemValues.CursorX.IsUserEnabled = false;
            areaSystemValues.AxisX.Enabled = AxisEnabled.False;
            _chartEcq.ChartAreas.Add(areaSystemValues);

            // 2 series Md

            Series seriesTotalCpu = new Series("SeriesMarketDepthClearingCount");
            seriesTotalCpu.ChartType = SeriesChartType.Line;
            seriesTotalCpu.BorderWidth = 3;
            seriesTotalCpu.Color = Color.DarkOrange;
            seriesTotalCpu.YAxisType = AxisType.Secondary;
            seriesTotalCpu.ChartArea = "ChartAreaSystemValues";
            seriesTotalCpu.ShadowOffset = 2;
            _chartEcq.Series.Add(seriesTotalCpu);

            // 3 series bid ask

            Series seriesOsEngineCpu = new Series("SeriesBidAskClearingCount");
            seriesOsEngineCpu.ChartType = SeriesChartType.Line;
            seriesOsEngineCpu.BorderWidth = 3;
            seriesOsEngineCpu.Color = Color.Red;
            seriesOsEngineCpu.YAxisType = AxisType.Secondary;
            seriesOsEngineCpu.ChartArea = "ChartAreaSystemValues";
            seriesOsEngineCpu.ShadowOffset = 2;
            _chartEcq.Series.Add(seriesOsEngineCpu);

            _chartEcq.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartEcq.ChartAreas != null && i < _chartEcq.ChartAreas.Count; i++)
            {
                _chartEcq.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartEcq.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartEcq.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartEcq.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartEcq.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartEcq.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void RePaintEcqChart(List<SystemUsagePointEcq> values)
        {
            try
            {
                if (_chartEcq.InvokeRequired)
                {
                    _chartEcq.Invoke(new Action<List<SystemUsagePointEcq>>(RePaintEcqChart), values);
                    return;
                }

                _chartEcq.Series[0].Points.ClearFast();
                _chartEcq.Series[1].Points.ClearFast();

                int maxPoints = SystemUsageAnalyzeMaster.EcqPointsMax;

                if (values.Count > maxPoints)
                {
                    values.RemoveRange(0, values.Count - maxPoints);
                }

                if (values == null
                    || values.Count == 0)
                {
                    return;
                }

                decimal maxValue = 0;

                for (int i = 0; i < values.Count; i++)
                {
                    SystemUsagePointEcq usagePoint = values[i];

                    _chartEcq.Series[0].Points.AddXY(i, usagePoint.MarketDepthClearingCount);
                    _chartEcq.Series[0].Points[^1].ToolTip = OsLocalization.Trader.Label566 + ": " + usagePoint.MarketDepthClearingCount;

                    _chartEcq.Series[1].Points.AddXY(i, usagePoint.BidAskClearingCount);
                    _chartEcq.Series[1].Points[^1].ToolTip = OsLocalization.Trader.Label567 + ": " + usagePoint.BidAskClearingCount;

                    if(usagePoint.BidAskClearingCount > maxValue)
                    {
                        maxValue = usagePoint.BidAskClearingCount;
                    }

                    if (usagePoint.MarketDepthClearingCount > maxValue)
                    {
                        maxValue = usagePoint.MarketDepthClearingCount;
                    }
                }

                if (maxValue != 0)
                {
                    _chartEcq.ChartAreas[0].AxisY2.Maximum = (double)maxValue;
                    _chartEcq.ChartAreas[0].AxisY2.Minimum = 0;
                }

                SystemUsagePointEcq lastPoint = values[^1];

                TextBoxMarketDepthClearingCount.Text = lastPoint.MarketDepthClearingCount.ToString();
                TextBoxBidAskClearingCount.Text = lastPoint.BidAskClearingCount.ToString();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }
        }

        private void SystemUsageAnalyzeMaster_EcqUsageCollectionChange(List<SystemUsagePointEcq> values)
        {
            RePaintEcqChart(values);
        }

        private void ButtonEcq_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label568);
                ui.ShowDialog();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region MOQ.

        private Chart _chartMoq;

        private void CreateMoqChart()
        {
            _chartMoq = new Chart();
            HostMoq.Child = _chartMoq;
            HostMoq.Child.Show();

            _chartMoq.Series.Clear();
            _chartMoq.ChartAreas.Clear();

            // 1 chart area

            ChartArea areaSystemValues = new ChartArea("ChartAreaSystemValues");
            areaSystemValues.Position.Height = 100;
            areaSystemValues.Position.Width = 100;
            areaSystemValues.Position.Y = 0;
            areaSystemValues.CursorX.IsUserSelectionEnabled = false;
            areaSystemValues.CursorX.IsUserEnabled = false;
            areaSystemValues.AxisX.Enabled = AxisEnabled.False;
            _chartMoq.ChartAreas.Add(areaSystemValues);

            // 2 series Md

            Series seriesTotalCpu = new Series("SeriesMaxOrdersCount");
            seriesTotalCpu.ChartType = SeriesChartType.Line;
            seriesTotalCpu.BorderWidth = 3;
            seriesTotalCpu.Color = Color.DarkOrange;
            seriesTotalCpu.YAxisType = AxisType.Secondary;
            seriesTotalCpu.ChartArea = "ChartAreaSystemValues";
            seriesTotalCpu.ShadowOffset = 2;
            _chartMoq.Series.Add(seriesTotalCpu);

            _chartMoq.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartMoq.ChartAreas != null && i < _chartMoq.ChartAreas.Count; i++)
            {
                _chartMoq.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartMoq.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartMoq.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartMoq.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartMoq.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartMoq.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void RePaintMoqChart(List<SystemUsagePointMoq> values)
        {
            try
            {
                if (_chartMoq.InvokeRequired)
                {
                    _chartMoq.Invoke(new Action<List<SystemUsagePointMoq>>(RePaintMoqChart), values);
                    return;
                }

                _chartMoq.Series[0].Points.ClearFast();

                if (values == null
                    || values.Count == 0)
                {
                    return;
                }

                decimal maxValue = 0;

                for (int i = 0; i < values.Count; i++)
                {
                    SystemUsagePointMoq usagePoint = values[i];

                    _chartMoq.Series[0].Points.AddXY(i, usagePoint.MaxOrdersInQueue);
                    _chartMoq.Series[0].Points[^1].ToolTip = OsLocalization.Trader.Label573 + ": " + usagePoint.MaxOrdersInQueue;

                    int maxPoints = SystemUsageAnalyzeMaster.MoqPointsMax;

                    if (values.Count > maxPoints)
                    {
                        values.RemoveRange(0, values.Count - maxPoints);
                    }

                    if (usagePoint.MaxOrdersInQueue > maxValue)
                    {
                        maxValue = usagePoint.MaxOrdersInQueue;
                    }
                }

                if (maxValue != 0)
                {
                    _chartMoq.ChartAreas[0].AxisY2.Maximum = (double)maxValue;
                    _chartMoq.ChartAreas[0].AxisY2.Minimum = 0;
                }

                SystemUsagePointMoq lastPoint = values[^1];

                TextBoxMaxOrdersInQueue.Text = lastPoint.MaxOrdersInQueue.ToString();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }

        }

        private void SystemUsageAnalyzeMaster_MoqUsageCollectionChange(List<SystemUsagePointMoq> values)
        {
            RePaintMoqChart(values);
        }

        private void ButtonMoqToolTip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label574);
                ui.ShowDialog();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

    }
}
