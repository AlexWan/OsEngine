/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Interaction logic  for PriceChannelUi.xaml
    /// Логика взаимодействия для PriceChannelUi.xaml
    /// </summary>
    public partial class PriceChannelUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private PriceChannel _indicator;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменились ли настройки у индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public PriceChannelUi(PriceChannel indicator)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _indicator = indicator;

            TextBoxLengthUp.Text = _indicator.LengthUpLine.ToString();
            TextBoxLengthDown.Text = _indicator.LengthDownLine.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _indicator.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _indicator.ColorDown;
            CheckBoxPaintOnOff.IsChecked = _indicator.PaintOn;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;
            LabelIndicatorShortPeriod.Content = OsLocalization.Charts.LabelIndicatorShortPeriod;

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
                if (Convert.ToInt32(TextBoxLengthUp.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLengthDown.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _indicator.ColorUp = HostColorUp.Child.BackColor;
            _indicator.ColorDown = HostColorDown.Child.BackColor;

            _indicator.LengthUpLine = Convert.ToInt32(TextBoxLengthUp.Text);
            _indicator.LengthDownLine = Convert.ToInt32(TextBoxLengthDown.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _indicator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            _indicator.Save();
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
