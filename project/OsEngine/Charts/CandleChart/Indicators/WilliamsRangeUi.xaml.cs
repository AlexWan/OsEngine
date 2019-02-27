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
    /// Interaction logic  for WilliamsRangeUi.xaml
    /// Логика взаимодействия для WilliamsRangeUi.xaml
    /// </summary>
    public partial class WilliamsRangeUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private WilliamsRange _wr;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="wr">configuration indicator/индикатор для настроек</param>
        public WilliamsRangeUi(WilliamsRange wr)
        {
            InitializeComponent();
            _wr = wr;

            TextBoxLenght.Text = _wr.Nperiod.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _wr.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _wr.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
        }

        /// <summary>
        /// accept button
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

            _wr.ColorBase = HostColorBase.Child.BackColor;
            _wr.Nperiod = Convert.ToInt32(TextBoxLenght.Text);
            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _wr.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            

            _wr.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
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

