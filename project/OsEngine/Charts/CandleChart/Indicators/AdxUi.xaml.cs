/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Логика взаимодействия для AdxUi.xaml
    /// </summary>
    public partial class AdxUi 
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private Adx _adx; 
        
        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="adx">индикатор который будем редактировать</param>
        public AdxUi(Adx adx) 
        {
            InitializeComponent();
            _adx = adx;

            TextBoxLenght.Text = _adx.Lenght.ToString();

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _adx.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _adx.PaintOn;

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
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

            _adx.ColorBase = HostColorBase.Child.BackColor;
            _adx.Lenght= Convert.ToInt32(TextBoxLenght.Text);
            _adx.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _adx.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// нажата кнопка изменения цвета
        /// </summary>
        private void ButtonColorAdx_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}
