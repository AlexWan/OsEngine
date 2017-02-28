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
    /// Логика взаимодействия для AdaptiveLookBackUi.xaml
    /// </summary>
    public partial class AdaptiveLookBackUi
    {
       /// <summary>
        /// индикатор
        /// </summary>
        private AdaptiveLookBack _alb; 
         
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="alb">индикатор для настроек</param>
        public AdaptiveLookBackUi(AdaptiveLookBack alb)
        {
            InitializeComponent();
            _alb = alb;

            TextBoxLenght.Text = _alb.Lenght.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _alb.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _alb.PaintOn;
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

            _alb.ColorBase = HostColorBase.Child.BackColor;
            _alb.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _alb.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _alb.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
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
