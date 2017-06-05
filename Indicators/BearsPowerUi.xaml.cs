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
    /// Логика взаимодействия для BearsPowerUi.xaml
    /// </summary>
    public partial class BearsPowerUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private BearsPower _bp;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public BearsPowerUi(BearsPower bp)
        {
            InitializeComponent();
            _bp = bp;

            TextBoxLenght.Text = _bp.Period.ToString();

            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = bp.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = bp.ColorDown;

            CheckBoxPaintOnOff.IsChecked = _bp.PaintOn;

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

            _bp.ColorUp = HostColorUp.Child.BackColor;
            _bp.ColorDown = HostColorDown.Child.BackColor;

            _bp.Period = Convert.ToInt32(TextBoxLenght.Text);
            _bp.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;


            _bp.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
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
