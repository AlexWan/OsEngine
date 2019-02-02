/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Логика взаимодействия для AlligatorUi.xaml
    /// </summary>
    public partial class AlligatorUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Alligator _alligator;

        /// <summary>
        /// изменились ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="alligator">индикатор который будем настраивать</param>
        public AlligatorUi(Alligator alligator)
        {
            InitializeComponent();
            _alligator = alligator;

            TextBoxLenghtBase.Text = _alligator.LenghtBase.ToString();
            TextBoxShiftBase.Text = _alligator.ShiftBase.ToString();
            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _alligator.ColorBase;


            TextBoxLenghtUp.Text = _alligator.LenghtUp.ToString();
            TextBoxShiftUp.Text = _alligator.ShiftUp.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _alligator.ColorUp;

            TextBoxLenghtDown.Text = _alligator.LenghtDown.ToString();
            TextBoxShiftDown.Text = _alligator.ShiftDown.ToString();
            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _alligator.ColorDown;


            CheckBoxPaintOnOff.IsChecked = _alligator.PaintOn;

            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Exponential);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Simple);
            ComboBoxMovingType.Items.Add(MovingAverageTypeCalculation.Weighted);

            ComboBoxMovingType.SelectedItem = _alligator.TypeCalculationAverage;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonColorBase.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod1.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorPeriod2.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorPeriod3.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorMethod.Content = OsLocalization.Charts.LabelIndicatorMethod;
            LabelIndicatorAlligator1.Content = OsLocalization.Charts.LabelIndicatorAlligator1;
            LabelIndicatorAlligator2.Content = OsLocalization.Charts.LabelIndicatorAlligator2;
            LabelIndicatorAlligator3.Content = OsLocalization.Charts.LabelIndicatorAlligator3;
            LabelIndicatorShift1.Content = OsLocalization.Charts.LabelIndicatorShift;
            LabelIndicatorShift2.Content = OsLocalization.Charts.LabelIndicatorShift;
            LabelIndicatorShift3.Content = OsLocalization.Charts.LabelIndicatorShift;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenghtBase.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLenghtDown.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLenghtUp.Text) <= 0 ||
                    Convert.ToInt32(TextBoxShiftBase.Text) <= 0 ||
                    Convert.ToInt32(TextBoxShiftDown.Text) <= 0 ||
                    Convert.ToInt32(TextBoxShiftUp.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }



            _alligator.LenghtBase = Convert.ToInt32(TextBoxLenghtBase.Text);
            _alligator.ShiftBase = Convert.ToInt32(TextBoxShiftBase.Text);
            _alligator.ColorBase = HostColorBase.Child.BackColor;

            _alligator.LenghtUp = Convert.ToInt32(TextBoxLenghtUp.Text);
            _alligator.ShiftUp = Convert.ToInt32(TextBoxShiftUp.Text);
            _alligator.ColorUp = HostColorUp.Child.BackColor;

            _alligator.LenghtDown = Convert.ToInt32(TextBoxLenghtDown.Text);
            _alligator.ShiftDown = Convert.ToInt32(TextBoxShiftDown.Text);
            _alligator.ColorDown = HostColorDown.Child.BackColor;

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _alligator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            

            MovingAverageTypeCalculation type;

            Enum.TryParse(ComboBoxMovingType.Text, true, out type);
            _alligator.TypeCalculationAverage = type;

            _alligator.Save();

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

        /// <summary>
        /// кнопка цвет центральной линии
        /// </summary>
        private void ButtonColorBase_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
