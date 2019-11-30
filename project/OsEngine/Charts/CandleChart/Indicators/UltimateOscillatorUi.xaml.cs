/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic for UltimateOscillatorUi.xaml
    /// </summary>
    public partial class UltimateOscillatorUi : Window
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private UltimateOscillator _indicator;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="indicator"> indicator that will be editing/индикатор который будем редактировать</param>
        public UltimateOscillatorUi(UltimateOscillator indicator)
        {
            InitializeComponent();
            _indicator = indicator;

            TextBoxLenght.Text = _indicator.Period1.ToString();
            TextBoxLenght2.Text = _indicator.Period2.ToString();
            TextBoxLenght3.Text = _indicator.Period3.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _indicator.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _indicator.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod + "1";
            LabelIndicatorPeriod2.Content = OsLocalization.Charts.LabelIndicatorPeriod + "2";
            LabelIndicatorPeriod3.Content = OsLocalization.Charts.LabelIndicatorPeriod + "3";


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
                     Convert.ToInt32(TextBoxLenght2.Text) <= 0 ||
                      Convert.ToInt32(TextBoxLenght3.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }


            _indicator.Period2 = Convert.ToInt32(TextBoxLenght2.Text);
            _indicator.Period3 = Convert.ToInt32(TextBoxLenght3.Text);
            _indicator.ColorBase = HostColorBase.Child.BackColor;
            _indicator.Period1 = Convert.ToInt32(TextBoxLenght.Text);
            _indicator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _indicator.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        ///  color setting button
        /// нажата кнопка изменения цвета
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
