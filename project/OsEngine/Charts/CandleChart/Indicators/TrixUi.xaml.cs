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
    /// Interaction logic  for TRIXUi.xaml
    /// Логика взаимодействия для TRIXUi.xaml
    /// </summary>
    public partial class TrixUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private Trix _trix;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="trix">configuration indicator/индикатор для настроек</param>
        public TrixUi(Trix trix)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _trix = trix;

            TextBoxLength.Text = _trix.Period.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _trix.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _trix.PaintOn;


            Movingbox.Items.Add(MovingAverageTypeCalculation.Exponential);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Simple);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Weighted);
            Movingbox.Items.Add(MovingAverageTypeCalculation.Adaptive);
            Movingbox.SelectedItem = _trix.TypeCalculationAverage;

            Movingbox.SelectedItem = _trix.TypeIndicator;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
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

            _trix.ColorBase = HostColorBase.Child.BackColor;
            _trix.Period = Convert.ToInt32(TextBoxLength.Text);
            _trix.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            Enum.TryParse(Movingbox.Text, out _trix.TypeCalculationAverage);

            _trix.Save();

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
