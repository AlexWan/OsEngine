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
    /// Логика взаимодействия для BollingerUi.xaml
    /// </summary>
    public partial class DonchianChannelUi
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private DonchianChannel _donchian;

        /// <summary>
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="bollinger">индикатор который мы будем настраивать</param>
        public DonchianChannelUi(DonchianChannel donchian)
        {
            InitializeComponent();
            _donchian = donchian;

            TextBoxLenght.Text = _donchian.Lenght.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _donchian.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _donchian.ColorDown;

            HostColorAvg.Child = new TextBox();
            HostColorAvg.Child.BackColor = _donchian.ColorAvg;

            CheckBoxPaintOnOff.IsChecked = _donchian.PaintOn;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _donchian.ColorUp = HostColorUp.Child.BackColor;
            _donchian.ColorDown = HostColorDown.Child.BackColor;
            _donchian.ColorAvg = HostColorAvg.Child.BackColor;

            _donchian.Lenght = Convert.ToInt32(TextBoxLenght.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _donchian.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            _donchian.Save();
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
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorAvg_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorAvg.Child.BackColor;
            dialog.ShowDialog();
            HostColorAvg.Child.BackColor = dialog.Color;
        }
    }
}
