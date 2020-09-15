using System;
using System.Windows;

namespace OsEngine.Robots.MoiRoboti
{
    /// <summary>
    /// Логика взаимодействия для Ver_01_Ui.xaml
    /// </summary>

    public partial class Ver_01_Ui : Window
    {
        public Ver_01 _myRobot;
        public Ver_01_Ui(Ver_01 myRobot)
        {
            InitializeComponent();
            // сохраняем ссылку на робота в переменную _myRobot
            _myRobot = myRobot;

            // выводим текущие настройки на форму
            TextBoxDoStop.Text = _myRobot.zn_stop_los.ToString();
            TextBoxOtProfit.Text = _myRobot.OtProfit.ToString();
            TextBoxmin_order.Text = _myRobot.vel_machki.ToString();
            TextBoxkomis_birgi.Text = _myRobot.komis_birgi.ToString();
            TextBoxveli4_usrednen.Text = _myRobot.veli4_usrednen.ToString();
            TextBoxdola_depa.Text = _myRobot.dola_depa.ToString();
            TextBoxDeltaVerx.Text = _myRobot.DeltaVerx.ToString();
            TextBoxDeltaUsredn.Text = _myRobot.DeltaUsredn.ToString();
            CheckBoxVkl.IsChecked = _myRobot.Vkl;

            ButtonSave.Click += ButtonSave_Click;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            // сохраняем данные из чекбоксов в переменные
            _myRobot.Vkl = CheckBoxVkl.IsChecked.Value;
            _myRobot.zn_stop_los = Convert.ToDecimal(TextBoxDoStop.Text);
            _myRobot.OtProfit = Convert.ToDecimal(TextBoxOtProfit.Text);
            _myRobot.vel_machki = Convert.ToInt32(TextBoxmin_order.Text);
            _myRobot.komis_birgi = Convert.ToDecimal(TextBoxkomis_birgi.Text);
            _myRobot.veli4_usrednen = Convert.ToDecimal(TextBoxveli4_usrednen.Text);
            _myRobot.dola_depa = Convert.ToUInt16(TextBoxdola_depa.Text);
            _myRobot.DeltaVerx = Convert.ToDecimal(TextBoxDeltaVerx.Text);
            _myRobot.DeltaUsredn = Convert.ToDecimal(TextBoxDeltaUsredn.Text);
            _myRobot.Save(); // сохранение в файл
            Close();  // закрыть окно

             
        }
    }
}
