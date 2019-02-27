/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for PivotUi.xaml
    /// Логика взаимодействия для PivotUi.xaml
    /// </summary>
    public partial class PivotUi
    {

        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private Pivot _pivot;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public PivotUi(Pivot pivot)
        {
            InitializeComponent();
            _pivot = pivot;


            CheckBoxPaintOnOff.IsChecked = _pivot.PaintOn;

            HostR1.Child = new TextBox();
            HostR1.Child.BackColor = _pivot.ColorR1;

            HostR2.Child = new TextBox();
            HostR2.Child.BackColor = _pivot.ColorR2;

            HostR3.Child = new TextBox();
            HostR3.Child.BackColor = _pivot.ColorR3;

            HostR4.Child = new TextBox();
            HostR4.Child.BackColor = _pivot.ColorR4;

            HostS1.Child = new TextBox();
            HostS1.Child.BackColor = _pivot.ColorS1;

            HostS2.Child = new TextBox();
            HostS2.Child.BackColor = _pivot.ColorS2;

            HostS3.Child = new TextBox();
            HostS3.Child.BackColor = _pivot.ColorS3;

            HostS4.Child = new TextBox();
            HostS4.Child.BackColor = _pivot.ColorS4;

            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _pivot.ColorR1 = HostR1.Child.BackColor;
            _pivot.ColorR2 = HostR2.Child.BackColor;
            _pivot.ColorR3 = HostR3.Child.BackColor;
            _pivot.ColorR4 = HostR4.Child.BackColor;

            _pivot.ColorS1 = HostS1.Child.BackColor;
            _pivot.ColorS2 = HostS2.Child.BackColor;
            _pivot.ColorS3 = HostS3.Child.BackColor;
            _pivot.ColorS4 = HostS4.Child.BackColor;

            _pivot.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _pivot.Save();

            IsChange = true;
            Close();
        }

        private void ButtonR1_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR1.Child.BackColor;
            dialog.ShowDialog();
            HostR1.Child.BackColor = dialog.Color;
        }

        private void ButtonR2_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR2.Child.BackColor;
            dialog.ShowDialog();
            HostR2.Child.BackColor = dialog.Color;
        }

        private void ButtonR3_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR3.Child.BackColor;
            dialog.ShowDialog();
            HostR3.Child.BackColor = dialog.Color;
        }

        private void ButtonR4_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR4.Child.BackColor;
            dialog.ShowDialog();
            HostR4.Child.BackColor = dialog.Color;
        }

        private void ButtonS1_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS1.Child.BackColor;
            dialog.ShowDialog();
            HostS1.Child.BackColor = dialog.Color;
        }

        private void ButtonS2_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS2.Child.BackColor;
            dialog.ShowDialog();
            HostS2.Child.BackColor = dialog.Color;
        }

        private void ButtonS3_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS3.Child.BackColor;
            dialog.ShowDialog();
            HostS3.Child.BackColor = dialog.Color;
        }

        private void ButtonS4_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS4.Child.BackColor;
            dialog.ShowDialog();
            HostS4.Child.BackColor = dialog.Color;
        }
    }
}
