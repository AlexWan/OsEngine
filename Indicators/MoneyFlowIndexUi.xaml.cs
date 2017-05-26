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
    /// Логика взаимодействия для MoneyFlowIndexUi.xaml
    /// </summary>
    public partial class MoneyFlowIndexUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private MoneyFlowIndex _mfi;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="mfi">индикатор для настроек</param>
        public MoneyFlowIndexUi(MoneyFlowIndex mfi)
        {
            InitializeComponent();
            _mfi = mfi;

            TextBoxLenght.Text = _mfi.Nperiod.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _mfi.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _mfi.PaintOn;



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

            _mfi.ColorBase = HostColorBase.Child.BackColor;
            _mfi.Nperiod = Convert.ToInt32(TextBoxLenght.Text);
            _mfi.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;


            _mfi.Save();

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
