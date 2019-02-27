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
    /// Interaction logic  for MacdUi.xaml
    /// Логика взаимодействия для MacdUi.xaml
    /// </summary>
    public partial class MacdHistogramUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private MacdHistogram _macd;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public MacdHistogramUi(MacdHistogram macd)
        {
            InitializeComponent();
            _macd = macd;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _macd.ColorUp;
            CheckBoxPaintOnOff.IsChecked = _macd.PaintOn;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _macd.ColorDown;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
         }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _macd.ColorUp = HostColorUp.Child.BackColor;
            _macd.ColorDown = HostColorDown.Child.BackColor;
            _macd.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            IsChange = true;


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

        private void ButtonColorAdxUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorAdxDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
