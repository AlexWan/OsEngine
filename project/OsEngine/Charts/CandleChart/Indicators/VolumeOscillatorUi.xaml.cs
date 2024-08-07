/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for  MovingAverageUi.xaml
    /// Логика взаимодействия для MovingAverageUi.xaml
    /// </summary>
    public partial class VolumeOscillatorUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private VolumeOscillator _mA;

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
        public VolumeOscillatorUi(VolumeOscillator mA)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _mA = mA;

            TextBoxLength1.Text = _mA.Length1.ToString();
            TextBoxLength2.Text = _mA.Length2.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _mA.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _mA.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod1.Content = OsLocalization.Charts.LabelIndicatorPeriod + " 1";
            LabelIndicatorPeriod2.Content = OsLocalization.Charts.LabelIndicatorPeriod + " 2";

            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((Convert.ToInt32(TextBoxLength1.Text) <= 0) 
                    || (Convert.ToInt32(TextBoxLength2.Text) <= 0))
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

            _mA.ColorBase = HostColor.Child.BackColor;
            _mA.Length1 = Convert.ToInt32(TextBoxLength1.Text);
            _mA.Length2 = Convert.ToInt32(TextBoxLength2.Text);
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
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }

    }
}
