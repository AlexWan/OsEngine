/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Windows;
using System.Collections.Generic;

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

            ButtonOpenRamFile.Click += ButtonOpenRamFile_Click;

            ComboBoxSavePeriodRam.Items.Add(SavePeriod.OneHour.ToString());
            ComboBoxSavePeriodRam.Items.Add(SavePeriod.OneDay.ToString());
            ComboBoxSavePeriodRam.Items.Add(SavePeriod.FiveDays.ToString());
            ComboBoxSavePeriodRam.SelectedItem = SystemUsageAnalyzeMaster.RamSavePeriod.ToString();
            ComboBoxSavePeriodRam.SelectionChanged += ComboBoxSavePeriodRam_SelectionChanged;

            CheckBoxCpuCollectDataIsOn.IsChecked = SystemUsageAnalyzeMaster.CpuCollectDataIsOn;
            CheckBoxCpuCollectDataIsOn.Checked += CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked += CheckBoxCpuCollectDataIsOn_Checked;

            ButtonOpenCpuFile.Click += ButtonOpenCpuFile_Click;

            ComboBoxSavePeriodCpu.Items.Add(SavePeriod.OneHour.ToString());
            ComboBoxSavePeriodCpu.Items.Add(SavePeriod.OneDay.ToString());
            ComboBoxSavePeriodCpu.Items.Add(SavePeriod.FiveDays.ToString());
            ComboBoxSavePeriodCpu.SelectedItem = SystemUsageAnalyzeMaster.CpuSavePeriod.ToString();
            ComboBoxSavePeriodCpu.SelectionChanged += ComboBoxSavePeriodCpu_SelectionChanged;

            this.Closed += SystemAnalyzeUi_Closed;

            Title = OsLocalization.Trader.Label556 + " В РАБОТЕ";
            CheckBoxRamCollectDataIsOn.Content = OsLocalization.Trader.Label557;
            CheckBoxCpuCollectDataIsOn.Content = OsLocalization.Trader.Label557;

            ButtonOpenRamFile.Content = OsLocalization.Trader.Label558;
            ButtonOpenCpuFile.Content = OsLocalization.Trader.Label558;

            LabelSavePeriodRam.Content = OsLocalization.Trader.Label559;
            LabelSavePeriodCpu.Content = OsLocalization.Trader.Label559;

            CreateRamChart();
            CreateCpuChart();

            RePaintRamChart();
            RePaintCpuChart();

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
            ButtonOpenRamFile.Click -= ButtonOpenRamFile_Click;

            CheckBoxCpuCollectDataIsOn.Checked -= CheckBoxCpuCollectDataIsOn_Checked;
            CheckBoxCpuCollectDataIsOn.Unchecked -= CheckBoxCpuCollectDataIsOn_Checked;
            ButtonOpenCpuFile.Click -= ButtonOpenCpuFile_Click;
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

        private void ButtonOpenCpuFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.ShowFileCpuCollection();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonOpenRamFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemUsageAnalyzeMaster.ShowFileRamCollection();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxSavePeriodCpu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePeriod currentValue;

                if (Enum.TryParse(ComboBoxSavePeriodCpu.SelectedItem.ToString(), out currentValue))
                {
                    SystemUsageAnalyzeMaster.CpuSavePeriod = currentValue;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxSavePeriodRam_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                SavePeriod currentValue;

                if (Enum.TryParse(ComboBoxSavePeriodRam.SelectedItem.ToString(), out currentValue))
                {
                    SystemUsageAnalyzeMaster.RamSavePeriod = currentValue;
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region RAM

        private void CreateRamChart()
        {



        }

        private void RePaintRamChart()
        {

        }

        private void SystemUsageAnalyzeMaster_RamUsageCollectionChange(List<SystemUsagePoint> values)
        {
            RePaintRamChart();
        }

        #endregion

        #region CPU

        private void CreateCpuChart()
        {



        }

        private void RePaintCpuChart()
        {

        }

        private void SystemUsageAnalyzeMaster_CpuUsageCollectionChange(List<SystemUsagePoint> values)
        {
            RePaintCpuChart();
        }

        #endregion

    }
}
