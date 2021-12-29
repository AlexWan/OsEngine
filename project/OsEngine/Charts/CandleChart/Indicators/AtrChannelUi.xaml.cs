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
    /// Interaction logic  for AtrUi.xaml
    /// Логика взаимодействия для AtrUi.xaml
    /// </summary>
    public partial class AtrChannelUi 
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private AtrChannel _atr;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="dtd">configuration indicator/индикатор для настроек</param>
        public AtrChannelUi(AtrChannel atr)
        {
            InitializeComponent();
            _atr = atr;

            TextBoxLenght.Text = _atr.Lenght.ToString();
            TextBoxMultiplier.Text = _atr.Multiplier.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _atr.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _atr.PaintOn;

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorMultiplier.Content = OsLocalization.Charts.LabelIndicatorMultiplier;
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

            _atr.ColorBase = HostColorBase.Child.BackColor;
            _atr.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _atr.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _atr.Multiplier = Convert.ToDecimal(TextBoxMultiplier.Text);
            _atr.Save();

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
