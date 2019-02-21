/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для RsiUi.xaml
    /// </summary>
    public partial class RsiUi 
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private Rsi _rsi; 

        /// <summary>
        /// изменялись ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="rsi">индикатор который будем настраивать</param>
        public RsiUi(Rsi rsi)
        {
            InitializeComponent();
            _rsi = rsi;

            TextBoxLenght.Text = _rsi.Lenght.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _rsi.ColorBase;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
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

            _rsi.ColorBase = HostColor.Child.BackColor;
            _rsi.Lenght = Convert.ToInt32(TextBoxLenght.Text);

            _rsi.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка далее выбор цвета
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }
    }
}
