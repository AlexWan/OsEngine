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
    /// Логика взаимодействия для LinearRegressionCurveUi.xaml
    /// </summary>
    public partial class LinearRegressionCurveUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private LinearRegressionCurve _lrc;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменился ли индикатор
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="linregc">configuration indicator/индикатор для настройки</param>
        public LinearRegressionCurveUi(LinearRegressionCurve linregc)
        {
            InitializeComponent();
            _lrc = linregc;

            TextBoxLenght.Text = _lrc.Lenght.ToString();
            TextBoxLag.Text = _lrc.Lag.ToString();

            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _lrc.ColorBase;


            CheckBoxPaintOnOff.IsChecked = _lrc.PaintOn;
            ComboBoxPriceField.Items.Add(PriceTypePoints.Open);
            ComboBoxPriceField.Items.Add(PriceTypePoints.High);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Low);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Close);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Median);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Typical);

            ComboBoxPriceField.SelectedItem = _lrc.TypePointsToSearch;


            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorCandleType.Content = OsLocalization.Charts.LabelIndicatorCandleType;


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
                    Convert.ToInt32(TextBoxLag.Text) < 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _lrc.ColorBase = HostColor.Child.BackColor;
            _lrc.Lenght = Convert.ToInt32(TextBoxLenght.Text);
            _lrc.Lag = Convert.ToInt32(TextBoxLag.Text);
            _lrc.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            Enum.TryParse(ComboBoxPriceField.SelectedItem.ToString(), true, out _lrc.TypePointsToSearch);

            _lrc.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка изменить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }
    }
}
