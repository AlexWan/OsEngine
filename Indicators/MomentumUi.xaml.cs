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
    /// Логика взаимодействия для Momentum.xaml
    /// </summary>
    public partial class MomentumUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Momentum _momentum;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public MomentumUi(Momentum momentum)
        {
            InitializeComponent();
            _momentum = momentum;

            TextBoxLenght.Text = _momentum.Nperiod.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _momentum.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _momentum.PaintOn;
            CandleBox.Items.Add(PriceTypePoints.Close);
            CandleBox.Items.Add(PriceTypePoints.Open);
            CandleBox.Items.Add(PriceTypePoints.High);
            CandleBox.Items.Add(PriceTypePoints.Low);

            CandleBox.SelectedItem = _momentum.TypePoint;
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

            _momentum.ColorBase = HostColorBase.Child.BackColor;
            _momentum.Nperiod = Convert.ToInt32(TextBoxLenght.Text);
            _momentum.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            Enum.TryParse(CandleBox.Text, out _momentum.TypePoint);

            _momentum.Save();

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
