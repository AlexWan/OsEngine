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
    /// Interaction logic  for EfficiencyRatioUi.xaml
    /// Логика взаимодействия для EfficiencyRatioUi.xaml
    /// </summary>
    public partial class EfficiencyRatioUi 
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private EfficiencyRatio _eR;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="eR">configuration indicator/индикатор для настроек</param>
        public EfficiencyRatioUi(EfficiencyRatio eR)
        {
            InitializeComponent();
            _eR = eR;

            TextBoxLenght.Text = _eR.Lenght.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _eR.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _eR.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;


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

            _eR.ColorBase = HostColorBase.Child.BackColor;
            _eR.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _eR.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _eR.Save();

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