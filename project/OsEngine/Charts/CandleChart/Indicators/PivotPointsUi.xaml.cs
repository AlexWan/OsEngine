﻿using System.Windows;
using System.Windows.Forms;
using OsEngine.OsTrader.Panels;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для PivotPointsRobotUi.xaml
    /// </summary>
    public partial class PivotPointsUi : Window
    {
        private PivotPoints _pivotPoints;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        public PivotPointsUi(PivotPoints pivotPoints)
        {
            InitializeComponent();
            _pivotPoints = pivotPoints;

            CheckBoxPaintOnOff.IsChecked = _pivotPoints.PaintOn;

            HostR1.Child = new TextBox();
            HostR1.Child.BackColor = _pivotPoints.ColorR1;

            HostR2.Child = new TextBox();
            HostR2.Child.BackColor = _pivotPoints.ColorR2;

            HostR3.Child = new TextBox();
            HostR3.Child.BackColor = _pivotPoints.ColorR3;


            HostS1.Child = new TextBox();
            HostS1.Child.BackColor = _pivotPoints.ColorS1;

            HostS2.Child = new TextBox();
            HostS2.Child.BackColor = _pivotPoints.ColorS2;

            HostS3.Child = new TextBox();
            HostS3.Child.BackColor = _pivotPoints.ColorS3;

        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _pivotPoints.ColorR1 = HostR1.Child.BackColor;
            _pivotPoints.ColorR2 = HostR2.Child.BackColor;
            _pivotPoints.ColorR3 = HostR3.Child.BackColor;


            _pivotPoints.ColorS1 = HostS1.Child.BackColor;
            _pivotPoints.ColorS2 = HostS2.Child.BackColor;
            _pivotPoints.ColorS3 = HostS3.Child.BackColor;


            _pivotPoints.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _pivotPoints.Save();

            IsChange = true;
            Close();
        }

        private void ButtonR1_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR1.Child.BackColor;
            dialog.ShowDialog();
            HostR1.Child.BackColor = dialog.Color;
        }

        private void ButtonR2_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR2.Child.BackColor;
            dialog.ShowDialog();
            HostR2.Child.BackColor = dialog.Color;
        }

        private void ButtonR3_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostR3.Child.BackColor;
            dialog.ShowDialog();
            HostR3.Child.BackColor = dialog.Color;
        }



        private void ButtonS1_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS1.Child.BackColor;
            dialog.ShowDialog();
            HostS1.Child.BackColor = dialog.Color;
        }

        private void ButtonS2_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS2.Child.BackColor;
            dialog.ShowDialog();
            HostS2.Child.BackColor = dialog.Color;
        }

        private void ButtonS3_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostS3.Child.BackColor;
            dialog.ShowDialog();
            HostS3.Child.BackColor = dialog.Color;
        }


    }
}
