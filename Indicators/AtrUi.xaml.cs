﻿/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для AtrUi.xaml
    /// </summary>
    public partial class AtrUi 
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Atr _atr; 
         
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="atr">индикатор для настроек</param>
        public AtrUi(Atr atr)
        {
            InitializeComponent();
            _atr = atr;

            TextBoxLenght.Text = _atr.Lenght.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _atr.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _atr.PaintOn;
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

            _atr.ColorBase = HostColorBase.Child.BackColor;
            _atr.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _atr.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _atr.Save();

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
