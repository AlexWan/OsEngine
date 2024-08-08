/*
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
    /// Interaction logic  for Momentum.xaml
    /// Логика взаимодействия для Momentum.xaml
    /// </summary>
    public partial class MomentumUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private Momentum _momentum;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public MomentumUi(Momentum momentum)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _momentum = momentum;

            TextBoxLength.Text = _momentum.Nperiod.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _momentum.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _momentum.PaintOn;
            CandleBox.Items.Add(PriceTypePoints.Close);
            CandleBox.Items.Add(PriceTypePoints.Open);
            CandleBox.Items.Add(PriceTypePoints.High);
            CandleBox.Items.Add(PriceTypePoints.Low);

            CandleBox.SelectedItem = _momentum.TypePoint;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            LabelIndicatorCandleType.Content = OsLocalization.Charts.LabelIndicatorCandleType;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;

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

            _momentum.ColorBase = HostColorBase.Child.BackColor;
            _momentum.Nperiod = Convert.ToInt32(TextBoxLength.Text);
            _momentum.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            Enum.TryParse(CandleBox.Text, out _momentum.TypePoint);

            _momentum.Save();

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
