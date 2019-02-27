/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for VolumeUi.xaml
    /// Логика взаимодействия для VolumeUi.xaml
    /// </summary>
    public partial class VolumeUi
    {
        private Volume _volume; // fractal//фрактал
      public VolumeUi(Volume fractail) // constructor//конструктор
        {
            InitializeComponent();
            _volume = fractail;
            ShowSettingsOnForm();

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
        }

        private void ShowSettingsOnForm()//upload the settings to form// выгрузить настройки на форму
        {
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _volume.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _volume.ColorDown;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) //accept// принять
        {
            _volume.ColorUp = HostColorUp.Child.BackColor;
            _volume.ColorDown = HostColorDown.Child.BackColor;
            _volume.Save();
            IsChange = true;
            Close();
        }

        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();

            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();

            HostColorDown.Child.BackColor = dialog.Color;
        }

        public bool IsChange;
    }
}
