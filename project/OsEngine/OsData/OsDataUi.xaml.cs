/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsData
{
    /// <summary>
    /// Interaction Logic for OsDataUi.xaml
    /// Логика взаимодействия для OsDataUi.xaml
    /// </summary>
    public partial class OsDataUi
    {
        OsDataMaster _osDataMaster;
        public OsDataUi()
        {
            
            InitializeComponent();
            _osDataMaster = new OsDataMaster(ChartHostPanel, HostLog, HostSource,
                HostSet, ComboBoxSecurity,ComboBoxTimeFrame,RectChart, GreedChartPanel);
            CheckBoxPaintOnOff.IsChecked = _osDataMaster.IsPaintEnabled;
            CheckBoxPaintOnOff.Click += CheckBoxPaintOnOff_Click;
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Closing += OsDataUi_Closing;
            Label4.Content = OsLocalization.Data.Label4;
            Label24.Content = OsLocalization.Data.Label24;
            CheckBoxPaintOnOff.Content = OsLocalization.Data.Label25;
            Label26.Header = OsLocalization.Data.Label26;

        }

        void CheckBoxPaintOnOff_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxPaintOnOff.IsChecked.HasValue &&
                CheckBoxPaintOnOff.IsChecked.Value)
            {
                _osDataMaster.StartPaint();
                _osDataMaster.SaveSettings();
            }
            else
            {
                _osDataMaster.StopPaint();
                _osDataMaster.SaveSettings();
            }
        }

        void OsDataUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
            }
        }
    }
}
