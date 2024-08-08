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
    /// Interaction logic  for  AdxUi.xaml
    /// Логика взаимодействия для AdxUi.xaml
    /// </summary>
    public partial class AdxUi 
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private Adx _adx;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="adx"> indicator that will be editing/индикатор который будем редактировать</param>
        public AdxUi(Adx adx) 
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _adx = adx;

            TextBoxLength.Text = _adx.Length.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _adx.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _adx.PaintOn;

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;

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
                if (Convert.ToInt32(TextBoxLength.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _adx.ColorBase = HostColorBase.Child.BackColor;
            _adx.Length= Convert.ToInt32(TextBoxLength.Text);
            _adx.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _adx.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        ///  color setting button
        /// нажата кнопка изменения цвета
        /// </summary>
        private void ButtonColorAdx_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
