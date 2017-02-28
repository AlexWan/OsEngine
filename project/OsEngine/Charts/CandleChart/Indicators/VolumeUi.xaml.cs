/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для VolumeUi.xaml
    /// </summary>
    public partial class VolumeUi
    {
        private Volume _volume; // фрактал
      public VolumeUi(Volume fractail) // конструктор
        {
            InitializeComponent();
            _volume = fractail;
            ShowSettingsOnForm();
        }

        private void ShowSettingsOnForm()// выгрузить настройки на форму
        {
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _volume.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _volume.ColorDown;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // принять
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
