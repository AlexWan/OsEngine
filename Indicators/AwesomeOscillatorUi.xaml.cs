/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для AwesomeOscillatorUi.xaml
    /// </summary>
    public partial class AwesomeOscillatorUi 
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private AwesomeOscillator _awesomeOscillatoro;

        /// <summary>
        /// изменяли ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="awesomeOscillator">индикатор который будем настраивать</param>
        public AwesomeOscillatorUi(AwesomeOscillator awesomeOscillator)
        {
            InitializeComponent();
            _awesomeOscillatoro = awesomeOscillator;

            TextBoxLenghtLong.Text = _awesomeOscillatoro.LenghtLong.ToString();
            TextBoxLenghtShort.Text = _awesomeOscillatoro.LenghtShort.ToString();

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _awesomeOscillatoro.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _awesomeOscillatoro.ColorDown;

            CheckBoxPaintOnOff.IsChecked = _awesomeOscillatoro.PaintOn;

            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Exponential);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Simple);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Weighted);

            ComboBoxMovingType.SelectedItem = _awesomeOscillatoro.TypeCalculationAverage;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenghtLong.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLenghtShort.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _awesomeOscillatoro.ColorUp = HostColorUp.Child.BackColor;
            _awesomeOscillatoro.ColorDown = HostColorDown.Child.BackColor;

            _awesomeOscillatoro.LenghtLong = Convert.ToInt32(TextBoxLenghtLong.Text);
            _awesomeOscillatoro.LenghtShort = Convert.ToInt32(TextBoxLenghtShort.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _awesomeOscillatoro.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            MovingAverageTypeCalculation movingAverageType;

            Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out movingAverageType);
            _awesomeOscillatoro.TypeCalculationAverage = movingAverageType;
            _awesomeOscillatoro.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка цвет верхней линии
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// кнопка цвет нижней линии
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
