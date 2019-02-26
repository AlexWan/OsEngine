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
    /// Interaction logic for IvashovRangeUi.xaml
    /// Логика взаимодействия для IvashovRangeUi.xaml
    /// </summary>
    public partial class IvashovRangeUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private IvashovRange _ir;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public IvashovRangeUi(IvashovRange ir)
        {
            InitializeComponent();
            _ir = ir;

            TextBoxLenght.Text = _ir.LenghtMa.ToString();
            TextBoxLenghtAverage.Text = _ir.LenghtAverage.ToString();
            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _ir.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _ir.PaintOn;

            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;

            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorSmoothing.Content = OsLocalization.Charts.LabelIndicatorSmoothing;


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

            _ir.ColorBase = HostColorBase.Child.BackColor;
            _ir.LenghtMa = Convert.ToInt32(TextBoxLenght.Text);
            _ir.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            _ir.LenghtAverage = Convert.ToInt32(TextBoxLenghtAverage.Text);
            _ir.Save();

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
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
