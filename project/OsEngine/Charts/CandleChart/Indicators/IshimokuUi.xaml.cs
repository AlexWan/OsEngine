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
    /// Interaction logic  for IshimokuUi.xaml
    /// Логика взаимодействия для IshimokuUi.xaml
    /// </summary>
    public partial class IshimokuUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private Ichimoku _ishimoku;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменились ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="ishimoku">configuration indicator/индикатор который будем настраивать</param>
        public IshimokuUi(Ichimoku ishimoku)
        {
            InitializeComponent();
            _ishimoku = ishimoku;

            TextBoxPeriodOne.Text = _ishimoku.LenghtFirst.ToString();
            TextBoxPerionTwo.Text = _ishimoku.LenghtSecond.ToString();
            TextBoxPerionThree.Text = _ishimoku.LenghtFird.ToString();
            TextBoxShift.Text = _ishimoku.LenghtSdvig.ToString();
            TextBoxChinkou.Text = _ishimoku.LenghtChinkou.ToString();

            HostLineLate.Child = new TextBox();
            HostLineLate.Child.BackColor = _ishimoku.ColorLineLate;

            HostEtalonLine.Child = new TextBox();
            HostEtalonLine.Child.BackColor = _ishimoku.ColorEtalonLine;

            HostLineRounded.Child = new TextBox();
            HostLineRounded.Child.BackColor = _ishimoku.ColorLineRounded;

            HostColorFirst.Child = new TextBox();
            HostColorFirst.Child.BackColor = _ishimoku.ColorLineFirst;

            HostColorSecond.Child = new TextBox();
            HostColorSecond.Child.BackColor = _ishimoku.ColorLineSecond;


            CheckBoxPaintOnOff.IsChecked = _ishimoku.PaintOn;

            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            ButtonEtalonLine.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonLineLate.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonLineRounded.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonFirst.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonSecond.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            LabelIndicatorDeviation.Content = OsLocalization.Charts.LabelIndicatorDeviation;
            
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxPeriodOne.Text) <= 0 ||
                    Convert.ToInt32(TextBoxPerionTwo.Text) <= 0 ||
                    Convert.ToInt32(TextBoxPerionThree.Text) <= 0 ||
                    Convert.ToInt32(TextBoxShift.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _ishimoku.LenghtFirst = Convert.ToInt32(TextBoxPeriodOne.Text);
            _ishimoku.LenghtSecond = Convert.ToInt32(TextBoxPerionTwo.Text);
            _ishimoku.LenghtFird = Convert.ToInt32(TextBoxPerionThree.Text);
            _ishimoku.LenghtSdvig = Convert.ToInt32(TextBoxShift.Text);
            _ishimoku.LenghtChinkou = Convert.ToInt32(TextBoxChinkou.Text);

            _ishimoku.ColorEtalonLine = HostEtalonLine.Child.BackColor;
            _ishimoku.ColorLineRounded = HostLineRounded.Child.BackColor;
            _ishimoku.ColorLineLate = HostLineLate.Child.BackColor;
            _ishimoku.ColorLineSecond = HostColorSecond.Child.BackColor;
            _ishimoku.ColorLineFirst = HostColorFirst.Child.BackColor;

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _ishimoku.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            _ishimoku.Save();

            IsChange = true;
            Close();
        }

        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostEtalonLine.Child.BackColor;
            dialog.ShowDialog();
            HostEtalonLine.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostLineRounded.Child.BackColor;
            dialog.ShowDialog();
            HostLineRounded.Child.BackColor = dialog.Color;
        }

        private void ButtonColorBase_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostLineLate.Child.BackColor;
            dialog.ShowDialog();
            HostLineLate.Child.BackColor = dialog.Color;
        }

        private void ButtonFirst_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorFirst.Child.BackColor;
            dialog.ShowDialog();
            HostColorFirst.Child.BackColor = dialog.Color;
        }

        private void ButtonSecond_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorSecond.Child.BackColor;
            dialog.ShowDialog();
            HostColorSecond.Child.BackColor = dialog.Color;
        }

    }
}



