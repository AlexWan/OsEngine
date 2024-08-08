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
    /// Interaction logic for UltimateOscillatorUi.xaml
    /// </summary>
    public partial class UltimateOscillatorUi : Window
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private UltimateOscillator _indicator;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="indicator"> indicator that will be editing/индикатор который будем редактировать</param>
        public UltimateOscillatorUi(UltimateOscillator indicator)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _indicator = indicator;

            TextBoxLength.Text = _indicator.Period1.ToString();
            TextBoxLength2.Text = _indicator.Period2.ToString();
            TextBoxLength3.Text = _indicator.Period3.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _indicator.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _indicator.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod + "1";
            LabelIndicatorPeriod2.Content = OsLocalization.Charts.LabelIndicatorPeriod + "2";
            LabelIndicatorPeriod3.Content = OsLocalization.Charts.LabelIndicatorPeriod + "3";

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
                if (Convert.ToInt32(TextBoxLength.Text) <= 0 ||
                     Convert.ToInt32(TextBoxLength2.Text) <= 0 ||
                      Convert.ToInt32(TextBoxLength3.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }


            _indicator.Period2 = Convert.ToInt32(TextBoxLength2.Text);
            _indicator.Period3 = Convert.ToInt32(TextBoxLength3.Text);
            _indicator.ColorBase = HostColorBase.Child.BackColor;
            _indicator.Period1 = Convert.ToInt32(TextBoxLength.Text);
            _indicator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _indicator.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        ///  color setting button
        /// нажата кнопка изменения цвета
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
