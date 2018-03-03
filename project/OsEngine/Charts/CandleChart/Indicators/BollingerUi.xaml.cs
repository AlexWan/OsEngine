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
    public partial class BollingerUi
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private Bollinger _bollinger;

        /// <summary>
        /// изменились ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="bollinger">индикатор который мы будем настраивать</param>
        public BollingerUi(Bollinger bollinger)
        {
            InitializeComponent();
            _bollinger = bollinger;

            TextBoxDeviation.Text = _bollinger.Deviation.ToString();

            TextBoxLenght.Text = _bollinger.Lenght.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _bollinger.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _bollinger.ColorDown;
            CheckBoxPaintOnOff.IsChecked = _bollinger.PaintOn;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0 ||
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

            _bollinger.Lenght = Convert.ToInt32(TextBoxLenght.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _bollinger.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            
            _bollinger.Save();
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
