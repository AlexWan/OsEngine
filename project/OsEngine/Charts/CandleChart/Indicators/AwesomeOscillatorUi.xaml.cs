﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Interaction logic  for AwesomeOscillatorUi.xaml
    /// Логика взаимодействия для AwesomeOscillatorUi.xaml
    /// </summary>
    public partial class AwesomeOscillatorUi 
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private AwesomeOscillator _awesomeOscillatoro;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменяли ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="awesomeOscillator">configuration indicator/индикатор который будем настраивать</param>
        public AwesomeOscillatorUi(AwesomeOscillator awesomeOscillator)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _awesomeOscillatoro = awesomeOscillator;

            TextBoxLengthLong.Text = _awesomeOscillatoro.LengthLong.ToString();
            TextBoxLengthShort.Text = _awesomeOscillatoro.LengthShort.ToString();

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _awesomeOscillatoro.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _awesomeOscillatoro.ColorDown;

            CheckBoxPaintOnOff.IsChecked = _awesomeOscillatoro.PaintOn;

            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Exponential);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Simple);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Weighted);

            ComboBoxMovingType.SelectedItem = _awesomeOscillatoro.TypeCalculationAverage;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;
            LabelIndicatorShortPeriod.Content = OsLocalization.Charts.LabelIndicatorShortPeriod;
            LabelIndicatorMethod.Content = OsLocalization.Charts.LabelIndicatorMethod;

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
                if (Convert.ToInt32(TextBoxLengthLong.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLengthShort.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _awesomeOscillatoro.ColorUp = HostColorUp.Child.BackColor;
            _awesomeOscillatoro.ColorDown = HostColorDown.Child.BackColor;

            _awesomeOscillatoro.LengthLong = Convert.ToInt32(TextBoxLengthLong.Text);
            _awesomeOscillatoro.LengthShort = Convert.ToInt32(TextBoxLengthShort.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _awesomeOscillatoro.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            MovingAverageTypeCalculation movingAverageType;

            Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out movingAverageType);
            _awesomeOscillatoro.TypeCalculationAverage = movingAverageType;
            _awesomeOscillatoro.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// top line color button
        /// кнопка цвет верхней линии
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// bottom line color button
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
