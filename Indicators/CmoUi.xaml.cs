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
    /// Логика взаимодействия для CMOUi.xaml
    /// </summary>
    public partial class CmoUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Cmo _cmo;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public CmoUi(Cmo cmo)
        {
            InitializeComponent();
            _cmo = cmo;
            TextBoxLenght.Text = _cmo.Period.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _cmo.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _cmo.PaintOn;
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

            _cmo.ColorBase = HostColorBase.Child.BackColor;
            _cmo.Period = Convert.ToInt32(TextBoxLenght.Text);
            _cmo.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;



            _cmo.Save();

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
