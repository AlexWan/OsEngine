/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Interaction logic  for BollingerUi.xaml
    /// Логика взаимодействия для BollingerUi.xaml
    /// </summary>
    public partial class BollingerUi
    {
        /// <summary>
        /// indicator
        /// индикатор который мы настраиваем
        /// </summary>
        private Bollinger _bollinger;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="bollinger">configuration indicator/индикатор который мы будем настраивать</param>
        public BollingerUi(Bollinger bollinger)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _bollinger = bollinger;

            TextBoxDeviation.Text = _bollinger.Deviation.ToString();

            TextBoxLength.Text = _bollinger.Length.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _bollinger.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _bollinger.ColorDown;
            CheckBoxPaintOnOff.IsChecked = _bollinger.PaintOn;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
            LabelIndicatorDeviation.Content = OsLocalization.Charts.LabelIndicatorDeviation;

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
                if (Convert.ToInt32(TextBoxLength.Text) <= 0 ||
                   Convert.ToDecimal(TextBoxDeviation.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _bollinger.ColorUp = HostColorUp.Child.BackColor;
            _bollinger.ColorDown = HostColorDown.Child.BackColor;
            _bollinger.Deviation = Convert.ToDecimal(TextBoxDeviation.Text);

            _bollinger.Length = Convert.ToInt32(TextBoxLength.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _bollinger.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            
            _bollinger.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// top line color button
        /// кнопка цвет верхней линии
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// bottom line color button
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
