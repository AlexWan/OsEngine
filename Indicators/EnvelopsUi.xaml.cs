﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для EnvelopesUi.xaml
    /// </summary>
    public partial class EnvelopsUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Envelops _envelops; 
         
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public EnvelopsUi(Envelops envelops)
        {
            InitializeComponent();
            _envelops = envelops;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _envelops.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _envelops.ColorDown;
            TextBoxDeviation.Text = _envelops.Deviation.ToString(new CultureInfo("ru-RU"));
            CheckBoxPaintOnOff.IsChecked = _envelops.PaintOn;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxDeviation.Text) <= 0)
                {
                    throw  new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения остановлен.");
                return;
            }

            decimal.TryParse(TextBoxDeviation.Text, out _envelops.Deviation);

            _envelops.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            IsChange = true;
            _envelops.ColorUp = HostColorUp.Child.BackColor;
            _envelops.ColorDown = HostColorDown.Child.BackColor;

            _envelops.Save();
            Close();
        }

        /// <summary>
        /// вызвать настройки скользящей средней
        /// </summary>
        private void ButtonMa_Click(object sender, RoutedEventArgs e)
        {
            _envelops.ShowMaSignalDialog();
        }

        /// <summary>
        /// цвет верхней линии
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// цвет нижней линии
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
