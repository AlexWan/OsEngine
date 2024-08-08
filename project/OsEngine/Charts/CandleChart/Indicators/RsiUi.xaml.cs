/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic for RsiUi.xaml
    /// Логика взаимодействия для RsiUi.xaml
    /// </summary>
    public partial class RsiUi 
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private Rsi _rsi;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="rsi">configuration indicator/индикатор который будем настраивать</param>
        public RsiUi(Rsi rsi)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _rsi = rsi;

            TextBoxLength.Text = _rsi.Length.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _rsi.ColorBase;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;

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

            _rsi.ColorBase = HostColor.Child.BackColor;
            _rsi.Length = Convert.ToInt32(TextBoxLength.Text);

            _rsi.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка далее выбор цвета
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorCustomDialog dialog = new ColorCustomDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }
    }
}
