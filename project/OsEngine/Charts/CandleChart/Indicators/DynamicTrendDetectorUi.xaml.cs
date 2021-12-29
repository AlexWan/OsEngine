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
    public partial class DynamicTrendDetectorUi 
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private DynamicTrendDetector _dtd;

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
        public DynamicTrendDetectorUi(DynamicTrendDetector dtd)
        {
            InitializeComponent();
            _dtd = dtd;

            TextBoxLenght.Text = _dtd.Lenght.ToString();
            TextBoxCorrectionCoeff.Text = _dtd.CorrectionCoeff.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _dtd.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _dtd.PaintOn;

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorCorrectionCoeff.Content = OsLocalization.Charts.LabelIndicatorCorrectionCoeff;
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

            _dtd.ColorBase = HostColorBase.Child.BackColor;
            _dtd.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _dtd.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _dtd.CorrectionCoeff = Convert.ToDecimal(TextBoxCorrectionCoeff.Text);
            _dtd.Save();

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
