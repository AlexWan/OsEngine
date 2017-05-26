﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для MacdUi.xaml
    /// </summary>
    public partial class MacdHistogramUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private MacdHistogram _macd; 
         
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public MacdHistogramUi(MacdHistogram macd)
        {
            InitializeComponent();
            _macd = macd;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _macd.ColorUp;
            CheckBoxPaintOnOff.IsChecked = _macd.PaintOn;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _macd.ColorDown;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _macd.ColorUp = HostColorUp.Child.BackColor;
            _macd.ColorDown = HostColorDown.Child.BackColor;
            _macd.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            IsChange = true;


            _macd.Save();
            Close();
        }

        private void ButtonMaShort_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaShortDialog();
        }

        private void ButtonMaLong_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaLongDialog();
        }

        private void ButtonMaSignal_Click(object sender, RoutedEventArgs e)
        {
            _macd.ShowMaSignalDialog();
        }

        private void ButtonColorAdxUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorAdxDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
