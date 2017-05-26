﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для EfficiencyRatioUi.xaml
    /// </summary>
    public partial class EfficiencyRatioUi 
    {
       /// <summary>
        /// индикатор
        /// </summary>
        private EfficiencyRatio _eR; 
         
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="eR">индикатор для настроек</param>
        public EfficiencyRatioUi(EfficiencyRatio eR)
        {
            InitializeComponent();
            _eR = eR;

            TextBoxLenght.Text = _eR.Lenght.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _eR.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _eR.PaintOn;
        }

        /// <summary>
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