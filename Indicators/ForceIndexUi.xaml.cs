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
    /// Логика взаимодействия для ForceIndexUi.xaml
    /// </summary>
    public partial class ForceIndexUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private ForceIndex _forceindex;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public ForceIndexUi(ForceIndex forceindex)
        {
            InitializeComponent();
            _forceindex = forceindex;

            TextBoxLenght.Text = _forceindex.Period.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _forceindex.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _forceindex.PaintOn;
            CandleBox.Items.Add(PriceTypePoints.Close);
            CandleBox.Items.Add(PriceTypePoints.Open);
            CandleBox.Items.Add(PriceTypePoints.High);
            CandleBox.Items.Add(PriceTypePoints.Low);

            Movingbox.Items.Add(MovingAverageTypeCalculation.Exponential);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Simple);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Weighted);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Adaptive);

            CandleBox.SelectedItem = _forceindex.TypePoint;
            Movingbox.SelectedItem = _forceindex.TypeCalculationAverage;
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

            _forceindex.ColorBase = HostColorBase.Child.BackColor;
            _forceindex.Period = Convert.ToInt32(TextBoxLenght.Text);
            _forceindex.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            Enum.TryParse(CandleBox.Text, out _forceindex.TypePoint);
            Enum.TryParse(Movingbox.Text, out _forceindex.TypeCalculationAverage);

            _forceindex.Save();

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
