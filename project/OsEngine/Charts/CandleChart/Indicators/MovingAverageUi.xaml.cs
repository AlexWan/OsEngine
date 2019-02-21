/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для MovingAverageUi.xaml
    /// </summary>
    public partial class MovingAverageUi 
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private MovingAverage _mA;

        /// <summary>
        /// изменился ли индикатор
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="mA">индикатор для настройки</param>
        public MovingAverageUi(MovingAverage mA)
        {
            InitializeComponent();
            _mA = mA;

            TextBoxLenght.Text = _mA.Lenght.ToString();
            TextBoxKaufmanFast.Text = _mA.KaufmanFastEma.ToString();
            TextBoxKaufmanSlow.Text = _mA.KaufmanSlowEma.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _mA.ColorBase;

            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Exponential);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Simple);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Weighted);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Radchenko);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Adaptive);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.VolumeWeighted);
            ComboBoxMovingType.SelectionChanged += ComboBoxMovingType_SelectionChanged;

            ComboBoxMovingType.SelectedItem = _mA.TypeCalculationAverage;
            

            CheckBoxPaintOnOff.IsChecked = _mA.PaintOn;
            ComboBoxPriceField.Items.Add(PriceTypePoints.Open);
            ComboBoxPriceField.Items.Add(PriceTypePoints.High);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Low);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Close);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Median);
            ComboBoxPriceField.Items.Add(PriceTypePoints.Typical);

            ComboBoxPriceField.SelectedItem = _mA.TypePointsToSearch;


            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorCandleType.Content = OsLocalization.Charts.LabelIndicatorCandleType;
            LabelIndicatorMethod.Content = OsLocalization.Charts.LabelIndicatorMethod;
            LabelIndicatorShortPeriod.Content = OsLocalization.Charts.LabelIndicatorShortPeriod;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;

        }

        void ComboBoxMovingType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

            if (ComboBoxMovingType.SelectedItem.ToString() == MovingAverageTypeCalculation.Adaptive.ToString())
            {
                Width = 490;
            }
            else
            {
                Width = 295;
            }
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0 ||
                    Convert.ToInt32(TextBoxKaufmanSlow.Text) <= 0 ||
                    Convert.ToInt32(TextBoxKaufmanFast.Text) <= 0)
                {
                    throw new Exception("error");
                }
                Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out _mA.TypeCalculationAverage);
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _mA.ColorBase = HostColor.Child.BackColor;
            _mA.Lenght = Convert.ToInt32(TextBoxLenght.Text);
            _mA.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _mA.KaufmanFastEma = Convert.ToInt32(TextBoxKaufmanFast.Text);
            _mA.KaufmanSlowEma = Convert.ToInt32(TextBoxKaufmanSlow.Text);


            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Exponential);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Simple);
            Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out _mA.TypeCalculationAverage);

            Enum.TryParse(ComboBoxPriceField.SelectedItem.ToString(), true, out _mA.TypePointsToSearch);

            _mA.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка изменить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }

    }
}
