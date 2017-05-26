﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для FractalUi.xaml
    /// </summary>
    public partial class FractalUi
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private Fractal _fractail;

        /// <summary>
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="fractail">индикатор для настройки</param>
        public FractalUi(Fractal fractail) 
        {
            InitializeComponent();
            _fractail = fractail;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _fractail.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _fractail.ColorDown;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _fractail.ColorUp = HostColorUp.Child.BackColor;
            _fractail.ColorDown = HostColorDown.Child.BackColor;
            _fractail.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка цвет верхних точек
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();

            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// кнопка цвет нижних точек
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
