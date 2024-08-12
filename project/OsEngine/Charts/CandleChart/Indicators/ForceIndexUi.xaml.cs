﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic for ForceIndexUi.xaml
    /// Логика взаимодействия для ForceIndexUi.xaml
    /// </summary>
    public partial class ForceIndexUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private ForceIndex _forceindex;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public ForceIndexUi(ForceIndex forceindex)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _forceindex = forceindex;

            TextBoxLength.Text = _forceindex.Period.ToString();

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

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorCandleType.Content = OsLocalization.Charts.LabelIndicatorCandleType;
            LabelIndicatorSmaType.Content = OsLocalization.Charts.LabelIndicatorSmaType;

            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLength.Text) <= 0)
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
            _forceindex.Period = Convert.ToInt32(TextBoxLength.Text);
            _forceindex.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            Enum.TryParse(CandleBox.Text, out _forceindex.TypePoint);
            Enum.TryParse(Movingbox.Text, out _forceindex.TypeCalculationAverage);

            _forceindex.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка настроить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
