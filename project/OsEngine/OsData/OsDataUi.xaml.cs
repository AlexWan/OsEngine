/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System.Windows;

namespace OsEngine.OsData
{
    /// <summary>
    /// Логика взаимодействия для OsDataUi.xaml
    /// </summary>
    public partial class OsDataUi
    {
        OsDataMaster _osDataMaster;
        public OsDataUi()
        {
            InitializeComponent();
            _osDataMaster = new OsDataMaster(ChartHostPanel, HostLog, HostSource, HostSet, ComboBoxSecurity,ComboBoxTimeFrame,RectChart);
            CheckBoxPaintOnOff.IsChecked = _osDataMaster.IsPaintEnabled;
            CheckBoxPaintOnOff.Click += CheckBoxPaintOnOff_Click;
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Closing += OsDataUi_Closing;
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
            AcceptDialogUi ui = new AcceptDialogUi("Вы собираетесь закрыть программу. Вы уверены?");
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
            }
        }
    }
}
