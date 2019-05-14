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
    /// Interaction logic  for  MovingAverageUi.xaml
    /// Логика взаимодействия для MovingAverageUi.xaml
    /// </summary>
    public partial class ParabolicSarUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private ParabolicSaR _mA;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменился ли индикатор
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="mA">configuration indicator/индикатор для настройки</param>
        public ParabolicSarUi(ParabolicSaR mA)
        {
            InitializeComponent();
            _mA = mA;

            TextBoxAf.Text = _mA.Af.ToString();
            TextBoxMaxAf.Text = _mA.MaxAf.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _mA.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _mA.ColorDown;

            CheckBoxPaintOnOff.IsChecked = _mA.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorStep.Content = OsLocalization.Charts.LabelIndicatorStep;
            LabelIndicatorMaxStep.Content = OsLocalization.Charts.LabelIndicatorMaxStep;

        }


        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDouble(TextBoxAf.Text) <= 0 || Convert.ToDouble(TextBoxMaxAf.Text) <= 0)
                {
                    throw new Exception("error");
                }
                //Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out _mA.TypeCalculationAverage);
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _mA.ColorUp = HostColor.Child.BackColor;
            _mA.ColorDown = HostColorDown.Child.BackColor;

            _mA.Af = Convert.ToDouble(TextBoxAf.Text);
            _mA.MaxAf = Convert.ToDouble(TextBoxMaxAf.Text);
            _mA.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            //Enum.TryParse(ComboBoxPriceField.SelectedItem.ToString(), true, out _mA.TypePointsToSearch);

            _mA.Save();
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

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();

            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
