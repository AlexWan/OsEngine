/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для BollingerUi.xaml
    /// </summary>
    public partial class DonchianChannelUi
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private DonchianChannel _indicator;

        /// <summary>
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="bollinger">индикатор который мы будем настраивать</param>
        public DonchianChannelUi(DonchianChannel indicator)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _indicator = indicator;

            TextBoxLength.Text = _indicator.Length.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _indicator.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _indicator.ColorDown;

            HostColorAvg.Child = new TextBox();
            HostColorAvg.Child.BackColor = _indicator.ColorAvg;

            CheckBoxPaintOnOff.IsChecked = _indicator.PaintOn;


            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            ButtonColorAvg.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;

            this.Activate();
            this.Focus();
        }

        /// <summary>
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

            _indicator.ColorUp = HostColorUp.Child.BackColor;
            _indicator.ColorDown = HostColorDown.Child.BackColor;
            _indicator.ColorAvg = HostColorAvg.Child.BackColor;

            _indicator.Length = Convert.ToInt32(TextBoxLength.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _indicator.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            _indicator.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
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
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorAvg_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColorAvg.Child.BackColor;
            dialog.ShowDialog();
            HostColorAvg.Child.BackColor = dialog.Color;
        }
    }
}