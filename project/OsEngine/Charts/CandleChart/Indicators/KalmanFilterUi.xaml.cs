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
    /// Interaction logic  for  AdxUi.xaml
    /// Логика взаимодействия для AdxUi.xaml
    /// </summary>
    public partial class KalmanFilterUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private KalmanFilter _indicator;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="indicator">configuration indicator/индикатор который будем редактировать</param>
        public KalmanFilterUi(KalmanFilter indicator)
        {
            InitializeComponent();
            _indicator = indicator;

            TextBoxSharpness.Text = _indicator.Sharpness.ToString();
            TextBoxK.Text = _indicator.K.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _indicator.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _indicator.PaintOn;

            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxSharpness.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxK.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _indicator.ColorBase = HostColorBase.Child.BackColor;
            _indicator.Sharpness = Convert.ToDecimal(TextBoxSharpness.Text);
            _indicator.K = Convert.ToDecimal(TextBoxK.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _indicator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            
            _indicator.Save();

            IsChange = true;

            Close();
        }

        /// <summary>
        /// color setting button
        /// нажата кнопка изменения цвета
        /// </summary>
        private void ButtonColorAdx_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
