/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for MacdLineUi.xaml
    /// Логика взаимодействия для MacdLineUi.xaml
    /// </summary>
    public partial class MacdLineUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private MacdLine _macd;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public MacdLineUi(MacdLine macd)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _macd = macd;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _macd.ColorUp;
            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _macd.ColorDown;
            CheckBoxPaintOnOff.IsChecked = _macd.PaintOn;

            ButtonColorMacd.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorSignalLine.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _macd.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            IsChange = true;
            _macd.ColorUp = HostColorUp.Child.BackColor;
            _macd.ColorDown = HostColorDown.Child.BackColor;

            _macd.Save();
            Close();
        }

        private void ButtonMaShort_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaShortDialog();
        }

        private void ButtonMaLong_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaLongDialog();
        }

        private void ButtonMaSignal_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaSignalDialog();
        }

        private void ButtonColorMacd_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorSignalLine_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
