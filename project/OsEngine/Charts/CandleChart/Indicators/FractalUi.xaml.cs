/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for FractalUi.xaml
    /// Логика взаимодействия для FractalUi.xaml
    /// </summary>
    public partial class FractalUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private Fractal _fractail;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="fractail">configuration indicator/индикатор для настройки</param>
        public FractalUi(Fractal fractail) 
        {
            InitializeComponent();
            _fractail = fractail;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _fractail.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _fractail.ColorDown;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;


        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _fractail.ColorUp = HostColorUp.Child.BackColor;
            _fractail.ColorDown = HostColorDown.Child.BackColor;
            _fractail.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// top points color button
        /// кнопка цвет верхних точек
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();

            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// button points color button
        /// кнопка цвет нижних точек
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();

            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
