/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Interaction logic for AcUi.xaml
    /// Логика взаимодействия для AcUi.xaml
    /// </summary>
    public partial class AcUi
    { 
        
        /// <summary>
        /// indocator
        /// индикатор
        /// </summary>
        private Ac _ac;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public AcUi(Ac ac)
        {
            InitializeComponent();
            _ac = ac;

            TextBoxLenght.Text = _ac.LenghtLong.ToString();
            TextBoxLenghtAverage.Text = _ac.LenghtShort.ToString();
            CheckBoxPaintOnOff.IsChecked = _ac.PaintOn;

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _ac.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _ac.ColorDown;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;
            LabelIndicatorShortPeriod.Content = OsLocalization.Charts.LabelIndicatorShortPeriod;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0
                    || Convert.ToInt32(TextBoxLenghtAverage.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _ac.ColorUp = HostColorUp.Child.BackColor;
            _ac.ColorDown = HostColorDown.Child.BackColor;

            _ac.LenghtLong = Convert.ToInt32(TextBoxLenght.Text);
            _ac.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _ac.LenghtShort = Convert.ToInt32(TextBoxLenghtAverage.Text);
            _ac.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка настроить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
