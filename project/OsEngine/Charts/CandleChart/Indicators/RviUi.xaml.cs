/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Логика взаимодействия для RVIUi.xaml
    /// </summary>
    public partial class RviUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Rvi _rvi;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="rvi">индикатор для настроек</param>
        public RviUi(Rvi rvi)
        {
            InitializeComponent();
            _rvi = rvi;

            TextBoxLenght.Text = _rvi.Period.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBaseCopy.Child = new TextBox();
            HostColorBase.Child.BackColor = _rvi.ColorDown;
            HostColorBaseCopy.Child.BackColor = _rvi.ColorUp;
            CheckBoxPaintOnOff.IsChecked = _rvi.PaintOn;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _rvi.ColorDown = HostColorBase.Child.BackColor;
            _rvi.ColorUp = HostColorBaseCopy.Child.BackColor;
            _rvi.Period = Convert.ToInt32(TextBoxLenght.Text);
            _rvi.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;



            _rvi.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка настроить цвет
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
