/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic for StochRsiUi.xaml
    /// </summary>
    public partial class StochRsiUi : Window
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private StochRsi _rsi;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="rsi">configuration indicator/индикатор который будем настраивать</param>
        public StochRsiUi(StochRsi rsi)
        {
            InitializeComponent();
            _rsi = rsi;

            TextBoxLenght.Text = _rsi.RsiLenght.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _rsi.ColorK;
            HostColorD.Child = new TextBox();
            HostColorD.Child.BackColor = _rsi.ColorD;

            ButtonColorK.Content = OsLocalization.Charts.LabelButtonIndicatorColor + " K";
            ButtonColorD.Content = OsLocalization.Charts.LabelButtonIndicatorColor + " D";
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            TextBoxStochasticLength.Text = _rsi.StochasticLength.ToString();
            TextBoxK.Text = _rsi.K.ToString();
            TextBoxD.Text = _rsi.D.ToString();
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0 ||
                    Convert.ToInt32(TextBoxStochasticLength.Text) <= 0 ||
                    Convert.ToInt32(TextBoxK.Text) <= 0 ||
                    Convert.ToInt32(TextBoxD.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _rsi.ColorK = HostColor.Child.BackColor;
            _rsi.ColorD = HostColorD.Child.BackColor;
            _rsi.RsiLenght = Convert.ToInt32(TextBoxLenght.Text);
            _rsi.StochasticLength = Convert.ToInt32(TextBoxStochasticLength.Text);
            _rsi.K = Convert.ToInt32(TextBoxK.Text);
            _rsi.D = Convert.ToInt32(TextBoxD.Text);

            _rsi.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка далее выбор цвета
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// color setting button
        /// кнопка далее выбор цвета
        /// </summary>
        private void ButtonColorD_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorD.Child.BackColor;
            dialog.ShowDialog();

            HostColorD.Child.BackColor = dialog.Color;
        }
    }
}
